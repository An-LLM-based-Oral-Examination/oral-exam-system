using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class StartPracticeSessionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student)> SeedBaseDataAsync(OralExamDbContext context, string? maxQuestionsConfig = "10")
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs with .NET 8",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            Role = UserRole.Student,
            IsActive = true
        };

        context.Courses.Add(course);
        context.Users.Add(student);

        if (maxQuestionsConfig != null)
        {
            context.SystemConfigs.Add(new SystemConfig
            {
                Id = Guid.NewGuid(),
                Key = "MaxPracticeQuestionsPerSession",
                Value = maxQuestionsConfig,
                Description = "Số lượng câu hỏi luyện tập tối đa"
            });
        }

        await context.SaveChangesAsync();
        return (course, student);
    }

    private async Task SeedQuestionsAsync(OralExamDbContext context, Guid courseId, string difficulty, int count, bool isActive = true)
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
                IsActive = isActive
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "1. (Happy Path) Khởi tạo thành công phiên luyện tập khi đủ số lượng câu hỏi")]
    public async Task Handle_Should_Succeed_When_Sufficient_Questions_Exist()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 3,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SessionId.Should().NotBeEmpty();
        result.Value.TranscriptBufferSeconds.Should().Be(60);
        result.Value.Questions.Should().HaveCount(3);

        // Xác nhận phiên luyện tập được lưu trong CSDL với trạng thái in_progress
        var sessionInDb = await context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == result.Value.SessionId);
        sessionInDb.Should().NotBeNull();
        sessionInDb!.Status.Should().Be("in_progress");
        sessionInDb.PracticeMode.Should().Be("per_question");
    }

    [Fact(DisplayName = "2. (Edge Case Thiếu Đề) Báo lỗi tiếng Việt chính xác và không tạo phiên rác khi kho đề thiếu câu hỏi")]
    public async Task Handle_Should_Fail_With_Exact_Vietnamese_Message_When_Insufficient_Questions()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "hard", 2); // Chỉ có 2 câu hard

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "hard",
            QuestionCount: 5, // Yêu cầu 5 câu hard
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Kho đề hiện tại chỉ có 2 câu hỏi Khó, vui lòng chọn số lượng ít hơn.");

        // Đảm bảo không tạo bất kỳ phiên luyện tập rác nào trong DB
        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Fact(DisplayName = "3. (Edge Case 0 Câu Hỏi) Báo lỗi chính xác khi kho đề có 0 câu hỏi với độ khó yêu cầu")]
    public async Task Handle_Should_Fail_When_Zero_Questions_Exist_For_Difficulty()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 3); // Có 3 câu easy, 0 câu medium

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "medium",
            QuestionCount: 2,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Kho đề hiện tại chỉ có 0 câu hỏi Trung bình, vui lòng chọn số lượng ít hơn.");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Fact(DisplayName = "4. (Edge Case Vượt Max Admin) Báo lỗi khi QuestionCount vượt quá cấu hình SystemConfig")]
    public async Task Handle_Should_Fail_When_QuestionCount_Exceeds_SystemConfig_Max()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context, maxQuestionsConfig: "10");
        await SeedQuestionsAsync(context, course.Id, "easy", 20); // Kho có đủ 20 câu

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 15, // Vượt quá max 10
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Số lượng câu hỏi yêu cầu (15) vượt quá giới hạn tối đa cho phép của hệ thống (10)");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Fact(DisplayName = "5. (Edge Case Fallback 10) Sử dụng giá trị fallback 10 khi SystemConfig chưa tồn tại trong DB")]
    public async Task Handle_Should_Fallback_To_Default_10_When_SystemConfig_Missing()
    {
        // Arrange
        using var context = CreateDbContext();
        // Không seed SystemConfig (null)
        var (course, student) = await SeedBaseDataAsync(context, maxQuestionsConfig: null);
        await SeedQuestionsAsync(context, course.Id, "easy", 15);

        var handler = new StartPracticeSessionCommandHandler(context);

        // Case A: 10 câu -> hợp lệ theo fallback 10
        var validCommand = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 10,
            IsFullSession: false,
            Topic: null
        );
        var validResult = await handler.Handle(validCommand, CancellationToken.None);
        validResult.IsSuccess.Should().BeTrue();
        validResult.Value!.Questions.Should().HaveCount(10);

        // Case B: 11 câu -> vượt quá fallback 10
        var invalidCommand = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 11,
            IsFullSession: false,
            Topic: null
        );
        var invalidResult = await handler.Handle(invalidCommand, CancellationToken.None);
        invalidResult.IsSuccess.Should().BeFalse();
        invalidResult.Error.Should().Contain("vượt quá giới hạn tối đa cho phép của hệ thống (10)");
    }

    [Fact(DisplayName = "6. Báo lỗi thất bại khi môn học CourseId không tồn tại")]
    public async Task Handle_Should_Return_Failure_When_Course_Not_Found()
    {
        // Arrange
        using var context = CreateDbContext();
        var (_, student) = await SeedBaseDataAsync(context);

        var handler = new StartPracticeSessionCommandHandler(context);
        var nonExistentCourseId = Guid.NewGuid();
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: nonExistentCourseId,
            Difficulty: "easy",
            QuestionCount: 3,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Môn học không tồn tại.");
    }

    [Fact(DisplayName = "7. Khớp độ khó không phân biệt hoa thường (Case-Insensitive)")]
    public async Task Handle_Should_Match_Difficulty_Case_Insensitively()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "hard", 4);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "HaRd", // Viết hoa lẫn thường
            QuestionCount: 2,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Questions.Should().HaveCount(2);
    }

    [Fact(DisplayName = "8. Chỉ lấy câu hỏi đang kích hoạt (IsActive = true), bỏ qua câu hỏi bị ẩn")]
    public async Task Handle_Should_Only_Select_Active_Questions()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 2, isActive: true);
        await SeedQuestionsAsync(context, course.Id, "easy", 3, isActive: false); // 3 câu bị ẩn

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 3, // Cần 3 câu nhưng chỉ có 2 câu active
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Kho đề hiện tại chỉ có 2 câu hỏi Dễ, vui lòng chọn số lượng ít hơn.");
    }

    [Fact(DisplayName = "9. Chọn đúng số lượng câu hỏi phân biệt, không trùng lặp")]
    public async Task Handle_Should_Select_Distinct_Questions()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "medium", 8);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "medium",
            QuestionCount: 4,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var questionIds = result.Value!.Questions.Select(q => q.QuestionId).ToList();
        questionIds.Should().HaveCount(4);
        questionIds.Distinct().Should().HaveCount(4, "Các câu hỏi được chọn phải phân biệt không trùng lặp");
    }

    [Fact(DisplayName = "10. Progressive: Khởi tạo thành công và sắp xếp tăng dần Dễ -> Trung bình -> Khó")]
    public async Task Handle_Should_Succeed_With_Ordered_Difficulties_When_Progressive_Selected()
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
            QuestionCount: 6, // 6/3 = 2 easy, 2 medium, 2 hard
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Questions.Should().HaveCount(6);

        var questionIds = result.Value.Questions.Select(q => q.QuestionId).ToList();
        var questionsInDb = await context.PracticeQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.Difficulty.ToLower());

        // 2 câu đầu phải là easy
        questionsInDb[questionIds[0]].Should().Be("easy");
        questionsInDb[questionIds[1]].Should().Be("easy");

        // 2 câu tiếp theo phải là medium
        questionsInDb[questionIds[2]].Should().Be("medium");
        questionsInDb[questionIds[3]].Should().Be("medium");

        // 2 câu cuối phải là hard
        questionsInDb[questionIds[4]].Should().Be("hard");
        questionsInDb[questionIds[5]].Should().Be("hard");

        var sessionInDb = await context.PracticeSessions.FirstOrDefaultAsync(s => s.Id == result.Value.SessionId);
        sessionInDb.Should().NotBeNull();
        sessionInDb!.PracticeMode.Should().Be("per_question");
    }

    [Theory(DisplayName = "11. Progressive: Phân bổ đúng số lượng Dễ, Trung bình, Khó cho các kích thước 3, 4, 5, 7, 8, 10")]
    [InlineData(3, 1, 1, 1)]
    [InlineData(4, 1, 2, 1)]
    [InlineData(5, 2, 2, 1)]
    [InlineData(7, 2, 3, 2)]
    [InlineData(8, 3, 3, 2)]
    [InlineData(10, 3, 4, 3)]
    public async Task Handle_Should_Distribute_Correct_Count_Per_Difficulty_In_Progressive(
        int totalCount, int expectedEasy, int expectedMedium, int expectedHard)
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 10);
        await SeedQuestionsAsync(context, course.Id, "medium", 10);
        await SeedQuestionsAsync(context, course.Id, "hard", 10);

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: totalCount,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Questions.Should().HaveCount(totalCount);

        var questionIds = result.Value.Questions.Select(q => q.QuestionId).ToList();
        var questionsInDb = await context.PracticeQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.Difficulty.ToLower());

        var easyCount = questionIds.Take(expectedEasy).Count(id => questionsInDb[id] == "easy");
        var mediumCount = questionIds.Skip(expectedEasy).Take(expectedMedium).Count(id => questionsInDb[id] == "medium");
        var hardCount = questionIds.Skip(expectedEasy + expectedMedium).Take(expectedHard).Count(id => questionsInDb[id] == "hard");

        easyCount.Should().Be(expectedEasy);
        mediumCount.Should().Be(expectedMedium);
        hardCount.Should().Be(expectedHard);
    }

    [Fact(DisplayName = "12. Progressive: Báo lỗi và không tạo session khi kho đề thiếu câu hỏi ở bất kỳ mức độ nào")]
    public async Task Handle_Should_Fail_And_Not_Create_Session_When_Progressive_Lacks_Questions()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        await SeedQuestionsAsync(context, course.Id, "easy", 5);
        await SeedQuestionsAsync(context, course.Id, "medium", 5);
        // Không seed câu hard nào (0 câu)

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 3, // Cần 1 easy, 1 medium, 1 hard
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó");
        result.Error.Should().Contain("Khó (có 0)");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0, "Tuyệt đối không tạo phiên rác trong CSDL khi kho đề không đủ câu hỏi");
    }

    [Fact(DisplayName = "13. Progressive: Báo lỗi khi QuestionCount nhỏ hơn MinMixedPracticeQuestions từ SystemConfig")]
    public async Task Handle_Should_Fail_When_Progressive_Count_Less_Than_MinConfig()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "MinMixedPracticeQuestions",
            Value = "3",
            Description = "Số câu tối thiểu progressive"
        });
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 2,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Fact(DisplayName = "14. Progressive: Báo lỗi khi QuestionCount lớn hơn MaxMixedPracticeQuestions từ SystemConfig")]
    public async Task Handle_Should_Fail_When_Progressive_Count_Exceeds_MaxConfig()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "MaxMixedPracticeQuestions",
            Value = "10",
            Description = "Số câu tối đa progressive"
        });
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 11,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Số lượng câu hỏi cho chế độ Dễ đến Khó phải từ 3 đến 10 câu.");

        var sessionCount = await context.PracticeSessions.CountAsync();
        sessionCount.Should().Be(0);
    }

    [Theory(DisplayName = "15. Progressive: Hỗ trợ cả 2 chế độ làm bài [Per-Question] và [Full-Session]")]
    [InlineData(false, "per_question")]
    [InlineData(true, "full_session")]
    public async Task Handle_Should_Support_Both_Modes_In_Progressive(bool isFullSession, string expectedPracticeMode)
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
            Difficulty: "progressive",
            QuestionCount: 3,
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
    }

    [Fact(DisplayName = "16. (SystemConfigs Priority) Ưu tiên đọc TranscriptBufferSeconds từ SystemConfig của Admin thay vì Course")]
    public async Task Handle_Should_Prioritize_TranscriptBufferSeconds_From_SystemConfig()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        course.TranscriptBufferSeconds = 60; // Giá trị của Course
        await SeedQuestionsAsync(context, course.Id, "easy", 3);

        // Admin cấu hình thời gian đệm hệ thống = 45 giây
        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "TranscriptBufferSeconds",
            Value = "45",
            Description = "Cấu hình thời gian đệm của Admin"
        });
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 1,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TranscriptBufferSeconds.Should().Be(45, "Phải ưu tiên cấu hình Admin từ bảng system_configs");
    }

    [Fact(DisplayName = "17. (Fallback BufferSeconds) Fallback về Course.TranscriptBufferSeconds khi chưa có cấu hình trong SystemConfigs")]
    public async Task Handle_Should_Fallback_TranscriptBufferSeconds_When_Config_Missing()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student) = await SeedBaseDataAsync(context);
        course.TranscriptBufferSeconds = 90;
        await SeedQuestionsAsync(context, course.Id, "easy", 3);
        await context.SaveChangesAsync();

        var handler = new StartPracticeSessionCommandHandler(context);
        var command = new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "easy",
            QuestionCount: 1,
            IsFullSession: false,
            Topic: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TranscriptBufferSeconds.Should().Be(90, "Fallback về cấu hình của Course khi SystemConfig chưa có");
    }
}
