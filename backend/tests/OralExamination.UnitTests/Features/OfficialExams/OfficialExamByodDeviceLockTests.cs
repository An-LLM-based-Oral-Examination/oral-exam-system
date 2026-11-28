using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamByodDeviceLockTests
{
    private static readonly DateTime InsideShift = new(2026, 11, 20, 8, 30, 0, DateTimeKind.Utc);
    private const string LaptopA = "DESKTOP-KIOSK-LAPTOP-A-0001";
    private const string LaptopB = "DESKTOP-KIOSK-LAPTOP-B-0002";

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Officer, User Proctor, User Student1, User Student2, RealExamSessionShift Shift, StudentExamTicket Ticket1, StudentExamTicket Ticket2)> SeedByodShiftAsync(
        OralExamDbContext context)
    {
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.lock@fpt.edu.vn", FullName = "Khảo thí", Role = UserRole.ExaminationOfficer, IsActive = true };
        var proctor = new User { Id = Guid.NewGuid(), Email = "proctor.lock@fpt.edu.vn", FullName = "Giám thị", Role = UserRole.Proctor, IsActive = true };
        var student1 = new User { Id = Guid.NewGuid(), Email = "sv1.lock@fpt.edu.vn", StudentCode = "SE171001", FullName = "Nguyễn An", Role = UserRole.Student, IsActive = true };
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2.lock@fpt.edu.vn", StudentCode = "SE171002", FullName = "Trần Bình", Role = UserRole.Student, IsActive = true };
        context.Users.AddRange(officer, proctor, student1, student2);

        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", Credits = 3, SemesterId = semester.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = officer.Id, Name = "Struct", IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            Title = "FE SWD392",
            ExamDate = new DateOnly(2026, 11, 20),
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 301",
            StartTime = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
            ProctorUserId = proctor.Id,
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true,
            MaxSeats = 24
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R1", TotalMaxScore = 10m };
        var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Explain Clean Architecture", ApprovalStatus = "APPROVED", IsActive = true };
        var set1 = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "SET-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var set2 = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "SET-02", Status = ExamSetStatus.HandedOver, IsActive = true };
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.Rubrics.Add(rubric);
        context.ExamQuestions.Add(q1);
        context.ExamSets.AddRange(set1, set2);
        context.ExamSetQuestions.AddRange(
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set1.Id, ExamQuestionId = q1.Id, OrderIndex = 1 },
            new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = set2.Id, ExamQuestionId = q1.Id, OrderIndex = 1 });

        var ticket1 = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student1.Id, ExamSetId = set1.Id, SeatNumber = 1, Status = ExamTicketStatus.Scheduled };
        var ticket2 = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student2.Id, ExamSetId = set2.Id, SeatNumber = 2, Status = ExamTicketStatus.Scheduled };
        context.StudentExamTickets.AddRange(ticket1, ticket2);

        await context.SaveChangesAsync();
        return (officer, proctor, student1, student2, shift, ticket1, ticket2);
    }

    [Fact]
    public async Task CheckIn_BindsDeviceId_Hashes24ByteSessionToken_AllowsIpRoamAuditOnly_AndReturnsQuestions()
    {
        await using var context = CreateDbContext();
        var (_, _, student1, _, _, ticket1, _) = await SeedByodShiftAsync(context);

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var checkIn = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.10.1.5", InsideShift, LaptopA, "1.0.0"),
            CancellationToken.None);

        checkIn.IsSuccess.Should().BeTrue();
        checkIn.Value.Status.Should().Be(ExamTicketStatus.InProgress);
        checkIn.Value.DeviceId.Should().Be(LaptopA);
        checkIn.Value.DeviceLockedAt.Should().NotBeNull();
        checkIn.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
        checkIn.Value.ActiveSessionToken!.Length.Should().BeGreaterOrEqualTo(32);

        var persisted = await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id);
        persisted.DeviceId.Should().Be(LaptopA);
        persisted.DeviceSessionTokenHash.Should().NotBeNullOrWhiteSpace();
        persisted.DeviceSessionTokenHash.Should().HaveLength(64);
        persisted.DeviceSessionTokenHash.Should().NotBe(checkIn.Value.ActiveSessionToken, "plaintext token must NEVER be stored in DB");
        OfficialExamDeviceGuard.VerifySessionToken(checkIn.Value.ActiveSessionToken, persisted.DeviceSessionTokenHash).Should().BeTrue();

        // Wi-Fi/NAT IP changes (10.10.1.5 -> 172.16.88.99) MUST succeed when DeviceId + SessionToken match
        var workstationHandler = new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator());
        var workstation = await workstationHandler.Handle(
            new GetMyWorkstationTicketQuery(student1.Id, "172.16.88.99", InsideShift, LaptopA, checkIn.Value.ActiveSessionToken),
            CancellationToken.None);

        workstation.IsSuccess.Should().BeTrue();
        workstation.Value.DeviceId.Should().Be(LaptopA);
        workstation.Value.Questions.Should().NotBeNull().And.HaveCount(1);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("invalid device id with spaces")]
    [InlineData("invalid/device\\chars@123456")]
    public async Task CheckIn_InvalidDeviceId_Returns400DeviceIdInvalid(string invalidDeviceId)
    {
        await using var context = CreateDbContext();
        var (_, _, student1, _, _, ticket1, _) = await SeedByodShiftAsync(context);

        var handler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var result = await handler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, invalidDeviceId),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.DeviceIdInvalid);
    }

    [Fact]
    public async Task CheckIn_SameDeviceResume_RotatesToken_InvalidatesOldTokenWithActiveSessionConflict_AndWritesAuditLog()
    {
        await using var context = CreateDbContext();
        var (_, _, student1, _, shift, ticket1, _) = await SeedByodShiftAsync(context);

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var first = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        var token1 = first.Value.ActiveSessionToken!;

        var resume = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.2", InsideShift.AddMinutes(5), LaptopA),
            CancellationToken.None);
        resume.IsSuccess.Should().BeTrue();
        var token2 = resume.Value.ActiveSessionToken!;
        token2.Should().NotBe(token1);

        var resumeAudit = await context.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Action == "KIOSK_SESSION_RESUMED" && a.EntityId == ticket1.Id);
        resumeAudit.Should().NotBeNull();

        // Stale token1 must be rejected with 409 ACTIVE_SESSION_CONFLICT across Kiosk endpoints
        var wsOld = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student1.Id, "10.0.0.2", InsideShift.AddMinutes(6), LaptopA, token1), CancellationToken.None);
        wsOld.IsSuccess.Should().BeFalse();
        wsOld.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);

        var stateOld = await new GetOfficialExamTicketStateQueryHandler(context)
            .Handle(new GetOfficialExamTicketStateQuery(student1.Id, ticket1.Id, LaptopA, token1), CancellationToken.None);
        stateOld.IsSuccess.Should().BeFalse();
        stateOld.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);

        var antiCheatOld = await new LogAntiCheatEventCommandHandler(context)
            .Handle(new LogAntiCheatEventCommand(student1.Id, ticket1.Id, shift.Id, student1.Id, "WINDOW_BLUR", "high", "Alt-Tab", "evt-stale-1", LaptopA, token1), CancellationToken.None);
        antiCheatOld.IsSuccess.Should().BeFalse();
        antiCheatOld.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);

        // Fresh token2 succeeds
        var stateFresh = await new GetOfficialExamTicketStateQueryHandler(context)
            .Handle(new GetOfficialExamTicketStateQuery(student1.Id, ticket1.Id, LaptopA, token2), CancellationToken.None);
        stateFresh.IsSuccess.Should().BeTrue();
        stateFresh.Value.DeviceId.Should().Be(LaptopA);
    }

    [Fact]
    public async Task CheckIn_DifferentLaptopAndCrossStudentSameLaptop_Return409DeviceConflicts()
    {
        await using var context = CreateDbContext();
        var (_, _, student1, student2, _, ticket1, ticket2) = await SeedByodShiftAsync(context);

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var first = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Student 1 tries second laptop without EO unlock -> 409 DEVICE_MISMATCH
        var mismatch = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift.AddMinutes(1), LaptopB),
            CancellationToken.None);
        mismatch.IsSuccess.Should().BeFalse();
        mismatch.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        // Student 2 tries LaptopA already bound to Student 1 in same shift -> 409 DEVICE_IN_USE_OTHER_TICKET
        var sharedDevice = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student2.Id, ticket2.Id, "10.0.0.2", InsideShift.AddMinutes(1), LaptopA),
            CancellationToken.None);
        sharedDevice.IsSuccess.Should().BeFalse();
        sharedDevice.Error.Should().Be(OfficialExamErrors.DeviceInUseOtherTicket);
    }

    [Theory]
    [InlineData(UserRole.Proctor)]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.DepartmentHead)]
    public async Task UnlockDevice_NonExaminationOfficer_ReturnsForbidden(string forbiddenRole)
    {
        await using var context = CreateDbContext();
        var (_, _, student1, _, _, ticket1, _) = await SeedByodShiftAsync(context);
        var actor = new User { Id = Guid.NewGuid(), Email = $"{forbiddenRole}@fpt.edu.vn", FullName = forbiddenRole, Role = forbiddenRole, IsActive = true };
        context.Users.Add(actor);
        ticket1.DeviceId = LaptopA;
        ticket1.DeviceLockedAt = InsideShift;
        await context.SaveChangesAsync();

        var handler = new UnlockExamTicketDeviceCommandHandler(context);
        var result = await handler.Handle(
            new UnlockExamTicketDeviceCommand(actor.Id, ticket1.Id, "Máy sinh viên sập nguồn giữa giờ thi."),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task UnlockDevice_ExaminationOfficer_ValidatesReasonAndState_ClearsBinding_Audits_AndAllowsReplacementLaptopRebind()
    {
        await using var context = CreateDbContext();
        var (officer, _, student1, _, _, ticket1, _) = await SeedByodShiftAsync(context);

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var unlockHandler = new UnlockExamTicketDeviceCommandHandler(context);

        // Unbound ticket -> 409 CONFLICT
        var unboundRes = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Thử mở khóa vé chưa gắn máy."),
            CancellationToken.None);
        unboundRes.IsSuccess.Should().BeFalse();
        unboundRes.Error.Should().StartWith("CONFLICT:");

        // Bind LaptopA
        var checkIn1 = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        checkIn1.IsSuccess.Should().BeTrue();

        // Reason too short (<10 chars) -> 422 UNPROCESSABLE
        var shortReason = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Hỏng máy"),
            CancellationToken.None);
        shortReason.IsSuccess.Should().BeFalse();
        shortReason.Error.Should().StartWith("UNPROCESSABLE:");

        // Reason too long (>500 chars) -> 422 UNPROCESSABLE
        var longReason = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, new string('x', 501)),
            CancellationToken.None);
        longReason.IsSuccess.Should().BeFalse();
        longReason.Error.Should().StartWith("UNPROCESSABLE:");

        // Valid EO unlock
        const string validReason = "Laptop cá nhân của sinh viên bị màn hình xanh, đã đổi sang máy dự phòng Lab.";
        var unlocked = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, validReason),
            CancellationToken.None);
        unlocked.IsSuccess.Should().BeTrue();
        unlocked.Value.PreviousDeviceId.Should().Be(LaptopA);
        unlocked.Value.DeviceUnlockCount.Should().Be(1);
        unlocked.Value.UnlockedBy.Should().Be(officer.Id);
        unlocked.Value.Reason.Should().Be(validReason);

        var afterUnlock = await context.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket1.Id);
        afterUnlock.DeviceId.Should().BeNull();
        afterUnlock.DeviceLockedAt.Should().BeNull();
        afterUnlock.DeviceSessionTokenHash.Should().BeNull();
        afterUnlock.DeviceUnlockCount.Should().Be(1);
        afterUnlock.LastDeviceUnlockedBy.Should().Be(officer.Id);

        (await context.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "OFFICIAL_TICKET_DEVICE_UNLOCKED" && a.EntityId == ticket1.Id))
            .Should().BeTrue();

        // Student rebinds on replacement LaptopB
        var rebind = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.22", InsideShift.AddMinutes(10), LaptopB),
            CancellationToken.None);
        rebind.IsSuccess.Should().BeTrue();
        rebind.Value.DeviceId.Should().Be(LaptopB);
        rebind.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
        (await context.AuditLogs.AsNoTracking().AnyAsync(a => a.Action == "KIOSK_DEVICE_REBOUND" && a.EntityId == ticket1.Id))
            .Should().BeTrue();

        // Submitted/Locked ticket -> 409 CONFLICT
        var tracked = await context.StudentExamTickets.SingleAsync(t => t.Id == ticket1.Id);
        tracked.Status = ExamTicketStatus.Submitted;
        await context.SaveChangesAsync();

        var afterSubmitUnlock = await unlockHandler.Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, validReason),
            CancellationToken.None);
        afterSubmitUnlock.IsSuccess.Should().BeFalse();
        afterSubmitUnlock.Error.Should().StartWith("CONFLICT:");
    }

    [Fact]
    public async Task OfficialExamsController_UnlockDeviceAndDeviceConflicts_MapExactAuthorizeAndRfc7807Types()
    {
        var unlockMethod = typeof(OfficialExamsController).GetMethod(nameof(OfficialExamsController.UnlockTicketDevice));
        unlockMethod.Should().NotBeNull();
        var authAttr = unlockMethod!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
        authAttr.Roles.Should().Be("examination_officer");

        var senderMock = new Mock<ISender>();
        var controller = new OfficialExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, "student")
                    ], "Test"))
                }
            }
        };

        var ticketId = Guid.NewGuid();
        var cases = new[]
        {
            (OfficialExamErrors.DeviceMismatch, "https://oralexam.fpt.edu.vn/errors/device-mismatch"),
            (OfficialExamErrors.ActiveSessionConflict, "https://oralexam.fpt.edu.vn/errors/active-session-conflict"),
            (OfficialExamErrors.DeviceInUseOtherTicket, "https://oralexam.fpt.edu.vn/errors/device-in-use-other-ticket")
        };

        foreach (var (error, expectedType) in cases)
        {
            senderMock.Setup(s => s.Send(It.IsAny<CheckInExamTicketCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ExamTicketResponse>.Failure(error));

            var action = await controller.CheckInTicket(ticketId, CancellationToken.None, new CheckInExamTicketRequest { DeviceId = LaptopB });
            var problem = action.Should().BeOfType<ObjectResult>().Subject;
            problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
            problem.Value.Should().BeOfType<ProblemDetails>().Which.Type.Should().Be(expectedType);
        }
    }

    [Fact]
    public async Task AfterOfficerUnlock_BeforeReplacementCheckIn_SuppressesQuestionsAndBlocksSubmit_AndAfterRebindReturnsSealedMainTurnsForF5Recovery()
    {
        await using var context = CreateDbContext();
        var (officer, _, student1, _, _, ticket1, _) = await SeedByodShiftAsync(context);
        var q1Id = await context.ExamSetQuestions
            .Where(esq => esq.ExamSetId == ticket1.ExamSetId)
            .Select(esq => esq.ExamQuestionId)
            .SingleAsync();

        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var firstCheckIn = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        firstCheckIn.IsSuccess.Should().BeTrue();

        // Record a sealed main turn WITHOUT generated follow-up prompt (AudioVerifiedAt != null, GeneratedFollowUpPrompt == null)
        var mainSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket1.Id,
            ExamQuestionId = q1Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            GeneratedFollowUpPrompt = null,
            TranscriptWhisper = "Clean Architecture decouples domain from infrastructure.",
            AudioObjectKey = "official-exams/shift/ticket/01_SE171001_Q01.webm",
            AudioR2Url = "official-exams/shift/ticket/01_SE171001_Q01.webm",
            AudioHashSha256 = new string('a', 64),
            AudioVerifiedAt = InsideShift.AddMinutes(3),
            GradingStatus = "pending"
        };
        context.ExamQuestionSubmissions.Add(mainSub);
        await context.SaveChangesAsync();

        // EO unlocks device (ticket stays IN_PROGRESS, DeviceId = null, DeviceSessionTokenHash = null, DeviceUnlockCount = 1)
        var unlockResult = await new UnlockExamTicketDeviceCommandHandler(context).Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Laptop sinh viên hỏng bàn phím, đổi sang máy dự phòng."),
            CancellationToken.None);
        unlockResult.IsSuccess.Should().BeTrue();

        // BEFORE replacement check-in:
        // 1. GetMyWorkstationTicket returns 200 with DeviceId = null so FE renders Check-in rebind button, but MUST suppress Questions & FollowUps
        var wsBeforeRebind = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student1.Id, "10.0.0.2", InsideShift.AddMinutes(5), LaptopB), CancellationToken.None);
        wsBeforeRebind.IsSuccess.Should().BeTrue();
        wsBeforeRebind.Value.DeviceId.Should().BeNull();
        wsBeforeRebind.Value.DeviceUnlockCount.Should().Be(1);
        wsBeforeRebind.Value.Questions.Should().BeNull("questions must not leak after unlock before replacement device checks in");
        wsBeforeRebind.Value.FollowUps.Should().BeEmpty();

        // 2. GetOfficialExamTicketState also suppresses FollowUps until rebound
        var stateBeforeRebind = await new GetOfficialExamTicketStateQueryHandler(context)
            .Handle(new GetOfficialExamTicketStateQuery(student1.Id, ticket1.Id, LaptopB), CancellationToken.None);
        stateBeforeRebind.IsSuccess.Should().BeTrue();
        stateBeforeRebind.Value.DeviceId.Should().BeNull();
        stateBeforeRebind.Value.FollowUps.Should().BeEmpty();

        // 3. SubmitOfficialExam MUST be blocked with 409 DEVICE_MISMATCH until replacement laptop checks in
        var rogueSubmit = await new SubmitOfficialExamCommandHandler(context).Handle(
            new SubmitOfficialExamCommand(student1.Id, ticket1.Id, "10.0.0.2", [], LaptopB, null),
            CancellationToken.None);
        rogueSubmit.IsSuccess.Should().BeFalse();
        rogueSubmit.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        // AFTER replacement LaptopB checks in:
        var rebind = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.2", InsideShift.AddMinutes(6), LaptopB),
            CancellationToken.None);
        rebind.IsSuccess.Should().BeTrue();
        var newToken = rebind.Value.ActiveSessionToken!;

        // Both GetMyWorkstationTicket and GetOfficialExamTicketState now return Questions + sealed main turn (FollowUpIndex = 0) for F5 recovery!
        var wsAfterRebind = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student1.Id, "10.0.0.2", InsideShift.AddMinutes(7), LaptopB, newToken), CancellationToken.None);
        wsAfterRebind.IsSuccess.Should().BeTrue();
        wsAfterRebind.Value.Questions.Should().NotBeNull().And.HaveCount(1);
        wsAfterRebind.Value.FollowUps.Should().ContainSingle(f =>
            f.SubmissionId == mainSub.Id
            && f.ExamQuestionId == q1Id
            && f.FollowUpIndex == 0
            && f.AudioObjectKey == mainSub.AudioObjectKey
            && f.GeneratedFollowUpPrompt == null);

        var stateAfterRebind = await new GetOfficialExamTicketStateQueryHandler(context)
            .Handle(new GetOfficialExamTicketStateQuery(student1.Id, ticket1.Id, LaptopB, newToken), CancellationToken.None);
        stateAfterRebind.IsSuccess.Should().BeTrue();
        stateAfterRebind.Value.FollowUps.Should().ContainSingle(f =>
            f.SubmissionId == mainSub.Id && f.FollowUpIndex == 0 && f.AudioObjectKey == mainSub.AudioObjectKey);
    }

    [Fact]
    public async Task CheckIn_InProgressSameDeviceResume_SucceedsWithin2MinGrace_AndFailsAfterGrace()
    {
        await using var context = CreateDbContext();
        var (_, _, student1, _, shift, ticket1, _) = await SeedByodShiftAsync(context);
        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());

        var initial = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        initial.IsSuccess.Should().BeTrue();

        var withinGrace = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", shift.EndTime.AddMinutes(1), LaptopA),
            CancellationToken.None);
        withinGrace.IsSuccess.Should().BeTrue();
        withinGrace.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();

        var afterGrace = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", shift.EndTime.AddMinutes(2).AddSeconds(1), LaptopA),
            CancellationToken.None);
        afterGrace.IsSuccess.Should().BeFalse();
        afterGrace.Error.Should().Be(OfficialExamErrors.ShiftNotOpen);
    }

    [Fact]
    public async Task CheckIn_InProgressEoUnlockedRebind_SucceedsWithin2MinGrace_AndFailsAfterGrace()
    {
        await using var context = CreateDbContext();
        var (officer, _, student1, _, shift, ticket1, _) = await SeedByodShiftAsync(context);
        var checkInHandler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());

        var initial = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.1", InsideShift, LaptopA),
            CancellationToken.None);
        initial.IsSuccess.Should().BeTrue();

        var unlock = await new UnlockExamTicketDeviceCommandHandler(context).Handle(
            new UnlockExamTicketDeviceCommand(officer.Id, ticket1.Id, "Đổi máy dự phòng trong giờ thi."),
            CancellationToken.None);
        unlock.IsSuccess.Should().BeTrue();

        var afterGrace = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.2", shift.EndTime.AddMinutes(2).AddSeconds(1), LaptopB),
            CancellationToken.None);
        afterGrace.IsSuccess.Should().BeFalse();
        afterGrace.Error.Should().Be(OfficialExamErrors.ShiftNotOpen);

        var withinGrace = await checkInHandler.Handle(
            new CheckInExamTicketCommand(student1.Id, ticket1.Id, "10.0.0.2", shift.EndTime.AddMinutes(1), LaptopB),
            CancellationToken.None);
        withinGrace.IsSuccess.Should().BeTrue();
        withinGrace.Value.DeviceId.Should().Be(LaptopB);
        withinGrace.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
    }
}
