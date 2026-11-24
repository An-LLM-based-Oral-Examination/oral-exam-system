using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealById;
using OralExamination.Application.Features.Appeals.Queries.GetAppealEvidence;
using OralExamination.Application.Features.Appeals.Queries.GetAppeals;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.CloseInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;
using OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;
using OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Reporting;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class P1HardeningMf05AndMf04WorkersAndScheduleTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string DeviceId = "BYOD-KIOSK-P1-DEVICE-001";
    private const string SessionToken = "BYOD-KIOSK-P1-SESSION-TOKEN-001";

    private static OralExamDbContext CreateDbContext(string? dbName = null) =>
        new(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options);

    private static async IAsyncEnumerable<GradingTask> YieldTasksAsync(params GradingTask[] tasks)
    {
        foreach (var task in tasks)
        {
            yield return task;
        }

        await Task.CompletedTask;
    }

    // 1. P1-04: GradingQueueWorker khóa theo (ShiftId, StudentId) khi lưu điểm & tổng hợp participant, gọi LLM ngoài lock
    [Fact(DisplayName = "P1-04A: GradingQueueWorker gọi LLM ngoài lock và khóa ExecuteInClassSubmissionAsync khi tổng hợp TotalAiScore/Status")]
    public async Task P1_04_GradingQueueWorker_SerializesMf05ParticipantAggregationUnderInClassSubmissionLock()
    {
        var dbName = Guid.NewGuid().ToString();
        var trace = new List<string>();
        var traceGate = new object();
        var inSubmissionLock = new AsyncLocal<bool>();
        var llmObservedInsideLock = false;
        var lockMap = new ConcurrentDictionary<(Guid ShiftId, Guid StudentId), SemaphoreSlim>();

        void Record(string entry)
        {
            lock (traceGate)
            {
                trace.Add(entry);
            }
        }

        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-p104@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-p104@fpt.edu.vn", FullName = "SV", StudentCode = "SE180104", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SE1801", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain DI", SampleAnswer = new string('a', 60), KeyPoints = "[]", Difficulty = QuestionDifficulty.Easy, BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "Explain EF", SampleAnswer = new string('b', 60), KeyPoints = "[]", Difficulty = QuestionDifficulty.Medium, BloomLevel = BloomLevel.Apply, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca P1-04",
            TotpSecretKey = "SECRET_P104",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 2,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = q1.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { q1.Id, q2.Id }),
            Status = "SUBMITTED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var sub1 = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, QuestionId = q1.Id, Transcript = "DI answer", Status = "SUBMITTED", SubmittedAt = DateTime.UtcNow.AddMinutes(-2) };
        var sub2 = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, QuestionId = q2.Id, Transcript = "EF answer", Status = "SUBMITTED", SubmittedAt = DateTime.UtcNow.AddMinutes(-1) };

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp =>
            new TrackingDbContextProxy(sp.GetRequiredService<OralExamDbContext>(), Record)
            {
                InClassLockMap = lockMap,
                InClassLockTracker = inSubmissionLock
            });

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiGradingRequest req, CancellationToken _) =>
            {
                if (inSubmissionLock.Value)
                {
                    llmObservedInsideLock = true;
                }
                Record($"LLM:{req.Transcript}");
                var score = req.Transcript.Contains("DI", StringComparison.Ordinal) ? 7.0m : 9.0m;
                return new AiGradingResult { Score = score, ConfidenceScore = 0.95m, Feedback = "Good" };
            });
        services.AddSingleton(aiMock.Object);

        using var provider = services.BuildServiceProvider();
        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.AddRange(lecturer, student);
            db.Courses.Add(course);
            db.Classes.Add(cls);
            db.Rubrics.Add(rubric);
            db.RubricCriteria.Add(criterion);
            db.PracticeQuestions.AddRange(q1, q2);
            db.InClassExamShifts.Add(shift);
            db.InClassShiftParticipants.Add(participant);
            db.InClassExamSubmissions.AddRange(sub1, sub2);
            await db.SaveChangesAsync();
        }

        var channelMock = new Mock<IGradingQueueChannel>();
        channelMock.Setup(c => c.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(YieldTasksAsync(
                new GradingTask { AnswerId = sub1.Id, InClassSubmissionId = sub1.Id, SessionId = shift.Id, StudentId = student.Id, QuestionId = q1.Id, AnswerText = sub1.Transcript },
                new GradingTask { AnswerId = sub2.Id, InClassSubmissionId = sub2.Id, SessionId = shift.Id, StudentId = student.Id, QuestionId = q2.Id, AnswerText = sub2.Transcript }));

        var practiceHubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var worker = new GradingQueueWorker(channelMock.Object, provider, practiceHubMock.Object, NullLogger<GradingQueueWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
        await worker.StopAsync(CancellationToken.None);

        trace.Count(e => e.StartsWith("LOCK:InClassSubmission:Enter:", StringComparison.Ordinal)).Should().Be(2);
        llmObservedInsideLock.Should().BeFalse("LLM I/O phải nằm hoàn toàn ngoài khóa ExecuteInClassSubmissionAsync");

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var refreshed = await verifyDb.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id);
        refreshed.Status.Should().Be("GRADED");
        refreshed.TotalAiScore.Should().Be(8.00m);
        refreshed.TotalFinalScore.Should().Be(8.00m);
    }

    // 2. P1-04: InClassShiftAutoSubmit giữ nguyên ABSENT, tôn trọng grace 2p, đặt FinishedAt và cô lập lỗi từng SV
    [Fact(DisplayName = "P1-04B: InClassShiftAutoSubmit bỏ qua ABSENT, tôn trọng grace 2 phút, gán FinishedAt và cô lập lỗi từng SV")]
    public async Task P1_04_InClassShiftAutoSubmit_SkipsAbsent_RespectsClosedGrace_SetsFinishedAt_AndIsolatesPerStudentErrors()
    {
        await using var db = CreateDbContext();
        var qId = Guid.NewGuid();
        var graceShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca trong 2p grace",
            TotpSecretKey = "S1",
            Status = "CLOSED",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddSeconds(-30),
            UpdatedAt = DateTime.UtcNow.AddSeconds(-30)
        };
        var pastGraceShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca quá 2p grace",
            TotpSecretKey = "S2",
            Status = "CLOSED",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var graceStudentId = Guid.NewGuid();
        var absentStudentId = Guid.NewGuid();
        var failingStudentId = Guid.NewGuid();
        var healthyStudentId = Guid.NewGuid();

        db.InClassExamShifts.AddRange(graceShift, pastGraceShift);
        db.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = graceShift.Id, StudentId = graceStudentId, AssignedQuestionId = qId, AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { qId }), Status = "IN_PROGRESS" },
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = pastGraceShift.Id, StudentId = absentStudentId, AssignedQuestionId = qId, AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { qId }), Status = "ABSENT" },
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = pastGraceShift.Id, StudentId = failingStudentId, AssignedQuestionId = qId, AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { qId }), Status = "IN_PROGRESS" },
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = pastGraceShift.Id, StudentId = healthyStudentId, AssignedQuestionId = qId, AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { qId }), Status = "IN_PROGRESS" });
        await db.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // 1. Khi respectClosedGrace = true, ca trong grace 2p không bị auto-submit
        var graceCount = await InClassShiftAutoSubmit.SubmitMissingAsync(
            db, queue.Object, CancellationToken.None, graceShift.Id, respectClosedGrace: true);
        graceCount.Should().Be(0);
        (await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.StudentId == graceStudentId))
            .Status.Should().Be("IN_PROGRESS");

        // 2. Ca quá grace: ABSENT giữ nguyên ABSENT, SV lỗi DbUpdateException bị cô lập, SV khỏe mạnh vẫn được nộp & đặt FinishedAt
        var proxy = new TrackingDbContextProxy(db, _ => { }) { FailInClassStudentId = failingStudentId };
        var created = await InClassShiftAutoSubmit.SubmitMissingAsync(
            proxy, queue.Object, CancellationToken.None, pastGraceShift.Id, respectClosedGrace: true);

        created.Should().Be(1);

        var absentAfter = await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.StudentId == absentStudentId);
        absentAfter.Status.Should().Be("ABSENT");
        absentAfter.FinishedAt.Should().BeNull();
        (await db.InClassExamSubmissions.AsNoTracking().CountAsync(s => s.StudentId == absentStudentId)).Should().Be(0);

        var healthyAfter = await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.StudentId == healthyStudentId);
        healthyAfter.Status.Should().Be("SUBMITTED");
        healthyAfter.FinishedAt.Should().NotBeNull();
        (await db.InClassExamSubmissions.AsNoTracking().CountAsync(s => s.StudentId == healthyStudentId)).Should().Be(1);
    }

    // 3. P1-04: CloseInClassShift & FinalizeInClassShiftGrades bọc trong ExecuteShiftMutationAsync và không lồng transaction
    [Fact(DisplayName = "P1-04C: CloseInClassShift và FinalizeInClassShiftGrades thực thi dưới ExecuteShiftMutationAsync không lồng giao dịch")]
    public async Task P1_04_CloseAndFinalizeInClassShift_ExecuteUnderShiftMutationLock()
    {
        await using var db = CreateDbContext();
        var events = new List<string>();
        var proxy = new TrackingDbContextProxy(db, events.Add);

        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-close@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-close@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "SWP", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1810", Name = "SE1810", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var qId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đóng và chốt",
            TotpSecretKey = "SECRET_CLOSE",
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = qId,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { qId }),
            Status = "IN_PROGRESS"
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        await db.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var closeRes = await new CloseInClassShiftCommandHandler(proxy, queue.Object)
            .Handle(new CloseInClassShiftCommand(lecturer.Id, shift.Id), CancellationToken.None);
        closeRes.IsSuccess.Should().BeTrue();

        var enterMutationIdx = events.IndexOf($"LOCK:ShiftMutation:Enter:{shift.Id}:False");
        var exitMutationIdx = events.IndexOf($"LOCK:ShiftMutation:Exit:{shift.Id}:False");
        var enterAutoSubmitIdx = events.IndexOf($"LOCK:InClassSubmission:Enter:{shift.Id}:{student.Id}");
        enterMutationIdx.Should().BeGreaterThanOrEqualTo(0);
        exitMutationIdx.Should().BeGreaterThan(enterMutationIdx);
        enterAutoSubmitIdx.Should().BeGreaterThan(exitMutationIdx, "AutoSubmit phải chạy SAU KHI ExecuteShiftMutationAsync thoát để không lồng giao dịch");

        // Chấm điểm xong submission để đủ điều kiện finalize
        var sub = await db.InClassExamSubmissions.SingleAsync(s => s.ShiftId == shift.Id && s.StudentId == student.Id);
        sub.AiScore = 8.0m;
        sub.FinalScore = 8.0m;
        sub.Status = "AI_GRADED";
        await db.SaveChangesAsync();

        events.Clear();
        var finalizeRes = await new FinalizeInClassShiftGradesCommandHandler(proxy)
            .Handle(new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id), CancellationToken.None);
        finalizeRes.IsSuccess.Should().BeTrue();
        events.Should().Contain($"LOCK:ShiftMutation:Enter:{shift.Id}:False");
        (await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id)).FinishedAt.Should().NotBeNull();
    }

    // 4. P1-05: POST /finish khóa vĩnh viễn participant, chặn ca đã khóa, cấm ghi đè bài trống và cấm gọi next-followup
    [Fact(DisplayName = "P1-05A: FinishStudentInClassExam chặn ca đã khóa, lưu FinishedAt và cấm ghi đè bài trống hay xin follow-up sau finish")]
    public async Task P1_05_FinishInClassExam_LocksParticipantPermanently_BlocksLockedShift_AndPreventsEmptyOverwriteAndFollowUp()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-fin@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-fin@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1811", Name = "SE1811", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Q1", SampleAnswer = "A1", KeyPoints = "[]", Difficulty = QuestionDifficulty.Easy, BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q2", Content = "Q2", SampleAnswer = "A2", KeyPoints = "[]", Difficulty = QuestionDifficulty.Easy, BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var lockedShift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = cls.Id, LecturerId = lecturer.Id, ShiftName = "Ca khóa", TotpSecretKey = "LK", Status = "FINALIZED", IsLocked = true, StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(1) };
        var activeShift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = cls.Id, LecturerId = lecturer.Id, ShiftName = "Ca mở", TotpSecretKey = "OP", Status = "IN_PROGRESS", TotalQuestionsPerStudent = 2, HasFollowUp = true, MaxFollowUpQuestions = 2, ExamDurationMinutes = 20, StartTime = DateTime.UtcNow.AddMinutes(-5), EndTime = DateTime.UtcNow.AddMinutes(30) };

        var lockedParticipant = new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = lockedShift.Id, StudentId = student.Id, Status = "SUBMITTED" };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = activeShift.Id,
            StudentId = student.Id,
            AssignedQuestionId = q1.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { q1.Id, q2.Id }),
            Status = "IN_PROGRESS",
            JoinedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var answeredQ1 = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = activeShift.Id,
            StudentId = student.Id,
            QuestionId = q1.Id,
            Transcript = "Câu trả lời Q1 đầy đủ",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.PracticeQuestions.AddRange(q1, q2);
        db.InClassExamShifts.AddRange(lockedShift, activeShift);
        db.InClassShiftParticipants.AddRange(lockedParticipant, participant);
        db.InClassExamSubmissions.Add(answeredQ1);
        await db.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(c => c.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        var finishHandler = new FinishStudentInClassExamCommandHandler(db, queue.Object);

        // 1. Ca đã khóa -> ShiftLocked (được InClassShiftsController.MapShiftFailure ánh xạ sang 403 Forbidden theo Invariant #6)
        var lockedRes = await finishHandler.Handle(new FinishStudentInClassExamCommand(student.Id, lockedShift.Id), CancellationToken.None);
        lockedRes.IsSuccess.Should().BeFalse();
        lockedRes.Error.Should().Be(InClassShiftErrors.ShiftLocked);

        // 2. Ca đang mở -> Finish thành công, lưu FinishedAt và sinh bài trống cho Q2
        var okFinish = await finishHandler.Handle(new FinishStudentInClassExamCommand(student.Id, activeShift.Id), CancellationToken.None);
        okFinish.IsSuccess.Should().BeTrue();
        var savedParticipant = await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id);
        savedParticipant.FinishedAt.Should().NotBeNull();
        savedParticipant.Status.Should().Be("SUBMITTED");

        // 3. Cố tình nộp đè vào câu Q2 trống sau khi Finish -> bị chặn CONFLICT, transcript vẫn rỗng
        var submitHandler = new SubmitInClassShiftAnswerCommandHandler(db, queue.Object, new SubmitInClassShiftAnswerCommandValidator());
        var overwriteRes = await submitHandler.Handle(
            new SubmitInClassShiftAnswerCommand(student.Id, activeShift.Id, q2.Id, "Cố tình ghi đè sau finish"),
            CancellationToken.None);
        overwriteRes.IsSuccess.Should().BeFalse();
        overwriteRes.Error.Should().StartWith("CONFLICT:");
        var q2Sub = await db.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.ShiftId == activeShift.Id && s.QuestionId == q2.Id);
        q2Sub.Transcript.Should().BeEmpty();

        // 4. Cố tình gọi GetNextInClassFollowUp sau khi Finish -> bị chặn CONFLICT
        var fuHandler = new GetNextInClassFollowUpQueryHandler(db, aiGradingService: null);
        var fuRes = await fuHandler.Handle(new GetNextInClassFollowUpQuery(student.Id, activeShift.Id, answeredQ1.Id), CancellationToken.None);
        fuRes.IsSuccess.Should().BeFalse();
        fuRes.Error.Should().StartWith("CONFLICT:");
    }

    // 5. P1-05 & MF-05 🟡: Kẹp hạn chót cá nhân theo shift.EndTime, kiểm tra ShiftId khi AdjustScore, và positive query trên InClassShiftHub
    [Fact(DisplayName = "P1-05B: Kẹp đồng hồ cá nhân theo shift.EndTime, chặn sai ShiftId ở AdjustScore và chặn user không tồn tại/MustChangePassword ở InClassShiftHub")]
    public async Task P1_05_PersonalDeadlineClampedToShiftEndTime_AdjustSubmissionChecksShiftId_AndHubRequiresActiveNonTempUser()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-clamp@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-clamp@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var tempPwdStudent = new User { Id = Guid.NewGuid(), Email = "sv-temp@fpt.edu.vn", FullName = "SV Temp", Role = UserRole.Student, IsActive = true, MustChangePassword = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1812", Name = "SE1812", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Q1", SampleAnswer = "A1", KeyPoints = "[]", Difficulty = QuestionDifficulty.Easy, BloomLevel = BloomLevel.Understand, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };

        var shiftEnd = DateTime.UtcNow.AddMinutes(-3); // Đã qua EndTime 3 phút (quá cả 2 phút grace)
        var joinedAt = DateTime.UtcNow.AddMinutes(-5); // Nếu không kẹp, JoinedAt + 20p vẫn còn 15p!
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca kẹp đồng hồ",
            TotpSecretKey = "CLAMP",
            Status = "IN_PROGRESS",
            ExamDurationMinutes = 20,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = shiftEnd
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = q1.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { q1.Id }),
            JoinedAt = joinedAt,
            Status = "IN_PROGRESS"
        };
        var existingSub = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = q1.Id,
            Transcript = "Đã nộp trước đó",
            AiScore = 6.0m,
            FinalScore = 6.0m,
            Status = "AI_GRADED"
        };

        db.Users.AddRange(lecturer, student, tempPwdStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = tempPwdStudent.Id, Status = "enrolled" });
        db.PracticeQuestions.Add(q1);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.Add(existingSub);
        await db.SaveChangesAsync();

        // 1. Kẹp deadline cá nhân = Min(JoinedAt + 20m, shift.EndTime) = shift.EndTime (-3m)
        var effectiveEndsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(participant, shift);
        effectiveEndsAt.Should().Be(shiftEnd);
        InClassShiftLifecycle.IsPersonalExamAnswerWindowExpired(participant, shift, DateTime.UtcNow).Should().BeTrue();

        // 2. AdjustInClassSubmissionScore với ShiftId không khớp -> NOT_FOUND, không đổi điểm
        var adjustHandler = new AdjustInClassSubmissionScoreCommandHandler(db, new AdjustInClassSubmissionScoreCommandValidator());
        var mismatchRes = await adjustHandler.Handle(
            new AdjustInClassSubmissionScoreCommand(lecturer.Id, existingSub.Id, 9.5m, "Hậu kiểm sai ca", ShiftId: Guid.NewGuid()),
            CancellationToken.None);
        mismatchRes.IsSuccess.Should().BeFalse();
        mismatchRes.Error.Should().StartWith("NOT_FOUND:");
        (await db.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == existingSub.Id)).FinalScore.Should().Be(6.0m);

        // 3. InClassShiftHub positive query: chặn user bị xóa (không tồn tại) và user MustChangePassword = true
        var groupsMock = new Mock<IGroupManager>();
        static InClassShiftHub BuildHub(OralExamDbContext ctx, IGroupManager grp, Guid uid, string role)
        {
            var hub = new InClassShiftHub(ctx) { Groups = grp };
            var caller = new Mock<HubCallerContext>();
            caller.SetupGet(c => c.ConnectionId).Returns("conn-1");
            caller.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
            caller.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth")));
            hub.Context = caller.Object;
            return hub;
        }

        await FluentActions.Invoking(() => BuildHub(db, groupsMock.Object, Guid.NewGuid(), "student").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>().WithMessage("*FORBIDDEN*");
        await FluentActions.Invoking(() => BuildHub(db, groupsMock.Object, tempPwdStudent.Id, "student").JoinShift(shift.Id.ToString()))
            .Should().ThrowAsync<HubException>().WithMessage("*FORBIDDEN*");
    }

    // 6. P1-06: UpdateInClassShift chặn CLO > TotalQuestions (HTTP 422) và chặn chuyển sang SELECTED_POOL
    [Fact(DisplayName = "P1-06: UpdateInClassShift trả UNPROCESSABLE (HTTP 422) khi số CLO vượt quá số câu/SV và chặn legacy SELECTED_POOL")]
    public async Task P1_06_UpdateInClassShift_RejectsExcessClosWith422_AndBlocksLegacySelectedPool()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-p106@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1813", Name = "SE1813", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "CLO 1", IsActive = true, OrderIndex = 1 };
        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "CLO 2", IsActive = true, OrderIndex = 2 };
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo1.Id, Title = "Q1", Content = "Q1", Difficulty = QuestionDifficulty.Easy, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo2.Id, Title = "Q2", Content = "Q2", Difficulty = QuestionDifficulty.Easy, ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca gốc",
            TotpSecretKey = "P106",
            Status = "SCHEDULED",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1)
        };

        db.Users.Add(lecturer);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.CourseClos.AddRange(clo1, clo2);
        db.PracticeQuestions.AddRange(q1, q2);
        db.InClassExamShifts.Add(shift);
        await db.SaveChangesAsync();

        var handler = new UpdateInClassShiftCommandHandler(db);

        // 1. 2 CLO > 1 câu/SV -> UNPROCESSABLE + HTTP 422
        var excessCloRes = await handler.Handle(new UpdateInClassShiftCommand(
            lecturer.Id, shift.Id, "Ca vượt CLO", shift.StartTime, shift.EndTime,
            QuestionPoolMode: "SELECTED_CLOS", TotalQuestionsPerStudent: 1,
            SelectedDifficulties: ["easy"], SelectedCloIds: [clo1.Id, clo2.Id]), CancellationToken.None);

        excessCloRes.IsSuccess.Should().BeFalse();
        excessCloRes.Error.Should().StartWith("UNPROCESSABLE:");
        InClassShiftErrors.IsUnprocessablePoolError(excessCloRes.Error).Should().BeTrue();
        var problem = ShiftMutationProblem.Create(excessCloRes.Error);
        problem.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        // 2. Chuyển sang SELECTED_POOL legacy -> bị chặn
        var legacyRes = await handler.Handle(new UpdateInClassShiftCommand(
            lecturer.Id, shift.Id, "Ca legacy", shift.StartTime, shift.EndTime,
            QuestionPoolMode: "SELECTED_POOL", SelectedQuestionIds: [q1.Id]), CancellationToken.None);
        legacyRes.IsSuccess.Should().BeFalse();
        legacyRes.Error.Should().Be(InClassShiftErrors.InvalidPoolMode);
    }

    // 7. P1-07: SubmitOfficialQuestionAnswer chạy R2 I/O NGOÀI ExecuteTicketDecisionAsync và siết cửa sổ ca thi
    [Fact(DisplayName = "P1-07A: SubmitOfficialQuestionAnswer xác minh R2 NGOÀI ExecuteTicketDecisionAsync và chặn ca chưa phát đề/đã hủy/quá giờ")]
    public async Task P1_07_SubmitOfficialQuestionAnswer_RunsR2VerifyOutsideTicketLock_AndEnforcesShiftWindow()
    {
        await using var db = CreateDbContext();
        var orderLog = new List<string>();
        var proxy = new TrackingDbContextProxy(db, orderLog.Add);

        var student = new User { Id = Guid.NewGuid(), Email = "sv-p107@fpt.edu.vn", FullName = "SV", StudentCode = "SE170107", Role = UserRole.Student, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = Guid.NewGuid(), ExamStructureId = Guid.NewGuid(), Title = "FE", ExamDate = new DateOnly(2026, 11, 20), ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit, CreatedBy = Guid.NewGuid() };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca P107", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddMinutes(-10), EndTime = DateTime.UtcNow.AddMinutes(30), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = session.CourseId, StructureId = session.ExamStructureId!.Value, SetCode = "SET-107", IsActive = true };
        var question = new ExamQuestion { Id = Guid.NewGuid(), CourseId = session.CourseId, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Q1", ApprovalStatus = "APPROVED", IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 3,
            Status = ExamTicketStatus.InProgress,
            DeviceId = DeviceId,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-5),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(SessionToken)
        };

        db.Users.Add(student);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.ExamSets.Add(examSet);
        db.ExamQuestions.Add(question);
        db.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = question.Id, OrderIndex = 1 });
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var expectedKey = OfficialExamAudioObjectKey.CreateForQuestion(shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!, 1, 0);
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetObjectMetadataAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                orderLog.Add("R2:GetObjectMetadata");
                return new StorageObjectMetadata(expectedKey, 2048, "audio/webm", "etag", DateTime.UtcNow);
            });
        storageMock.Setup(s => s.VerifySha256Async(expectedKey, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                orderLog.Add("R2:VerifySha256");
                return true;
            });

        var handler = new SubmitOfficialQuestionAnswerCommandHandler(proxy, storageMock.Object);
        var validCmd = new SubmitOfficialQuestionAnswerCommand(
            student.Id, ticket.Id, question.Id, 0, null, expectedKey, ValidSha256,
            "Initial answer", "Initial answer", null, 30, false, DeviceId, SessionToken);

        // 1. Ca chưa phát đề -> CONFLICT (không gọi R2)
        shift.IsExamPublished = false;
        await db.SaveChangesAsync();
        var unpublishedRes = await handler.Handle(validCmd, CancellationToken.None);
        unpublishedRes.IsSuccess.Should().BeFalse();
        unpublishedRes.Error.Should().StartWith("CONFLICT:");
        orderLog.Should().BeEmpty();

        // 2. Ca đã hết giờ (> EndTime + 2m) -> CONFLICT
        shift.IsExamPublished = true;
        shift.EndTime = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        var expiredRes = await handler.Handle(validCmd, CancellationToken.None);
        expiredRes.IsSuccess.Should().BeFalse();
        expiredRes.Error.Should().StartWith("CONFLICT:");

        // 3. Ca hợp lệ -> R2 metadata + SHA-256 PHẢI chạy hoàn toàn TRƯỚC KHI vào ExecuteTicketDecisionAsync
        shift.EndTime = DateTime.UtcNow.AddMinutes(30);
        await db.SaveChangesAsync();
        var okRes = await handler.Handle(validCmd, CancellationToken.None);
        okRes.IsSuccess.Should().BeTrue();

        var r2MetaIdx = orderLog.IndexOf("R2:GetObjectMetadata");
        var r2HashIdx = orderLog.IndexOf("R2:VerifySha256");
        var lockEnterIdx = orderLog.IndexOf($"LOCK:TicketDecision:Enter:{ticket.Id}");
        r2MetaIdx.Should().BeGreaterThanOrEqualTo(0);
        r2HashIdx.Should().BeGreaterThan(r2MetaIdx);
        lockEnterIdx.Should().BeGreaterThan(r2HashIdx, "R2 HTTP I/O tuyệt đối không được chạy bên trong PostgreSQL transaction ExecuteTicketDecisionAsync");
    }

    // 8. P1-07: SealAudio, GenerateFollowUp & SubmitOfficialExam đều kiểm tra Student active & cửa sổ ca thi
    [Fact(DisplayName = "P1-07B: SealAudio, GenerateFollowUp và SubmitOfficialExam chặn SV bị khóa và ca chưa phát đề/đã hủy/hết giờ")]
    public async Task P1_07_SealAudio_GenerateFollowUp_AndSubmitOfficialExam_EnforceActiveStudentAndShiftWindow()
    {
        await using var db = CreateDbContext();
        var student = new User { Id = Guid.NewGuid(), Email = "sv-p107b@fpt.edu.vn", FullName = "SV", StudentCode = "SE170108", Role = UserRole.Student, IsActive = false };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = Guid.NewGuid(), ExamStructureId = Guid.NewGuid(), Title = "FE", ExamDate = new DateOnly(2026, 11, 20), HasFollowUp = true, MaxFollowUpQuestions = 2, CreatedBy = Guid.NewGuid() };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca P107B", RoomLab = "Lab 2", StartTime = DateTime.UtcNow.AddMinutes(-30), EndTime = DateTime.UtcNow.AddMinutes(20), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = session.CourseId, StructureId = session.ExamStructureId!.Value, SetCode = "SET-107B", IsActive = true };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = session.CourseId, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Q1", ApprovalStatus = "APPROVED", IsActive = true };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = DeviceId,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-10),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(SessionToken)
        };
        var sub = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, TranscriptWhisper = "Valid answer", GradingStatus = "pending" };

        db.Users.Add(student);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.ExamSets.Add(examSet);
        db.ExamQuestions.Add(q1);
        db.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(sub);
        await db.SaveChangesAsync();

        var masterKey = OfficialExamAudioObjectKey.Create(shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!);
        var storageMock = new Mock<IStorageService>();

        // 1. SV bị khóa -> Forbidden trên cả SealAudio & SubmitOfficialExam
        var sealInactive = await new SealOfficialExamAudioCommandHandler(db, storageMock.Object)
            .Handle(new SealOfficialExamAudioCommand(student.Id, ticket.Id, "10.0.0.1", masterKey, ValidSha256, DeviceId, SessionToken), CancellationToken.None);
        sealInactive.IsSuccess.Should().BeFalse();
        sealInactive.Error.Should().Be(OfficialExamErrors.Forbidden);

        var submitInactive = await new SubmitOfficialExamCommandHandler(db)
            .Handle(new SubmitOfficialExamCommand(student.Id, ticket.Id, "10.0.0.1", null, DeviceId, SessionToken), CancellationToken.None);
        submitInactive.IsSuccess.Should().BeFalse();
        submitInactive.Error.Should().Be(OfficialExamErrors.Forbidden);

        // 2. SV active nhưng ca bị cancelled/quá giờ -> CONFLICT trên SealAudio, GenerateFollowUp và SubmitOfficialExam
        student.IsActive = true;
        shift.Status = ExamShiftWindow.Cancelled;
        await db.SaveChangesAsync();

        var sealCancelled = await new SealOfficialExamAudioCommandHandler(db, storageMock.Object)
            .Handle(new SealOfficialExamAudioCommand(student.Id, ticket.Id, "10.0.0.1", masterKey, ValidSha256, DeviceId, SessionToken), CancellationToken.None);
        sealCancelled.IsSuccess.Should().BeFalse();
        sealCancelled.Error.Should().StartWith("CONFLICT:");

        var fuCancelled = await new GenerateOfficialExamFollowUpCommandHandler(db)
            .Handle(new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, sub.Id, DeviceId, SessionToken), CancellationToken.None);
        fuCancelled.IsSuccess.Should().BeFalse();
        fuCancelled.Error.Should().StartWith("CONFLICT:");

        shift.Status = ExamShiftWindow.InProgress;
        shift.EndTime = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var submitExpired = await new SubmitOfficialExamCommandHandler(db)
            .Handle(new SubmitOfficialExamCommand(student.Id, ticket.Id, "10.0.0.1", null, DeviceId, SessionToken), CancellationToken.None);
        submitExpired.IsSuccess.Should().BeFalse();
        submitExpired.Error.Should().StartWith("CONFLICT:");
    }

    // 9. P1-08: OfficialExamGradingWorker monotonic stage guard (skip STT khi đã transcribed, không kéo lùi) + lease ownership guard
    [Fact(DisplayName = "P1-08A: OfficialExamGradingWorker bỏ qua Whisper/R2 khi đã transcribed, không kéo lùi ai_graded và hủy ghi khi mất lease")]
    public async Task P1_08_OfficialExamGradingWorker_MonotonicStageGuard_SkipsSttWhenTranscribed_AndBlocksRegressAndStolenLease()
    {
        // Case A: Đã transcribed với TranscriptWhisper hợp lệ -> bỏ qua R2 & Whisper STT, đi thẳng vào chấm AI
        var dbNameA = Guid.NewGuid().ToString();
        var storageMockA = new Mock<IStorageService>();
        var sttMockA = new Mock<ISpeechToTextService>();
        var aiMockA = new Mock<IAiGradingService>();
        aiMockA.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult { Score = 8.5m, ConfidenceScore = 0.95m, Feedback = "Good" });

        var servicesA = new ServiceCollection();
        servicesA.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbNameA));
        servicesA.AddSingleton(aiMockA.Object);
        servicesA.AddSingleton(storageMockA.Object);
        servicesA.AddSingleton(sttMockA.Object);
        using var providerA = servicesA.BuildServiceProvider();

        var (jobA, subA) = await SeedOfficialGradingCaseAsync(providerA, "transcribed", "Đã bóc băng sẵn từ lần trước", aiScore: null, jobStatus: "processing", attemptCount: 1);
        var workerA = new OfficialExamGradingWorker(new Mock<IOfficialExamGradingQueue>().Object, providerA.GetRequiredService<IServiceScopeFactory>(), NullLogger<OfficialExamGradingWorker>.Instance);
        await workerA.ProcessAsync(new OfficialExamGradingTask(jobA.Id, subA.Id), CancellationToken.None);

        storageMockA.Verify(s => s.VerifySha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sttMockA.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        aiMockA.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        using (var checkScopeA = providerA.CreateScope())
        {
            var checkDb = checkScopeA.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var savedSub = await checkDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == subA.Id);
            savedSub.GradingStatus.Should().Be("ai_graded");
            savedSub.AiScore.Should().Be(8.5m);
            (await checkDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == jobA.Id)).Status.Should().Be("completed");
        }

        // Case B: Lease takeover — trong lúc worker chậm đang gọi AI, worker khác claim lại (AttemptCount 1 -> 2) -> worker chậm hủy ghi
        var dbNameB = Guid.NewGuid().ToString();
        var servicesB = new ServiceCollection();
        servicesB.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbNameB));
        ServiceProvider? providerBRef = null;
        var stolenJobId = Guid.Empty;

        var aiMockB = new Mock<IAiGradingService>();
        aiMockB.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                using var stealScope = providerBRef!.CreateScope();
                var stealDb = stealScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
                var trackedJob = stealDb.GradingJobs.Single(j => j.Id == stolenJobId);
                trackedJob.AttemptCount = 2; // Worker khác đã tiếp quản lease!
                stealDb.SaveChanges();
                return new AiGradingResult { Score = 4.0m, ConfidenceScore = 0.9m, Feedback = "Stale slow worker result" };
            });
        servicesB.AddSingleton(aiMockB.Object);
        using var providerB = servicesB.BuildServiceProvider();
        providerBRef = providerB;

        var (jobB, subB) = await SeedOfficialGradingCaseAsync(providerB, "transcribed", "Transcript hợp lệ", aiScore: null, jobStatus: "processing", attemptCount: 1);
        stolenJobId = jobB.Id;

        var workerB = new OfficialExamGradingWorker(new Mock<IOfficialExamGradingQueue>().Object, providerB.GetRequiredService<IServiceScopeFactory>(), NullLogger<OfficialExamGradingWorker>.Instance);
        await workerB.ProcessAsync(new OfficialExamGradingTask(jobB.Id, subB.Id), CancellationToken.None);

        using (var checkScopeB = providerB.CreateScope())
        {
            var checkDb = checkScopeB.ServiceProvider.GetRequiredService<OralExamDbContext>();
            (await checkDb.AiEvaluations.AsNoTracking().CountAsync(e => e.ExamSubmissionId == subB.Id)).Should().Be(0, "Worker mất lease không được phép ghi đè AiEvaluation");
            var jobAfter = await checkDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == jobB.Id);
            jobAfter.AttemptCount.Should().Be(2);
            jobAfter.Status.Should().Be("processing");
        }

        // Case C: Đã có AiScore -> không kéo lùi trạng thái, không gọi STT/AI, đồng bộ job sang completed và kiểm tra Dispatcher lease = 10 phút
        var dbNameC = Guid.NewGuid().ToString();
        var storageMockC = new Mock<IStorageService>();
        var sttMockC = new Mock<ISpeechToTextService>();
        var aiMockC = new Mock<IAiGradingService>();
        var servicesC = new ServiceCollection();
        servicesC.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbNameC));
        servicesC.AddSingleton(aiMockC.Object);
        servicesC.AddSingleton(storageMockC.Object);
        servicesC.AddSingleton(sttMockC.Object);
        using var providerC = servicesC.BuildServiceProvider();

        var (jobC, subC) = await SeedOfficialGradingCaseAsync(providerC, "pending", "Đã chấm trước đó", aiScore: 9.0m, jobStatus: "pending", attemptCount: 0);
        var beforeDispatch = DateTime.UtcNow;
        var officialQueueMock = new Mock<IOfficialExamGradingQueue>();
        officialQueueMock.Setup(q => q.EnqueueAsync(It.IsAny<OfficialExamGradingTask>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        var dispatcher = new OfficialExamGradingDispatcher(providerC.GetRequiredService<IServiceScopeFactory>(), officialQueueMock.Object, NullLogger<OfficialExamGradingDispatcher>.Instance);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        using (var leaseScope = providerC.CreateScope())
        {
            var leaseDb = leaseScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var claimedJob = await leaseDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == jobC.Id);
            claimedJob.LockedUntil.Should().BeAfter(beforeDispatch.AddMinutes(9.5), "LeaseDuration phải được tăng lên 10 phút");
        }

        var workerC = new OfficialExamGradingWorker(officialQueueMock.Object, providerC.GetRequiredService<IServiceScopeFactory>(), NullLogger<OfficialExamGradingWorker>.Instance);
        await workerC.ProcessAsync(new OfficialExamGradingTask(jobC.Id, subC.Id), CancellationToken.None);

        storageMockC.Verify(s => s.VerifySha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sttMockC.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        aiMockC.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        using (var verifyScopeC = providerC.CreateScope())
        {
            var verifyDbC = verifyScopeC.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var finalSubC = await verifyDbC.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == subC.Id);
            finalSubC.GradingStatus.Should().Be("ai_graded");
            finalSubC.AiScore.Should().Be(9.0m);
            (await verifyDbC.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == jobC.Id)).Status.Should().Be("completed");
        }
    }

    // 10. P1-08: OfficialExamDlqReplayWorker cô lập DI scope theo từng item để 1 lỗi không làm hỏng các item còn lại
    [Fact(DisplayName = "P1-08B: OfficialExamDlqReplayWorker cô lập scope riêng cho từng mục DLQ để lỗi 1 mục không chặn mục kế tiếp")]
    public async Task P1_08_OfficialExamDlqReplayWorker_IsolatesPoisonEntryInSeparateScope_SoRemainingEntriesReplay()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        var enqueueCalls = 0;
        var queueMock = new Mock<IGradingQueueChannel>();
        queueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                enqueueCalls++;
                if (enqueueCalls == 1)
                {
                    throw new InvalidOperationException("Simulated poison channel error on DLQ item #1");
                }
                return ValueTask.CompletedTask;
            });
        services.AddSingleton(queueMock.Object);

        using var provider = services.BuildServiceProvider();
        var answer1Id = Guid.NewGuid();
        var officialJobId = Guid.NewGuid();
        var officialSubId = Guid.NewGuid();
        var dlq1Id = Guid.NewGuid();
        var dlq2Id = Guid.NewGuid();

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var practiceAnswer = new PracticeAnswer { Id = answer1Id, SessionId = Guid.NewGuid(), QuestionId = Guid.NewGuid(), StudentId = Guid.NewGuid(), AnswerText = "Test", Status = "failed", SubmittedAt = DateTime.UtcNow };
            var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = Guid.NewGuid(), StudentId = Guid.NewGuid(), SeatNumber = 1, Status = ExamTicketStatus.Submitted, IsLocked = false };
            var officialSub = new ExamQuestionSubmission { Id = officialSubId, TicketId = ticket.Id, ExamQuestionId = Guid.NewGuid(), GradingStatus = "failed", SubmittedAt = DateTime.UtcNow };
            var officialJob = new GradingJob { Id = officialJobId, SubmissionId = officialSubId, Status = "failed", AttemptCount = 1, NextAttemptAt = DateTime.UtcNow.AddMinutes(-5) };

            db.PracticeAnswers.Add(practiceAnswer);
            db.StudentExamTickets.Add(ticket);
            db.ExamQuestionSubmissions.Add(officialSub);
            db.GradingJobs.Add(officialJob);
            db.DeadLetterQueues.AddRange(
                new OralExamination.Domain.Entities.DeadLetterQueue
                {
                    Id = dlq1Id,
                    TaskType = "PracticeGrading",
                    CorrelationId = answer1Id,
                    PayloadJson = JsonSerializer.Serialize(new GradingTask { AnswerId = answer1Id, SessionId = practiceAnswer.SessionId, QuestionId = practiceAnswer.QuestionId, StudentId = practiceAnswer.StudentId, AnswerText = "Test" }),
                    ErrorMessage = "Transient grading error 1",
                    Status = "pending",
                    RetryCount = 1,
                    NextRetryAt = DateTime.UtcNow.AddMinutes(-2)
                },
                new OralExamination.Domain.Entities.DeadLetterQueue
                {
                    Id = dlq2Id,
                    TaskType = "OfficialExamGrading",
                    CorrelationId = officialJobId,
                    PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(officialJobId, officialSubId)),
                    ErrorMessage = "Transient official grading error 2",
                    Status = "pending",
                    RetryCount = 1,
                    NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
                });
            await db.SaveChangesAsync();
        }

        var worker = new OfficialExamDlqReplayWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueMock.Object);

        await FluentActions.Invoking(() => worker.ReplayDueAsync(CancellationToken.None))
            .Should().NotThrowAsync();

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var dlq2After = await verifyDb.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == dlq2Id);
        dlq2After.Status.Should().Be("resolved");
        var jobAfter = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == officialJobId);
        jobAfter.Status.Should().Be("pending");
    }

    // 11. P1-09: OpenExamShift & UpdateExamShift kiểm tra trùng lịch Phòng/Giám thị/Giám khảo dưới ExecuteOfficialShiftScheduleBatchAsync
    [Fact(DisplayName = "P1-09A: OpenExamShift và UpdateExamShift chặn trùng khung giờ Phòng/Giám thị/Giám khảo, bỏ qua ca cancelled và cho tự cập nhật")]
    public async Task P1_09_OpenAndUpdateExamShift_DetectRoomProctorExaminerOverlap_IgnoreCancelled_AndAllowSelfUpdate()
    {
        await using var db = CreateDbContext();
        var events = new List<string>();
        var proxy = new TrackingDbContextProxy(db, events.Add);

        var officer = new User { Id = Guid.NewGuid(), Email = "eo-p109@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var proctor1 = new User { Id = Guid.NewGuid(), Email = "p1@fpt.edu.vn", FullName = "Proctor 1", Role = UserRole.Proctor, IsActive = true };
        var proctor2 = new User { Id = Guid.NewGuid(), Email = "p2@fpt.edu.vn", FullName = "Proctor 2", Role = UserRole.Proctor, IsActive = true };
        var examiner1 = new User { Id = Guid.NewGuid(), Email = "e1@fpt.edu.vn", FullName = "Examiner 1", Role = UserRole.Lecturer, IsActive = true };
        var examiner2 = new User { Id = Guid.NewGuid(), Email = "e2@fpt.edu.vn", FullName = "Examiner 2", Role = UserRole.Lecturer, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = Guid.NewGuid(), ExamStructureId = Guid.NewGuid(), Title = "FE", ExamDate = new DateOnly(2026, 12, 1), CreatedBy = officer.Id };

        var baseStart = DateTime.UtcNow.AddDays(3);
        var baseEnd = baseStart.AddHours(2);
        var cancelledShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca đã hủy",
            RoomLab = "Lab 301",
            StartTime = baseStart,
            EndTime = baseEnd,
            ProctorUserId = proctor1.Id,
            ExaminerUserId = examiner1.Id,
            Status = ExamShiftWindow.Cancelled
        };

        db.Users.AddRange(officer, proctor1, proctor2, examiner1, examiner2);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(cancelledShift);
        await db.SaveChangesAsync();

        var openHandler = new OpenExamShiftCommandHandler(proxy, new OpenExamShiftCommandValidator());

        // 1. Ca cancelled không chặn mở ca mới trùng phòng/giám thị/giám khảo
        var firstOpen = await openHandler.Handle(new OpenExamShiftCommand(
            officer.Id, session.Id, "Ca 1", "Lab 301", baseStart, baseEnd, proctor1.Id, examiner1.Id, 24), CancellationToken.None);
        firstOpen.IsSuccess.Should().BeTrue();
        events.Should().Contain("LOCK:OfficialShiftScheduleBatch:Enter");

        // 2. Trùng phòng Lab -> CONFLICT
        var roomOverlap = await openHandler.Handle(new OpenExamShiftCommand(
            officer.Id, session.Id, "Ca trùng phòng", "lab 301", baseStart.AddMinutes(30), baseEnd.AddMinutes(30), proctor2.Id, examiner2.Id, 24), CancellationToken.None);
        roomOverlap.IsSuccess.Should().BeFalse();
        roomOverlap.Error.Should().StartWith("CONFLICT:");

        // 3. Trùng giám thị -> CONFLICT
        var proctorOverlap = await openHandler.Handle(new OpenExamShiftCommand(
            officer.Id, session.Id, "Ca trùng GT", "Lab 302", baseStart.AddMinutes(30), baseEnd.AddMinutes(30), proctor1.Id, examiner2.Id, 24), CancellationToken.None);
        proctorOverlap.IsSuccess.Should().BeFalse();
        proctorOverlap.Error.Should().StartWith("CONFLICT:");

        // 4. Trùng giám khảo -> CONFLICT
        var examinerOverlap = await openHandler.Handle(new OpenExamShiftCommand(
            officer.Id, session.Id, "Ca trùng GK", "Lab 302", baseStart.AddMinutes(30), baseEnd.AddMinutes(30), proctor2.Id, examiner1.Id, 24), CancellationToken.None);
        examinerOverlap.IsSuccess.Should().BeFalse();
        examinerOverlap.Error.Should().StartWith("CONFLICT:");

        // 5. Mở ca thứ 2 hợp lệ ở phòng/người khác
        var secondOpen = await openHandler.Handle(new OpenExamShiftCommand(
            officer.Id, session.Id, "Ca 2", "Lab 302", baseStart, baseEnd, proctor2.Id, examiner2.Id, 24), CancellationToken.None);
        secondOpen.IsSuccess.Should().BeTrue();

        var updateHandler = new UpdateExamShiftCommandHandler(proxy);

        // 6. Tự cập nhật chính mình (giữ nguyên phòng/giờ/người) -> KHÔNG bị tự xung đột
        var selfUpdate = await updateHandler.Handle(new UpdateExamShiftCommand(
            officer.Id, UserRole.ExaminationOfficer, firstOpen.Value.Id, "Ca 1 đổi tên", "Lab 301", baseStart, baseEnd, proctor1.Id, examiner1.Id, 24), CancellationToken.None);
        selfUpdate.IsSuccess.Should().BeTrue();

        // 7. Cập nhật Ca 1 sang phòng Lab 302 của Ca 2 cùng giờ -> CONFLICT
        var conflictUpdate = await updateHandler.Handle(new UpdateExamShiftCommand(
            officer.Id, UserRole.ExaminationOfficer, firstOpen.Value.Id, "Ca 1 đụng Ca 2", "Lab 302", baseStart, baseEnd, proctor1.Id, examiner1.Id, 24), CancellationToken.None);
        conflictUpdate.IsSuccess.Should().BeFalse();
        conflictUpdate.Error.Should().StartWith("CONFLICT:");
    }

    // 12. P1-09: UpdateExamShiftStatus chụp đúng OldValues trước mutate và chặn hủy khi có vé IN_PROGRESS/LOCKED
    [Fact(DisplayName = "P1-09B: UpdateExamShiftStatus ghi đúng OldValues trước mutate và chặn hủy ca khi có vé IN_PROGRESS hoặc LOCKED")]
    public async Task P1_09_UpdateExamShiftStatus_CapturesPreMutationAuditOldValues_AndBlocksCancelWhenTicketInProgressOrLocked()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-stat@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca audit",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false
        };
        var lockedShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = shift.SessionId,
            ShiftName = "Ca có vé đang thi",
            RoomLab = "Lab 102",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        db.Users.Add(officer);
        db.RealExamSessionShifts.AddRange(shift, lockedShift);
        db.StudentExamTickets.Add(new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = lockedShift.Id,
            StudentId = Guid.NewGuid(),
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress
        });
        await db.SaveChangesAsync();

        var handler = new UpdateExamShiftStatusCommandHandler(db);

        // 1. scheduled (isExamPublished=false) -> in_progress (isExamPublished=true): OldValues phải giữ nguyên false!
        var toInProgress = await handler.Handle(new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift.Id, ExamShiftWindow.InProgress), CancellationToken.None);
        toInProgress.IsSuccess.Should().BeTrue();

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityId == shift.Id);
        audit.OldValues.Should().Contain("\"status\":\"scheduled\"");
        audit.OldValues.Should().Contain("\"isExamPublished\":false");
        audit.NewValues.Should().Contain("\"status\":\"in_progress\"");
        audit.NewValues.Should().Contain("\"isExamPublished\":true");

        // 2. Chặn hủy ca có vé IN_PROGRESS
        var cancelInProgress = await handler.Handle(new UpdateExamShiftStatusCommand(officer.Id, officer.Role, lockedShift.Id, ExamShiftWindow.Cancelled), CancellationToken.None);
        cancelInProgress.IsSuccess.Should().BeFalse();
        cancelInProgress.Error.Should().StartWith("CONFLICT:");

        // 3. Chặn hủy ca có vé LOCKED
        var ticket = await db.StudentExamTickets.SingleAsync(t => t.ShiftId == lockedShift.Id);
        ticket.Status = ExamTicketStatus.Locked;
        ticket.IsLocked = true;
        await db.SaveChangesAsync();

        var cancelLocked = await handler.Handle(new UpdateExamShiftStatusCommand(officer.Id, officer.Role, lockedShift.Id, ExamShiftWindow.Cancelled), CancellationToken.None);
        cancelLocked.IsSuccess.Should().BeFalse();
        cancelLocked.Error.Should().StartWith("CONFLICT:");
    }

    // 13. P1-09: PublishExamShift chặn công bố khi có vé chưa gán ExamSetId
    [Fact(DisplayName = "P1-09C: PublishExamShift chặn công bố (CONFLICT) khi còn vé chưa có ExamSetId và mở khi đã gán đủ")]
    public async Task P1_09_PublishExamShift_BlocksUnassignedTickets_AndAllowsFullyAssignedShift()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-pub@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca công bố",
            RoomLab = "Lab 201",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = Guid.NewGuid(),
            SeatNumber = 1,
            ExamSetId = null,
            Status = ExamTicketStatus.Scheduled
        };
        db.Users.Add(officer);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new PublishExamShiftCommandHandler(db);

        var blocked = await handler.Handle(new PublishExamShiftCommand(shift.Id, officer.Id, officer.Role), CancellationToken.None);
        blocked.IsSuccess.Should().BeFalse();
        blocked.Error.Should().StartWith("CONFLICT:");
        (await db.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).IsExamPublished.Should().BeFalse();

        ticket.ExamSetId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var allowed = await handler.Handle(new PublishExamShiftCommand(shift.Id, officer.Id, officer.Role), CancellationToken.None);
        allowed.IsSuccess.Should().BeTrue();
        (await db.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).IsExamPublished.Should().BeTrue();
    }

    // 14. P1-09: PadesPdfDigitalSigner bảo toàn sub-dictionary lồng nhau trong /Catalog + AppealsController ánh xạ 403/404/409 RFC 7807
    [Fact(DisplayName = "P1-09D: PadesPdfDigitalSigner bảo toàn nested dict trong /Catalog và AppealsController ánh xạ chuẩn 403/404/409 RFC 7807")]
    public async Task P1_09_PadesPdfDigitalSigner_PreservesNestedCatalogDictionaries_AndAppealsControllerMaps403_404_409()
    {
        // Part 1: PDF /Catalog có nested sub-dictionaries + literal string chứa <<>> + AcroForm lồng nhau
        var nestedCatalogPdf = BuildMinimalPdfWithNestedCatalog();
        using var signer = new PadesPdfDigitalSigner();
        var signed = signer.SignPdf(nestedCatalogPdf, "P1-09 Nested Catalog Seal", "FPTU HCMC", DateTime.UtcNow);

        var verification = PadesPdfDigitalSigner.VerifyEmbeddedSignature(signed.SignedPdfBytes);
        verification.IsValid.Should().BeTrue(verification.Error);

        var signedText = Encoding.Latin1.GetString(signed.SignedPdfBytes);
        var incrementalCatalogIdx = signedText.LastIndexOf("1 0 obj", StringComparison.Ordinal);
        incrementalCatalogIdx.Should().BeGreaterThan(nestedCatalogPdf.Length - 1);
        var updatedCatalogSegment = signedText[incrementalCatalogIdx..];

        updatedCatalogSegment.Should().Contain("/Pages 2 0 R");
        updatedCatalogSegment.Should().Contain("/Names << /Dests 5 0 R >>");
        updatedCatalogSegment.Should().Contain("/ViewerPreferences << /DisplayDocTitle true /Title (Exam (Nested) <<>>) >>");
        updatedCatalogSegment.Should().Contain("/MarkInfo << /Marked true >>");
        updatedCatalogSegment.Should().NotContain("/Fields [9 0 R]", "AcroForm lồng nhau cũ phải được gỡ sạch trước khi chèn AcroForm mới");
        updatedCatalogSegment.Should().Contain("/AcroForm << /Fields [");
        updatedCatalogSegment.Should().Contain("/SigFlags 3 >>");

        // Part 2: AppealsController ánh xạ FORBIDDEN -> 403, NOT_FOUND -> 404, CONFLICT -> 409 ProblemDetails nhất quán
        var senderMock = new Mock<ISender>();
        var controller = new AppealsController(senderMock.Object);
        var actorId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                    new Claim(ClaimTypes.Role, "examination_officer")
                }, "TestAuth"))
            }
        };

        senderMock.Setup(s => s.Send(It.IsAny<CreateAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("FORBIDDEN: Không có quyền."));
        var createForbidden = await controller.CreateAppeal(new CreateAppealRequestDto { TicketId = Guid.NewGuid(), Reason = "Reason" });
        createForbidden.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        senderMock.Setup(s => s.Send(It.IsAny<GetAppealByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("NOT_FOUND: Không tồn tại."));
        var detailNotFound = await controller.GetAppealById(Guid.NewGuid());
        detailNotFound.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().BeOfType<ProblemDetails>();

        senderMock.Setup(s => s.Send(It.IsAny<BatchAssignAppealsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BatchAssignAppealsResponseDto>.Failure("CONFLICT: Quá hạn mức 24 bài/GV."));
        var batchConflict = await controller.BatchAssign(new BatchAssignAppealsRequestDto());
        batchConflict.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().BeOfType<ProblemDetails>();

        senderMock.Setup(s => s.Send(It.IsAny<GetAppealEvidenceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuditEvidenceItemDto>.Failure("NOT_FOUND: Không thấy bằng chứng."));
        var evidenceNotFound = await controller.GetAppealEvidence(Guid.NewGuid());
        evidenceNotFound.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().BeOfType<ProblemDetails>();

        senderMock.Setup(s => s.Send(It.IsAny<RegradeAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AppealResponseDto>.Failure("NOT_FOUND: Đơn không tồn tại."));
        var regradeNotFound = await controller.Regrade(Guid.NewGuid(), new RegradeAppealRequestDto { NewScore = 8.0m, ReviewNotes = "OK" }, CancellationToken.None);
        regradeNotFound.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // 15. Remediation B1+B2 & 🟡 B3: Fencing Token xuyên suốt Dispatcher -> Worker -> MarkFailed/FlagAudioIntegrityMismatch & DLQ bảo toàn transcribed
    [Fact(DisplayName = "Remediation B1+B2+B3: Fencing token ClaimedAttemptCount chặn stale worker ghi đè MarkFailed/FlagMismatch và DLQ bảo toàn trạng thái transcribed")]
    public async Task Remediation_B1_B2_OfficialExamGradingWorker_FencingTokenBlocksStaleWorkerMarkFailedAndFlagMismatch_AndDlqPreservesTranscribed()
    {
        // Part 1: Dispatcher gắn đúng ClaimedAttemptCount = AttemptCount + 1 và stale worker thoát sạch ở ProcessAsync + MarkFailedAsync
        var dbName1 = Guid.NewGuid().ToString();
        var storageMock1 = new Mock<IStorageService>();
        var sttMock1 = new Mock<ISpeechToTextService>();
        var aiMock1 = new Mock<IAiGradingService>();
        var services1 = new ServiceCollection();
        services1.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName1));
        services1.AddSingleton(storageMock1.Object);
        services1.AddSingleton(sttMock1.Object);
        services1.AddSingleton(aiMock1.Object);
        using var provider1 = services1.BuildServiceProvider();

        var (job1, sub1) = await SeedOfficialGradingCaseAsync(
            provider1,
            gradingStatus: "transcribed",
            transcript: "Bản bóc băng hợp lệ trước khi worker 2 tiếp quản",
            aiScore: null,
            jobStatus: "pending",
            attemptCount: 1);

        using (var prepScope = provider1.CreateScope())
        {
            var prepDb = prepScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var trackedJob = await prepDb.GradingJobs.SingleAsync(j => j.Id == job1.Id);
            trackedJob.LockedUntil = null;
            trackedJob.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
            await prepDb.SaveChangesAsync();
        }

        OfficialExamGradingTask? dispatchedTask = null;
        var queueMock1 = new Mock<IOfficialExamGradingQueue>();
        queueMock1.Setup(q => q.EnqueueAsync(It.IsAny<OfficialExamGradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<OfficialExamGradingTask, CancellationToken>((t, _) => dispatchedTask = t)
            .Returns(ValueTask.CompletedTask);

        var dispatcher = new OfficialExamGradingDispatcher(
            provider1.GetRequiredService<IServiceScopeFactory>(),
            queueMock1.Object,
            NullLogger<OfficialExamGradingDispatcher>.Instance);
        await dispatcher.DispatchBatchAsync(CancellationToken.None);

        dispatchedTask.Should().NotBeNull();
        dispatchedTask!.JobId.Should().Be(job1.Id);
        dispatchedTask.SubmissionId.Should().Be(sub1.Id);
        dispatchedTask.ClaimedAttemptCount.Should().Be(2, "Dispatcher phải truyền đúng AttemptCount mới sau khi claim vào fencing token");

        // Worker 1 cũ cầm stale ClaimedAttemptCount = 1 khi DB đã là AttemptCount = 2 -> ProcessAsync thoát sạch không gọi R2/STT/AI
        var worker1 = new OfficialExamGradingWorker(
            queueMock1.Object,
            provider1.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);
        var staleTask = new OfficialExamGradingTask(job1.Id, sub1.Id, ClaimedAttemptCount: 1);

        await worker1.ProcessAsync(staleTask, CancellationToken.None);
        storageMock1.Verify(s => s.VerifySha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sttMock1.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        aiMock1.Verify(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // Worker 1 cũ gặp lỗi muộn gọi MarkFailedAsync với stale ClaimedAttemptCount = 1 -> bỏ qua sạch, không ghi đè submission/job hay tạo DLQ giả
        await worker1.MarkFailedAsync(staleTask, new TimeoutException("Stale worker 1 timeout"), CancellationToken.None);

        using (var verifyScope1 = provider1.CreateScope())
        {
            var verifyDb = verifyScope1.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var subAfter = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub1.Id);
            var jobAfter = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job1.Id);
            subAfter.GradingStatus.Should().Be("transcribed", "Stale worker mất lease tuyệt đối không được hạ ngược submission sang failed");
            jobAfter.Status.Should().Be("processing");
            jobAfter.AttemptCount.Should().Be(2);
            (await verifyDb.DeadLetterQueues.CountAsync()).Should().Be(0, "Stale worker mất lease không được sinh bản ghi DLQ giả");
        }

        // Part 2: FlagAudioIntegrityMismatchUnderTicketLockAsync bỏ qua khi worker mất lease trong lúc VerifySha256Async
        var dbName2 = Guid.NewGuid().ToString();
        ServiceProvider? provider2Ref = null;
        var stolenJobId = Guid.Empty;
        var storageMock2 = new Mock<IStorageService>();
        storageMock2.Setup(s => s.VerifySha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                using var stealScope = provider2Ref!.CreateScope();
                var stealDb = stealScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
                var j = stealDb.GradingJobs.Single(item => item.Id == stolenJobId);
                j.AttemptCount = 2; // Worker khác đã tiếp quản lease trước khi SHA-256 trả về false
                stealDb.SaveChanges();
                return false;
            });
        var sttMock2 = new Mock<ISpeechToTextService>();
        var aiMock2 = new Mock<IAiGradingService>();
        var services2 = new ServiceCollection();
        services2.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName2));
        services2.AddSingleton(storageMock2.Object);
        services2.AddSingleton(sttMock2.Object);
        services2.AddSingleton(aiMock2.Object);
        using var provider2 = services2.BuildServiceProvider();
        provider2Ref = provider2;

        var (job2, sub2) = await SeedOfficialGradingCaseAsync(
            provider2,
            gradingStatus: "pending",
            transcript: null,
            aiScore: null,
            jobStatus: "processing",
            attemptCount: 1);
        stolenJobId = job2.Id;

        var worker2 = new OfficialExamGradingWorker(
            new Mock<IOfficialExamGradingQueue>().Object,
            provider2.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);

        await FluentActions.Invoking(() => worker2.ProcessAsync(
                new OfficialExamGradingTask(job2.Id, sub2.Id, ClaimedAttemptCount: 1),
                CancellationToken.None))
            .Should().NotThrowAsync("Worker đã mất lease không được phép gắn cờ nghi vấn hoặc ném lỗi AUDIO_INTEGRITY_MISMATCH giả");

        using (var verifyScope2 = provider2.CreateScope())
        {
            var verifyDb = verifyScope2.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var sub2After = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub2.Id);
            sub2After.IsSuspicious.Should().BeFalse();
            sub2After.SuspiciousReason.Should().BeNull();
        }

        // Part 3: OfficialExamDlqReplayWorker CHỈ giữ stage "transcribed" khi DLQ payload mang cờ ResumeFromTranscribed=true và !IsSuspicious;
        // mọi ca lỗi ở "transcribing"/SHA-256 mismatch (dù có sẵn client TranscriptWhisper + AudioVerifiedAt) BẮT BUỘC quay về "pending" để kiểm tra lại R2/Whisper.
        var dbName3 = Guid.NewGuid().ToString();
        var services3 = new ServiceCollection();
        services3.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName3));
        using var provider3 = services3.BuildServiceProvider();

        var (job3, sub3) = await SeedOfficialGradingCaseAsync(
            provider3,
            gradingStatus: "failed",
            transcript: "Transcript đã được Whisper bóc băng và xác minh âm thanh",
            aiScore: null,
            jobStatus: "failed",
            attemptCount: 2);
        var (job4, sub4) = await SeedOfficialGradingCaseAsync(
            provider3,
            gradingStatus: "failed",
            transcript: "Client transcript chưa qua Whisper (lỗi SHA-256/STT ở stage transcribing)",
            aiScore: null,
            jobStatus: "failed",
            attemptCount: 1);
        var (job5, sub5) = await SeedOfficialGradingCaseAsync(
            provider3,
            gradingStatus: "failed",
            transcript: "Suspicious audio transcript",
            aiScore: null,
            jobStatus: "failed",
            attemptCount: 1);
        using (var seedDlqScope = provider3.CreateScope())
        {
            var seedDb = seedDlqScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var trackedSub3 = await seedDb.ExamQuestionSubmissions.Include(s => s.Ticket).SingleAsync(s => s.Id == sub3.Id);
            trackedSub3.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);
            trackedSub3.Ticket.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);

            var trackedSub4 = await seedDb.ExamQuestionSubmissions.Include(s => s.Ticket).SingleAsync(s => s.Id == sub4.Id);
            trackedSub4.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);
            trackedSub4.Ticket.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);

            var trackedSub5 = await seedDb.ExamQuestionSubmissions.Include(s => s.Ticket).SingleAsync(s => s.Id == sub5.Id);
            trackedSub5.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);
            trackedSub5.Ticket.AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-10);
            trackedSub5.IsSuspicious = true;
            trackedSub5.SuspiciousReason = "AUDIO_INTEGRITY_MISMATCH";

            seedDb.DeadLetterQueues.AddRange(
                new OralExamination.Domain.Entities.DeadLetterQueue
                {
                    Id = Guid.NewGuid(),
                    TaskType = "OfficialExamGrading",
                    CorrelationId = job3.Id,
                    PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(job3.Id, sub3.Id, ResumeFromTranscribed: true)),
                    ErrorMessage = "Gemini 503",
                    Status = "pending",
                    RetryCount = 2,
                    NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new OralExamination.Domain.Entities.DeadLetterQueue
                {
                    Id = Guid.NewGuid(),
                    TaskType = "OfficialExamGrading",
                    CorrelationId = job4.Id,
                    PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(job4.Id, sub4.Id, ResumeFromTranscribed: false)),
                    ErrorMessage = "Whisper 503 during transcribing",
                    Status = "pending",
                    RetryCount = 1,
                    NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new OralExamination.Domain.Entities.DeadLetterQueue
                {
                    Id = Guid.NewGuid(),
                    TaskType = "OfficialExamGrading",
                    CorrelationId = job5.Id,
                    PayloadJson = JsonSerializer.Serialize(new OfficialExamGradingTask(job5.Id, sub5.Id, ResumeFromTranscribed: true)),
                    ErrorMessage = "AUDIO_INTEGRITY_MISMATCH",
                    Status = "pending",
                    RetryCount = 1,
                    NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
                });
            await seedDb.SaveChangesAsync();
        }

        var replayWorker = new OfficialExamDlqReplayWorker(
            provider3.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            new Mock<IGradingQueueChannel>().Object);
        await replayWorker.ReplayDueAsync(CancellationToken.None);

        using (var verifyScope3 = provider3.CreateScope())
        {
            var verifyDb = verifyScope3.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var sub3After = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub3.Id);
            var sub4After = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub4.Id);
            var sub5After = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub5.Id);
            var job3After = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job3.Id);

            sub3After.GradingStatus.Should().Be("transcribed", "Ca lỗi sau khi đã qua Whisper (ResumeFromTranscribed=true, !IsSuspicious) được giữ stage transcribed");
            sub4After.GradingStatus.Should().Be("pending", "Ca lỗi trong bước transcribing/SHA-256 (ResumeFromTranscribed=false) BẮT BUỘC quay về pending dù có sẵn client transcript & AudioVerifiedAt");
            sub5After.GradingStatus.Should().Be("pending", "Ca đã bị gắn IsSuspicious=true BẮT BUỘC quay về pending xác minh lại, không được bỏ qua SHA-256/Whisper");
            sub5After.IsSuspicious.Should().BeTrue();
            sub5After.SuspiciousReason.Should().Be("AUDIO_INTEGRITY_MISMATCH");
            job3After.Status.Should().Be("pending");
        }
    }

    // 16. Remediation B3: InClassShiftAutoSubmit.FinalizeParticipantAsync xóa stale Identity Map, không hạ ngược GRADED về SUBMITTED
    [Fact(DisplayName = "Remediation B3: FinalizeParticipantAsync nạp tươi từ DB chống EF Identity-Map stale read và không hạ ngược GRADED về SUBMITTED")]
    public async Task Remediation_B3_InClassShiftAutoSubmit_FinalizeParticipantReloadsFreshState_AndNeverDowngradesGraded()
    {
        var dbName = Guid.NewGuid().ToString();
        var shiftId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var q1Id = Guid.NewGuid();
        var finishedAt = DateTime.UtcNow.AddMinutes(-1);

        await using var callerDb = CreateDbContext(dbName);
        var seededParticipant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = studentId,
            AssignedQuestionId = q1Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { q1Id }),
            Status = "IN_PROGRESS",
            JoinedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        callerDb.InClassShiftParticipants.Add(seededParticipant);
        await callerDb.SaveChangesAsync();

        // Caller vẫn đang track instance participant cũ với Status = "IN_PROGRESS" trong ChangeTracker của callerDb
        var staleTrackedParticipant = await callerDb.InClassShiftParticipants
            .SingleAsync(p => p.ShiftId == shiftId && p.StudentId == studentId);
        staleTrackedParticipant.Status.Should().Be("IN_PROGRESS");

        // Worker song song (DbContext khác) chấm xong và chuyển participant trong DB sang "GRADED"
        await using (var workerDb = CreateDbContext(dbName))
        {
            var dbParticipant = await workerDb.InClassShiftParticipants
                .SingleAsync(p => p.ShiftId == shiftId && p.StudentId == studentId);
            dbParticipant.Status = "GRADED";
            dbParticipant.FinishedAt = finishedAt;
            dbParticipant.TotalAiScore = 8.50m;
            dbParticipant.TotalFinalScore = 8.50m;
            await workerDb.SaveChangesAsync();
        }

        var queueMock = new Mock<IGradingQueueChannel>();
        var createdCount = await InClassShiftAutoSubmit.FinalizeParticipantAsync(
            callerDb,
            queueMock.Object,
            staleTrackedParticipant,
            CancellationToken.None);

        createdCount.Should().Be(0);
        staleTrackedParticipant.Status.Should().Be("GRADED");
        staleTrackedParticipant.FinishedAt.Should().Be(finishedAt);
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);

        await using var verifyDb = CreateDbContext(dbName);
        var persisted = await verifyDb.InClassShiftParticipants.AsNoTracking()
            .SingleAsync(p => p.ShiftId == shiftId && p.StudentId == studentId);
        persisted.Status.Should().Be("GRADED", "FinalizeParticipantAsync tuyệt đối không được hạ ngược GRADED về SUBMITTED do EF Identity Map stale read");
        (await verifyDb.InClassExamSubmissions.AsNoTracking().CountAsync(s => s.ShiftId == shiftId && s.StudentId == studentId)).Should().Be(0);
    }

    // 17. Remediation B4: AdjustInClassSubmissionScore chạy dưới ExecuteInClassSubmissionAsync và không ép 0m giả cho câu chưa chấm
    [Fact(DisplayName = "Remediation B4: AdjustInClassSubmissionScore khóa dưới ExecuteInClassSubmissionAsync và không ép 0m giả cho câu đang chờ AI chấm")]
    public async Task Remediation_B4_AdjustInClassSubmissionScore_LocksUnderInClassSubmission_AndDoesNotInjectZeroForUngradedQuestion()
    {
        await using var db = CreateDbContext();
        var events = new List<string>();
        var proxy = new TrackingDbContextProxy(db, events.Add);

        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-b4@fpt.edu.vn", FullName = "GV B4", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-b4@fpt.edu.vn", FullName = "SV B4", StudentCode = "SE180404", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1804", Name = "SE1804", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var q1Id = Guid.NewGuid();
        var q2Id = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca B4",
            TotpSecretKey = "B4_SECRET",
            Status = "CLOSED",
            TotalQuestionsPerStudent = 2,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = q1Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { q1Id, q2Id }),
            Status = "SUBMITTED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-25)
        };
        var sub1 = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = q1Id,
            Transcript = "Câu 1 đã được AI chấm",
            AiScore = 6.0m,
            FinalScore = 6.0m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var sub2Pending = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = q2Id,
            Transcript = "Câu 2 đang chờ GradingQueueWorker chấm",
            AiScore = null,
            FinalScore = null,
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-9)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.AddRange(sub1, sub2Pending);
        await db.SaveChangesAsync();

        var handler = new AdjustInClassSubmissionScoreCommandHandler(proxy, new AdjustInClassSubmissionScoreCommandValidator());
        var result = await handler.Handle(
            new AdjustInClassSubmissionScoreCommand(lecturer.Id, sub1.Id, 8.0m, "Hậu kiểm nâng điểm câu 1", shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FinalScore.Should().Be(8.0m);
        result.Value.Status.Should().Be("AUDITED");

        events.Should().Contain($"LOCK:InClassSubmission:Enter:{shift.Id}:{student.Id}");
        events.Should().Contain($"LOCK:InClassSubmission:Exit:{shift.Id}:{student.Id}");

        var updatedParticipant = await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id);
        updatedParticipant.TotalFinalScore.Should().Be(8.00m, "Câu 2 đang SUBMITTED chưa có điểm không được ép thành 0m làm tụt điểm trung bình xuống 4.00m");
        updatedParticipant.TotalAiScore.Should().Be(6.00m);
        updatedParticipant.Status.Should().Be("SUBMITTED", "Chưa được nâng lên GRADED khi câu 2 vẫn đang SUBMITTED");
    }

    // 18. Remediation B5: UpdateExamShiftStatus chặn in_progress khi còn vé thiếu ExamSetId và completed không tự bật IsExamPublished
    [Fact(DisplayName = "Remediation B5: UpdateExamShiftStatus chặn mở ca khi còn vé chưa gán đề và completed không tự ép IsExamPublished = true")]
    public async Task Remediation_B5_UpdateExamShiftStatus_BlocksInProgressWithUnassignedTickets_AndCompletedNeverForcesPublish()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-b5@fpt.edu.vn", FullName = "EO B5", Role = UserRole.ExaminationOfficer, IsActive = true };
        var shift1 = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca B5 thiếu đề",
            RoomLab = "Lab 501",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false
        };
        var ticketWithoutSet = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift1.Id,
            StudentId = Guid.NewGuid(),
            SeatNumber = 1,
            ExamSetId = null,
            Status = ExamTicketStatus.Scheduled
        };
        var shift2Unpublished = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = shift1.SessionId,
            ShiftName = "Ca B5 đóng chưa công bố",
            RoomLab = "Lab 502",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false
        };

        db.Users.Add(officer);
        db.RealExamSessionShifts.AddRange(shift1, shift2Unpublished);
        db.StudentExamTickets.Add(ticketWithoutSet);
        await db.SaveChangesAsync();

        var handler = new UpdateExamShiftStatusCommandHandler(db);

        // 1. Chặn chuyển scheduled -> in_progress khi còn vé có ExamSetId == null
        var blocked = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift1.Id, ExamShiftWindow.InProgress),
            CancellationToken.None);
        blocked.IsSuccess.Should().BeFalse();
        blocked.Error.Should().StartWith("CONFLICT:");
        var shift1AfterBlock = await db.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift1.Id);
        shift1AfterBlock.Status.Should().Be(ExamShiftWindow.Scheduled);
        shift1AfterBlock.IsExamPublished.Should().BeFalse();

        // 2. Sau khi gán đủ ExamSetId -> cho phép chuyển sang in_progress
        ticketWithoutSet.ExamSetId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var allowed = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift1.Id, ExamShiftWindow.InProgress),
            CancellationToken.None);
        allowed.IsSuccess.Should().BeTrue();
        (await db.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift1.Id)).IsExamPublished.Should().BeTrue();

        // 3. Chuyển scheduled (chưa công bố) -> completed KHÔNG tự ép IsExamPublished = true
        var completed = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift2Unpublished.Id, ExamShiftWindow.Completed),
            CancellationToken.None);
        completed.IsSuccess.Should().BeTrue();
        var shift2After = await db.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift2Unpublished.Id);
        shift2After.Status.Should().Be(ExamShiftWindow.Completed);
        shift2After.IsExamPublished.Should().BeFalse("Đóng ca chưa từng công bố đề không được tự bật IsExamPublished = true");
        shift2After.PublishedAt.Should().BeNull();
    }

    // 19. Remediation 🟡 Panel B: SealAudio R2 ngoài lock, chặn ABSENT ở Enter/Resume và AppealsController truyền CancellationToken
    [Fact(DisplayName = "Remediation 🟡: SealOfficialExamAudio chạy R2 ngoài ExecuteTicketDecisionAsync, Enter/Resume chặn ABSENT và AppealsController chuyển tiếp CancellationToken")]
    public async Task Remediation_Yellow_SealAudioRunsR2OutsideLock_AbsentBlockedOnEnterAndResume_AndAppealsControllerForwardsCancellationToken()
    {
        // Part 1: SealOfficialExamAudio chạy R2 GetObjectMetadata + VerifySha256 hoàn toàn NGOÀI ExecuteTicketDecisionAsync
        await using var db = CreateDbContext();
        var orderLog = new List<string>();
        var proxy = new TrackingDbContextProxy(db, orderLog.Add);

        var student = new User { Id = Guid.NewGuid(), Email = "sv-seal@fpt.edu.vn", FullName = "SV Seal", StudentCode = "SE170999", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Seal 2-phase",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 2,
            Status = ExamTicketStatus.InProgress,
            DeviceId = DeviceId,
            DeviceLockedAt = DateTime.UtcNow.AddMinutes(-5),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(SessionToken)
        };

        db.Users.Add(student);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var expectedKey = OfficialExamAudioObjectKey.Create(shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!);
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetObjectMetadataAsync(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                orderLog.Add("R2:GetObjectMetadata");
                return new StorageObjectMetadata(expectedKey, 4096, "audio/webm", "etag", DateTime.UtcNow);
            });
        storageMock.Setup(s => s.VerifySha256Async(expectedKey, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                orderLog.Add("R2:VerifySha256");
                return true;
            });

        var sealRes = await new SealOfficialExamAudioCommandHandler(proxy, storageMock.Object).Handle(
            new SealOfficialExamAudioCommand(student.Id, ticket.Id, "10.0.0.1", expectedKey, ValidSha256, DeviceId, SessionToken),
            CancellationToken.None);
        sealRes.IsSuccess.Should().BeTrue();

        var metaIdx = orderLog.IndexOf("R2:GetObjectMetadata");
        var hashIdx = orderLog.IndexOf("R2:VerifySha256");
        var lockEnterIdx = orderLog.IndexOf($"LOCK:TicketDecision:Enter:{ticket.Id}");
        var lockExitIdx = orderLog.IndexOf($"LOCK:TicketDecision:Exit:{ticket.Id}");
        metaIdx.Should().BeGreaterThanOrEqualTo(0);
        hashIdx.Should().BeGreaterThan(metaIdx);
        lockEnterIdx.Should().BeGreaterThan(hashIdx, "SealOfficialExamAudio phải chạy toàn bộ R2 HTTP I/O NGOÀI khóa ExecuteTicketDecisionAsync");
        lockExitIdx.Should().BeGreaterThan(lockEnterIdx);

        // Part 2: EnterInClassShift & ResumeInClassShift chặn SV có trạng thái ABSENT bằng CONFLICT
        EnterInClassShiftCommandHandler.IsCompletedParticipation("ABSENT").Should().BeTrue();

        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-abs@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1899", Name = "SE1899", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var secretKey = TotpPasswordHelper.GenerateSecretKey(32);
        var inClassShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca chặn ABSENT",
            TotpSecretKey = secretKey,
            Status = "IN_PROGRESS",
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var absentParticipant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = inClassShift.Id,
            StudentId = student.Id,
            Status = "ABSENT",
            JoinedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        db.Users.Add(lecturer);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student.Id, Status = "enrolled" });
        db.InClassExamShifts.Add(inClassShift);
        db.InClassShiftParticipants.Add(absentParticipant);
        await db.SaveChangesAsync();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        var validOtp = TotpPasswordHelper.ComputeTotp(secretKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var enterAbsentRes = await new EnterInClassShiftCommandHandler(db, new EnterInClassShiftCommandValidator(), gradingQueueMock.Object)
            .Handle(new EnterInClassShiftCommand(student.Id, inClassShift.Id, validOtp), CancellationToken.None);
        enterAbsentRes.IsSuccess.Should().BeFalse();
        enterAbsentRes.Error.Should().StartWith("CONFLICT:");

        var resumeAbsentRes = await new ResumeInClassShiftCommandHandler(db, gradingQueueMock.Object)
            .Handle(new ResumeInClassShiftCommand(student.Id, inClassShift.Id), CancellationToken.None);
        resumeAbsentRes.IsSuccess.Should().BeFalse();
        resumeAbsentRes.Error.Should().StartWith("CONFLICT:");

        // Part 3: AppealsController chuyển tiếp đúng CancellationToken cho CreateAppeal, GetAppeals, GetAppealById và ReviewAppeal
        using var cts = new CancellationTokenSource();
        var expectedToken = cts.Token;
        var senderMock = new Mock<ISender>();
        var appealDto = new AppealResponseDto { Id = Guid.NewGuid(), Status = "PENDING", Reason = "Test reason" };
        senderMock.Setup(s => s.Send(It.IsAny<CreateAppealCommand>(), expectedToken)).ReturnsAsync(Result<AppealResponseDto>.Success(appealDto));
        senderMock.Setup(s => s.Send(It.IsAny<GetAppealsQuery>(), expectedToken)).ReturnsAsync(Result<List<AppealResponseDto>>.Success([appealDto]));
        senderMock.Setup(s => s.Send(It.IsAny<GetAppealByIdQuery>(), expectedToken)).ReturnsAsync(Result<AppealResponseDto>.Success(appealDto));
        senderMock.Setup(s => s.Send(It.IsAny<ReviewAppealDecisionCommand>(), expectedToken)).ReturnsAsync(Result<AppealResponseDto>.Success(appealDto));

        var appealsController = new AppealsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
                        new Claim(ClaimTypes.Role, "examination_officer")
                    }, "TestAuth"))
                }
            }
        };

        await appealsController.CreateAppeal(new CreateAppealRequestDto { TicketId = ticket.Id, Reason = "Lý do phúc khảo hợp lệ" }, expectedToken);
        await appealsController.GetAppeals(null, null, null, null, expectedToken);
        await appealsController.GetAppealById(appealDto.Id, expectedToken);
        await appealsController.ReviewAppeal(appealDto.Id, new ReviewAppealRequestDto { Decision = "APPROVED", ProposedScore = 8.5m, ReviewNotes = "Đồng ý" }, expectedToken);

        senderMock.Verify(s => s.Send(It.IsAny<CreateAppealCommand>(), expectedToken), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<GetAppealsQuery>(), expectedToken), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<GetAppealByIdQuery>(), expectedToken), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<ReviewAppealDecisionCommand>(), expectedToken), Times.Once);
    }

    private static byte[] BuildMinimalPdfWithNestedCatalog()
    {
        using var ms = new MemoryStream();
        static void Write(Stream s, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            s.Write(bytes, 0, bytes.Length);
        }

        Write(ms, "%PDF-1.4\n");
        var off1 = ms.Position;
        Write(ms, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R /Names << /Dests 5 0 R >> /ViewerPreferences << /DisplayDocTitle true /Title (Exam (Nested) <<>>) >> /AcroForm << /Fields [9 0 R] /DR << /Font << /F1 8 0 R >> >> >> /MarkInfo << /Marked true >> >>\nendobj\n");
        var off2 = ms.Position;
        Write(ms, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        var off3 = ms.Position;
        Write(ms, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>\nendobj\n");
        var xrefPos = ms.Position;
        Write(ms, "xref\n0 4\n0000000000 65535 f \n");
        Write(ms, $"{off1:D10} 00000 n \n");
        Write(ms, $"{off2:D10} 00000 n \n");
        Write(ms, $"{off3:D10} 00000 n \n");
        Write(ms, $"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
        return ms.ToArray();
    }

    private static async Task<(GradingJob Job, ExamQuestionSubmission Submission)> SeedOfficialGradingCaseAsync(
        ServiceProvider provider,
        string gradingStatus,
        string? transcript,
        decimal? aiScore,
        string jobStatus,
        int attemptCount)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var question = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain ACID", SampleAnswer = new string('s', 60), ApprovalStatus = "APPROVED", IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = Guid.NewGuid(), Title = "FE", ExamDate = new DateOnly(2026, 11, 20), ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit, CreatedBy = Guid.NewGuid() };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddMinutes(-30), EndTime = DateTime.UtcNow.AddMinutes(30), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), SeatNumber = 1, Status = ExamTicketStatus.Submitted };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            AudioObjectKey = $"official-exams/{shift.Id:N}/{ticket.Id:N}/01_SE170001_Q01.webm",
            AudioHashSha256 = ValidSha256,
            TranscriptWhisper = transcript,
            GradingStatus = gradingStatus,
            AiScore = aiScore,
            SubmittedAt = DateTime.UtcNow
        };
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = jobStatus,
            AttemptCount = attemptCount,
            LockedUntil = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(criterion);
        db.ExamQuestions.Add(question);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(submission);
        db.GradingJobs.Add(job);
        await db.SaveChangesAsync();

        return (job, submission);
    }

    private sealed class TrackingDbContextProxy(OralExamDbContext inner, Action<string> record) : IApplicationDbContext
    {
        public Guid? FailInClassStudentId { get; set; }
        public ConcurrentDictionary<(Guid ShiftId, Guid StudentId), SemaphoreSlim>? InClassLockMap { get; set; }
        public AsyncLocal<bool>? InClassLockTracker { get; set; }

        public DbSet<User> Users => inner.Users;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<Semester> Semesters => inner.Semesters;
        public DbSet<AcademicProgram> AcademicPrograms => inner.AcademicPrograms;
        public DbSet<Course> Courses => inner.Courses;
        public DbSet<CourseOffering> CourseOfferings => inner.CourseOfferings;
        public DbSet<CourseClo> CourseClos => inner.CourseClos;
        public DbSet<QuestionReviewRequest> QuestionReviewRequests => inner.QuestionReviewRequests;
        public DbSet<QuestionReviewRequestItem> QuestionReviewRequestItems => inner.QuestionReviewRequestItems;
        public DbSet<CourseLecturer> CourseLecturers => inner.CourseLecturers;
        public DbSet<Class> Classes => inner.Classes;
        public DbSet<ClassEnrollment> ClassEnrollments => inner.ClassEnrollments;
        public DbSet<Rubric> Rubrics => inner.Rubrics;
        public DbSet<RubricCriterion> RubricCriteria => inner.RubricCriteria;
        public DbSet<PracticeQuestion> PracticeQuestions => inner.PracticeQuestions;
        public DbSet<ExamQuestion> ExamQuestions => inner.ExamQuestions;
        public DbSet<PracticeSession> PracticeSessions => inner.PracticeSessions;
        public DbSet<PracticeAnswer> PracticeAnswers => inner.PracticeAnswers;
        public DbSet<AiEvaluation> AiEvaluations => inner.AiEvaluations;
        public DbSet<AiEvaluationDetail> AiEvaluationDetails => inner.AiEvaluationDetails;
        public DbSet<ExamStructure> ExamStructures => inner.ExamStructures;
        public DbSet<ExamSet> ExamSets => inner.ExamSets;
        public DbSet<ExamSetQuestion> ExamSetQuestions => inner.ExamSetQuestions;
        public DbSet<MockExamQuota> MockExamQuotas => inner.MockExamQuotas;
        public DbSet<MockExamSession> MockExamSessions => inner.MockExamSessions;
        public DbSet<MockExamAnswer> MockExamAnswers => inner.MockExamAnswers;
        public DbSet<OfficialExamSession> OfficialExamSessions => inner.OfficialExamSessions;
        public DbSet<OfficialExamCandidate> OfficialExamCandidates => inner.OfficialExamCandidates;
        public DbSet<RealExamSessionShift> RealExamSessionShifts => inner.RealExamSessionShifts;
        public DbSet<StudentExamTicket> StudentExamTickets => inner.StudentExamTickets;
        public DbSet<ExamQuestionSubmission> ExamQuestionSubmissions => inner.ExamQuestionSubmissions;
        public DbSet<ExamAntiCheatLog> ExamAntiCheatLogs => inner.ExamAntiCheatLogs;
        public DbSet<LecturerAudit> LecturerAudits => inner.LecturerAudits;
        public DbSet<LecturerAuditDetail> LecturerAuditDetails => inner.LecturerAuditDetails;
        public DbSet<AppealRequest> AppealRequests => inner.AppealRequests;
        public DbSet<OralExamination.Domain.Entities.DeadLetterQueue> DeadLetterQueues => inner.DeadLetterQueues;
        public DbSet<GradingJob> GradingJobs => inner.GradingJobs;
        public DbSet<AuditLog> AuditLogs => inner.AuditLogs;
        public DbSet<SystemConfig> SystemConfigs => inner.SystemConfigs;
        public DbSet<InClassExamShift> InClassExamShifts => inner.InClassExamShifts;
        public DbSet<InClassShiftPassword> InClassShiftPasswords => inner.InClassShiftPasswords;
        public DbSet<InClassShiftParticipant> InClassShiftParticipants => inner.InClassShiftParticipants;
        public DbSet<InClassExamSubmission> InClassExamSubmissions => inner.InClassExamSubmissions;
        public DbSet<Notification> Notifications => inner.Notifications;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);

        public Task<T> ExecuteShiftAllocationAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> allocate, CancellationToken cancellationToken) =>
            inner.ExecuteShiftAllocationAsync(shiftId, allocate, cancellationToken);

        public async Task<T> ExecuteShiftMutationAsync<T>(Guid shiftId, bool isOfficial, Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken = default)
        {
            record($"LOCK:ShiftMutation:Enter:{shiftId}:{isOfficial}");
            try
            {
                return await inner.ExecuteShiftMutationAsync(shiftId, isOfficial, mutation, cancellationToken);
            }
            finally
            {
                record($"LOCK:ShiftMutation:Exit:{shiftId}:{isOfficial}");
            }
        }

        public Task<IAsyncDisposable?> BeginShiftAllocationLockScopeAsync(Guid shiftId, CancellationToken cancellationToken = default) =>
            inner.BeginShiftAllocationLockScopeAsync(shiftId, cancellationToken);

        public async Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default)
        {
            record($"LOCK:TicketDecision:Enter:{ticketId}");
            try
            {
                return await inner.ExecuteTicketDecisionAsync(ticketId, decision, cancellationToken);
            }
            finally
            {
                record($"LOCK:TicketDecision:Exit:{ticketId}");
            }
        }

        public Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate, Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default) =>
            inner.ExecuteMockExamStartAsync(studentId, courseId, quotaDate, start, cancellationToken);

        public Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join, CancellationToken cancellationToken = default) =>
            inner.ExecuteInClassJoinAsync(shiftId, join, cancellationToken);

        public Task<T> ExecuteClassEnrollmentAsync<T>(Guid classId, Func<CancellationToken, Task<T>> enrollment, CancellationToken cancellationToken = default) =>
            inner.ExecuteClassEnrollmentAsync(classId, enrollment, cancellationToken);

        public async Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId, Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default)
        {
            if (FailInClassStudentId.HasValue && FailInClassStudentId.Value == studentId)
            {
                throw new DbUpdateException("Simulated per-student 23505 unique conflict.");
            }

            SemaphoreSlim? gate = InClassLockMap?.GetOrAdd((shiftId, studentId), static _ => new SemaphoreSlim(1, 1));
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            var prevFlag = InClassLockTracker?.Value ?? false;
            if (InClassLockTracker is not null)
            {
                InClassLockTracker.Value = true;
            }

            record($"LOCK:InClassSubmission:Enter:{shiftId}:{studentId}");
            try
            {
                return await inner.ExecuteInClassSubmissionAsync(shiftId, studentId, submit, cancellationToken);
            }
            finally
            {
                record($"LOCK:InClassSubmission:Exit:{shiftId}:{studentId}");
                if (InClassLockTracker is not null)
                {
                    InClassLockTracker.Value = prevFlag;
                }

                gate?.Release();
            }
        }

        public Task<T> ExecuteMockExamGradingAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecuteMockExamGradingAsync(sessionId, action, cancellationToken);

        public Task<T> ExecutePracticeSessionMutationAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecutePracticeSessionMutationAsync(sessionId, action, cancellationToken);

        public Task<T> ExecuteAppealTransitionAsync<T>(Guid appealId, Func<CancellationToken, Task<T>> transition, CancellationToken cancellationToken = default) =>
            inner.ExecuteAppealTransitionAsync(appealId, transition, cancellationToken);

        public Task<T> ExecuteAppealDecisionAsync<T>(Guid appealId, Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default) =>
            inner.ExecuteAppealDecisionAsync(appealId, ticketId, decision, cancellationToken);

        public Task<T> ExecuteAppealAssignmentAsync<T>(IReadOnlyCollection<Guid> appealIds, IReadOnlyCollection<Guid> lecturerIds, Func<CancellationToken, Task<T>> assignment, CancellationToken cancellationToken = default) =>
            inner.ExecuteAppealAssignmentAsync(appealIds, lecturerIds, assignment, cancellationToken);

        public Task<T> ExecuteBulkExamTicketImportAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> import, CancellationToken cancellationToken = default) =>
            inner.ExecuteBulkExamTicketImportAsync(shiftId, import, cancellationToken);

        public Task<T> ExecuteOfficialSemesterBatchAsync<T>(Guid semesterId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecuteOfficialSemesterBatchAsync(semesterId, action, cancellationToken);

        public async Task<T> ExecuteOfficialShiftScheduleBatchAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            record("LOCK:OfficialShiftScheduleBatch:Enter");
            try
            {
                return await inner.ExecuteOfficialShiftScheduleBatchAsync(action, cancellationToken);
            }
            finally
            {
                record("LOCK:OfficialShiftScheduleBatch:Exit");
            }
        }

        public Task<T> ExecuteOfficialSessionCandidatesAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecuteOfficialSessionCandidatesAsync(sessionId, action, cancellationToken);

        public Task<T> ExecuteOfficialSessionAllocationAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> allocation, CancellationToken cancellationToken = default) =>
            inner.ExecuteOfficialSessionAllocationAsync(sessionId, allocation, cancellationToken);

        public Task<T> ExecuteRefreshTokenRotationAsync<T>(Func<CancellationToken, Task<T>> rotate, CancellationToken cancellationToken = default) =>
            inner.ExecuteRefreshTokenRotationAsync(rotate, cancellationToken);

        public Task<IRefreshTokenLockScope> BeginRefreshTokenLockScopeAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            inner.BeginRefreshTokenLockScopeAsync(tokenHash, cancellationToken);
    }
}
