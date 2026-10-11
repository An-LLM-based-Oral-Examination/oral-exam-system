using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class SubmitPracticeAnswerCommandHandlerTests
{
    private OralExamDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Nộp câu trả lời MF-01 lưu PracticeAnswer không có AudioUrl và đẩy vào BoundedChannel")]
    public async Task Handle_WithValidCommand_CreatesPracticeAnswerWithoutAudioUrl_AndEnqueuesGrading()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var queueMock = new Mock<IGradingQueueChannel>();

        var studentId = Guid.NewGuid();
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StudentId = studentId,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new SubmitPracticeAnswerCommandHandler(context, queueMock.Object);

        var questionId = Guid.NewGuid();
        var command = new SubmitPracticeAnswerCommand(
            session.Id,
            questionId,
            studentId,
            "Nguyên lý Dependency Inversion trong Clean Architecture.",
            false,
            null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var savedAnswer = await context.PracticeAnswers.FirstOrDefaultAsync(a => a.Id == result.Value);
        savedAnswer.Should().NotBeNull();
        savedAnswer!.AnswerText.Should().Be("Nguyên lý Dependency Inversion trong Clean Architecture.");
        savedAnswer.Status.Should().Be("pending");

        // Verify grading task was enqueued
        queueMock.Verify(q => q.EnqueueAsync(It.Is<GradingTask>(t =>
            t.AnswerId == savedAnswer.Id &&
            t.SessionId == session.Id &&
            t.QuestionId == questionId &&
            t.AnswerText == savedAnswer.AnswerText
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "2. Entity PracticeAnswer không còn thuộc tính AudioUrl")]
    public void Domain_PracticeAnswer_DoesNotContainAudioUrlProperty()
    {
        var prop = typeof(PracticeAnswer).GetProperty("AudioUrl");
        prop.Should().BeNull("Cột AudioUrl đã bị loại bỏ hoàn toàn khỏi bảng practice_answers");
    }

    [Fact(DisplayName = "3. Entity ExamQuestionSubmission (MF-04) bảo toàn 100% AudioR2Url và AudioHashSha256")]
    public void Domain_ExamQuestionSubmission_PreservesSha256AndR2Url_ForMF04()
    {
        var r2Prop = typeof(ExamQuestionSubmission).GetProperty("AudioR2Url");
        r2Prop.Should().NotBeNull("MF-04 thi thật bắt buộc lưu AudioR2Url");

        var hashProp = typeof(ExamQuestionSubmission).GetProperty("AudioHashSha256");
        hashProp.Should().NotBeNull("MF-04 thi thật bắt buộc niêm phong AudioHashSha256");
    }
}
