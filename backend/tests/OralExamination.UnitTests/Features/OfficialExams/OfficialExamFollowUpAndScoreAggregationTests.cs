using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamFollowUpAndScoreAggregationTests
{
    private const string DeviceId = "BYOD-FOLLOWUP-TEST-0001";
    private const string SessionToken = "followup-session-token-1234567890abcdef";
    private static readonly string ShaQ1Main = new('1', 64);
    private static readonly string ShaQ1Fu1 = new('2', 64);

    private static OralExamDbContext CreateDbContext(bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }
        return new OralExamDbContext(builder.Options);
    }

    private static async Task<(User Officer, User Examiner, User Student, RealExamSessionShift Shift, StudentExamTicket Ticket, ExamQuestion Q1, ExamQuestion Q2, RubricCriterion C1)> SeedTwoQuestionExamAsync(
        OralExamDbContext context)
    {
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.fu@fpt.edu.vn", FullName = "Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };
        var examiner = new User { Id = Guid.NewGuid(), Email = "examiner.fu@fpt.edu.vn", FullName = "Giám khảo", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.fu@fpt.edu.vn", StudentCode = "SE173001", FullName = "Phạm Minh", Role = UserRole.Student, IsActive = true };

        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Software Architecture", Credits = 3, SemesterId = semester.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = officer.Id, Name = "Struct", IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "FE SWD392",
            ExamDate = new DateOnly(2026, 11, 20),
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 202",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            EndTime = DateTime.UtcNow.AddMinutes(40),
            ExaminerUserId = examiner.Id,
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var c1 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Depth", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain CQRS", SampleAnswer = new string('s', 60), ApprovalStatus = "APPROVED", IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "Explain Event Sourcing", SampleAnswer = new string('s', 60), ApprovalStatus = "APPROVED", IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "SET-FU-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 7,
            Status = ExamTicketStatus.InProgress,
            DeviceId = DeviceId,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-15),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(SessionToken)
        };

        context.Users.AddRange(officer, examiner, student);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.Add(c1);
        context.ExamQuestions.AddRange(q1, q2);
        context.ExamSets.Add(examSet);
        context.ExamSetQuestions.AddRange(
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q2.Id, OrderIndex = 2 });
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        return (officer, examiner, student, shift, ticket, q1, q2, c1);
    }

    [Fact]
    public async Task PerTurnFollowUpFlow_UppercaseKeys_ChecksumHeader_GeneratedPromptOnly_ZeroScoreLeak_AndFinalizeAutoFills()
    {
        await using var context = CreateDbContext();
        var (_, _, student, shift, ticket, q1, q2, _) = await SeedTwoQuestionExamAsync(context);

        var expectedBase64 = Convert.ToBase64String(SHA256.HashData(new byte[] { 1, 2, 3, 4 }));
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.CreatePresignedUploadUrlAsync(
                It.IsAny<string>(), "audio/webm", It.IsAny<TimeSpan>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, string ct, TimeSpan exp, string? shaB64, CancellationToken _) =>
            {
                var headers = new Dictionary<string, string> { ["Content-Type"] = ct, ["If-None-Match"] = "*" };
                if (!string.IsNullOrWhiteSpace(shaB64)) headers["x-amz-checksum-sha256"] = shaB64;
                return new StoragePresignedUploadResult($"https://r2.example/{key}", key, DateTime.UtcNow.Add(exp), headers);
            });
        storageMock.Setup(s => s.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => new StorageObjectMetadata(key, 2048, "audio/webm", "etag", DateTime.UtcNow));
        storageMock.Setup(s => s.VerifySha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // 1. Authorize Q1 main audio upload -> uppercase _Q01.webm + x-amz-checksum-sha256
        var authHandler = new CreateQuestionAudioUploadAuthorizationCommandHandler(context, storageMock.Object);
        var mainAuth = await authHandler.Handle(new CreateQuestionAudioUploadAuthorizationCommand(
            student.Id, ticket.Id, q1.Id, 0, expectedBase64, DeviceId, SessionToken, DateTime.UtcNow), CancellationToken.None);
        mainAuth.IsSuccess.Should().BeTrue();
        mainAuth.Value.ObjectKey.Should().EndWith("07_SE173001_Q01.webm");
        mainAuth.Value.RequiredHeaders.Should().ContainKey("x-amz-checksum-sha256").WhoseValue.Should().Be(expectedBase64);

        // 2. Submit Q1 main answer
        var answerHandler = new SubmitOfficialQuestionAnswerCommandHandler(context, storageMock.Object);
        var mainSubmit = await answerHandler.Handle(new SubmitOfficialQuestionAnswerCommand(
            student.Id, ticket.Id, q1.Id, 0, null, mainAuth.Value.ObjectKey, ShaQ1Main,
            "CQRS tách biệt command và query", "CQRS tách biệt command và query", null, 40, false, DeviceId, SessionToken), CancellationToken.None);
        mainSubmit.IsSuccess.Should().BeTrue();
        mainSubmit.Value.CanRequestFollowUp.Should().BeTrue();

        // Idempotent retry returns exact same submissionId
        var retryMain = await answerHandler.Handle(new SubmitOfficialQuestionAnswerCommand(
            student.Id, ticket.Id, q1.Id, 0, null, mainAuth.Value.ObjectKey, ShaQ1Main,
            "CQRS tách biệt command và query", "CQRS tách biệt command và query", null, 40, false, DeviceId, SessionToken), CancellationToken.None);
        retryMain.IsSuccess.Should().BeTrue();
        retryMain.Value.SubmissionId.Should().Be(mainSubmit.Value.SubmissionId);

        // 3. Generate follow-up prompt -> writes ONLY GeneratedFollowUpPrompt, NEVER FollowUpPrompt, 0% score leak!
        typeof(OfficialExamNextFollowUpResponse).GetProperties()
            .Where(p => p.Name.Contains("Score", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("Kiosk follow-up response MUST NEVER expose scores");

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 6.5m,
                ConfidenceScore = 0.90m,
                FollowUpPrompt = "Khi nào nên dùng eventual consistency giữa Read Model và Write Model trong CQRS?"
            });

        var fuHandler = new GenerateOfficialExamFollowUpCommandHandler(context, aiMock.Object);
        var nextFu = await fuHandler.Handle(new GenerateOfficialExamFollowUpCommand(
            student.Id, ticket.Id, mainSubmit.Value.SubmissionId, DeviceId, SessionToken), CancellationToken.None);

        nextFu.IsSuccess.Should().BeTrue();
        nextFu.Value.HasMoreFollowUp.Should().BeTrue();
        nextFu.Value.FollowUpIndex.Should().Be(1);
        nextFu.Value.FollowUpPrompt.Should().Contain("eventual consistency");

        var mainRow = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == mainSubmit.Value.SubmissionId);
        mainRow.FollowUpPrompt.Should().BeNull("main row must keep follow_up_prompt = NULL for PostgreSQL CHECK 23514");
        mainRow.GeneratedFollowUpPrompt.Should().Be(nextFu.Value.FollowUpPrompt);

        // Idempotent replay does NOT call LLM again
        var replayFu = await fuHandler.Handle(new GenerateOfficialExamFollowUpCommand(
            student.Id, ticket.Id, mainSubmit.Value.SubmissionId, DeviceId, SessionToken), CancellationToken.None);
        replayFu.IsSuccess.Should().BeTrue();
        replayFu.Value.FollowUpPrompt.Should().Be(nextFu.Value.FollowUpPrompt);
        aiMock.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        // 4. Authorize & submit Follow-Up #1 -> uppercase _Q01_FU1.webm, copies parent.GeneratedFollowUpPrompt -> child.FollowUpPrompt
        var fuAuth = await authHandler.Handle(new CreateQuestionAudioUploadAuthorizationCommand(
            student.Id, ticket.Id, q1.Id, 1, expectedBase64, DeviceId, SessionToken, DateTime.UtcNow), CancellationToken.None);
        fuAuth.IsSuccess.Should().BeTrue();
        fuAuth.Value.ObjectKey.Should().EndWith("07_SE173001_Q01_FU1.webm");

        var fuSubmit = await answerHandler.Handle(new SubmitOfficialQuestionAnswerCommand(
            student.Id, ticket.Id, q1.Id, 1, mainSubmit.Value.SubmissionId, fuAuth.Value.ObjectKey, ShaQ1Fu1,
            "Dùng eventual consistency khi cần mở rộng độc lập Read DB", "Dùng eventual consistency khi cần mở rộng độc lập Read DB",
            null, 35, false, DeviceId, SessionToken), CancellationToken.None);
        fuSubmit.IsSuccess.Should().BeTrue();

        var childRow = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == fuSubmit.Value.SubmissionId);
        childRow.IsFollowUp.Should().BeTrue();
        childRow.ParentSubmissionId.Should().Be(mainSubmit.Value.SubmissionId);
        childRow.FollowUpIndex.Should().Be(1);
        childRow.FollowUpPrompt.Should().Be(nextFu.Value.FollowUpPrompt);

        // 5. Finalize exam with empty Answers -> auto-fills unanswered Q2 main submission, locks Kiosk, creates GradingJobs for all 3 submissions
        var finalizeHandler = new SubmitOfficialExamCommandHandler(context);
        var finalized = await finalizeHandler.Handle(new SubmitOfficialExamCommand(
            student.Id, ticket.Id, "10.0.0.1", [], DeviceId, SessionToken), CancellationToken.None);

        finalized.IsSuccess.Should().BeTrue();
        finalized.Value.Status.Should().Be(ExamTicketStatus.Submitted);
        finalized.Value.IsKioskLocked.Should().BeTrue();
        finalized.Value.SubmissionCount.Should().Be(3, "Q1 main + Q1 FU1 + auto-filled empty Q2 main");
        (await context.ExamQuestionSubmissions.CountAsync(s => s.TicketId == ticket.Id && s.ExamQuestionId == q2.Id && !s.IsFollowUp))
            .Should().Be(1);
        (await context.GradingJobs.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task OfficialExamScoreAggregator_TwoMainQuestionsPlusThreeFollowUps_DividesByTwoNeverFive_AcrossAllConsumers()
    {
        await using var context = CreateDbContext();
        var (officer, _, student, shift, ticket, q1, q2, _) = await SeedTwoQuestionExamAsync(context);

        // Shift ended so PublishGrades can proceed
        shift.StartTime = DateTime.UtcNow.AddHours(-2);
        shift.EndTime = DateTime.UtcNow.AddMinutes(-10);
        ticket.Status = ExamTicketStatus.AiGraded;

        // Q1 main = 6.0, Q1 FU1 = 8.0, Q1 FU2 = 9.0 -> avg FU = 8.5 >= 6.0 -> effective Q1 = max(6.0, 9.0) = 9.0
        // Q2 main = 7.0, Q2 FU1 = 8.0 -> avg FU = 8.0 >= 7.0 -> effective Q2 = max(7.0, 8.0) = 8.0
        // Ticket score MUST be (9.0 + 8.0) / 2 = 8.50 (NEVER (6+8+9+7+8)/5 = 7.60!)
        var q1Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q1 main", AiScore = 6.0m, GradingStatus = "ai_graded", AudioObjectKey = "official-exams/q1.webm", AudioR2Url = "official-exams/q1.webm" };
        var q1Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 1, IsFollowUp = true, ParentSubmissionId = q1Main.Id, FollowUpPrompt = "FU1?", TranscriptWhisper = "FU1 ans", AiScore = 8.0m, GradingStatus = "ai_graded", AudioObjectKey = "official-exams/q1_fu1.webm", AudioR2Url = "official-exams/q1_fu1.webm" };
        var q1Fu2 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 2, IsFollowUp = true, ParentSubmissionId = q1Fu1.Id, FollowUpPrompt = "FU2?", TranscriptWhisper = "FU2 ans", AiScore = 9.0m, GradingStatus = "ai_graded", AudioObjectKey = "official-exams/q1_fu2.webm", AudioR2Url = "official-exams/q1_fu2.webm" };
        var q2Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q2 main", AiScore = 7.0m, GradingStatus = "ai_graded", AudioObjectKey = "official-exams/q2.webm", AudioR2Url = "official-exams/q2.webm" };
        var q2Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, FollowUpIndex = 1, IsFollowUp = true, ParentSubmissionId = q2Main.Id, FollowUpPrompt = "Q2 FU1?", TranscriptWhisper = "Q2 FU1 ans", AiScore = 8.0m, GradingStatus = "ai_graded", AudioObjectKey = "official-exams/q2_fu1.webm", AudioR2Url = "official-exams/q2_fu1.webm" };

        context.ExamQuestionSubmissions.AddRange(q1Main, q1Fu1, q1Fu2, q2Main, q2Fu1);
        context.AiEvaluations.AddRange(
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q1Main.Id, TotalScore = 6.0m, ConfidenceScore = 0.90m, CotTrace = "CoT Q1" },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q1Fu1.Id, TotalScore = 8.0m, ConfidenceScore = 0.92m, CotTrace = "CoT Q1 FU1" },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q1Fu2.Id, TotalScore = 9.0m, ConfidenceScore = 0.95m, CotTrace = "CoT Q1 FU2" },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q2Main.Id, TotalScore = 7.0m, ConfidenceScore = 0.88m, CotTrace = "CoT Q2" },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q2Fu1.Id, TotalScore = 8.0m, ConfidenceScore = 0.91m, CotTrace = "CoT Q2 FU1" });
        await context.SaveChangesAsync();

        // 1. Aggregator directly
        var allSubs = new[] { q1Main, q1Fu1, q1Fu2, q2Main, q2Fu1 };
        OfficialExamScoreAggregator.ComputeTicketAiScore(allSubs).Should().Be(8.50m);
        OfficialExamScoreAggregator.ComputeTicketFinalScore(allSubs).Should().Be(8.50m);

        // 2. GetAuditEvidenceQueryHandler (uses AuditEvidenceItemBuilder + OfficialExamScoreAggregator)
        var evidenceResult = await new GetAuditEvidenceQueryHandler(context)
            .Handle(new GetAuditEvidenceQuery(shift.Id, officer.Id), CancellationToken.None);
        evidenceResult.IsSuccess.Should().BeTrue();
        var evidence = evidenceResult.Value.Single();
        evidence.AiScore.Should().Be(8.50m);
        evidence.FinalScore.Should().Be(8.50m);
        evidence.FollowUpItems.Should().NotBeNull().And.HaveCount(3);
        evidence.FollowUpItems!.Select(f => f.FollowUpPrompt).Should().BeEquivalentTo(["FU1?", "FU2?", "Q2 FU1?"]);

        // 3. PublishGrades persists CertificateHash & locks ticket
        var publish = await new PublishGradesCommandHandler(context)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publish.IsSuccess.Should().BeTrue();

        var publishedShift = await context.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        publishedShift.CertificateHash.Should().NotBeNullOrWhiteSpace().And.HaveLength(64);
        publishedShift.CertificateSignedAt.Should().NotBeNull();

        // 4. Student GetOfficialExamGrade returns 8.50m (not 7.60m!)
        var grade = await new GetOfficialExamGradeQueryHandler(context)
            .Handle(new GetOfficialExamGradeQuery(ticket.Id, student.Id), CancellationToken.None);
        grade.IsSuccess.Should().BeTrue();
        grade.Value.TotalScore.Should().Be(8.50m);
    }

    [Fact]
    public async Task OneWayLockInterceptor_BlocksTamperingOnLockedFollowUpAndDeviceFields_AndAllowsApprovedAppealWithCertificateRehash()
    {
        await using var context = CreateDbContext(withLockInterceptor: true);
        var (officer, examiner, student, shift, ticket, q1, q2, _) = await SeedTwoQuestionExamAsync(context);
        var appealLecturer = new User { Id = Guid.NewGuid(), Email = "regrader@fpt.edu.vn", FullName = "GV Chấm lại", Role = UserRole.Lecturer, IsActive = true };
        context.Users.Add(appealLecturer);

        shift.StartTime = DateTime.UtcNow.AddHours(-2);
        shift.EndTime = DateTime.UtcNow.AddMinutes(-5);
        ticket.Status = ExamTicketStatus.Audited;

        var q1Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q1 main", AiScore = 6.0m, FinalScore = 6.0m, GradingStatus = "ai_graded" };
        var q1Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 1, IsFollowUp = true, ParentSubmissionId = q1Main.Id, FollowUpPrompt = "FU1?", TranscriptWhisper = "FU1 ans", AiScore = 7.0m, FinalScore = 7.0m, GradingStatus = "ai_graded" };
        var q2Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q2 main", AiScore = 8.0m, FinalScore = 8.0m, GradingStatus = "ai_graded" };
        var audit = new LecturerAudit { Id = Guid.NewGuid(), TicketId = ticket.Id, LecturerId = examiner.Id, AuditedScore = 7.50m, OverrideReason = "Initial audit", IsLocked = false };
        context.ExamQuestionSubmissions.AddRange(q1Main, q1Fu1, q2Main);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        // Publish grades -> One-Way Lock ON + initial CertificateHash persisted
        var publishResult = await new PublishGradesCommandHandler(context)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publishResult.IsSuccess.Should().BeTrue();
        var initialCertHash = (await context.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).CertificateHash;
        initialCertHash.Should().HaveLength(64);

        // 1. Tampering locked follow-up row -> OneWayLockException
        var trackedFu = await context.ExamQuestionSubmissions.SingleAsync(s => s.Id == q1Fu1.Id);
        trackedFu.TranscriptWhisper = "TAMPERED FOLLOW-UP";
        var tamperFuAct = () => context.SaveChangesAsync();
        await tamperFuAct.Should().ThrowAsync<OneWayLockException>();
        context.Entry(trackedFu).Reload();

        // 2. Tampering locked ticket DeviceId -> OneWayLockException
        var trackedTicket = await context.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id);
        trackedTicket.DeviceId = "ROGUE-DEVICE-99999999";
        var tamperDeviceAct = () => context.SaveChangesAsync();
        await tamperDeviceAct.Should().ThrowAsync<OneWayLockException>();
        context.Entry(trackedTicket).Reload();

        // 3. Approved AppealRequest opens controlled exception -> updates Q1 FU1 FinalScore + LecturerAudit.AuditedScore + recomputes shift CertificateHash!
        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = shift.SessionId,
            SubmissionId = q1Fu1.Id,
            Reason = "Xin chấm lại câu phụ Q1 vì nhận diện sai thuật ngữ",
            Status = AppealStatus.RegradedByLecturer,
            AssignedTo = appealLecturer.Id,
            OriginalScore = 7.50m,
            ProposedScore = 9.0m
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var reviewResult = await new ReviewAppealDecisionCommandHandler(context)
            .Handle(new ReviewAppealDecisionCommand(appeal.Id, officer.Id, AppealStatus.Approved, 9.0m, "Phê duyệt điểm chấm lại sau khi nghe lại audio."), CancellationToken.None);

        reviewResult.IsSuccess.Should().BeTrue();

        var updatedFu = await context.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == q1Fu1.Id);
        updatedFu.FinalScore.Should().Be(9.0m);

        var updatedAudit = await context.LecturerAudits.AsNoTracking().SingleAsync(a => a.TicketId == ticket.Id);
        // Q1 effective = max(6.0, 9.0) = 9.0; Q2 = 8.0 => avg = 8.50m
        updatedAudit.AuditedScore.Should().Be(8.50m);

        var rehashedShift = await context.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
        rehashedShift.CertificateHash.Should().HaveLength(64);
        rehashedShift.CertificateHash.Should().NotBe(initialCertHash, "approved appeal must recompute shift CertificateHash atomically");
    }

    [Fact]
    public async Task PerTurnFinalize_WhenAllSubmissionsAlreadyGraded_TransitionsDirectlyToAiGraded_AndAllowsPublishGrades()
    {
        await using var context = CreateDbContext();
        var (officer, _, student, shift, ticket, q1, q2, _) = await SeedTwoQuestionExamAsync(context);

        // Worker already finished grading all per-turn submissions while ticket is still IN_PROGRESS
        var now = DateTime.UtcNow;
        var q1Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q1 ans", AudioObjectKey = "official-exams/q1.webm", AudioR2Url = "official-exams/q1.webm", AudioHashSha256 = ShaQ1Main, AudioVerifiedAt = now, AiScore = 7.0m, GradingStatus = "ai_graded" };
        var q1Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, FollowUpIndex = 1, IsFollowUp = true, ParentSubmissionId = q1Main.Id, FollowUpPrompt = "FU1?", TranscriptWhisper = "FU1 ans", AudioObjectKey = "official-exams/q1_fu1.webm", AudioR2Url = "official-exams/q1_fu1.webm", AudioHashSha256 = ShaQ1Fu1, AudioVerifiedAt = now, AiScore = 8.5m, GradingStatus = "ai_graded" };
        var q2Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, FollowUpIndex = 0, IsFollowUp = false, TranscriptWhisper = "Q2 ans", AudioObjectKey = "official-exams/q2.webm", AudioR2Url = "official-exams/q2.webm", AudioHashSha256 = ShaQ1Main, AudioVerifiedAt = now, AiScore = 8.0m, GradingStatus = "ai_graded" };
        context.ExamQuestionSubmissions.AddRange(q1Main, q1Fu1, q2Main);
        context.AiEvaluations.AddRange(
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q1Main.Id, TotalScore = 7.0m, ConfidenceScore = 0.91m },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q1Fu1.Id, TotalScore = 8.5m, ConfidenceScore = 0.92m },
            new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", StudentId = student.Id, ExamSubmissionId = q2Main.Id, TotalScore = 8.0m, ConfidenceScore = 0.90m });
        await context.SaveChangesAsync();

        var finalizeHandler = new SubmitOfficialExamCommandHandler(context);
        var finalized = await finalizeHandler.Handle(
            new SubmitOfficialExamCommand(student.Id, ticket.Id, "10.0.0.1", [], DeviceId, SessionToken),
            CancellationToken.None);

        finalized.IsSuccess.Should().BeTrue();
        finalized.Value.Status.Should().Be(ExamTicketStatus.AiGraded, "when all per-turn submissions are already graded, finalize must transition directly to AI_GRADED so PublishGrades is never deadlocked");
        finalized.Value.SubmissionCount.Should().Be(3);

        // Advance shift end time and verify PublishGrades succeeds cleanly
        shift.StartTime = DateTime.UtcNow.AddHours(-2);
        shift.EndTime = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        var publish = await new PublishGradesCommandHandler(context)
            .Handle(new PublishGradesCommand(shift.Id, officer.Id), CancellationToken.None);
        publish.IsSuccess.Should().BeTrue();
        publish.Value.TotalPublished.Should().Be(1);
    }
}
