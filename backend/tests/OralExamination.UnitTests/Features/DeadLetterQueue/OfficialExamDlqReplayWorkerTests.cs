using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class OfficialExamDlqReplayWorkerTests
{
    private static (IServiceScopeFactory ScopeFactory, OralExamDbContext Context) CreateTestEnvironment()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<OralExamDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<OralExamDbContext>();

        return (scopeFactory, context);
    }

    [Fact(DisplayName = "1. OfficialExamDlqReplayWorker: Bài nộp có DLQ mang trạng thái 'abandoned' bị loại trừ, không bị đưa lại vào hàng đợi (Chặn Infinite Replay Loop)")]
    public async Task ReplayDueAsync_WhenSubmissionHasAbandonedDlq_DoesNotReEnqueue()
    {
        // Arrange
        var (scopeFactory, context) = CreateTestEnvironment();
        var submissionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        // Bài nộp bị kẹt quá 2 phút
        var submission = new InClassExamSubmission
        {
            Id = submissionId,
            ShiftId = shiftId,
            StudentId = studentId,
            QuestionId = questionId,
            Transcript = "Bài làm bị lỗi xử lý.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.InClassExamSubmissions.Add(submission);

        // Bản ghi DLQ đã bị đánh dấu abandoned (sau 9 lần thất bại)
        var abandonedDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = "{}",
            ErrorMessage = "Model hallucination / permanent error",
            RetryCount = 9,
            Status = "abandoned",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            LastAttemptAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.DeadLetterQueues.Add(abandonedDlq);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Channel TUYỆT ĐỐI KHÔNG nhận task cho submission đã bị abandoned
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.InClassSubmissionId == submissionId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "2. OfficialExamDlqReplayWorker: Bài nộp có DLQ pending với NextRetryAt trong tương lai bị loại trừ do đang backoff")]
    public async Task ReplayDueAsync_WhenSubmissionHasPendingBackoffDlq_DoesNotReEnqueue()
    {
        // Arrange
        var (scopeFactory, context) = CreateTestEnvironment();
        var submissionId = Guid.NewGuid();

        var submission = new InClassExamSubmission
        {
            Id = submissionId,
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Transcript",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.InClassExamSubmissions.Add(submission);

        // DLQ đang pending với NextRetryAt = 4 phút nữa
        var pendingDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = "{}",
            ErrorMessage = "Transient HTTP 429",
            RetryCount = 3,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(4),
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        context.DeadLetterQueues.Add(pendingDlq);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Không khôi phục sớm trước khi NextRetryAt đến hạn
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.InClassSubmissionId == submissionId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "3. OfficialExamDlqReplayWorker: Bài nộp bị kẹt quá 2 phút không có DLQ thì được tự động khôi phục vào hàng đợi chấm")]
    public async Task ReplayDueAsync_WhenSubmissionIsStuckWithoutDlq_ReEnqueuesSuccessfully()
    {
        // Arrange
        var (scopeFactory, context) = CreateTestEnvironment();
        var submissionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var submission = new InClassExamSubmission
        {
            Id = submissionId,
            ShiftId = shiftId,
            StudentId = studentId,
            QuestionId = questionId,
            Transcript = "Transcript hợp lệ cần chấm bù do sập server.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-3) // Quá 2 phút
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        queueChannelMock
            .Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Được đưa vào queue chấm ngầm
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.InClassSubmissionId == submissionId && t.AnswerText == submission.Transcript),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "4. OfficialExamDlqReplayWorker: DLQ InClassGrading có RetryCount >= 9 tự động chuyển sang status 'abandoned'")]
    public async Task ReplayDueAsync_WhenDlqRetryCountReachesMax_MarksAsAbandoned()
    {
        // Arrange
        var (scopeFactory, context) = CreateTestEnvironment();
        var submissionId = Guid.NewGuid();

        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = "{}",
            ErrorMessage = "Repeated permanent error",
            RetryCount = 9, // Đã đạt ngưỡng 9 lần
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1), // Đã đến hạn
            CreatedAt = DateTime.UtcNow.AddMinutes(-60)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Bản ghi DLQ chuyển sang 'abandoned'
        var updatedDlq = await context.DeadLetterQueues.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dlqEntry.Id);
        updatedDlq!.Status.Should().Be("abandoned");
        updatedDlq.NextRetryAt.Should().BeNull();
    }
}
