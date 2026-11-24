using System;
using System.Linq;
using System.Net;
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
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.KioskHeartbeat;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class Pr6KioskHeartbeatAndAntiCheatTests
{
    private const string LaptopA = "DESKTOP-KIOSK-PR6-LAPTOP-0001";
    private const string LaptopB = "DESKTOP-KIOSK-PR6-LAPTOP-0002";
    private static readonly DateTime FixedNowUtc = new(2026, 11, 24, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Student, User OtherStudent, User Proctor, RealExamSessionShift Shift, StudentExamTicket Ticket, string RawToken)>
        SeedBoundKioskTicketAsync(
            OralExamDbContext context,
            string ticketStatus = ExamTicketStatus.InProgress,
            string shiftStatus = ExamShiftWindow.InProgress,
            int remainingMinutes = 30,
            bool bindDevice = true,
            int unlockCount = 0)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.pr6@fpt.edu.vn",
            FullName = "Nguyễn Hoàng Nam",
            StudentCode = "SE181001",
            Role = UserRole.Student,
            IsActive = true
        };
        var otherStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.other.pr6@fpt.edu.vn",
            FullName = "Lê Minh Khoa",
            StudentCode = "SE181002",
            Role = UserRole.Student,
            IsActive = true
        };
        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor.pr6@fpt.edu.vn",
            FullName = "Giám thị Phòng Lab",
            Role = UserRole.Proctor,
            IsActive = true
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1 - PR6",
            RoomLab = "AL-L301",
            StartTime = FixedNowUtc.AddMinutes(-30),
            EndTime = FixedNowUtc.AddMinutes(remainingMinutes),
            ProctorUserId = proctor.Id,
            Status = shiftStatus,
            IsExamPublished = true,
            MaxSeats = 24
        };

        var rawToken = OfficialExamDeviceGuard.GenerateSessionToken();
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 7,
            Status = ticketStatus,
            DeviceId = bindDevice ? LaptopA : null,
            DeviceLockedAt = bindDevice ? FixedNowUtc.AddMinutes(-20) : null,
            DeviceSessionTokenHash = bindDevice ? OfficialExamDeviceGuard.HashSessionToken(rawToken) : null,
            DeviceUnlockCount = unlockCount
        };

        context.Users.AddRange(student, otherStudent, proctor);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        return (student, otherStudent, proctor, shift, ticket, rawToken);
    }

    [Fact(DisplayName = "PR6-HB-01: Heartbeat hợp lệ trả thời gian còn lại chính xác và trạng thái phiên hoạt động")]
    public async Task KioskHeartbeat_ValidBoundDeviceAndSession_ReturnsHealthyState()
    {
        await using var context = CreateDbContext();
        var (student, _, _, shift, ticket, rawToken) = await SeedBoundKioskTicketAsync(context, remainingMinutes: 30);

        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));
        var result = await handler.Handle(
            new KioskHeartbeatCommand(
                student.Id,
                ticket.Id,
                LaptopA,
                rawToken,
                "1.2.0",
                "10.10.20.5",
                AudioLevelDb: 68,
                IsFullscreen: true,
                DisplayCount: 1),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TicketId.Should().Be(ticket.Id);
        result.Value.ShiftId.Should().Be(shift.Id);
        result.Value.TicketStatus.Should().Be(ExamTicketStatus.InProgress);
        result.Value.ShiftStatus.Should().Be(ExamShiftWindow.InProgress);
        result.Value.ServerUtcNow.Should().Be(FixedNowUtc);
        result.Value.RemainingShiftSeconds.Should().Be(1800);
        result.Value.IsLocked.Should().BeFalse();
        result.Value.DeviceUnlockRequired.Should().BeFalse();
        result.Value.ShouldTerminateExam.Should().BeFalse();
        result.Value.Notice.Should().BeNull();
    }

    [Fact(DisplayName = "PR6-HB-02: DisplayCount > 1 trả Notice cảnh báo MULTI_MONITOR và không tự ghi trùng log")]
    public async Task KioskHeartbeat_MultiMonitorTelemetry_ReturnsNoticeWithoutWritingDuplicateLogs()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);

        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));
        var result = await handler.Handle(
            new KioskHeartbeatCommand(
                student.Id,
                ticket.Id,
                LaptopA,
                rawToken,
                "1.2.0",
                "10.10.20.5",
                AudioLevelDb: 65,
                IsFullscreen: true,
                DisplayCount: 2),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShouldTerminateExam.Should().BeFalse();
        result.Value.Notice.Should().NotBeNullOrWhiteSpace();
        result.Value.Notice.Should().Contain(AntiCheatEventType.MultiMonitor);
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "PR6-HB-03: Chặn sinh viên khác, sai role, hoặc ActorUserId rỗng với lỗi Forbidden")]
    public async Task KioskHeartbeat_WrongOwnerOrNonStudent_ReturnsForbidden()
    {
        await using var context = CreateDbContext();
        var (_, otherStudent, proctor, _, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);
        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));

        var emptyActor = await handler.Handle(
            new KioskHeartbeatCommand(Guid.Empty, ticket.Id, LaptopA, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        emptyActor.IsSuccess.Should().BeFalse();
        emptyActor.Error.Should().Be(OfficialExamErrors.Forbidden);

        var wrongStudent = await handler.Handle(
            new KioskHeartbeatCommand(otherStudent.Id, ticket.Id, LaptopA, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        wrongStudent.IsSuccess.Should().BeFalse();
        wrongStudent.Error.Should().Be(OfficialExamErrors.Forbidden);

        var proctorCall = await handler.Handle(
            new KioskHeartbeatCommand(proctor.Id, ticket.Id, LaptopA, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        proctorCall.IsSuccess.Should().BeFalse();
        proctorCall.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Fact(DisplayName = "PR6-HB-04: Thiếu/sai DeviceId hoặc sai định dạng DeviceId trả về lỗi tương ứng")]
    public async Task KioskHeartbeat_MissingWrongOrInvalidDeviceId_ReturnsExpectedError()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);
        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));

        var missingDevice = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, null, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        missingDevice.IsSuccess.Should().BeFalse();
        missingDevice.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        var wrongDevice = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopB, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        wrongDevice.IsSuccess.Should().BeFalse();
        wrongDevice.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        var malformedDevice = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, "short", rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        malformedDevice.IsSuccess.Should().BeFalse();
        malformedDevice.Error.Should().Be(OfficialExamErrors.DeviceIdInvalid);
    }

    [Fact(DisplayName = "PR6-HB-05: Thiếu hoặc sai/đã xoay vòng KioskSessionToken trả về ACTIVE_SESSION_CONFLICT")]
    public async Task KioskHeartbeat_MissingOrStaleSessionToken_ReturnsActiveSessionConflict()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, ticket, _) = await SeedBoundKioskTicketAsync(context);
        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));

        var missingToken = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, null, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        missingToken.IsSuccess.Should().BeFalse();
        missingToken.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);

        var rotatedToken = OfficialExamDeviceGuard.GenerateSessionToken();
        var staleToken = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rotatedToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);
        staleToken.IsSuccess.Should().BeFalse();
        staleToken.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);
    }

    [Fact(DisplayName = "PR6-HB-06: Vé chưa khóa thiết bị bị chặn bởi requireBoundDevice=true nhưng giữ tương thích ngược khi false")]
    public async Task KioskHeartbeat_UnboundTicket_IsBlockedByRequireBoundDevice_AndGuardPreservesBackwardCompatibility()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, freshUnboundTicket, rawToken) = await SeedBoundKioskTicketAsync(
            context,
            bindDevice: false,
            unlockCount: 0);

        // Guard compatibility check: false allows legacy unbound check, true blocks unbound ticket
        OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            freshUnboundTicket,
            LaptopA,
            rawToken,
            requireSessionToken: true,
            requireBoundDevice: false).Should().BeNull();

        OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            freshUnboundTicket,
            LaptopA,
            rawToken,
            requireSessionToken: true,
            requireBoundDevice: true).Should().Be(OfficialExamErrors.DeviceMismatch);

        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));
        var result = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, freshUnboundTicket.Id, LaptopA, rawToken, "1.0.0", "10.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }

    [Theory(DisplayName = "PR6-HB-07: Vé bị khóa/đã nộp hoặc ca hết giờ/đã đóng đều trả ShouldTerminateExam=true")]
    [InlineData(ExamTicketStatus.Locked, ExamShiftWindow.InProgress, 20, true)]
    [InlineData(ExamTicketStatus.Submitted, ExamShiftWindow.InProgress, 20, false)]
    [InlineData(ExamTicketStatus.InProgress, ExamShiftWindow.InProgress, -1, false)]
    [InlineData(ExamTicketStatus.InProgress, ExamShiftWindow.Completed, 20, false)]
    public async Task KioskHeartbeat_LockedSubmittedExpiredOrClosed_SetsShouldTerminateExamTrue(
        string ticketStatus,
        string shiftStatus,
        int remainingMinutes,
        bool expectedIsLocked)
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, ticket, rawToken) = await SeedBoundKioskTicketAsync(
            context,
            ticketStatus: ticketStatus,
            shiftStatus: shiftStatus,
            remainingMinutes: remainingMinutes);

        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));
        var result = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rawToken, "1.2.0", "10.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsLocked.Should().Be(expectedIsLocked);
        result.Value.ShouldTerminateExam.Should().BeTrue();
        if (remainingMinutes <= 0)
        {
            result.Value.RemainingShiftSeconds.Should().Be(0);
        }

        result.Value.Notice.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "PR6-CTRL-01: OfficialExamKioskController đọc đúng header, khóa role student và map RFC 7807 (400/401/403/404/409)")]
    public async Task OfficialExamKioskController_Heartbeat_EnforcesAuthorize_ReadsHeaders_AndMapsRfc7807()
    {
        var controllerAuth = typeof(OfficialExamKioskController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
        controllerAuth.Roles.Should().Be("student");

        var senderMock = new Mock<ISender>();
        var studentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        // 1. Missing claims -> 401
        var unauthController = new OfficialExamKioskController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        (await unauthController.Heartbeat(ticketId)).Should().BeOfType<UnauthorizedResult>();

        // 2. Happy path forwards X-Device-Id, X-Kiosk-Session-Token, X-Kiosk-Version, IP and telemetry body
        KioskHeartbeatCommand? captured = null;
        var expectedResponse = new KioskHeartbeatResponse(
            ticketId,
            shiftId,
            ExamTicketStatus.InProgress,
            ExamShiftWindow.InProgress,
            FixedNowUtc,
            1200,
            false,
            false,
            false,
            null);

        senderMock.Setup(s => s.Send(It.IsAny<KioskHeartbeatCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<KioskHeartbeatResponse>>, CancellationToken>((req, _) => captured = (KioskHeartbeatCommand)req)
            .ReturnsAsync(Result<KioskHeartbeatResponse>.Success(expectedResponse));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, studentId.ToString()),
                new Claim(ClaimTypes.Role, UserRole.Student)
            ], "Test"))
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("172.16.10.42");
        httpContext.Request.Headers[OfficialExamDeviceGuard.DeviceIdHeaderName] = LaptopA;
        httpContext.Request.Headers[OfficialExamDeviceGuard.SessionTokenHeaderName] = "session-token-xyz";
        httpContext.Request.Headers[OfficialExamDeviceGuard.KioskVersionHeaderName] = "2.1.0";

        var controller = new OfficialExamKioskController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var okResult = await controller.Heartbeat(
            ticketId,
            new KioskHeartbeatRequest { AudioLevelDb = 72, IsFullscreen = true, DisplayCount = 1 },
            CancellationToken.None);

        okResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(expectedResponse);
        captured.Should().NotBeNull();
        captured!.ActorUserId.Should().Be(studentId);
        captured.TicketId.Should().Be(ticketId);
        captured.DeviceId.Should().Be(LaptopA);
        captured.KioskSessionToken.Should().Be("session-token-xyz");
        captured.KioskVersion.Should().Be("2.1.0");
        captured.RemoteIpAddress.Should().Be("172.16.10.42");
        captured.AudioLevelDb.Should().Be(72);
        captured.IsFullscreen.Should().BeTrue();
        captured.DisplayCount.Should().Be(1);

        // 3. RFC 7807 error mappings (400 / 403 / 404 / 409)
        var errorCases = new[]
        {
            (OfficialExamErrors.DeviceIdInvalid, StatusCodes.Status400BadRequest, "https://oralexam.fpt.edu.vn/errors/official-exam-kiosk-400"),
            (OfficialExamErrors.Forbidden, StatusCodes.Status403Forbidden, "https://oralexam.fpt.edu.vn/errors/official-exam-kiosk-403"),
            (OfficialExamErrors.TicketNotFound, StatusCodes.Status404NotFound, "https://oralexam.fpt.edu.vn/errors/official-exam-kiosk-404"),
            (OfficialExamErrors.DeviceMismatch, StatusCodes.Status409Conflict, "https://oralexam.fpt.edu.vn/errors/device-mismatch"),
            (OfficialExamErrors.ActiveSessionConflict, StatusCodes.Status409Conflict, "https://oralexam.fpt.edu.vn/errors/active-session-conflict")
        };

        foreach (var (error, expectedStatus, expectedType) in errorCases)
        {
            senderMock.Setup(s => s.Send(It.IsAny<KioskHeartbeatCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<KioskHeartbeatResponse>.Failure(error));

            var action = await controller.Heartbeat(ticketId, cancellationToken: CancellationToken.None);
            var objectResult = action.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(expectedStatus);
            var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            problem.Status.Should().Be(expectedStatus);
            problem.Type.Should().Be(expectedType);
            problem.Detail.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory(DisplayName = "PR6-AC-01: Validator chấp nhận đủ 5 loại sự kiện BYOD Anti-Cheat mới")]
    [InlineData(AntiCheatEventType.MultiMonitor)]
    [InlineData(AntiCheatEventType.VmDetected)]
    [InlineData(AntiCheatEventType.RemoteControl)]
    [InlineData(AntiCheatEventType.AudioTamper)]
    [InlineData(AntiCheatEventType.HeartbeatTimeout)]
    public void LogAntiCheatEventValidator_AcceptsAllNewByodEventTypes(string eventType)
    {
        AntiCheatEventType.All.Should().Contain(eventType);

        var validator = new LogAntiCheatEventCommandValidator();
        var cmd = new LogAntiCheatEventCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            eventType,
            AntiCheatSeverity.Medium,
            "BYOD Kiosk telemetry");

        var validation = validator.Validate(cmd);
        validation.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "PR6-AC-02: VM_DETECTED, REMOTE_CONTROL, AUDIO_TAMPER tự động nâng lên Critical ngay lần 1 và hỗ trợ idempotency")]
    [InlineData(AntiCheatEventType.VmDetected)]
    [InlineData(AntiCheatEventType.RemoteControl)]
    [InlineData(AntiCheatEventType.AudioTamper)]
    public async Task LogAntiCheatEvent_CriticalByodEvents_AutoEscalateToCritical_AndRespectClientEventIdIdempotency(
        string criticalEventType)
    {
        await using var context = CreateDbContext();
        var (student, _, _, shift, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);

        var notifierMock = new Mock<IAntiCheatNotifier>();
        var handler = new LogAntiCheatEventCommandHandler(context, notifierMock.Object);

        var clientEventId = $"evt-{criticalEventType.ToLowerInvariant()}-001";
        var first = await handler.Handle(
            new LogAntiCheatEventCommand(
                student.Id,
                ticket.Id,
                shift.Id,
                student.Id,
                criticalEventType,
                AntiCheatSeverity.Low,
                "Phát hiện vi phạm BYOD mức nghiêm trọng",
                clientEventId,
                LaptopA,
                rawToken),
            CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        first.Value.EventType.Should().Be(criticalEventType);
        first.Value.ViolationCount.Should().Be(1);
        first.Value.Severity.Should().Be(AntiCheatSeverity.Critical);
        first.Value.ClientEventId.Should().Be(clientEventId);

        var persisted = await context.ExamAntiCheatLogs.AsNoTracking().SingleAsync();
        persisted.Severity.Should().Be(AntiCheatSeverity.Critical);
        persisted.EventType.Should().Be(criticalEventType);

        // Idempotent retry with same ClientEventId must return same record and not re-notify SignalR
        var replay = await handler.Handle(
            new LogAntiCheatEventCommand(
                student.Id,
                ticket.Id,
                shift.Id,
                student.Id,
                criticalEventType,
                AntiCheatSeverity.Low,
                "Retry offline queue",
                clientEventId,
                LaptopA,
                rawToken),
            CancellationToken.None);

        replay.IsSuccess.Should().BeTrue();
        replay.Value.Id.Should().Be(first.Value.Id);
        replay.Value.ViolationCount.Should().Be(1);
        replay.Value.Severity.Should().Be(AntiCheatSeverity.Critical);
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(1);

        notifierMock.Verify(
            n => n.NotifyAntiCheatEventAsync(
                It.Is<ExamAntiCheatLogDto>(d => d.EventType == criticalEventType && d.Severity == AntiCheatSeverity.Critical),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory(DisplayName = "PR6-AC-03: MULTI_MONITOR và HEARTBEAT_TIMEOUT giữ severity lần 1-2 và nâng Critical từ lần 3")]
    [InlineData(AntiCheatEventType.MultiMonitor)]
    [InlineData(AntiCheatEventType.HeartbeatTimeout)]
    public async Task LogAntiCheatEvent_MultiMonitorAndHeartbeatTimeout_EscalateOnThirdViolation(string eventType)
    {
        await using var context = CreateDbContext();
        var (student, _, _, shift, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);

        var notifierMock = new Mock<IAntiCheatNotifier>();
        var handler = new LogAntiCheatEventCommandHandler(context, notifierMock.Object);

        var res1 = await handler.Handle(
            new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, eventType, AntiCheatSeverity.Low, "Lần 1", "evt-1", LaptopA, rawToken),
            CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();
        res1.Value.Severity.Should().Be(AntiCheatSeverity.Low);
        res1.Value.ViolationCount.Should().Be(1);

        var res2 = await handler.Handle(
            new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, eventType, AntiCheatSeverity.Medium, "Lần 2", "evt-2", LaptopA, rawToken),
            CancellationToken.None);
        res2.IsSuccess.Should().BeTrue();
        res2.Value.Severity.Should().Be(AntiCheatSeverity.Medium);
        res2.Value.ViolationCount.Should().Be(2);

        var res3 = await handler.Handle(
            new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, eventType, AntiCheatSeverity.Low, "Lần 3", "evt-3", LaptopA, rawToken),
            CancellationToken.None);
        res3.IsSuccess.Should().BeTrue();
        res3.Value.Severity.Should().Be(AntiCheatSeverity.Critical);
        res3.Value.ViolationCount.Should().Be(3);
        res3.Value.IsLocked.Should().BeFalse();

        notifierMock.Verify(
            n => n.NotifyAntiCheatEventAsync(It.IsAny<ExamAntiCheatLogDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Theory(DisplayName = "PR6-RT-01: LogAntiCheatEvent chặn sinh viên gửi sự kiện khi vé đã kết thúc (SUBMITTED/AI_GRADED/AUDITED/PUBLISHED)")]
    [InlineData(ExamTicketStatus.Submitted)]
    [InlineData(ExamTicketStatus.AiGraded)]
    [InlineData(ExamTicketStatus.Audited)]
    [InlineData(ExamTicketStatus.Published)]
    public async Task LogAntiCheatEvent_BlocksStudent_WhenTicketAlreadyFinalized(string finalizedStatus)
    {
        await using var context = CreateDbContext();
        var (student, _, _, shift, ticket, rawToken) = await SeedBoundKioskTicketAsync(
            context,
            ticketStatus: finalizedStatus);

        var handler = new LogAntiCheatEventCommandHandler(context);
        var result = await handler.Handle(
            new LogAntiCheatEventCommand(
                student.Id,
                ticket.Id,
                shift.Id,
                student.Id,
                AntiCheatEventType.WindowBlur,
                AntiCheatSeverity.Medium,
                "Late event after submit",
                "evt-finalized-1",
                LaptopA,
                rawToken),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "PR6-RT-02: LogAntiCheatEvent chặn Details > 2000 ký tự, kiểm tra vé gắn DeviceId dù thiếu SessionHash, và ghi LogWarning khi SignalR lỗi")]
    public async Task LogAntiCheatEvent_EnforcesDetailsLimit_BoundDeviceWithoutSessionHash_AndLogsNotifierFailure()
    {
        await using var context = CreateDbContext();
        var (student, _, _, shift, ticket, _) = await SeedBoundKioskTicketAsync(context);

        // Giả lập vé đã gắn DeviceId nhưng chưa có DeviceSessionTokenHash
        var trackedTicket = await context.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id);
        trackedTicket.DeviceSessionTokenHash = null;
        await context.SaveChangesAsync();

        var oversizedDetails = new string('X', 2001);

        // 1. Validator và Handler đều chặn Details > 2000 ký tự
        var validator = new LogAntiCheatEventCommandValidator();
        var oversizedCmd = new LogAntiCheatEventCommand(
            student.Id, ticket.Id, shift.Id, student.Id,
            AntiCheatEventType.WindowBlur, AntiCheatSeverity.Low, oversizedDetails, "evt-over", LaptopA, null);
        validator.Validate(oversizedCmd).IsValid.Should().BeFalse();

        var notifierMock = new Mock<IAntiCheatNotifier>();
        notifierMock
            .Setup(n => n.NotifyAntiCheatEventAsync(It.IsAny<ExamAntiCheatLogDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR transient outage"));
        var loggerMock = new Mock<ILogger<LogAntiCheatEventCommandHandler>>();

        var handler = new LogAntiCheatEventCommandHandler(context, notifierMock.Object, loggerMock.Object);

        var overResult = await handler.Handle(oversizedCmd, CancellationToken.None);
        overResult.IsSuccess.Should().BeFalse();
        overResult.Error.Should().StartWith("VALIDATION:");

        // 2. Vé đã gắn DeviceId nhưng SV bỏ trống DeviceId -> không được bỏ lọt, phải trả CONFLICT DEVICE_MISMATCH
        var missingDeviceRes = await handler.Handle(
            new LogAntiCheatEventCommand(
                student.Id, ticket.Id, shift.Id, student.Id,
                AntiCheatEventType.WindowBlur, AntiCheatSeverity.Low, "Missing device", "evt-nodev", null, null),
            CancellationToken.None);
        missingDeviceRes.IsSuccess.Should().BeFalse();
        missingDeviceRes.Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        // 3. DeviceId khớp và notifier lỗi -> vẫn lưu log thành công và ghi LogWarning thay vì nuốt lỗi rỗng
        var okRes = await handler.Handle(
            new LogAntiCheatEventCommand(
                student.Id, ticket.Id, shift.Id, student.Id,
                AntiCheatEventType.WindowBlur, AntiCheatSeverity.Low, "Valid event", "evt-ok", LaptopA, null),
            CancellationToken.None);
        okRes.IsSuccess.Should().BeTrue();
        (await context.ExamAntiCheatLogs.CountAsync()).Should().Be(1);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception?>(e => e is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName = "PR6-RT-03: DeviceGuard chặn thiếu SessionTokenHash khi requireBoundDevice=true; KioskHeartbeat kiểm tra biên AudioLevelDb và cảnh báo MICROPHONE_LOW_SIGNAL")]
    public async Task KioskHeartbeat_AndDeviceGuard_ValidateMissingSessionHash_AudioLevelBounds_AndLowMicNotice()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, ticket, rawToken) = await SeedBoundKioskTicketAsync(context);

        var handler = new KioskHeartbeatCommandHandler(context, new FixedTimeProvider(FixedNowUtc));

        // 1. AudioLevelDb ngoài khoảng [0..150] -> BAD_REQUEST
        var negAudio = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rawToken, "1.2.0", "10.0.0.1", AudioLevelDb: -1),
            CancellationToken.None);
        negAudio.IsSuccess.Should().BeFalse();
        negAudio.Error.Should().StartWith("BAD_REQUEST:");

        var highAudio = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rawToken, "1.2.0", "10.0.0.1", AudioLevelDb: 151),
            CancellationToken.None);
        highAudio.IsSuccess.Should().BeFalse();
        highAudio.Error.Should().StartWith("BAD_REQUEST:");

        // 2. AudioLevelDb trong [0..9] -> cảnh báo MICROPHONE_LOW_SIGNAL trong Notice
        var lowMic = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rawToken, "1.2.0", "10.0.0.1", AudioLevelDb: 5, IsFullscreen: true, DisplayCount: 1),
            CancellationToken.None);
        lowMic.IsSuccess.Should().BeTrue();
        lowMic.Value.Notice.Should().NotBeNullOrWhiteSpace();
        lowMic.Value.Notice.Should().Contain("MICROPHONE_LOW_SIGNAL");

        // 3. OfficialExamDeviceGuard: requireBoundDevice=true & requireSessionToken=true nhưng ticket.DeviceSessionTokenHash == null -> ActiveSessionConflict
        var trackedTicket = await context.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id);
        trackedTicket.DeviceSessionTokenHash = null;
        await context.SaveChangesAsync();

        OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            trackedTicket,
            LaptopA,
            rawToken,
            requireSessionToken: true,
            requireBoundDevice: true).Should().Be(OfficialExamErrors.ActiveSessionConflict);

        var hbMissingHash = await handler.Handle(
            new KioskHeartbeatCommand(student.Id, ticket.Id, LaptopA, rawToken, "1.2.0", "10.0.0.1", AudioLevelDb: 60),
            CancellationToken.None);
        hbMissingHash.IsSuccess.Should().BeFalse();
        hbMissingHash.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);
    }
}
