using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Domain.Entities;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class ReplayDlqTaskCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OralExamDbContext(options);
        DlqTestActors.Seed(context);
        return context;
    }

    [Fact(DisplayName = "1. ReplayDlqTaskCommand: Trả về Failure khi không tìm thấy tác vụ DLQ với Id đã cho")]
    public async Task Handle_WhenTaskNotFound_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);
        var command = new ReplayDlqTaskCommand(Guid.NewGuid(), DlqTestActors.Admin, "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy tác vụ DLQ");
        queueChannelMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "2. ReplayDlqTaskCommand: Trả về Failure khi tác vụ DLQ đã được resolved trước đó")]
    public async Task Handle_WhenTaskAlreadyResolved_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var task = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{}",
            ErrorMessage = "Previous error",
            RetryCount = 3,
            Status = "resolved",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ResolvedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.DeadLetterQueues.Add(task);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);
        var command = new ReplayDlqTaskCommand(task.Id, DlqTestActors.Admin, "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã được giải quyết");
        queueChannelMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "3. ReplayDlqTaskCommand: Trả về Failure khi PayloadJson bị lỗi định dạng")]
    public async Task Handle_WhenPayloadJsonIsInvalid_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var task = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{ corrupt_json: ",
            ErrorMessage = "Previous error",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.DeadLetterQueues.Add(task);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);
        var command = new ReplayDlqTaskCommand(task.Id, DlqTestActors.Admin, "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không thể giải mã dữ liệu tác vụ");
        queueChannelMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "4. ReplayDlqTaskCommand: Đẩy task vào IGradingQueueChannel, chuyển Status = resolved, cập nhật PracticeAnswer.Status = pending")]
    public async Task Handle_WhenValid_EnqueuesTaskAndUpdatesDlqAndAnswerStatus()
    {
        // Arrange
        using var context = CreateDbContext();
        var answerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var answer = new PracticeAnswer
        {
            Id = answerId,
            SessionId = sessionId,
            QuestionId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            AnswerText = "Sinh viên trả lời phần mềm",
            Status = "failed"
        };
        context.PracticeAnswers.Add(answer);

        var gradingTask = new GradingTask
        {
            AnswerId = answerId,
            SessionId = sessionId,
            QuestionId = answer.QuestionId,
            StudentId = answer.StudentId,
            AnswerText = answer.AnswerText
        };

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = JsonSerializer.Serialize(gradingTask),
            ErrorMessage = "Gemini API unavailable",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        queueChannelMock
            .Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);
        var command = new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 1. Enqueue must have been called with the reconstructed task
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.AnswerId == answerId && t.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. DLQ entry updated to resolved
        var updatedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updatedDlq!.Status.Should().Be("resolved");
        updatedDlq.ResolvedAt.Should().NotBeNull();

        // 3. PracticeAnswer updated to pending
        var updatedAnswer = await context.PracticeAnswers.FindAsync(answerId);
        updatedAnswer!.Status.Should().Be("pending");
    }

    [Fact(DisplayName = "5. ReplayDlqTaskCommand: Tác vụ OfficialExamGrading khôi phục GradingJob và ExamQuestionSubmission về pending, đánh dấu DLQ resolved")]
    public async Task Handle_WhenOfficialExamGradingTask_RequeuesGradingJobAndSubmission()
    {
        // Arrange
        using var context = CreateDbContext();
        var submissionId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var submission = new ExamQuestionSubmission
        {
            Id = submissionId,
            TicketId = Guid.NewGuid(),
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Trả lời thi thật MF-04",
            GradingStatus = "failed"
        };
        context.ExamQuestionSubmissions.Add(submission);

        var gradingJob = new GradingJob
        {
            Id = jobId,
            SubmissionId = submissionId,
            Status = "failed",
            AttemptCount = 3,
            LockedUntil = DateTime.UtcNow.AddMinutes(5),
            NextAttemptAt = DateTime.UtcNow.AddMinutes(5),
            LastError = "Gemini 429 Too Many Requests"
        };
        context.GradingJobs.Add(gradingJob);

        var officialTask = new OfficialExamGradingTask(jobId, submissionId);
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            CorrelationId = jobId,
            PayloadJson = JsonSerializer.Serialize(officialTask),
            ErrorMessage = "Gemini 429 Too Many Requests",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Officer, "examination_officer"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        queueChannelMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);

        var updatedJob = await context.GradingJobs.FindAsync(jobId);
        updatedJob!.Status.Should().Be("pending");
        updatedJob.LockedUntil.Should().BeNull();
        updatedJob.NextAttemptAt.Should().NotBeNull();

        var updatedSubmission = await context.ExamQuestionSubmissions.FindAsync(submissionId);
        updatedSubmission!.GradingStatus.Should().Be("pending");

        var updatedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updatedDlq!.Status.Should().Be("resolved");
        updatedDlq.ResolvedAt.Should().NotBeNull();
        updatedDlq.NextRetryAt.Should().BeNull();
    }

    [Fact(DisplayName = "6. ReplayDlqTaskCommand: Tác vụ MockExamGrading khôi phục MockExamSession về submitted và đánh dấu DLQ resolved")]
    public async Task Handle_WhenMockExamGradingTask_ResetsMockExamSessionStatusToSubmitted()
    {
        // Arrange
        using var context = CreateDbContext();
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = sessionId,
            StudentId = studentId,
            CourseId = courseId,
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            TotalScore = 5.0m
        };
        context.MockExamSessions.Add(session);

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "MockExamGrading",
            FlowType = "MOCK_EXAM",
            ManagedByRole = "admin",
            CorrelationId = sessionId,
            PayloadJson = JsonSerializer.Serialize(new { SessionId = sessionId, StudentId = studentId }),
            ErrorMessage = "Transient Gemini error",
            RetryCount = 1,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedSession = await context.MockExamSessions.FindAsync(sessionId);
        updatedSession!.Status.Should().Be("submitted");
        updatedSession.TotalScore.Should().BeNull();

        var updatedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updatedDlq!.Status.Should().Be("resolved");
        updatedDlq.ResolvedAt.Should().NotBeNull();
        updatedDlq.NextRetryAt.Should().BeNull();
    }

    [Fact(DisplayName = "6b. ReplayDlqTaskCommand: MockExamGrading trả về Failure và giữ DLQ pending khi AI vẫn lỗi DEFERRED_GRADING")]
    public async Task Handle_WhenMockExamGradingFailsAgain_ReturnsFailureAndKeepsDlqPending()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET 8" };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10.0m };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain DI",
            Difficulty = "easy",
            BloomLevel = "Understand"
        };
        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            Status = "submitted",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            TotalScore = null
        };
        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            OrderIndex = 1,
            AnswerText = "DI decouples components.",
            Status = "answered"
        });
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "MockExamGrading",
            FlowType = "MOCK_EXAM",
            ManagedByRole = "admin",
            CorrelationId = session.Id,
            PayloadJson = JsonSerializer.Serialize(new { SessionId = session.Id, StudentId = studentId }),
            ErrorMessage = "Initial timeout",
            RetryCount = 1,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.MockExamSessions.Add(session);
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Gemini 429 rate limited"));

        var handler = new ReplayDlqTaskCommandHandler(context, new Mock<IGradingQueueChannel>().Object, aiMock.Object);
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("DEFERRED_GRADING:");
        var refreshedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        refreshedDlq!.Status.Should().Be("pending");
    }

    [Fact(DisplayName = "7. ReplayDlqTaskCommand: Replay tác vụ Practice với role admin thành công")]
    public async Task Handle_WhenPracticeTask_WithAdminRole_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var answerId = Guid.NewGuid();
        var gradingTask = new GradingTask { AnswerId = answerId };

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = JsonSerializer.Serialize(gradingTask),
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "8. ReplayDlqTaskCommand: Replay tác vụ Practice hoặc MockExam với role khác admin bị từ chối FORBIDDEN")]
    public async Task Handle_WhenPracticeOrMockTask_WithNonAdminRole_ReturnsForbiddenFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act - Thử bằng role examination_officer hoặc lecturer
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Officer, "examination_officer"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
    }

    [Fact(DisplayName = "9. ReplayDlqTaskCommand: Replay tác vụ OfficialExam với role khác examination_officer bị từ chối FORBIDDEN")]
    public async Task Handle_WhenOfficialExamTask_WithNonExamOfficerRole_ReturnsForbiddenFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(Guid.NewGuid(), Guid.NewGuid())),
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act - Thử bằng role admin
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
    }

    [Fact(DisplayName = "10. ReplayDlqTaskCommand: Ghi nhận ResolvedByUserId khi người dùng thực hiện replay")]
    public async Task Handle_WhenActorUserIdProvided_RecordsResolvedByUserId()
    {
        // Arrange
        using var context = CreateDbContext();
        var actorUserId = Guid.NewGuid();
        context.Users.Add(DlqTestActors.User(actorUserId, "admin"));
        var answerId = Guid.NewGuid();
        var gradingTask = new GradingTask { AnswerId = answerId };

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = JsonSerializer.Serialize(gradingTask),
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, ActorUserId: actorUserId, ActorRole: "admin"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updatedDlq!.ResolvedByUserId.Should().Be(actorUserId);
    }

    [Fact(DisplayName = "11. ReplayDlqTaskCommand: InClassGrading đẩy tác vụ vào GradingQueueChannel, cập nhật InClassExamSubmission về SUBMITTED và giải quyết DLQ")]
    public async Task Handle_WhenInClassGrading_EnqueuesTask_UpdatesSubmissionStatus_AndResolvesDlq()
    {
        // Arrange
        using var context = CreateDbContext();
        var submissionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        context.InClassExamShifts.Add(new InClassExamShift { Id = shiftId, ClassId = Guid.NewGuid(), LecturerId = DlqTestActors.Lecturer, ShiftName = "QA", TotpSecretKey = "QA" });
        var studentId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var submission = new InClassExamSubmission
        {
            Id = submissionId,
            ShiftId = shiftId,
            StudentId = studentId,
            QuestionId = questionId,
            Transcript = "Kiến trúc Clean Architecture gồm 4 tầng độc lập.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.InClassExamSubmissions.Add(submission);

        var gradingTask = new GradingTask
        {
            AnswerId = submissionId,
            SessionId = shiftId,
            QuestionId = questionId,
            StudentId = studentId,
            AnswerText = submission.Transcript,
            InClassSubmissionId = submissionId
        };

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = JsonSerializer.Serialize(gradingTask),
            ErrorMessage = "Gemini API HTTP 429 Too Many Requests",
            Status = "pending",
            RetryCount = 3,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        queueChannelMock
            .Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act - DLQ MF-05 do Admin quản lý, không giao quyền replay cho giảng viên.
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Admin, "admin"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 1. Kiểm tra EnqueueAsync được gọi đúng task
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.InClassSubmissionId == submissionId && t.AnswerText == submission.Transcript),
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. Kiểm tra submission được cập nhật Status = "SUBMITTED"
        var updatedSubmission = await context.InClassExamSubmissions.FindAsync(submissionId);
        updatedSubmission!.Status.Should().Be("SUBMITTED");

        // 3. Kiểm tra DLQ được giải quyết
        var updatedDlq = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updatedDlq!.Status.Should().Be("resolved");
        updatedDlq.ResolvedAt.Should().NotBeNull();
        updatedDlq.NextRetryAt.Should().BeNull();
    }

    [Fact(DisplayName = "12. ReplayDlqTaskCommand: InClassGrading chặn vai trò không hợp lệ (ví dụ examination_officer hoặc student) trả về FORBIDDEN")]
    public async Task Handle_WhenInClassGrading_UnauthorizedRole_ReturnsForbidden()
    {
        // Arrange
        using var context = CreateDbContext();
        var submissionId = Guid.NewGuid();

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = "{}",
            ErrorMessage = "Network error",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);

        // Act - Role examination_officer cố replay ca thi trong lớp
        var result = await handler.Handle(new ReplayDlqTaskCommand(dlqEntry.Id, DlqTestActors.Officer, "examination_officer"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }
}
