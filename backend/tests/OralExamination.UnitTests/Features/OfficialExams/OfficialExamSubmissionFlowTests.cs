using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamSubmissionFlowTests
{
    private const string WorkstationIp = "192.168.10.21";
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CreateUploadAuthorization_OwnInProgressTicket_ReturnsImmutableFileName()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedTicketAsync(context, audioSealed: false);
        var expectedKey = OfficialExamAudioObjectKey.Create(
            fixture.Ticket.ShiftId,
            fixture.Ticket.Id,
            fixture.Ticket.SeatNumber,
            fixture.Student.StudentCode!);
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.CreatePresignedUploadUrlAsync(
                expectedKey,
                "audio/webm",
                TimeSpan.FromMinutes(OfficialExamRules.AudioUploadUrlLifetimeMinutes),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoragePresignedUploadResult(
                "https://example.r2/upload-signature",
                expectedKey,
                DateTime.UtcNow.AddMinutes(10),
                new Dictionary<string, string>
                {
                    ["Content-Type"] = "audio/webm",
                    ["If-None-Match"] = "*"
                }));

        var result = await new CreateAudioUploadAuthorizationCommandHandler(context, storage.Object)
            .Handle(new CreateAudioUploadAuthorizationCommand(
                fixture.Student.Id,
                fixture.Ticket.Id,
                WorkstationIp,
                DateTime.UtcNow), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("01_SE170123.webm");
        result.Value.ObjectKey.Should().Be(expectedKey);
        result.Value.RequiredHeaders["If-None-Match"].Should().Be("*");
    }

    [Fact]
    public async Task SealAudio_VerifiesRealObjectHash_BeforePersistingSeal()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedTicketAsync(context, audioSealed: false);
        var objectKey = OfficialExamAudioObjectKey.Create(
            fixture.Ticket.ShiftId,
            fixture.Ticket.Id,
            fixture.Ticket.SeatNumber,
            fixture.Student.StudentCode!);
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(objectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(
                objectKey,
                1_024,
                "audio/webm",
                "etag",
                DateTime.UtcNow,
                null));
        storage.Setup(x => x.VerifySha256Async(objectKey, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new SealOfficialExamAudioCommandHandler(context, storage.Object)
            .Handle(new SealOfficialExamAudioCommand(
                fixture.Student.Id,
                fixture.Ticket.Id,
                WorkstationIp,
                objectKey,
                ValidSha256), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var savedTicket = await context.StudentExamTickets.SingleAsync();
        savedTicket.AudioObjectKey.Should().Be(objectKey);
        savedTicket.AudioHashSha256.Should().Be(ValidSha256);
        savedTicket.AudioVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SealAudio_HashMismatch_DoesNotSealTicket()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedTicketAsync(context, audioSealed: false);
        var objectKey = OfficialExamAudioObjectKey.Create(
            fixture.Ticket.ShiftId,
            fixture.Ticket.Id,
            fixture.Ticket.SeatNumber,
            fixture.Student.StudentCode!);
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(objectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(objectKey, 1_024, "audio/webm", null, null, null));
        storage.Setup(x => x.VerifySha256Async(objectKey, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new SealOfficialExamAudioCommandHandler(context, storage.Object)
            .Handle(new SealOfficialExamAudioCommand(
                fixture.Student.Id,
                fixture.Ticket.Id,
                WorkstationIp,
                objectKey,
                ValidSha256), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.AudioHashMismatch);
        (await context.StudentExamTickets.SingleAsync()).AudioVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Submit_SealedAudio_PersistsSubmissionsJobsAndTicketAtomically()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedTicketAsync(context, audioSealed: true);
        var answers = fixture.Questions.Select((question, index) =>
            new OfficialExamAnswerSubmissionRequest(
                question.Id,
                $"Câu trả lời {index + 1} có đầy đủ nội dung kỹ thuật.",
                "[]",
                60)).ToList();
        var handler = new SubmitOfficialExamCommandHandler(context);

        var result = await handler.Handle(new SubmitOfficialExamCommand(
            fixture.Student.Id,
            fixture.Ticket.Id,
            WorkstationIp,
            answers), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ExamTicketStatus.Submitted);
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(answers.Count);
        (await context.GradingJobs.CountAsync()).Should().Be(answers.Count);
        var savedTicket = await context.StudentExamTickets.SingleAsync();
        savedTicket.Status.Should().Be(ExamTicketStatus.Submitted);
        savedTicket.SubmittedAt.Should().NotBeNull();

        var repeated = await handler.Handle(new SubmitOfficialExamCommand(
            fixture.Student.Id,
            fixture.Ticket.Id,
            WorkstationIp,
            answers), CancellationToken.None);
        repeated.IsSuccess.Should().BeTrue();
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(answers.Count);
        (await context.GradingJobs.CountAsync()).Should().Be(answers.Count);
    }

    [Fact]
    public async Task Submit_UnsealedAudio_IsRejectedWithoutDurableJobs()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedTicketAsync(context, audioSealed: false);
        var answers = fixture.Questions.Select(question =>
            new OfficialExamAnswerSubmissionRequest(question.Id, "Transcript hợp lệ", "[]", 60)).ToList();

        var result = await new SubmitOfficialExamCommandHandler(context)
            .Handle(new SubmitOfficialExamCommand(
                fixture.Student.Id,
                fixture.Ticket.Id,
                WorkstationIp,
                answers), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.AudioNotSealed);
        (await context.ExamQuestionSubmissions.CountAsync()).Should().Be(0);
        (await context.GradingJobs.CountAsync()).Should().Be(0);
    }

    private static async Task<SubmissionFixture> SeedTicketAsync(OralExamDbContext context, bool audioSealed)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Sinh viên kiểm thử",
            StudentCode = "SE170123",
            Role = UserRole.Student,
            IsActive = true
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca kiểm thử",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            Status = ExamShiftWindow.InProgress
        };
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            SetCode = "BE10-TEST",
            IsActive = true
        };
        var questions = new[]
        {
            CreateQuestion("Question 1"),
            CreateQuestion("Question 2")
        };
        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.ExamSets.Add(examSet);
        context.ExamQuestions.AddRange(questions);
        context.ExamSetQuestions.AddRange(questions.Select((question, index) => new ExamSetQuestion
        {
            ExamSetId = examSet.Id,
            ExamQuestionId = question.Id,
            OrderIndex = index + 1
        }));

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            IpAddress = WorkstationIp,
            Status = ExamTicketStatus.InProgress
        };
        if (audioSealed)
        {
            ticket.AudioObjectKey = OfficialExamAudioObjectKey.Create(
                shift.Id,
                ticket.Id,
                ticket.SeatNumber,
                student.StudentCode);
            ticket.AudioR2Url = ticket.AudioObjectKey;
            ticket.AudioHashSha256 = ValidSha256;
            ticket.AudioVerifiedAt = DateTime.UtcNow;
        }

        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();
        return new SubmissionFixture(student, ticket, questions);
    }

    private static ExamQuestion CreateQuestion(string title) => new()
    {
        Id = Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        RubricId = Guid.NewGuid(),
        Title = title,
        Content = "Nội dung câu hỏi",
        ApprovalStatus = "APPROVED",
        IsActive = true
    };

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private sealed record SubmissionFixture(
        User Student,
        StudentExamTicket Ticket,
        IReadOnlyList<ExamQuestion> Questions);
}
