using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class SubmitMockExamAnswerCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. Nộp câu trả lời Voice-First thành công: stream Whisper bóc băng, lưu transcript và Zero R2")]
    public async Task Handle_Should_Transcribe_And_Save_Answer_Without_R2_Upload()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(25),
            Status = "in_progress"
        };

        var answer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionId,
            Status = "pending",
            TimeTakenSeconds = 0,
            AnswerText = null
        };

        session.Answers.Add(answer);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Kiến trúc Clean Architecture gồm 4 tầng độc lập.");

        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("fake-audio-bytes"));
        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audioStream,
            FileName: "answer.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 45
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AnswerId.Should().Be(answer.Id);
        result.Value.QuestionId.Should().Be(questionId);
        result.Value.Transcript.Should().Be("Kiến trúc Clean Architecture gồm 4 tầng độc lập.");
        result.Value.Status.Should().Be("answered");

        var updatedAnswer = await context.MockExamAnswers.FindAsync(answer.Id);
        updatedAnswer.Should().NotBeNull();
        updatedAnswer!.AnswerText.Should().Be("Kiến trúc Clean Architecture gồm 4 tầng độc lập.");
        updatedAnswer.TimeTakenSeconds.Should().Be(45);
        updatedAnswer.Status.Should().Be("answered");
    }

    [Fact(DisplayName = "2. Hỗ trợ ghi đè câu trả lời khi sinh viên nói lại trước khi hết giờ ca thi")]
    public async Task Handle_Should_Overwrite_Previous_Answer_When_ReAnswered()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            Status = "in_progress"
        };

        var answer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionId,
            Status = "answered",
            TimeTakenSeconds = 30,
            AnswerText = "Bản nói ban đầu chưa đầy đủ."
        };

        session.Answers.Add(answer);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 50,
            DirectTranscript: "Bản nói hiệu đính lần 2 đầy đủ và chính xác hơn."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().Be("Bản nói hiệu đính lần 2 đầy đủ và chính xác hơn.");

        var updatedAnswer = await context.MockExamAnswers.FindAsync(answer.Id);
        updatedAnswer!.AnswerText.Should().Be("Bản nói hiệu đính lần 2 đầy đủ và chính xác hơn.");
        updatedAnswer.TimeTakenSeconds.Should().Be(50);
    }

    [Fact(DisplayName = "3. Chặn nộp bài khi ca thi đã hết thời gian làm bài (quá Master Timer + 10s grace period)")]
    public async Task Handle_Should_Fail_When_Session_Is_Overdue()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-40),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Đã hết giờ 5 phút trước
            Status = "in_progress"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: Guid.NewGuid(),
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 10,
            DirectTranscript: "Trả lời muộn"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("hết thời gian");
    }

    [Fact(DisplayName = "4. Chặn nộp câu trả lời khi ca thi đã nộp hoặc đã chấm điểm")]
    public async Task Handle_Should_Fail_When_Session_Already_Submitted_Or_Graded()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = "graded"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: Guid.NewGuid(),
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 10,
            DirectTranscript: "Nộp thêm"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không thể ghi nhận thêm");
    }

    [Fact(DisplayName = "5. Chặn nộp câu hỏi không thuộc danh sách câu hỏi của ca thi")]
    public async Task Handle_Should_Fail_When_Question_Not_In_Session()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = Guid.NewGuid(),
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            Status = "in_progress"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: Guid.NewGuid(), // QuestionId lạ không có trong Answers
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 10,
            DirectTranscript: "Câu hỏi ngoài đề"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Câu hỏi không thuộc đề thi này");
    }

    [Fact(DisplayName = "6. SubmitMockExamAnswerCommandValidator báo lỗi khi thiếu tham số bắt buộc")]
    public void Validator_Should_Enforce_Required_Parameters()
    {
        var validator = new SubmitMockExamAnswerCommandValidator();

        var invalidCommand = new SubmitMockExamAnswerCommand(
            SessionId: Guid.Empty,
            StudentId: Guid.Empty,
            QuestionId: Guid.Empty,
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: -5,
            DirectTranscript: null
        );

        var result = validator.Validate(invalidCommand);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamAnswerCommand.SessionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamAnswerCommand.StudentId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamAnswerCommand.QuestionId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamAnswerCommand.TimeTakenSeconds));
    }
}
