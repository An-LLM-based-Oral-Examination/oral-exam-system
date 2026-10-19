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
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. ReplayDlqTaskCommand: Trả về Failure khi không tìm thấy tác vụ DLQ với Id đã cho")]
    public async Task Handle_WhenTaskNotFound_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var handler = new ReplayDlqTaskCommandHandler(context, queueChannelMock.Object);
        var command = new ReplayDlqTaskCommand(Guid.NewGuid());

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
        var command = new ReplayDlqTaskCommand(task.Id);

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
        var command = new ReplayDlqTaskCommand(task.Id);

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
        var command = new ReplayDlqTaskCommand(dlqEntry.Id);

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
}
