using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class StartPracticeSessionAdversarialStressTests
{
    private readonly StartPracticeSessionCommandValidator _validator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student)> SeedBaseDataAsync(OralExamDbContext context)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Software Development Project",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student_challenger@fpt.edu.vn",
            FullName = "Nguyễn Văn Thử Thách",
            Role = UserRole.Student,
            IsActive = true
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        await context.SaveChangesAsync();
        return (course, student);
    }

    private async Task SeedQuestionsAsync(OralExamDbContext context, Guid courseId, string difficulty, int count)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = $"Barem {difficulty}",
            TotalMaxScore = 10.0m
        };
        context.Rubrics.Add(rubric);

        for (int i = 1; i <= count; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                RubricId = rubric.Id,
                Title = $"Câu hỏi {difficulty} #{i}",
                Content = $"Nội dung câu hỏi {difficulty} #{i}",
                Difficulty = difficulty,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    [Theory(DisplayName = "ADV-01. Validator: Chặn toàn diện các giá trị out-of-bounds của QuestionCount khi difficulty = progressive")]
    [InlineData(-10)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(100)]
    public void Validator_Should_Fail_When_Progressive_QuestionCount_Is_Out_Of_Bounds(int questionCount)
    {
        var command = new StartPracticeSessionCommand(
            StudentId: Guid.NewGuid(),
            CourseId: Guid.NewGuid(),
            Difficulty: "progressive",
            QuestionCount: questionCount,
            IsFullSession: false,
            Topic: null
        );

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionCount);
        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "ADV-02. Validator: Chấp nhận toàn bộ N từ 3 đến 10 khi difficulty = progressive")]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Validator_Should_Pass_For_All_N_From_3_To_10_When_Progressive(int questionCount)
    {
        var command = new StartPracticeSessionCommand(
            StudentId: Guid.NewGuid(),
            CourseId: Guid.NewGuid(),
            Difficulty: "progressive",
            QuestionCount: questionCount,
            IsFullSession: false,
            Topic: null
        );

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.QuestionCount);
        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "ADV-03. Handler: Kiểm chứng toàn diện 8 ca biên N = 3..10 phân bổ chính xác và thứ tự tăng dần Dễ -> TB -> Khó")]
    [InlineData(3, 1, 1, 1)]
    [InlineData(4, 1, 2, 1)]
    [InlineData(5, 2, 2, 1)]
    [InlineData(6, 2, 2, 2)]
    [InlineData(7, 2, 3, 2)]
    [InlineData(8, 3, 3, 2)]
    [InlineData(9, 3, 3, 3)]
    [InlineData(10, 3, 4, 3)]
    public async Task Handler_Should_Distribute_And_Order_Strictly_For_All_N_3_To_10(
        int totalN, int expectedEasy, int expectedMedium, int expectedHard)
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 15);
        await SeedQuestionsAsync(context, course.Id, "medium", 15);
        await SeedQuestionsAsync(context, course.Id, "hard", 15);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: totalN,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Questions.Should().HaveCount(totalN);

        var questionIds = result.Value.Questions.Select(q => q.QuestionId).ToList();
        var questionsInDb = await context.PracticeQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.Difficulty.ToLower());

        // 1. Kiểm tra chính xác số lượng từng mức
        var easyQuestions = questionIds.Take(expectedEasy).Select(id => questionsInDb[id]).ToList();
        var mediumQuestions = questionIds.Skip(expectedEasy).Take(expectedMedium).Select(id => questionsInDb[id]).ToList();
        var hardQuestions = questionIds.Skip(expectedEasy + expectedMedium).Take(expectedHard).Select(id => questionsInDb[id]).ToList();

        easyQuestions.Should().OnlyContain(d => d == "easy");
        mediumQuestions.Should().OnlyContain(d => d == "medium");
        hardQuestions.Should().OnlyContain(d => d == "hard");

        // 2. Kiểm tra tính đơn điệu tăng dần Dễ (1) -> Trung bình (2) -> Khó (3)
        var difficultyWeights = new Dictionary<string, int>
        {
            ["easy"] = 1,
            ["medium"] = 2,
            ["hard"] = 3
        };

        var weights = questionIds.Select(id => difficultyWeights[questionsInDb[id]]).ToList();
        for (int i = 0; i < weights.Count - 1; i++)
        {
            weights[i].Should().BeLessThanOrEqualTo(weights[i + 1],
                $"Thứ tự phát vấn tại vị trí {i} ({weights[i]}) phải <= vị trí {i + 1} ({weights[i + 1]})");
        }
    }

    [Fact(DisplayName = "ADV-04. Ca biên: Dễ đủ (10 câu), Khó đủ (10 câu) nhưng Trung bình thiếu (chỉ có 1 câu khi cần 2 câu cho N=4)")]
    public async Task Handler_Should_Fail_When_Medium_Is_Insufficient_Even_If_Easy_And_Hard_Are_Abundant()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 10);
        await SeedQuestionsAsync(context, course.Id, "medium", 1); // Cần 2, chỉ có 1
        await SeedQuestionsAsync(context, course.Id, "hard", 10);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 4, // N=4 cần 1 easy, 2 medium, 1 hard
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó");
        result.Error.Should().Contain("2 Trung bình (có 1)");

        // Tuyệt đối không tạo phiên rác trong CSDL
        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0, "Không được tạo session rác trong DB khi thiếu câu hỏi ở mức Trung bình");
    }

    [Fact(DisplayName = "ADV-05. Ca biên: Trung bình đủ, Khó đủ nhưng Dễ thiếu (0 câu Dễ khi N=3)")]
    public async Task Handler_Should_Fail_When_Easy_Is_Insufficient_Even_If_Medium_And_Hard_Are_Abundant()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        // 0 câu easy
        await SeedQuestionsAsync(context, course.Id, "medium", 10);
        await SeedQuestionsAsync(context, course.Id, "hard", 10);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 3,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó");
        result.Error.Should().Contain("1 Dễ (có 0)");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0, "Không được tạo session rác trong DB khi thiếu câu hỏi ở mức Dễ");
    }

    [Fact(DisplayName = "ADV-06. Ca biên: Kho đề hoàn toàn rỗng (0 câu ở cả 3 mức Dễ, TB, Khó)")]
    public async Task Handler_Should_Fail_When_All_Question_Pools_Are_Empty()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        // 0 câu hỏi nào được seed

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Theory(DisplayName = "ADV-07. Kiểm chứng cả 2 chế độ [Per-Question] và [Full-Session] lưu đúng PracticeMode")]
    [InlineData(false, "per_question")]
    [InlineData(true, "full_session")]
    public async Task Handler_Should_Persist_Correct_PracticeMode_For_Both_Modes_In_Progressive(
        bool isFullSession, string expectedPracticeMode)
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 5);
        await SeedQuestionsAsync(context, course.Id, "medium", 5);
        await SeedQuestionsAsync(context, course.Id, "hard", 5);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: isFullSession,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var sessionInDb = await context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == result.Value!.SessionId);
        sessionInDb.Should().NotBeNull();
        sessionInDb!.PracticeMode.Should().Be(expectedPracticeMode);
        sessionInDb.Status.Should().Be("in_progress");
        sessionInDb.StudentId.Should().Be(student.Id);
        sessionInDb.CourseId.Should().Be(course.Id);
    }

    [Theory(DisplayName = "ADV-08. Handler: Chấp nhận độ khó progressive không phân biệt hoa thường và khoảng trắng")]
    [InlineData("progressive")]
    [InlineData("Progressive")]
    [InlineData("PROGRESSIVE")]
    [InlineData("  progressive  ")]
    public async Task Handler_Should_Handle_Progressive_Case_Insensitively_With_Whitespace(string difficulty)
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 3);
        await SeedQuestionsAsync(context, course.Id, "medium", 3);
        await SeedQuestionsAsync(context, course.Id, "hard", 3);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: difficulty,
            QuestionCount: 3,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Questions.Should().HaveCount(3);
    }
}
