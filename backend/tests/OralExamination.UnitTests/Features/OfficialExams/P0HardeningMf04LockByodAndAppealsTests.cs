using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;
using OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class P0HardeningMf04LockByodAndAppealsTests
{
    private const string LaptopA = "DESKTOP-KIOSK-P0A-LAPTOP-0001";
    private const string LaptopB = "DESKTOP-KIOSK-P0A-LAPTOP-0002";
    private static readonly DateTime InsideShift = new(2026, 11, 24, 8, 30, 0, DateTimeKind.Utc);

    private static OralExamDbContext CreateDbContext(string? dbName = null, bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? $"p0a-{Guid.NewGuid():N}");
        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }

        return new OralExamDbContext(builder.Options);
    }

    private static User CreateUser(string role, string emailPrefix, string? studentCode = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{emailPrefix}.{Guid.NewGuid():N}@fpt.edu.vn",
        FullName = $"User {emailPrefix}",
        StudentCode = studentCode,
        Role = role,
        IsActive = true
    };

    private static async Task<(User Officer, User Examiner, User Proctor, User Student1, User Student2, Course Course, OfficialExamSession Session, RealExamSessionShift Shift, ExamQuestion Q1, ExamQuestion Q2, StudentExamTicket Ticket1, StudentExamTicket Ticket2)>
        SeedStandardShiftAsync(OralExamDbContext context, bool shiftEnded = false)
    {
        var officer = CreateUser(UserRole.ExaminationOfficer, "eo");
        var examiner = CreateUser(UserRole.Lecturer, "examiner");
        var proctor = CreateUser(UserRole.Proctor, "proctor");
        var student1 = CreateUser(UserRole.Student, "sv1", "SE181001");
        var student2 = CreateUser(UserRole.Student, "sv2", "SE181002");

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"FA{Guid.NewGuid():N}"[..8],
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = officer.Id,
            Name = "Struct P0A",
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric P0A",
            TotalMaxScore = 10m
        };
        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Accuracy",
            MaxScore = 10m,
            Weight = 1m,
            OrderIndex = 1
        };
        var q1 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain Clean Architecture",
            SampleAnswer = new string('s', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var q2 = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q2",
            Content = "Explain CQRS",
            SampleAnswer = new string('q', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var set1 = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-P0A-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var set2 = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-P0A-02",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            Title = "FE SWD392 P0A",
            ExamDate = new DateOnly(2026, 11, 24),
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            Status = "in_progress",
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 P0A",
            RoomLab = "Lab 301",
            StartTime = shiftEnded ? DateTime.UtcNow.AddHours(-2) : InsideShift.AddMinutes(-30),
            EndTime = shiftEnded ? DateTime.UtcNow.AddMinutes(-10) : InsideShift.AddMinutes(90),
            ProctorUserId = proctor.Id,
            ExaminerUserId = examiner.Id,
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true,
            MaxSeats = 24
        };
        var ticket1 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student1.Id,
            ExamSetId = set1.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled
        };
        var ticket2 = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student2.Id,
            ExamSetId = set2.Id,
            SeatNumber = 2,
            Status = ExamTicketStatus.Scheduled
        };

        context.Users.AddRange(officer, examiner, proctor, student1, student2);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.Add(criterion);
        context.ExamQuestions.AddRange(q1, q2);
        context.ExamSets.AddRange(set1, set2);
        context.ExamSetQuestions.AddRange(
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set1.Id, ExamQuestionId = q1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set1.Id, ExamQuestionId = q2.Id, OrderIndex = 2 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set2.Id, ExamQuestionId = q1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set2.Id, ExamQuestionId = q2.Id, OrderIndex = 2 });
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.AddRange(ticket1, ticket2);
        await context.SaveChangesAsync();

        return (officer, examiner, proctor, student1, student2, course, session, shift, q1, q2, ticket1, ticket2);
    }

    // =========================================================================
    // P0-01: BYOD Device Guard, Anti-Cheat Bypass After Unlock, & Attendance
    // =========================================================================

    [Fact(DisplayName = "P0-01A: Sau unlock-device (DeviceId=null, DeviceUnlockCount>0), DeviceGuard & LogAntiCheat chặn SV gửi log không kèm hoặc kèm DeviceId trước khi rebind")]
    public async Task P0_01_UnlockedTicket_BlocksStudentAntiCheatLogWithoutDeviceId_AndDeviceGuardRejectsWhenRequireSessionTokenFalse()
    {
        await using var context = CreateDbContext();
        var (officer, _, _, student1, _, _, _, shift, _, _, ticket1, _) = await SeedStandardShiftAsync(context);

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var checkIn = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        checkIn.IsSuccess.Should().BeTrue();

        // EO unlocks device -> DeviceId = null, DeviceSessionTokenHash = null, DeviceUnlockCount = 1
        var unlock = await new UnlockExamTicketDeviceCommandHandler(context).Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Laptop thí sinh sập nguồn cần đổi sang máy dự phòng."),
            CancellationToken.None);
        unlock.IsSuccess.Should().BeTrue();

        var unlockedTicket = await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id);
        unlockedTicket.DeviceId.Should().BeNull();
        unlockedTicket.DeviceUnlockCount.Should().Be(1);

        // 1. OfficialExamDeviceGuard rejects even when requireSessionToken == false (unless caller explicitly opts into allowUnlockedPendingRebind)
        OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            unlockedTicket,
            rawDeviceId: null,
            rawSessionToken: null,
            requireSessionToken: false).Should().Be(OfficialExamErrors.DeviceMismatch);

        OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            unlockedTicket,
            rawDeviceId: LaptopB,
            rawSessionToken: null,
            requireSessionToken: false).Should().Be(OfficialExamErrors.DeviceMismatch);

        // 2. LogAntiCheatEventCommandHandler blocks student omitting X-Device-Id AND sending unbound LaptopB
        var antiCheatHandler = new LogAntiCheatEventCommandHandler(context);
        var noDeviceLog = await antiCheatHandler.Handle(
            new LogAntiCheatEventCommand(
                student1.Id,
                ticket1.Id,
                shift.Id,
                student1.Id,
                AntiCheatEventType.WindowBlur,
                AntiCheatSeverity.Medium,
                "Bypass attempt without X-Device-Id",
                "evt-p0-01-none",
                DeviceId: null,
                KioskSessionToken: null),
            CancellationToken.None);
        noDeviceLog.IsSuccess.Should().BeFalse();
        noDeviceLog.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        var unboundDeviceLog = await antiCheatHandler.Handle(
            new LogAntiCheatEventCommand(
                student1.Id,
                ticket1.Id,
                shift.Id,
                student1.Id,
                AntiCheatEventType.WindowBlur,
                AntiCheatSeverity.Medium,
                "Bypass attempt before rebind",
                "evt-p0-01-unbound",
                DeviceId: LaptopB,
                KioskSessionToken: null),
            CancellationToken.None);
        unboundDeviceLog.IsSuccess.Should().BeFalse();
        unboundDeviceLog.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(0);

        // 3. After rebinding on replacement LaptopB, anti-cheat log with matching device + token succeeds
        var rebind = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.2", InsideShift.AddMinutes(5), LaptopB),
            CancellationToken.None);
        rebind.IsSuccess.Should().BeTrue();

        var reboundLog = await antiCheatHandler.Handle(
            new LogAntiCheatEventCommand(
                student1.Id,
                ticket1.Id,
                shift.Id,
                student1.Id,
                AntiCheatEventType.WindowBlur,
                AntiCheatSeverity.Medium,
                "Valid blur after rebind",
                "evt-p0-01-rebound",
                LaptopB,
                rebind.Value.ActiveSessionToken),
            CancellationToken.None);
        reboundLog.IsSuccess.Should().BeTrue();
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "P0-01B: Proctor UpdateTicketAttendance không thể đánh vắng/hạ ngược trạng thái vé đã gắn hoặc từng mở khóa thiết bị BYOD")]
    public async Task P0_01_ProctorAttendance_CannotRevertBoundOrUnlockedByodTicketToAbsent()
    {
        await using var context = CreateDbContext();
        var (officer, _, proctor, student1, _, _, _, _, _, _, ticket1, _) = await SeedStandardShiftAsync(context);

        var checkIn = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA), CancellationToken.None);
        checkIn.IsSuccess.Should().BeTrue();

        var attendanceHandler = new UpdateTicketAttendanceCommandHandler(context);
        var revertBound = await attendanceHandler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket1.Id, IsPresent: false),
            CancellationToken.None);
        revertBound.IsSuccess.Should().BeFalse();
        revertBound.Error.Should().StartWith("CONFLICT:");

        // Even after EO unlocks device (DeviceId == null, DeviceUnlockCount == 1), Proctor still cannot revert to absent/SCHEDULED
        var unlock = await new UnlockExamTicketDeviceCommandHandler(context).Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Mở khóa máy để đổi sang máy dự phòng."),
            CancellationToken.None);
        unlock.IsSuccess.Should().BeTrue();

        var revertUnlocked = await attendanceHandler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket1.Id, IsPresent: false),
            CancellationToken.None);
        revertUnlocked.IsSuccess.Should().BeFalse();
        revertUnlocked.Error.Should().StartWith("CONFLICT:");

        var savedTicket = await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id);
        savedTicket.Status.Should().Be(ExamTicketStatus.InProgress);
        savedTicket.DeviceUnlockCount.Should().Be(1);
    }

    // =========================================================================
    // P0-02: PublishGrades Deadlock When Tickets Were Manually Locked
    // =========================================================================

    [Fact(DisplayName = "P0-02: PublishGrades không bị deadlock khi 100% vé đã khóa tay (LockExamTicket), nâng cấp LOCKED -> PUBLISHED xuyên OneWayLockInterceptor và sửa chuỗi thông báo Phòng Khảo thí")]
    public async Task P0_02_PublishGrades_WhenAllTicketsManuallyLocked_UpgradesToPublishedUnderOneWayLock_AndAllowsAppeal()
    {
        var dbName = $"p0-02-{Guid.NewGuid():N}";
        Guid officerId;
        Guid examinerId;
        Guid student1Id;
        Guid shiftId;
        Guid ticket1Id;
        Guid ticket2Id;
        Guid sub2Id;

        await using (var seedContext = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var (officer, examiner, _, student1, _, _, _, shift, q1, _, ticket1, ticket2) =
                await SeedStandardShiftAsync(seedContext, shiftEnded: true);

            ticket1.Status = ExamTicketStatus.Audited;
            ticket2.Status = ExamTicketStatus.AiGraded;

            var sub1 = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                ExamQuestionId = q1.Id,
                TranscriptWhisper = "Trả lời câu 1 đầy đủ.",
                AiScore = 7.5m,
                FinalScore = 8.0m,
                GradingStatus = "audited"
            };
            var audit1 = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                LecturerId = examiner.Id,
                OriginalAiScore = 7.5m,
                AuditedScore = 8.0m,
                OverrideReason = "Giám khảo đã nghe lại audio và cộng thêm điểm ví dụ thực tế.",
                IsLocked = false
            };
            var sub2 = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket2.Id,
                ExamQuestionId = q1.Id,
                TranscriptWhisper = "Trả lời câu 1 rõ ràng.",
                AiScore = 7.0m,
                FinalScore = null,
                GradingStatus = "ai_graded"
            };
            seedContext.ExamQuestionSubmissions.AddRange(sub1, sub2);
            seedContext.LecturerAudits.Add(audit1);
            await seedContext.SaveChangesAsync();

            officerId = officer.Id;
            examinerId = examiner.Id;
            student1Id = student1.Id;
            shiftId = shift.Id;
            ticket1Id = ticket1.Id;
            ticket2Id = ticket2.Id;
            sub2Id = sub2.Id;
        }

        await using var lockedContext = CreateDbContext(dbName, withLockInterceptor: true);
        var lockHandler = new LockExamTicketCommandHandler(lockedContext);

        // 1. Examiner manually locks Ticket 1 -> verify message says "từ Phòng Khảo thí" (not "từ Trưởng Bộ Môn")
        var lock1 = await lockHandler.Handle(new LockExamTicketCommand(examinerId, ticket1Id), CancellationToken.None);
        lock1.IsSuccess.Should().BeTrue();
        lock1.Value.Status.Should().Be(ExamTicketStatus.Locked);
        lock1.Value.Message.Should().Contain("từ Phòng Khảo thí");
        lock1.Value.Message.Should().NotContain("từ Trưởng Bộ Môn");

        // 2. Also simulate Ticket 2 manually locked (IsLocked=true, Status=LOCKED, GradesPublishedAt=null, FinalScore still null)
        await using (var directSeed = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var t2 = await directSeed.StudentExamTickets.SingleAsync(t => t.Id == ticket2Id);
            t2.Status = ExamTicketStatus.Locked;
            t2.IsLocked = true;
            t2.GradesPublishedAt = null;
            await directSeed.SaveChangesAsync();
        }

        // 3. PublishGrades on a shift where 100% of tickets have IsLocked=true & Status=LOCKED MUST succeed (no deadlock!)
        await using var publishContext = CreateDbContext(dbName, withLockInterceptor: true);
        var publishHandler = new PublishGradesCommandHandler(publishContext);
        var publish = await publishHandler.Handle(new PublishGradesCommand(shiftId, officerId), CancellationToken.None);

        publish.IsSuccess.Should().BeTrue(because: publish.Error);
        publish.Value.TotalPublished.Should().Be(2);

        var persistedTickets = await publishContext.StudentExamTickets.AsNoTracking()
            .Where(t => t.ShiftId == shiftId)
            .ToListAsync();
        persistedTickets.Should().HaveCount(2);
        persistedTickets.Should().OnlyContain(t =>
            t.Status == ExamTicketStatus.Published
            && t.IsLocked
            && t.GradesPublishedAt == publish.Value.PublishedAt);

        var syncedSub2 = await publishContext.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub2Id);
        syncedSub2.FinalScore.Should().Be(7.0m);

        var persistedShift = await publishContext.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shiftId);
        persistedShift.CertificateHash.Should().NotBeNullOrWhiteSpace().And.HaveLength(64);

        // 4. Re-publishing the same shift MUST now return AlreadyPublished
        var duplicatePublish = await publishHandler.Handle(new PublishGradesCommand(shiftId, officerId), CancellationToken.None);
        duplicatePublish.IsSuccess.Should().BeFalse();
        duplicatePublish.Error.Should().Be(PublishGradesErrors.AlreadyPublished);

        // 5. Student 1 can now file an appeal within the 3-day window anchored on GradesPublishedAt
        var appealResult = await new CreateAppealCommandHandler(publishContext).Handle(
            new CreateAppealCommand(student1Id, ticket1Id, null, "Đề nghị phúc khảo điểm sau khi ca thi đã công bố chính thức."),
            CancellationToken.None);
        appealResult.IsSuccess.Should().BeTrue(because: appealResult.Error);
    }

    // =========================================================================
    // P0-03: OneWayLockInterceptor Async + Added Children + OriginalValue FK
    // =========================================================================

    [Theory(DisplayName = "P0-03A: OneWayLockInterceptor chặn EntityState.Added cho LecturerAudit và toàn bộ bảng con MF-04 vào vé đã khóa (cả Async và Sync)")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task P0_03_OneWayLockInterceptor_BlocksAddedLecturerAuditAndMf04Children_OnLockedTicket(bool useAsync)
    {
        var dbName = $"p0-03-add-{Guid.NewGuid():N}";
        Guid lockedTicketId;
        Guid lockedSubId;
        Guid lockedAuditId;
        Guid lockedEvalId;
        Guid lecturerId;
        Guid criterionId;
        Guid questionId;

        await using (var seed = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var (_, examiner, _, student1, _, _, _, _, q1, _, ticket1, _) = await SeedStandardShiftAsync(seed, shiftEnded: true);
            criterionId = await seed.RubricCriteria.Select(c => c.Id).FirstAsync();
            lecturerId = examiner.Id;
            questionId = q1.Id;
            lockedTicketId = ticket1.Id;

            ticket1.Status = ExamTicketStatus.Published;
            ticket1.GradesPublishedAt = DateTime.UtcNow;
            ticket1.IsLocked = true;

            var sub = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                ExamQuestionId = q1.Id,
                TranscriptWhisper = "Locked submission",
                AiScore = 8.0m,
                FinalScore = 8.0m,
                GradingStatus = "audited"
            };
            var audit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                LecturerId = examiner.Id,
                OriginalAiScore = 8.0m,
                AuditedScore = 8.0m,
                OverrideReason = "Initial locked audit",
                IsLocked = true
            };
            var eval = new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "exam",
                StudentId = student1.Id,
                ExamSubmissionId = sub.Id,
                TotalScore = 8.0m,
                ConfidenceScore = 0.95m
            };

            seed.ExamQuestionSubmissions.Add(sub);
            seed.LecturerAudits.Add(audit);
            seed.AiEvaluations.Add(eval);
            await seed.SaveChangesAsync();

            lockedSubId = sub.Id;
            lockedAuditId = audit.Id;
            lockedEvalId = eval.Id;
        }

        // 1. Added LecturerAudit on locked ticket without approved appeal
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.LecturerAudits.Add(new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = lockedTicketId,
                LecturerId = lecturerId,
                OriginalAiScore = 8.0m,
                AuditedScore = 10.0m,
                OverrideReason = "Forged audit injection",
                IsLocked = false
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 2. Added ExamQuestionSubmission on locked ticket
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = lockedTicketId,
                ExamQuestionId = questionId,
                TranscriptWhisper = "Forged submission injection",
                AiScore = 10.0m,
                FinalScore = 10.0m,
                GradingStatus = "audited"
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 3. Added LecturerAuditDetail on locked ticket's audit without approved appeal
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.LecturerAuditDetails.Add(new LecturerAuditDetail
            {
                Id = Guid.NewGuid(),
                AuditId = lockedAuditId,
                CriterionId = criterionId,
                SubmissionId = lockedSubId,
                AiScore = 8.0m,
                AuditedScore = 10.0m,
                LecturerComment = "Forged detail injection"
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 4. Added AiEvaluation targeting locked submission
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.AiEvaluations.Add(new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "exam",
                ExamSubmissionId = lockedSubId,
                TotalScore = 10.0m,
                ConfidenceScore = 0.99m
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 5. Added AiEvaluationDetail targeting locked evaluation
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.AiEvaluationDetails.Add(new AiEvaluationDetail
            {
                Id = Guid.NewGuid(),
                EvaluationId = lockedEvalId,
                CriterionId = criterionId,
                Score = 10.0m,
                Comment = "Forged AI detail injection"
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
    }

    [Fact(DisplayName = "P0-03B: OneWayLockInterceptor chặn reparent FK (OriginalValue) và detached Update() trỏ sang vé chưa khóa")]
    public async Task P0_03_OneWayLockInterceptor_BlocksForeignKeyReparentingAndDetachedUpdate()
    {
        var dbName = $"p0-03-reparent-{Guid.NewGuid():N}";
        Guid lockedSubId;
        Guid lockedAuditDetailId;
        Guid lockedEvalId;
        Guid lockedEvalDetailId;
        Guid unlockedTicketId;
        Guid unlockedSubId;
        Guid unlockedAuditId;
        Guid unlockedEvalId;
        Guid questionId;

        await using (var seed = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var (_, examiner, _, student1, student2, _, _, _, q1, _, lockedTicket, unlockedTicket) =
                await SeedStandardShiftAsync(seed, shiftEnded: true);
            var criterionId = await seed.RubricCriteria.Select(c => c.Id).FirstAsync();
            questionId = q1.Id;
            unlockedTicketId = unlockedTicket.Id;

            lockedTicket.Status = ExamTicketStatus.Published;
            lockedTicket.GradesPublishedAt = DateTime.UtcNow;
            lockedTicket.IsLocked = true;

            unlockedTicket.Status = ExamTicketStatus.AiGraded;
            unlockedTicket.IsLocked = false;

            var lockedSub = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = lockedTicket.Id,
                ExamQuestionId = q1.Id,
                TranscriptWhisper = "Locked",
                AiScore = 6.0m,
                FinalScore = 6.0m,
                GradingStatus = "audited"
            };
            var unlockedSub = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = unlockedTicket.Id,
                ExamQuestionId = q1.Id,
                TranscriptWhisper = "Unlocked",
                AiScore = 7.0m,
                FinalScore = 7.0m,
                GradingStatus = "ai_graded"
            };
            var lockedAudit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = lockedTicket.Id,
                LecturerId = examiner.Id,
                OriginalAiScore = 6.0m,
                AuditedScore = 6.0m,
                OverrideReason = "Locked audit",
                IsLocked = true
            };
            var unlockedAudit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = unlockedTicket.Id,
                LecturerId = examiner.Id,
                OriginalAiScore = 7.0m,
                AuditedScore = 7.0m,
                OverrideReason = "Unlocked audit",
                IsLocked = false
            };
            var lockedAuditDetail = new LecturerAuditDetail
            {
                Id = Guid.NewGuid(),
                AuditId = lockedAudit.Id,
                CriterionId = criterionId,
                SubmissionId = lockedSub.Id,
                AiScore = 6.0m,
                AuditedScore = 6.0m
            };
            var lockedEval = new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "exam",
                StudentId = student1.Id,
                ExamSubmissionId = lockedSub.Id,
                TotalScore = 6.0m
            };
            var unlockedEval = new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "exam",
                StudentId = student2.Id,
                ExamSubmissionId = unlockedSub.Id,
                TotalScore = 7.0m
            };
            var lockedEvalDetail = new AiEvaluationDetail
            {
                Id = Guid.NewGuid(),
                EvaluationId = lockedEval.Id,
                CriterionId = criterionId,
                Score = 6.0m
            };

            seed.ExamQuestionSubmissions.AddRange(lockedSub, unlockedSub);
            seed.LecturerAudits.AddRange(lockedAudit, unlockedAudit);
            seed.LecturerAuditDetails.Add(lockedAuditDetail);
            seed.AiEvaluations.AddRange(lockedEval, unlockedEval);
            seed.AiEvaluationDetails.Add(lockedEvalDetail);
            await seed.SaveChangesAsync();

            lockedSubId = lockedSub.Id;
            lockedAuditDetailId = lockedAuditDetail.Id;
            lockedEvalId = lockedEval.Id;
            lockedEvalDetailId = lockedEvalDetail.Id;
            unlockedSubId = unlockedSub.Id;
            unlockedAuditId = unlockedAudit.Id;
            unlockedEvalId = unlockedEval.Id;
        }

        // 1. Reparent ExamQuestionSubmission.TicketId -> unlocked ticket
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var sub = await ctx.ExamQuestionSubmissions.SingleAsync(s => s.Id == lockedSubId);
            sub.TicketId = unlockedTicketId;
            sub.FinalScore = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        // 2. Reparent LecturerAuditDetail.AuditId -> unlocked audit
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var detail = await ctx.LecturerAuditDetails.SingleAsync(d => d.Id == lockedAuditDetailId);
            detail.AuditId = unlockedAuditId;
            detail.AuditedScore = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        // 3. Reparent AiEvaluation.ExamSubmissionId -> null (and -> unlocked submission)
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var eval = await ctx.AiEvaluations.SingleAsync(e => e.Id == lockedEvalId);
            eval.ExamSubmissionId = null;
            eval.TotalScore = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var eval = await ctx.AiEvaluations.SingleAsync(e => e.Id == lockedEvalId);
            eval.ExamSubmissionId = unlockedSubId;
            eval.TotalScore = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        // 4. Reparent AiEvaluationDetail.EvaluationId -> unlocked evaluation
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var evalDetail = await ctx.AiEvaluationDetails.SingleAsync(d => d.Id == lockedEvalDetailId);
            evalDetail.EvaluationId = unlockedEvalId;
            evalDetail.Score = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        // 5. Disconnected Update() with forged TicketId pointing to unlocked ticket MUST still be caught via DB lookup
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.ExamQuestionSubmissions.Update(new ExamQuestionSubmission
            {
                Id = lockedSubId,
                TicketId = unlockedTicketId,
                ExamQuestionId = questionId,
                TranscriptWhisper = "Forged detached reparent",
                AiScore = 6.0m,
                FinalScore = 10.0m,
                GradingStatus = "audited"
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }
    }

    [Fact(DisplayName = "P0-03C: OneWayLockInterceptor chỉ cho phép nâng cấp LOCKED -> PUBLISHED khi không sửa trường cấm (SeatNumber/FinalScore giả)")]
    public async Task P0_03_OneWayLockInterceptor_BlocksPiggybackedTamperingDuringPublishUpgrade()
    {
        var dbName = $"p0-03-upgrade-{Guid.NewGuid():N}";
        Guid ticketId;
        Guid subId;

        await using (var seed = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var (_, _, _, _, _, _, _, _, q1, _, ticket1, _) = await SeedStandardShiftAsync(seed, shiftEnded: true);
            ticket1.Status = ExamTicketStatus.Locked;
            ticket1.IsLocked = true;
            ticket1.GradesPublishedAt = null;
            var sub = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket1.Id,
                ExamQuestionId = q1.Id,
                AiScore = 7.5m,
                FinalScore = null,
                GradingStatus = "ai_graded"
            };
            seed.ExamQuestionSubmissions.Add(sub);
            await seed.SaveChangesAsync();
            ticketId = ticket1.Id;
            subId = sub.Id;
        }

        // 1. Piggybacking SeatNumber change on LOCKED -> PUBLISHED upgrade MUST throw OneWayLockException
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var ticket = await ctx.StudentExamTickets.SingleAsync(t => t.Id == ticketId);
            ticket.Status = ExamTicketStatus.Published;
            ticket.GradesPublishedAt = DateTime.UtcNow;
            ticket.SeatNumber = 22;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }

        // 2. Piggybacking FinalScore != AiScore during LOCKED -> PUBLISHED upgrade MUST throw OneWayLockException
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var ticket = await ctx.StudentExamTickets.SingleAsync(t => t.Id == ticketId);
            var sub = await ctx.ExamQuestionSubmissions.SingleAsync(s => s.Id == subId);
            ticket.Status = ExamTicketStatus.Published;
            ticket.GradesPublishedAt = DateTime.UtcNow;
            sub.FinalScore = 9.5m; // Differs from AiScore = 7.5m
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync: true);
        }
    }

    [Theory(DisplayName = "P0-03D: OneWayLockInterceptor chặn detached Update()/Remove() trên gốc khóa (Ticket/Shift/Audit), AiEvaluation MF-05 đã khóa và sửa TicketId của AppealRequest")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task P0_03_OneWayLockInterceptor_BlocksDetachedRootMutations_LockedInClassAiEvaluation_AndAppealForgery(bool useAsync)
    {
        var dbName = $"p0-03-detached-inclass-{useAsync}-{Guid.NewGuid():N}";
        Guid lockedTicketId;
        Guid otherLockedTicketId;
        Guid lockedAuditOnlyId;
        Guid lockedInClassShiftId;
        Guid unlockedInClassSubId;
        Guid lockedInClassSubId;
        Guid lockedInClassEvalId;
        Guid lockedInClassEvalDetailId;
        Guid appealId;
        Guid lecturerId;
        Guid studentId;
        Guid courseId;
        Guid classId;
        Guid rubricCriterionId;

        await using (var seed = CreateDbContext(dbName, withLockInterceptor: false))
        {
            var (course, _, _, lecturer, _, student1, student2, _, q1, _, ticket1, ticket2) =
                await SeedStandardShiftAsync(seed, shiftEnded: true);
            lecturerId = lecturer.Id;
            studentId = student1.Id;
            courseId = course.Id;

            ticket1.Status = ExamTicketStatus.Published;
            ticket1.IsLocked = true;
            ticket1.GradesPublishedAt = DateTime.UtcNow.AddMinutes(-10);
            ticket2.Status = ExamTicketStatus.Published;
            ticket2.IsLocked = true;
            ticket2.GradesPublishedAt = DateTime.UtcNow.AddMinutes(-10);

            var unlockedTicketForLockedAudit = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = ticket1.ShiftId,
                StudentId = student2.Id,
                SeatNumber = 3,
                Status = ExamTicketStatus.Audited,
                IsLocked = false
            };
            var lockedAuditOnly = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = unlockedTicketForLockedAudit.Id,
                LecturerId = lecturer.Id,
                AuditedScore = 7.0m,
                OverrideReason = "Initial audit locked only",
                IsLocked = true
            };
            var lockedAuditOnTicket2 = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = ticket2.Id,
                LecturerId = lecturer.Id,
                AuditedScore = 6.0m,
                OverrideReason = "Initial audit ticket 2",
                IsLocked = true
            };
            var appealForTicket1 = new AppealRequest
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                TicketId = ticket1.Id,
                StudentId = student1.Id,
                Reason = "Xin chấm lại",
                OriginalScore = 6.0m,
                ProposedScore = 8.0m,
                Status = AppealStatus.RegradedByLecturer,
                Decision = AppealStatus.Pending
            };

            var rubric = await seed.Rubrics.Include(r => r.Criteria).FirstAsync();
            rubricCriterionId = rubric.Criteria.First().Id;
            var practiceQuestion = new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = "PQ MF-05",
                Content = "Explain ACID",
                SampleAnswer = new string('A', 60),
                Difficulty = "medium",
                ApprovalStatus = ApprovalStatus.Approved,
                IsActive = true
            };
            var cls = new Class
            {
                Id = Guid.NewGuid(),
                Code = "SE1705",
                Name = "SE1705",
                CourseId = course.Id,
                SemesterId = (await seed.Semesters.FirstAsync()).Id,
                LecturerId = lecturer.Id,
                JoinCode = "JOIN05",
                MaxStudents = 30,
                IsActive = true
            };
            classId = cls.Id;
            var lockedShift = new InClassExamShift
            {
                Id = Guid.NewGuid(),
                ClassId = cls.Id,
                LecturerId = lecturer.Id,
                ShiftName = "Ca thi đã chốt",
                ExamDurationMinutes = 30,
                TotalQuestionsPerStudent = 1,
                TotpSecretKey = "JBSWY3DPEHPK3PXP",
                Status = "FINALIZED",
                IsLocked = true,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1)
            };
            var unlockedShift = new InClassExamShift
            {
                Id = Guid.NewGuid(),
                ClassId = cls.Id,
                LecturerId = lecturer.Id,
                ShiftName = "Ca thi chưa chốt",
                ExamDurationMinutes = 30,
                TotalQuestionsPerStudent = 1,
                TotpSecretKey = "JBSWY3DPEHPK3PXQ",
                Status = "ACTIVE",
                IsLocked = false,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EndTime = DateTime.UtcNow.AddMinutes(20)
            };
            var lockedInClassSub = new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = lockedShift.Id,
                StudentId = student1.Id,
                QuestionId = practiceQuestion.Id,
                Transcript = "ACID explanation",
                AiScore = 7.0m,
                FinalScore = 7.0m,
                Status = "FINALIZED"
            };
            var unlockedInClassSub = new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = unlockedShift.Id,
                StudentId = student2.Id,
                QuestionId = practiceQuestion.Id,
                Transcript = "Unlocked submission",
                AiScore = 5.0m,
                Status = "GRADED"
            };
            var lockedInClassEval = new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "in_class",
                InClassSubmissionId = lockedInClassSub.Id,
                TotalScore = 7.0m,
                Feedback = "Good"
            };
            var lockedInClassEvalDetail = new AiEvaluationDetail
            {
                Id = Guid.NewGuid(),
                EvaluationId = lockedInClassEval.Id,
                CriterionId = rubricCriterionId,
                Score = 7.0m
            };

            seed.StudentExamTickets.Add(unlockedTicketForLockedAudit);
            seed.LecturerAudits.AddRange(lockedAuditOnly, lockedAuditOnTicket2);
            seed.AppealRequests.Add(appealForTicket1);
            seed.PracticeQuestions.Add(practiceQuestion);
            seed.Classes.Add(cls);
            seed.InClassExamShifts.AddRange(lockedShift, unlockedShift);
            seed.InClassExamSubmissions.AddRange(lockedInClassSub, unlockedInClassSub);
            seed.AiEvaluations.Add(lockedInClassEval);
            seed.AiEvaluationDetails.Add(lockedInClassEvalDetail);
            await seed.SaveChangesAsync();

            lockedTicketId = ticket1.Id;
            otherLockedTicketId = ticket2.Id;
            lockedAuditOnlyId = lockedAuditOnly.Id;
            lockedInClassShiftId = lockedShift.Id;
            lockedInClassSubId = lockedInClassSub.Id;
            unlockedInClassSubId = unlockedInClassSub.Id;
            lockedInClassEvalId = lockedInClassEval.Id;
            lockedInClassEvalDetailId = lockedInClassEvalDetail.Id;
            appealId = appealForTicket1.Id;
            _ = q1;
            _ = courseId;
            _ = studentId;
        }

        // 1. Detached Remove & Update(IsLocked=false) on locked StudentExamTicket MUST throw
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.StudentExamTickets.Remove(new StudentExamTicket { Id = lockedTicketId });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.StudentExamTickets.Update(new StudentExamTicket
            {
                Id = lockedTicketId,
                Status = ExamTicketStatus.InProgress,
                IsLocked = false
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 2. Detached Remove & Update(IsLocked=false) on locked InClassExamShift MUST throw
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.InClassExamShifts.Remove(new InClassExamShift { Id = lockedInClassShiftId });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.InClassExamShifts.Update(new InClassExamShift
            {
                Id = lockedInClassShiftId,
                ClassId = classId,
                LecturerId = lecturerId,
                ShiftName = "Forged unlock",
                TotpSecretKey = "JBSWY3DPEHPK3PXP",
                IsLocked = false
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 3. Detached Remove on locked LecturerAudit (even when parent ticket is unlocked) MUST throw
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.LecturerAudits.Remove(new LecturerAudit { Id = lockedAuditOnlyId });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 4. MF-05 AiEvaluation & AiEvaluationDetail Added / Modified / Reparent on locked InClassExamShift MUST throw
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.AiEvaluations.Add(new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "in_class",
                InClassSubmissionId = lockedInClassSubId,
                TotalScore = 10.0m,
                Feedback = "Injected MF-05 AI evaluation"
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var eval = await ctx.AiEvaluations.SingleAsync(e => e.Id == lockedInClassEvalId);
            eval.InClassSubmissionId = unlockedInClassSubId;
            eval.TotalScore = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var detail = await ctx.AiEvaluationDetails.SingleAsync(d => d.Id == lockedInClassEvalDetailId);
            detail.Score = 10.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            ctx.AiEvaluationDetails.Add(new AiEvaluationDetail
            {
                Id = Guid.NewGuid(),
                EvaluationId = lockedInClassEvalId,
                CriterionId = rubricCriterionId,
                Score = 10.0m
            });
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }

        // 5. Forging AppealRequest.TicketId -> otherLockedTicketId during approval MUST NOT unlock other ticket's audit
        await using (var ctx = CreateDbContext(dbName, withLockInterceptor: true))
        {
            var appeal = await ctx.AppealRequests.SingleAsync(a => a.Id == appealId);
            var otherAudit = await ctx.LecturerAudits.SingleAsync(a => a.TicketId == otherLockedTicketId);
            appeal.TicketId = otherLockedTicketId;
            appeal.Decision = AppealStatus.Approved;
            appeal.Status = AppealStatus.Approved;
            otherAudit.AuditedScore = 8.0m;
            await AssertSaveThrowsOneWayLockAsync(ctx, useAsync);
        }
    }

    // =========================================================================
    // P0-04: Proctor Privilege Escalation Guard & Sample ExamSet Exclusion
    // =========================================================================

    [Fact(DisplayName = "P0-04A: Gỡ proctor khỏi [Authorize] của CheckInTicket và CheckInExamTicketCommandHandler chặn proctor bằng Forbidden")]
    public async Task P0_04_ProctorCannotCallCheckInTicket_OnControllerOrHandler()
    {
        var checkInMethod = typeof(OfficialExamsController).GetMethod(nameof(OfficialExamsController.CheckInTicket));
        checkInMethod.Should().NotBeNull();
        var authAttr = checkInMethod!.GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        authAttr.Should().NotBeNull();
        var allowedRoles = authAttr!.Roles!.Split(',', StringSplitOptions.TrimEntries);
        allowedRoles.Should().NotContain(UserRole.Proctor);
        allowedRoles.Should().Contain(UserRole.Student);

        var legacyControllerAuth = typeof(OfficialExamCheckInController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        legacyControllerAuth.Should().NotBeNull();
        legacyControllerAuth!.Roles!.Split(',', StringSplitOptions.TrimEntries).Should().NotContain(UserRole.Proctor);

        await using var context = CreateDbContext();
        var (_, _, proctor, _, _, _, _, _, _, _, ticket1, _) = await SeedStandardShiftAsync(context);

        var handler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var result = await handler.Handle(
            new CheckInExamTicketCommand(proctor.Id, ticket1.Id, "10.0.0.9", InsideShift, LaptopA),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
        (await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id))
            .Status.Should().Be(ExamTicketStatus.Scheduled);
    }

    [Fact(DisplayName = "P0-04B: Proctor ghi log WINDOW_BLUR > 3 lần tuyệt đối KHÔNG tự động khóa vé LOCKED (chỉ Kiosk student mới kích hoạt auto-lock)")]
    public async Task P0_04_ProctorLoggingWindowBlurOverThreshold_NeverAutoLocksTicket()
    {
        await using var context = CreateDbContext();
        var (_, _, proctor, student1, _, _, _, shift, _, _, ticket1, _) = await SeedStandardShiftAsync(context);

        var checkIn = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA), CancellationToken.None);
        checkIn.IsSuccess.Should().BeTrue();
        var sessionToken = checkIn.Value.ActiveSessionToken!;

        var handler = new LogAntiCheatEventCommandHandler(context);
        for (var i = 1; i <= 4; i++)
        {
            var proctorLog = await handler.Handle(
                new LogAntiCheatEventCommand(
                    proctor.Id,
                    ticket1.Id,
                    shift.Id,
                    student1.Id,
                    AntiCheatEventType.WindowBlur,
                    AntiCheatSeverity.High,
                    $"Proctor recorded blur #{i}",
                    $"proctor-blur-{i}"),
                CancellationToken.None);

            proctorLog.IsSuccess.Should().BeTrue();
            proctorLog.Value.ViolationCount.Should().Be(i);
            proctorLog.Value.IsLocked.Should().BeFalse("Proctor must NEVER auto-lock a student's exam ticket");
        }

        var ticketAfterProctor = await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id);
        ticketAfterProctor.Status.Should().Be(ExamTicketStatus.InProgress);
        (await context.AuditLogs.AnyAsync(a => a.Action == "TICKET_AUTO_LOCKED_BLUR_VIOLATION")).Should().BeFalse();

        // Student Kiosk reporting WINDOW_BLUR (> 3) still triggers server-authoritative auto-lock
        var studentBlur = await handler.Handle(
            new LogAntiCheatEventCommand(
                student1.Id,
                ticket1.Id,
                shift.Id,
                student1.Id,
                AntiCheatEventType.WindowBlur,
                AntiCheatSeverity.High,
                "Student kiosk blur #5",
                "student-blur-5",
                LaptopA,
                sessionToken),
            CancellationToken.None);

        studentBlur.IsSuccess.Should().BeTrue();
        studentBlur.Value.IsLocked.Should().BeTrue();
        (await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id))
            .Status.Should().Be(ExamTicketStatus.Locked);
    }

    [Fact(DisplayName = "P0-04C: DistributeShiftTickets bỏ qua bộ đề mẫu (IsSampleSet = true) dù trạng thái HANDED_OVER")]
    public async Task P0_04_DistributeShiftTickets_ExcludesHandedOverSampleSets()
    {
        await using var context = CreateDbContext();
        var (officer, _, _, _, _, course, session, shift, _, _, ticket1, ticket2) = await SeedStandardShiftAsync(context);

        // Clear existing sets & ticket ExamSetIds; seed 1 sample HANDED_OVER set + 1 real HANDED_OVER set for 2 tickets
        ticket1.ExamSetId = null;
        ticket2.ExamSetId = null;
        var existingSets = await context.ExamSets.ToListAsync();
        context.ExamSets.RemoveRange(existingSets);

        var sampleSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "SAMPLE-HANDED-01",
            Status = ExamSetStatus.HandedOver,
            IsSampleSet = true,
            IsActive = true,
            HandedOverAt = DateTime.UtcNow.AddDays(-2)
        };
        var realSet1 = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "REAL-HANDED-01",
            Status = ExamSetStatus.HandedOver,
            IsSampleSet = false,
            IsActive = true,
            HandedOverAt = DateTime.UtcNow.AddDays(-1)
        };
        context.ExamSets.AddRange(sampleSet, realSet1);
        await context.SaveChangesAsync();

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var insufficient = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, UserRole.ExaminationOfficer, shift.Id, AllowRandomShuffle: false),
            CancellationToken.None);

        insufficient.IsSuccess.Should().BeFalse();
        insufficient.Error.Should().StartWith("UNPROCESSABLE:");
        insufficient.Error.Should().Contain("chỉ có 1 bộ đề khả dụng");

        // Adding a second real HANDED_OVER set succeeds and never assigns sampleSet
        var realSet2 = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "REAL-HANDED-02",
            Status = ExamSetStatus.HandedOver,
            IsSampleSet = false,
            IsActive = true,
            HandedOverAt = DateTime.UtcNow
        };
        context.ExamSets.Add(realSet2);
        await context.SaveChangesAsync();

        var ok = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, UserRole.ExaminationOfficer, shift.Id, AllowRandomShuffle: false),
            CancellationToken.None);

        ok.IsSuccess.Should().BeTrue(because: ok.Error);
        ok.Value.TotalDistributed.Should().Be(2);
        ok.Value.DistributedTickets.Select(d => d.ExamSetId).Should().NotContain(sampleSet.Id);
        ok.Value.DistributedTickets.Select(d => d.ExamSetId).Should().BeEquivalentTo([realSet1.Id, realSet2.Id]);
    }

    // =========================================================================
    // P0-12: Batch Master Audio Grading in OfficialExamGradingWorker
    // =========================================================================

    [Fact(DisplayName = "P0-12A: OfficialExamGradingWorker ở chế độ Batch Master Audio không ghi đè transcript từng câu, không báo động giả Levenshtein và cache SHA-256")]
    public async Task P0_12_BatchMasterAudio_PreservesPerQuestionTranscripts_AvoidsFalseSuspicious_AndCachesSha256()
    {
        var masterAudioKey = "official-exams/shift1/ticket1/01_SE181001.webm";
        var validHash = new string('a', 64);

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(masterAudioKey, validHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storageMock.Setup(s => s.OpenReadStreamAsync(masterAudioKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("master-audio-bytes")));

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(
                Text: "Toàn bộ câu trả lời dài cho câu 1 Clean Architecture và câu 2 CQRS kết hợp rất dài để gây lệch Levenshtein nếu bị so nhầm.",
                RawText: "Master raw",
                Provider: "Whisper",
                UsedFallback: false,
                TimestampsJson: null,
                ConfidenceScore: 0.95));

        var capturedTranscripts = new List<string>();
        var gradingMock = new Mock<IAiGradingService>();
        gradingMock.Setup(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiGradingRequest, CancellationToken>((req, _) => capturedTranscripts.Add(req.Transcript))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.5m,
                ConfidenceScore = 0.94m,
                Feedback = "Trình bày chính xác.",
                CotTrace = "Verified criteria"
            });

        // 1. VoiceWithTranscriptEdit with 2 questions sharing master audio
        var (provider1, task1, task2, sub1Id, sub2Id) = CreateBatchMasterAudioHarness(
            ExamInputMode.VoiceWithTranscriptEdit,
            masterAudioKey,
            validHash,
            q1Initial: "Clean Architecture tách biệt tầng Domain khỏi tầng Infrastructure.",
            q1Transcript: "Clean Architecture tách biệt tầng Domain khỏi tầng Infrastructure và API.",
            q2Initial: "CQRS tách biệt luồng Command ghi và Query đọc.",
            q2Transcript: "CQRS tách biệt luồng Command ghi và Query đọc dữ liệu.",
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider1)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider1.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(task1, CancellationToken.None);
            await worker.ProcessAsync(task2, CancellationToken.None);

            storageMock.Verify(s => s.VerifySha256Async(masterAudioKey, validHash, It.IsAny<CancellationToken>()), Times.Once,
                "SHA-256 verification on shared master audio must be cached across sibling submissions");
            sttMock.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never,
                "Whisper STT must not run on master audio when per-question transcripts are already present");

            using var verifyScope = provider1.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved1 = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub1Id);
            var saved2 = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub2Id);

            saved1.IsSuspicious.Should().BeFalse("must not raise false-positive Levenshtein tamper against full master audio");
            saved2.IsSuspicious.Should().BeFalse();
            saved1.TranscriptWhisper.Should().Be("Clean Architecture tách biệt tầng Domain khỏi tầng Infrastructure và API.");
            saved2.TranscriptWhisper.Should().Be("CQRS tách biệt luồng Command ghi và Query đọc dữ liệu.");
            capturedTranscripts.Should().BeEquivalentTo(
            [
                "Clean Architecture tách biệt tầng Domain khỏi tầng Infrastructure và API.",
                "CQRS tách biệt luồng Command ghi và Query đọc dữ liệu."
            ]);
        }

        // 2. VoiceOnly with existing per-question transcripts MUST NOT overwrite them with master transcript
        storageMock.Invocations.Clear();
        sttMock.Invocations.Clear();
        capturedTranscripts.Clear();

        var (provider2, voTask1, voTask2, voSub1Id, voSub2Id) = CreateBatchMasterAudioHarness(
            ExamInputMode.VoiceOnly,
            masterAudioKey,
            validHash,
            q1Initial: "Đáp án riêng của câu 1 ở chế độ VoiceOnly.",
            q1Transcript: "Đáp án riêng của câu 1 ở chế độ VoiceOnly.",
            q2Initial: "Đáp án riêng của câu 2 ở chế độ VoiceOnly.",
            q2Transcript: "Đáp án riêng của câu 2 ở chế độ VoiceOnly.",
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider2)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider2.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(voTask1, CancellationToken.None);
            await worker.ProcessAsync(voTask2, CancellationToken.None);

            sttMock.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

            using var verifyScope = provider2.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            (await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == voSub1Id))
                .TranscriptWhisper.Should().Be("Đáp án riêng của câu 1 ở chế độ VoiceOnly.");
            (await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == voSub2Id))
                .TranscriptWhisper.Should().Be("Đáp án riêng của câu 2 ở chế độ VoiceOnly.");
        }
    }

    [Fact(DisplayName = "P0-12B: OfficialExamGradingWorker ở chế độ Batch Master Audio vẫn bắt gian lận sửa quá 35% nội bộ câu đơn lẻ và chỉ gọi Whisper 1 lần khi transcript rỗng")]
    public async Task P0_12_BatchMasterAudio_DetectsSingleQuestionEditTamper_AndCachesWhisperFallbackWhenEmpty()
    {
        var masterAudioKey = "official-exams/shift2/ticket2/02_SE181002.webm";
        var validHash = new string('b', 64);

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(masterAudioKey, validHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storageMock.Setup(s => s.OpenReadStreamAsync(masterAudioKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("master-audio-empty-fallback")));

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(
                Text: "Nội dung bóc băng fallback từ master audio.",
                RawText: "Nội dung bóc băng fallback từ master audio.",
                Provider: "Whisper",
                UsedFallback: false,
                TimestampsJson: "[{\"word\":\"Nội\",\"start\":0.1}]",
                ConfidenceScore: 0.91));

        var gradingMock = new Mock<IAiGradingService>();
        gradingMock.Setup(g => g.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult { Score = 7.5m, ConfidenceScore = 0.92m, Feedback = "OK", CotTrace = "CoT" });

        // 1. Single-question tamper (> 35% edit between InitialVoiceTranscript and TranscriptWhisper of Q1)
        var (provider1, tamperTask1, _, tamperSub1Id, _) = CreateBatchMasterAudioHarness(
            ExamInputMode.VoiceWithTranscriptEdit,
            masterAudioKey,
            validHash,
            q1Initial: "Em không nhớ rõ câu này ạ.",
            q1Transcript: "Single Responsibility, Open Closed, Liskov Substitution, Interface Segregation and Dependency Inversion.",
            q2Initial: "CQRS",
            q2Transcript: "CQRS",
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider1)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider1.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(tamperTask1, CancellationToken.None);

            using var verifyScope = provider1.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var saved = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == tamperSub1Id);
            saved.IsSuspicious.Should().BeTrue();
            saved.TranscriptEditRatio.Should().BeGreaterThan(OfficialExamRules.MaxTranscriptEditRatio);
        }

        // 2. Both Q1 & Q2 have empty transcripts -> Whisper STT called ONCE on master audio and cached for Q2
        storageMock.Invocations.Clear();
        sttMock.Invocations.Clear();

        var (provider2, emptyTask1, emptyTask2, emptySub1Id, emptySub2Id) = CreateBatchMasterAudioHarness(
            ExamInputMode.VoiceOnly,
            masterAudioKey,
            validHash,
            q1Initial: null,
            q1Transcript: "",
            q2Initial: null,
            q2Transcript: "",
            storageMock.Object,
            sttMock.Object,
            gradingMock.Object);

        using (provider2)
        {
            var worker = new OfficialExamGradingWorker(
                Mock.Of<IOfficialExamGradingQueue>(),
                provider2.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<OfficialExamGradingWorker>.Instance);

            await worker.ProcessAsync(emptyTask1, CancellationToken.None);
            await worker.ProcessAsync(emptyTask2, CancellationToken.None);

            sttMock.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once,
                "Master audio fallback Whisper transcription must be cached across sibling empty submissions");

            using var verifyScope = provider2.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            (await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == emptySub1Id))
                .TranscriptWhisper.Should().Be("Nội dung bóc băng fallback từ master audio.");
            (await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == emptySub2Id))
                .TranscriptWhisper.Should().Be("Nội dung bóc băng fallback từ master audio.");
        }
    }

    // =========================================================================
    // P0-13: Appeal OriginalScore With Follow-ups & Shift Lock Serialization
    // =========================================================================

    [Fact(DisplayName = "P0-13A: CreateAppeal toàn vé có Follow-up tính OriginalScore bằng OfficialExamScoreAggregator (8.50đ thay vì 7.60đ)")]
    public async Task P0_13_CreateAppeal_WholeTicketWithFollowUps_UsesOfficialExamScoreAggregator()
    {
        await using var context = CreateDbContext();
        var (_, _, _, student1, _, _, _, _, q1, q2, ticket1, _) = await SeedStandardShiftAsync(context, shiftEnded: true);

        ticket1.Status = ExamTicketStatus.Published;
        ticket1.GradesPublishedAt = DateTime.UtcNow.AddHours(-2);
        ticket1.IsLocked = true;

        // Q1 main=6.0, FU1=8.0, FU2=9.0 -> effective Q1 = 9.0
        // Q2 main=7.0, FU1=8.0 -> effective Q2 = 8.0
        // Aggregator score = (9.0 + 8.0) / 2 = 8.50 (naive Sum/Count would be 38/5 = 7.60!)
        var q1Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket1.Id, ExamQuestionId = q1.Id, IsFollowUp = false, FollowUpIndex = 0, AiScore = 6.0m, FinalScore = 6.0m, GradingStatus = "ai_graded" };
        var q1Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket1.Id, ExamQuestionId = q1.Id, IsFollowUp = true, ParentSubmissionId = q1Main.Id, FollowUpIndex = 1, FollowUpPrompt = "FU1?", AiScore = 8.0m, FinalScore = 8.0m, GradingStatus = "ai_graded" };
        var q1Fu2 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket1.Id, ExamQuestionId = q1.Id, IsFollowUp = true, ParentSubmissionId = q1Fu1.Id, FollowUpIndex = 2, FollowUpPrompt = "FU2?", AiScore = 9.0m, FinalScore = 9.0m, GradingStatus = "ai_graded" };
        var q2Main = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket1.Id, ExamQuestionId = q2.Id, IsFollowUp = false, FollowUpIndex = 0, AiScore = 7.0m, FinalScore = 7.0m, GradingStatus = "ai_graded" };
        var q2Fu1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket1.Id, ExamQuestionId = q2.Id, IsFollowUp = true, ParentSubmissionId = q2Main.Id, FollowUpIndex = 1, FollowUpPrompt = "Q2 FU1?", AiScore = 8.0m, FinalScore = 8.0m, GradingStatus = "ai_graded" };

        context.ExamQuestionSubmissions.AddRange(q1Main, q1Fu1, q1Fu2, q2Main, q2Fu1);
        await context.SaveChangesAsync();

        var result = await new CreateAppealCommandHandler(context).Handle(
            new CreateAppealCommand(student1.Id, ticket1.Id, null, "Đề nghị phúc khảo tổng điểm toàn bài thi."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value.OriginalScore.Should().Be(8.50m);
    }

    [Fact(DisplayName = "P0-13B: OverrideExamScore, LockExamTicket, SuspendExamTicket, PublishGrades và ReviewAppealDecision đều khóa đồng thời qua ExecuteShiftMutationAsync(shiftId, isOfficial: true)")]
    public async Task P0_13_AllFiveGradeLifecycleHandlers_SerializeUnderOfficialShiftLock()
    {
        var dbName = $"p0-13-locks-{Guid.NewGuid():N}";
        await using var inner = CreateDbContext(dbName, withLockInterceptor: true);
        var spy = new ShiftLockSpyDbContext(inner);

        var (officer, examiner, _, student1, student2, _, session, shift, q1, _, ticket1, ticket2) =
            await SeedStandardShiftAsync(inner, shiftEnded: true);
        var appealLecturer = CreateUser(UserRole.Lecturer, "regrader");
        inner.Users.Add(appealLecturer);

        ticket1.Status = ExamTicketStatus.AiGraded;
        ticket2.Status = ExamTicketStatus.AiGraded;

        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket1.Id,
            ExamQuestionId = q1.Id,
            TranscriptWhisper = "SV1 answer",
            AiScore = 6.5m,
            GradingStatus = "ai_graded"
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket2.Id,
            ExamQuestionId = q1.Id,
            TranscriptWhisper = "SV2 answer",
            AiScore = 7.0m,
            GradingStatus = "ai_graded"
        };
        inner.ExamQuestionSubmissions.AddRange(sub1, sub2);
        await inner.SaveChangesAsync();

        // 1. OverrideExamScoreCommandHandler
        var overrideRes = await new OverrideExamScoreCommandHandler(spy).Handle(
            new OverrideExamScoreCommand(examiner.Id, ticket1.Id, 7.5m, "Hậu kiểm và điều chỉnh điểm hợp lệ theo rubric."),
            CancellationToken.None);
        overrideRes.IsSuccess.Should().BeTrue(because: overrideRes.Error);

        // 2. SuspendExamTicketCommandHandler (on ticket2 before publish)
        var suspendRes = await new SuspendExamTicketCommandHandler(spy).Handle(
            new SuspendExamTicketCommand(officer.Id, ticket2.Id, "Đình chỉ thi do vi phạm quy chế phòng thi Lab."),
            CancellationToken.None);
        suspendRes.IsSuccess.Should().BeTrue(because: suspendRes.Error);

        // 3. LockExamTicketCommandHandler (on ticket1)
        var lockRes = await new LockExamTicketCommandHandler(spy).Handle(
            new LockExamTicketCommand(examiner.Id, ticket1.Id),
            CancellationToken.None);
        lockRes.IsSuccess.Should().BeTrue(because: lockRes.Error);

        // 4. PublishGradesCommandHandler (publishes both manually locked ticket1 and suspended ticket2)
        var publishRes = await new PublishGradesCommandHandler(spy).Handle(
            new PublishGradesCommand(shift.Id, officer.Id),
            CancellationToken.None);
        publishRes.IsSuccess.Should().BeTrue(because: publishRes.Error);

        var certHashAfterPublish = (await inner.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).CertificateHash;
        certHashAfterPublish.Should().HaveLength(64);

        // 5. ReviewAppealDecisionCommandHandler (approves appeal on ticket1 -> recomputes CertificateHash under shift lock)
        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket1.Id,
            StudentId = student1.Id,
            SessionId = session.Id,
            SubmissionId = sub1.Id,
            Reason = "Xin phúc khảo câu 1",
            Status = AppealStatus.RegradedByLecturer,
            AssignedTo = appealLecturer.Id,
            OriginalScore = 7.5m,
            ProposedScore = 9.0m
        };
        inner.AppealRequests.Add(appeal);
        await inner.SaveChangesAsync();

        var reviewRes = await new ReviewAppealDecisionCommandHandler(spy).Handle(
            new ReviewAppealDecisionCommand(appeal.Id, officer.Id, AppealStatus.Approved, 9.0m, "Phê duyệt điểm chấm lại độc lập."),
            CancellationToken.None);
        reviewRes.IsSuccess.Should().BeTrue(because: reviewRes.Error);

        var certHashAfterAppeal = (await inner.RealExamSessionShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id)).CertificateHash;
        certHashAfterAppeal.Should().HaveLength(64);
        certHashAfterAppeal.Should().NotBe(certHashAfterPublish);

        // Verify ALL 5 handlers acquired ExecuteShiftMutationAsync(shift.Id, isOfficial: true)
        spy.ShiftLockCalls.Should().HaveCount(5);
        spy.ShiftLockCalls.Should().OnlyContain(call => call.ShiftId == shift.Id && call.IsOfficial);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static async Task AssertSaveThrowsOneWayLockAsync(OralExamDbContext context, bool useAsync)
    {
        if (useAsync)
        {
            var act = () => context.SaveChangesAsync(CancellationToken.None);
            await act.Should().ThrowAsync<OneWayLockException>();
        }
        else
        {
            var act = () => context.SaveChanges();
            act.Should().Throw<OneWayLockException>();
        }
    }

    private static (ServiceProvider Provider, OfficialExamGradingTask Task1, OfficialExamGradingTask Task2, Guid Sub1Id, Guid Sub2Id)
        CreateBatchMasterAudioHarness(
            string examInputMode,
            string masterAudioKey,
            string masterAudioHash,
            string? q1Initial,
            string q1Transcript,
            string? q2Initial,
            string q2Transcript,
            IStorageService storage,
            ISpeechToTextService stt,
            IAiGradingService grading)
    {
        var dbName = $"p0-12-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton(grading);
        services.AddSingleton(storage);
        services.AddSingleton(stt);

        var provider = services.BuildServiceProvider();
        using var seedScope = provider.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();

        var student = CreateUser(UserRole.Student, "batch-sv", "SE181099");
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", Credits = 3, IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Batch Master Audio Exam",
            ExamDate = new DateOnly(2026, 11, 24),
            ExamInputMode = examInputMode,
            HasFollowUp = false,
            MaxFollowUpQuestions = 2
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca Batch",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = ExamShiftWindow.InProgress
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Depth", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain Clean Architecture", SampleAnswer = new string('a', 60), ApprovalStatus = "APPROVED", IsActive = true };
        var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "Explain CQRS", SampleAnswer = new string('b', 60), ApprovalStatus = "APPROVED", IsActive = true };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Submitted,
            AudioObjectKey = masterAudioKey,
            AudioR2Url = masterAudioKey,
            AudioHashSha256 = masterAudioHash,
            AudioVerifiedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q1.Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            AudioObjectKey = masterAudioKey,
            AudioR2Url = masterAudioKey,
            AudioHashSha256 = masterAudioHash,
            InitialVoiceTranscript = q1Initial,
            TranscriptWhisper = q1Transcript,
            GradingStatus = "pending"
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q2.Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            AudioObjectKey = masterAudioKey,
            AudioR2Url = masterAudioKey,
            AudioHashSha256 = masterAudioHash,
            InitialVoiceTranscript = q2Initial,
            TranscriptWhisper = q2Transcript,
            GradingStatus = "pending"
        };
        var job1 = new GradingJob { Id = Guid.NewGuid(), SubmissionId = sub1.Id, Status = "pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var job2 = new GradingJob { Id = Guid.NewGuid(), SubmissionId = sub2.Id, Status = "pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        db.Users.Add(student);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(criterion);
        db.ExamQuestions.AddRange(q1, q2);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.AddRange(sub1, sub2);
        db.GradingJobs.AddRange(job1, job2);
        db.SaveChanges();

        return (provider, new OfficialExamGradingTask(job1.Id, sub1.Id), new OfficialExamGradingTask(job2.Id, sub2.Id), sub1.Id, sub2.Id);
    }

    private sealed class ShiftLockSpyDbContext(OralExamDbContext inner) : IApplicationDbContext
    {
        public List<(Guid ShiftId, bool IsOfficial)> ShiftLockCalls { get; } = [];

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

        public Task<T> ExecuteShiftMutationAsync<T>(Guid shiftId, bool isOfficial, Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken = default)
        {
            ShiftLockCalls.Add((shiftId, isOfficial));
            return inner.ExecuteShiftMutationAsync(shiftId, isOfficial, mutation, cancellationToken);
        }

        public Task<IAsyncDisposable?> BeginShiftAllocationLockScopeAsync(Guid shiftId, CancellationToken cancellationToken = default) =>
            inner.BeginShiftAllocationLockScopeAsync(shiftId, cancellationToken);

        public Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default) =>
            inner.ExecuteTicketDecisionAsync(ticketId, decision, cancellationToken);

        public Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate, Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default) =>
            inner.ExecuteMockExamStartAsync(studentId, courseId, quotaDate, start, cancellationToken);

        public Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join, CancellationToken cancellationToken = default) =>
            inner.ExecuteInClassJoinAsync(shiftId, join, cancellationToken);

        public Task<T> ExecuteClassEnrollmentAsync<T>(Guid classId, Func<CancellationToken, Task<T>> enrollment, CancellationToken cancellationToken = default) =>
            inner.ExecuteClassEnrollmentAsync(classId, enrollment, cancellationToken);

        public Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId, Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default) =>
            inner.ExecuteInClassSubmissionAsync(shiftId, studentId, submit, cancellationToken);

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

        public Task<T> ExecuteOfficialShiftScheduleBatchAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default) =>
            inner.ExecuteOfficialShiftScheduleBatchAsync(action, cancellationToken);

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
