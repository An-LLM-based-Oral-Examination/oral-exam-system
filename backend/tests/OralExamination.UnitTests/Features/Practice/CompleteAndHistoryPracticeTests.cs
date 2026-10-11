using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class CompleteAndHistoryPracticeTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "CompletePracticeSession: Cập nhật trạng thái completed và EndedAt thành công")]
    public async Task CompletePracticeSession_Should_Update_Status_And_EndedAt()
    {
        // Arrange
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var session = new PracticeSession
        {
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new CompletePracticeSessionCommandHandler(context);
        var command = new CompletePracticeSessionCommand(session.Id, studentId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedSession = await context.PracticeSessions.FindAsync(session.Id);
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be("completed");
        updatedSession.CompletedAt.Should().NotBeNull();
        updatedSession.CompletedAt.Should().BeAfter(updatedSession.StartedAt);
    }

    [Fact(DisplayName = "CompletePracticeSession: Trả về lỗi khi không tìm thấy phiên")]
    public async Task CompletePracticeSession_Should_Fail_When_Session_Not_Found()
    {
        // Arrange
        using var context = CreateDbContext();
        var handler = new CompletePracticeSessionCommandHandler(context);
        var command = new CompletePracticeSessionCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
    }

    [Fact(DisplayName = "CompletePracticeSession: Trả về lỗi khi sinh viên không sở hữu phiên")]
    public async Task CompletePracticeSession_Should_Fail_When_Student_Mismatch()
    {
        // Arrange
        using var context = CreateDbContext();
        var session = new PracticeSession
        {
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new CompletePracticeSessionCommandHandler(context);
        var command = new CompletePracticeSessionCommand(session.Id, Guid.NewGuid()); // Khác StudentId

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
    }

    [Fact(DisplayName = "CompletePracticeSession: Có tính Idempotent khi phiên đã completed trước đó")]
    public async Task CompletePracticeSession_Should_Be_Idempotent_When_Already_Completed()
    {
        // Arrange
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var completedTime = DateTime.UtcNow.AddMinutes(-10);
        var session = new PracticeSession
        {
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            CompletedAt = completedTime
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new CompletePracticeSessionCommandHandler(context);
        var command = new CompletePracticeSessionCommand(session.Id, studentId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var existing = await context.PracticeSessions.FindAsync(session.Id);
        existing!.CompletedAt.Should().Be(completedTime); // Không bị ghi đè thời gian kết thúc cũ
    }

    [Fact(DisplayName = "GetStudentPracticeHistory: Trả về lịch sử luyện tập kèm điểm số trung bình")]
    public async Task GetStudentPracticeHistory_Should_Return_History_With_Average_Score()
    {
        // Arrange
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Code = "SWP391",
            Name = "Software Development Project"
        };
        context.Courses.Add(course);

        var session = new PracticeSession
        {
            StudentId = studentId,
            CourseId = course.Id,
            Course = course,
            PracticeMode = "per_question",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-40)
        };
        context.PracticeSessions.Add(session);

        var answer1 = new PracticeAnswer
        {
            SessionId = session.Id,
            QuestionId = Guid.NewGuid(),
            StudentId = studentId,
            AnswerText = "Trả lời câu 1",
            IsFollowUp = false,
            Status = "graded"
        };
        var eval1 = new AiEvaluation
        {
            PracticeAnswerId = answer1.Id,
            AnswerType = "practice",
            TotalScore = 8.0m,
            Feedback = "Tốt"
        };
        answer1.AiEvaluations.Add(eval1);

        var answer2 = new PracticeAnswer
        {
            SessionId = session.Id,
            QuestionId = Guid.NewGuid(),
            StudentId = studentId,
            AnswerText = "Trả lời câu 2",
            IsFollowUp = false,
            Status = "graded"
        };
        var eval2 = new AiEvaluation
        {
            PracticeAnswerId = answer2.Id,
            AnswerType = "practice",
            TotalScore = 6.0m,
            Feedback = "Khá"
        };
        answer2.AiEvaluations.Add(eval2);

        context.PracticeAnswers.AddRange(answer1, answer2);
        await context.SaveChangesAsync();

        var handler = new GetStudentPracticeHistoryQueryHandler(context);
        var query = new GetStudentPracticeHistoryQuery(studentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);

        var item = result.Value[0];
        item.SessionId.Should().Be(session.Id);
        item.CourseCode.Should().Be("SWP391");
        item.CourseName.Should().Be("Software Development Project");
        item.Status.Should().Be("completed");
        item.TotalQuestions.Should().Be(2);
        item.AnsweredQuestions.Should().Be(2);
        item.AverageScore.Should().Be(7.0m); // (8.0 + 6.0) / 2 = 7.0
    }

    [Fact(DisplayName = "GetStudentPracticeHistory: Trả về danh sách rỗng khi sinh viên chưa có phiên nào")]
    public async Task GetStudentPracticeHistory_Should_Return_Empty_When_No_Sessions()
    {
        // Arrange
        using var context = CreateDbContext();
        var handler = new GetStudentPracticeHistoryQueryHandler(context);
        var query = new GetStudentPracticeHistoryQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
