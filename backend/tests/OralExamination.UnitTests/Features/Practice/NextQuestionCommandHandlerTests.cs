using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class NextQuestionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, PracticeSession Session)> SeedSessionAsync(
        OralExamDbContext context,
        List<string>? selectedDifficulties = null,
        string mode = "per_question",
        string status = "in_progress",
        int timeoutMinutes = 10)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWT301",
            Name = "Software Testing",
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

        var diffJson = selectedDifficulties != null
            ? JsonSerializer.Serialize(selectedDifficulties)
            : JsonSerializer.Serialize(new List<string> { "easy", "medium", "hard" });

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Status = status,
            PracticeMode = mode,
            SelectedDifficulties = diffJson
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);

        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "SessionInactivityTimeoutMinutes",
            Value = timeoutMinutes.ToString(),
            Description = "Timeout luyện tập không tương tác"
        });

        await context.SaveChangesAsync();
        return (course, student, session);
    }

    private async Task SeedQuestionsAsync(OralExamDbContext context, Guid courseId, string difficulty, int count)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = $"Rubric {difficulty}",
            TotalMaxScore = 10.0m
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Tiêu chí 1",
            Description = $"Tiêu chí đánh giá cho {difficulty}",
            MaxScore = 10.0m,
            OrderIndex = 1
        });
        context.Rubrics.Add(rubric);

        for (int i = 1; i <= count; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                RubricId = rubric.Id,
                Title = $"Câu {difficulty} #{i}",
                Content = $"Nội dung chi tiết câu hỏi {difficulty} #{i}",
                Difficulty = difficulty,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact(DisplayName = "1. NextQuestion bốc câu hỏi chính xác khi chọn đơn mức độ easy")]
    public async Task Handle_WithSingleDifficulty_ShouldReturnQuestionOfThatDifficulty()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy" });
        await SeedQuestionsAsync(context, course.Id, "easy", 5);
        await SeedQuestionsAsync(context, course.Id, "hard", 5); // Khác độ khó để kiểm tra không bị lẫn

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question.Should().NotBeNull();
        result.Value.Question!.Difficulty.ToLower().Should().Be("easy");
        result.Value.Question.QuestionOrder.Should().Be(1);
        result.Value.Question.RubricCriteria.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "2. NextQuestion bốc câu hỏi thuộc tập độ khó khi chọn tổ hợp [easy, medium]")]
    public async Task Handle_WithCombinationOfTwoDifficulties_ShouldReturnQuestionWithinSelectedDifficulties()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });
        await SeedQuestionsAsync(context, course.Id, "easy", 3);
        await SeedQuestionsAsync(context, course.Id, "medium", 3);
        await SeedQuestionsAsync(context, course.Id, "hard", 10); // Không được bốc hard

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question.Should().NotBeNull();
        var diff = result.Value.Question!.Difficulty.ToLower();
        diff.Should().BeOneOf("easy", "medium");
        diff.Should().NotBe("hard");
    }

    [Fact(DisplayName = "3. NextQuestion bốc câu hỏi thuộc tập độ khó khi chọn tổ hợp [easy, medium, hard]")]
    public async Task Handle_WithCombinationOfThreeDifficulties_ShouldReturnQuestionWithinSelectedDifficulties()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium", "hard" });
        await SeedQuestionsAsync(context, course.Id, "easy", 2);
        await SeedQuestionsAsync(context, course.Id, "medium", 2);
        await SeedQuestionsAsync(context, course.Id, "hard", 2);

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question.Should().NotBeNull();
        result.Value.Question!.Difficulty.ToLower().Should().BeOneOf("easy", "medium", "hard");
    }

    [Fact(DisplayName = "4. NextQuestion không bao giờ bốc lại câu hỏi sinh viên đã trả lời trong phiên")]
    public async Task Handle_ShouldNeverReturnAlreadyAnsweredQuestions()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy" });
        await SeedQuestionsAsync(context, course.Id, "easy", 2);

        var existingQuestions = await context.PracticeQuestions.Where(q => q.CourseId == course.Id).ToListAsync();
        var answeredQuestion = existingQuestions[0];
        var remainingQuestion = existingQuestions[1];

        // Giả lập sinh viên đã trả lời câu 1
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = answeredQuestion.Id,
            StudentId = student.Id,
            AnswerText = "Sinh viên đã trả lời câu này",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question!.Id.Should().Be(remainingQuestion.Id);
        result.Value.Question.Id.Should().NotBe(answeredQuestion.Id);
        result.Value.Question.QuestionOrder.Should().Be(2);
    }

    [Fact(DisplayName = "5. NextQuestion tự động fallback sang các mức còn lại khi một mức độ trong tổ hợp cạn câu")]
    public async Task Handle_WhenOneBucketIsExhausted_ShouldFallbackToRemainingAvailableDifficulties()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });
        await SeedQuestionsAsync(context, course.Id, "easy", 1);
        await SeedQuestionsAsync(context, course.Id, "medium", 3);

        var easyQuestion = await context.PracticeQuestions.FirstAsync(q => q.Difficulty == "easy");

        // Giả lập câu easy duy nhất đã được trả lời
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestion.Id,
            StudentId = student.Id,
            AnswerText = "Đã làm câu easy",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
            IsFollowUp = false
        });
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question!.Difficulty.ToLower().Should().Be("medium");
    }

    [Fact(DisplayName = "6. NextQuestion trả về HasMoreQuestions = false khi tất cả các mức đã chọn cạn sạch câu hỏi")]
    public async Task Handle_WhenAllBucketsExhausted_ShouldReturnHasMoreQuestionsFalse()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, new List<string> { "easy", "medium" });
        // Không seed câu hỏi nào cho môn học

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeFalse();
        result.Value.Question.Should().BeNull();
        result.Value.Message.Should().Contain("Đã hoàn thành toàn bộ câu hỏi khả dụng");
    }

    [Fact(DisplayName = "7. NextQuestion trả về Failure khi phiên không tồn tại hoặc sai quyền sinh viên")]
    public async Task Handle_WhenSessionNotFoundOrWrongStudent_ShouldReturnFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context);

        var handler = new NextQuestionCommandHandler(context);

        // Act 1: Sai session id
        var nonExistentResult = await handler.Handle(new NextQuestionCommand(Guid.NewGuid(), student.Id), CancellationToken.None);
        // Act 2: Sai student id
        var wrongStudentResult = await handler.Handle(new NextQuestionCommand(session.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        nonExistentResult.IsSuccess.Should().BeFalse();
        nonExistentResult.Error.Should().Contain("không tồn tại");

        wrongStudentResult.IsSuccess.Should().BeFalse();
        wrongStudentResult.Error.Should().Contain("không thuộc về sinh viên");
    }

    [Fact(DisplayName = "8. NextQuestion trả về Failure khi phiên không ở chế độ per_question")]
    public async Task Handle_WhenSessionNotPerQuestionMode_ShouldReturnFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var (course, student, session) = await SeedSessionAsync(context, mode: "full_session");
        await SeedQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("chỉ áp dụng cho chế độ [Per-Question]");
    }
}
