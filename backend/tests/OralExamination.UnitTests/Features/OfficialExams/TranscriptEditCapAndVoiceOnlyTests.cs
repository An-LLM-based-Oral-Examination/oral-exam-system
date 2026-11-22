using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class TranscriptEditCapAndVoiceOnlyTests
{
    private const string DeviceId = "BYOD-EDIT-CAP-TEST-0001";
    private const string SessionToken = "valid-session-token-12345678901234567890";
    private static readonly string ValidSha256 = new('a', 64);

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public void Calculator_ExactBoundary_ZeroPercent_35Percent_35Point1Percent_EmptyRaw_AndVietnameseNfc()
    {
        TranscriptEditRatioCalculator.CalculateRatio("Clean Architecture", "Clean Architecture").Should().Be(0m);

        var base100 = new string('a', 100);
        var edited35 = new string('b', 35) + new string('a', 65);
        var edited36 = new string('b', 36) + new string('a', 64);

        var ok35 = TranscriptEditRatioCalculator.ValidateAndCompute(
            ExamInputMode.VoiceWithTranscriptEdit, base100, edited35, isManuallyEdited: true);
        ok35.IsSuccess.Should().BeTrue();
        ok35.Value.Should().Be(0.3500m);

        var fail36 = TranscriptEditRatioCalculator.ValidateAndCompute(
            ExamInputMode.VoiceWithTranscriptEdit, base100, edited36, isManuallyEdited: true);
        fail36.IsSuccess.Should().BeFalse();
        fail36.Error.Should().StartWith(OfficialExamErrors.TranscriptEditLimitExceeded);

        // 35.1% boundary (351 / 1000)
        var base1000 = new string('x', 1000);
        var edited351 = new string('y', 351) + new string('x', 649);
        var fail351 = TranscriptEditRatioCalculator.ValidateAndCompute(
            ExamInputMode.VoiceWithTranscriptEdit, base1000, edited351, isManuallyEdited: true);
        fail351.IsSuccess.Should().BeFalse();
        fail351.Error.Should().StartWith(OfficialExamErrors.TranscriptEditLimitExceeded);

        // Empty raw voice + typed text -> 100% edit ratio -> rejected
        var fabricated = TranscriptEditRatioCalculator.ValidateAndCompute(
            ExamInputMode.VoiceWithTranscriptEdit, "", "Sinh viên tự gõ câu trả lời hoàn toàn mới", isManuallyEdited: true);
        fabricated.IsSuccess.Should().BeFalse();
        fabricated.Error.Should().StartWith(OfficialExamErrors.TranscriptEditLimitExceeded);

        // Vietnamese NFC vs NFD normalization + whitespace collapse -> 0% edit
        var nfc = "Kiến trúc sạch giúp tách biệt tầng nghiệp vụ".Normalize(NormalizationForm.FormC);
        var nfd = "Kiến   trúc sạch  giúp tách biệt tầng nghiệp vụ".Normalize(NormalizationForm.FormD);
        TranscriptEditRatioCalculator.CalculateRatio(nfc, nfd).Should().Be(0m);
    }

    private static async Task<(User Student, StudentExamTicket Ticket, ExamQuestion Question, string AudioKey)> SeedExamAsync(
        OralExamDbContext context,
        string examInputMode)
    {
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.cap@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.cap@fpt.edu.vn", StudentCode = "SE172001", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET", Credits = 3, SemesterId = semester.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = officer.Id, Name = "S1", IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "FE PRN231",
            ExamDate = new DateOnly(2026, 11, 20),
            ExamInputMode = examInputMode,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow.AddMinutes(45),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var question = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain DI", ApprovalStatus = "APPROVED", IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "SET-CAP-01", Status = ExamSetStatus.HandedOver, IsActive = true };

        var ticketId = Guid.NewGuid();
        var perTurnKey = OfficialExamAudioObjectKey.CreateForQuestion(shift.Id, ticketId, 1, student.StudentCode!, 1, 0);
        var ticketAudioKey = OfficialExamAudioObjectKey.Create(shift.Id, ticketId, 1, student.StudentCode!);

        var ticket = new StudentExamTicket
        {
            Id = ticketId,
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = DeviceId,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-10),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(SessionToken),
            AudioObjectKey = ticketAudioKey,
            AudioHashSha256 = ValidSha256,
            AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        context.Users.AddRange(officer, student);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.Rubrics.Add(rubric);
        context.ExamQuestions.Add(question);
        context.ExamSets.Add(examSet);
        context.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = question.Id, OrderIndex = 1 });
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        return (student, ticket, question, perTurnKey);
    }

    private static Mock<IStorageService> CreateVerifiedStorageMock(string key)
    {
        var mock = new Mock<IStorageService>();
        mock.Setup(s => s.GetObjectMetadataAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageObjectMetadata(key, 4096, "audio/webm", "etag", DateTime.UtcNow));
        mock.Setup(s => s.VerifySha256Async(key, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    [Fact]
    public async Task VoiceOnly_RejectsManualEditOnBatchAndPerTurn_AndAllowsEmptyTranscriptForWhisperFallback()
    {
        // 1. Per-turn rejects manual edit in VoiceOnly
        await using (var context = CreateDbContext())
        {
            var (student, ticket, question, audioKey) = await SeedExamAsync(context, ExamInputMode.VoiceOnly);
            var storage = CreateVerifiedStorageMock(audioKey);
            var handler = new SubmitOfficialQuestionAnswerCommandHandler(context, storage.Object);

            var editedFlag = await handler.Handle(new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null, audioKey, ValidSha256,
                "Dependency injection helps decoupling", "Dependency injection helps decoupling", null, 30,
                IsManuallyEdited: true, DeviceId, SessionToken), CancellationToken.None);

            editedFlag.IsSuccess.Should().BeFalse();
            editedFlag.Error.Should().Be(OfficialExamErrors.TranscriptEditNotAllowedInVoiceOnlyMode);

            var mutatedText = await handler.Handle(new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null, audioKey, ValidSha256,
                "Dependency injection", "Dependency injection modified", null, 30,
                IsManuallyEdited: false, DeviceId, SessionToken), CancellationToken.None);

            mutatedText.IsSuccess.Should().BeFalse();
            mutatedText.Error.Should().Be(OfficialExamErrors.TranscriptEditNotAllowedInVoiceOnlyMode);

            // Empty transcript allowed in VoiceOnly for server Whisper R2 fallback
            var emptyVoice = await handler.Handle(new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null, audioKey, ValidSha256,
                null, "", null, 45,
                IsManuallyEdited: false, DeviceId, SessionToken), CancellationToken.None);

            emptyVoice.IsSuccess.Should().BeTrue();
        }

        // 2. Batch SubmitOfficialExam rejects manual edit in VoiceOnly and allows empty transcript
        await using (var context = CreateDbContext())
        {
            var (student, ticket, question, _) = await SeedExamAsync(context, ExamInputMode.VoiceOnly);
            var handler = new SubmitOfficialExamCommandHandler(context);

            var rejected = await handler.Handle(new SubmitOfficialExamCommand(
                student.Id, ticket.Id, "10.0.0.1",
                [new OfficialExamAnswerSubmissionRequest(question.Id, "edited text", null, 20, "raw text", IsManuallyEdited: true)],
                DeviceId, SessionToken), CancellationToken.None);

            rejected.IsSuccess.Should().BeFalse();
            rejected.Error.Should().Be(OfficialExamErrors.TranscriptEditNotAllowedInVoiceOnlyMode);

            var acceptedEmpty = await handler.Handle(new SubmitOfficialExamCommand(
                student.Id, ticket.Id, "10.0.0.1",
                [new OfficialExamAnswerSubmissionRequest(question.Id, "", null, 20, null, IsManuallyEdited: false)],
                DeviceId, SessionToken), CancellationToken.None);

            acceptedEmpty.IsSuccess.Should().BeTrue();
            acceptedEmpty.Value.IsKioskLocked.Should().BeTrue();
        }
    }

    [Fact]
    public async Task VoiceWithTranscriptEdit_PersistsRatioWithin35Percent_AndRejectsAbove35Percent_OnBatchAndPerTurn()
    {
        var raw100 = new string('a', 100);
        var okEdited30 = new string('b', 30) + new string('a', 70);
        var overEdited40 = new string('b', 40) + new string('a', 60);

        // 1. Per-turn handler
        await using (var context = CreateDbContext())
        {
            var (student, ticket, question, audioKey) = await SeedExamAsync(context, ExamInputMode.VoiceWithTranscriptEdit);
            var storage = CreateVerifiedStorageMock(audioKey);
            var handler = new SubmitOfficialQuestionAnswerCommandHandler(context, storage.Object);

            var over = await handler.Handle(new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null, audioKey, ValidSha256,
                raw100, overEdited40, null, 25, IsManuallyEdited: true, DeviceId, SessionToken), CancellationToken.None);
            over.IsSuccess.Should().BeFalse();
            over.Error.Should().StartWith(OfficialExamErrors.TranscriptEditLimitExceeded);

            var valid = await handler.Handle(new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null, audioKey, ValidSha256,
                raw100, okEdited30, null, 25, IsManuallyEdited: true, DeviceId, SessionToken), CancellationToken.None);
            valid.IsSuccess.Should().BeTrue();

            var sub = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync();
            sub.InitialVoiceTranscript.Should().Be(raw100);
            sub.TranscriptWhisper.Should().Be(okEdited30);
            sub.TranscriptEditRatio.Should().Be(0.3000m);
        }

        // 2. Batch SubmitOfficialExam handler
        await using (var context = CreateDbContext())
        {
            var (student, ticket, question, _) = await SeedExamAsync(context, ExamInputMode.VoiceWithTranscriptEdit);
            var handler = new SubmitOfficialExamCommandHandler(context);

            var over = await handler.Handle(new SubmitOfficialExamCommand(
                student.Id, ticket.Id, "10.0.0.1",
                [new OfficialExamAnswerSubmissionRequest(question.Id, overEdited40, null, 25, raw100, IsManuallyEdited: true)],
                DeviceId, SessionToken), CancellationToken.None);
            over.IsSuccess.Should().BeFalse();
            over.Error.Should().StartWith(OfficialExamErrors.TranscriptEditLimitExceeded);

            var valid = await handler.Handle(new SubmitOfficialExamCommand(
                student.Id, ticket.Id, "10.0.0.1",
                [new OfficialExamAnswerSubmissionRequest(question.Id, okEdited30, null, 25, raw100, IsManuallyEdited: true)],
                DeviceId, SessionToken), CancellationToken.None);
            valid.IsSuccess.Should().BeTrue();

            var sub = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync();
            sub.InitialVoiceTranscript.Should().Be(raw100);
            sub.TranscriptEditRatio.Should().Be(0.3000m);
        }
    }
}
