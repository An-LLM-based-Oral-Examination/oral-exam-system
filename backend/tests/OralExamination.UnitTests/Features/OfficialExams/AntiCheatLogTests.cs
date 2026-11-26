using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class AntiCheatLogTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "ANTICHEAT-01: Ghi nhận sự kiện vi phạm an ninh phòng thi và tăng biến đếm lần vi phạm")]
    public async Task LogAntiCheatEvent_Should_Record_Log_And_Increment_Violation_Count()
    {
        await using var context = CreateDbContext();

        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Nguyễn Văn A", StudentCode = "SE180001", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "Ca 1", RoomLab = "AL-L201", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(2), Status = "in_progress" };
        const string boundDeviceId = "BYOD-ANTICHEAT-TEST-0001";
        const string sessionToken = "anticheat-session-token-0001";
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            Status = "IN_PROGRESS",
            IpAddress = "192.168.1.105",
            DeviceId = boundDeviceId,
            DeviceLockedAt = DateTime.UtcNow,
            DeviceSessionTokenHash = OralExamination.Application.Features.OfficialExams.Common.OfficialExamDeviceGuard.HashSessionToken(sessionToken)
        };

        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var mockNotifier = new Mock<IAntiCheatNotifier>();
        var handler = new LogAntiCheatEventCommandHandler(context, mockNotifier.Object);

        // Lần 1: TAB_SWITCH
        var cmd1 = new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Low, "Chuyển tab sang Chrome", null, boundDeviceId, sessionToken);
        var res1 = await handler.Handle(cmd1, CancellationToken.None);

        res1.IsSuccess.Should().BeTrue();
        res1.Value.ViolationCount.Should().Be(1);
        res1.Value.Severity.Should().Be(AntiCheatSeverity.Low);
        res1.Value.StudentCode.Should().Be("SE180001");
        res1.Value.ClientEventId.Should().NotBeNullOrWhiteSpace();

        // Lần 2: TAB_SWITCH tiếp tục lặp lại
        var cmd2 = new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Medium, "Mở ứng dụng khác", null, boundDeviceId, sessionToken);
        var res2 = await handler.Handle(cmd2, CancellationToken.None);

        res2.IsSuccess.Should().BeTrue();
        res2.Value.ViolationCount.Should().Be(2);

        // Lần 3: TAB_SWITCH lặp lại >= 3 lần -> Tự động nâng cấp severity lên CRITICAL
        var cmd3 = new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Low, "Tiếp tục chuyển tab lần 3", null, boundDeviceId, sessionToken);
        var res3 = await handler.Handle(cmd3, CancellationToken.None);

        res3.IsSuccess.Should().BeTrue();
        res3.Value.ViolationCount.Should().Be(3);
        res3.Value.Severity.Should().Be(AntiCheatSeverity.Critical, "Vi phạm từ 3 lần trở lên phải tự động nâng cấp thành CRITICAL");

        mockNotifier.Verify(n => n.NotifyAntiCheatEventAsync(It.IsAny<ExamAntiCheatLogDto>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "ANTICHEAT-02: Truy vấn danh sách vi phạm của ca thi theo thứ tự thời gian giảm dần")]
    public async Task GetAntiCheatLogs_Should_Return_Logs_Ordered_By_OccurredAt_Desc()
    {
        await using var context = CreateDbContext();

        var proctor = new User { Id = Guid.NewGuid(), Email = "proctor@fe.edu.vn", FullName = "Giám thị A", Role = UserRole.Proctor, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Trần Thị B", StudentCode = "SE180002", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddHours(1),
            ProctorUserId = proctor.Id,
            Status = "in_progress"
        };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 10, Status = "IN_PROGRESS", IpAddress = "192.168.1.110" };

        context.Users.AddRange(proctor, student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);

        var log1 = new ExamAntiCheatLog { Id = Guid.NewGuid(), TicketId = ticket.Id, ShiftId = shift.Id, StudentId = student.Id, EventType = AntiCheatEventType.WindowBlur, Severity = AntiCheatSeverity.Medium, OccurredAt = DateTime.UtcNow.AddMinutes(-5), ViolationCount = 1 };
        var log2 = new ExamAntiCheatLog { Id = Guid.NewGuid(), TicketId = ticket.Id, ShiftId = shift.Id, StudentId = student.Id, EventType = AntiCheatEventType.MultipleFaces, Severity = AntiCheatSeverity.High, OccurredAt = DateTime.UtcNow.AddMinutes(-1), ViolationCount = 1 };

        context.ExamAntiCheatLogs.AddRange(log1, log2);
        await context.SaveChangesAsync();

        var handler = new GetAntiCheatLogsQueryHandler(context);
        var result = await handler.Handle(new GetAntiCheatLogsQuery(shift.Id, null, proctor.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].EventType.Should().Be(AntiCheatEventType.MultipleFaces, "Sự kiện xảy ra sau (-1 min) phải đứng trước");
        result.Value[1].EventType.Should().Be(AntiCheatEventType.WindowBlur);
        result.Value[0].ClientEventId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "ANTICHEAT-03: Validator từ chối EventType hoặc Severity không hợp lệ")]
    public void Validator_Should_Reject_Invalid_EventType_Or_Severity()
    {
        var validator = new LogAntiCheatEventCommandValidator();

        var invalidEvent = new LogAntiCheatEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INVALID_EVENT", AntiCheatSeverity.Low, null);
        var res1 = validator.Validate(invalidEvent);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == "EventType");

        var invalidSeverity = new LogAntiCheatEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AntiCheatEventType.TabSwitch, "EXTREME", null);
        var res2 = validator.Validate(invalidSeverity);
        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().Contain(e => e.PropertyName == "Severity");
    }

    [Fact(DisplayName = "ANTICHEAT-04: WINDOW_BLUR > 3 (lần thứ 4) tự khóa vé LOCKED, clientEventId idempotent không đếm đúp, và chặn BOLA spoofing")]
    public async Task LogAntiCheatEvent_BlurAutoLock_ClientEventIdIdempotency_AndStudentBola()
    {
        await using var context = CreateDbContext();

        var owner = new User { Id = Guid.NewGuid(), Email = "sv1@fe.edu.vn", FullName = "SV 1", StudentCode = "SE180011", Role = UserRole.Student, IsActive = true };
        var attacker = new User { Id = Guid.NewGuid(), Email = "sv2@fe.edu.vn", FullName = "SV 2", StudentCode = "SE180012", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "Ca Blur", RoomLab = "Lab 2", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), Status = "in_progress" };
        const string boundDeviceId = "BYOD-ANTICHEAT-BLUR-0001";
        const string sessionToken = "anticheat-blur-token-0001";
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = owner.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = boundDeviceId,
            DeviceLockedAt = DateTime.UtcNow,
            DeviceSessionTokenHash = OralExamination.Application.Features.OfficialExams.Common.OfficialExamDeviceGuard.HashSessionToken(sessionToken)
        };

        context.Users.AddRange(owner, attacker);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var mockNotifier = new Mock<IAntiCheatNotifier>();
        var handler = new LogAntiCheatEventCommandHandler(context, mockNotifier.Object);

        // 1. BOLA: Attacker student tries to log violation against owner's ticket -> FORBIDDEN
        var spoofResult = await handler.Handle(
            new LogAntiCheatEventCommand(attacker.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Spoof"),
            CancellationToken.None);
        spoofResult.IsSuccess.Should().BeFalse();
        spoofResult.Error.Should().StartWith("FORBIDDEN:");

        // 2. Blur 1..3 keeps IN_PROGRESS; duplicate clientEventId is idempotent
        var blur1 = await handler.Handle(
            new LogAntiCheatEventCommand(owner.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 1", "evt-blur-1", boundDeviceId, sessionToken),
            CancellationToken.None);
        blur1.IsSuccess.Should().BeTrue();
        blur1.Value.ViolationCount.Should().Be(1);
        blur1.Value.RemainingStrikes.Should().Be(3);
        blur1.Value.IsLocked.Should().BeFalse();

        // Retry same clientEventId "evt-blur-1" -> idempotent!
        var blur1Dup = await handler.Handle(
            new LogAntiCheatEventCommand(owner.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 1 retry", "evt-blur-1", boundDeviceId, sessionToken),
            CancellationToken.None);
        blur1Dup.IsSuccess.Should().BeTrue();
        blur1Dup.Value.Id.Should().Be(blur1.Value.Id);
        blur1Dup.Value.ViolationCount.Should().Be(1);

        var blur2 = await handler.Handle(
            new LogAntiCheatEventCommand(owner.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 2", "evt-blur-2", boundDeviceId, sessionToken),
            CancellationToken.None);
        blur2.Value.ViolationCount.Should().Be(2);
        blur2.Value.RemainingStrikes.Should().Be(2);
        blur2.Value.IsLocked.Should().BeFalse();

        var blur3 = await handler.Handle(
            new LogAntiCheatEventCommand(owner.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 3", "evt-blur-3", boundDeviceId, sessionToken),
            CancellationToken.None);
        blur3.Value.ViolationCount.Should().Be(3);
        blur3.Value.Severity.Should().Be(AntiCheatSeverity.Critical);
        blur3.Value.RemainingStrikes.Should().Be(1);
        blur3.Value.IsLocked.Should().BeFalse();
        (await context.StudentExamTickets.FindAsync(ticket.Id))!.Status.Should().Be(ExamTicketStatus.InProgress);

        // 3. Blur 4 (> 3) auto-locks ticket to LOCKED and writes AuditLog
        var blur4 = await handler.Handle(
            new LogAntiCheatEventCommand(owner.Id, ticket.Id, shift.Id, owner.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Medium, "Blur 4", "evt-blur-4", boundDeviceId, sessionToken),
            CancellationToken.None);
        blur4.IsSuccess.Should().BeTrue();
        blur4.Value.ViolationCount.Should().Be(4);
        blur4.Value.RemainingStrikes.Should().Be(0);
        blur4.Value.IsLocked.Should().BeTrue();
        blur4.Value.MaxViolations.Should().Be(3);
        blur4.Value.LockThreshold.Should().Be(4);

        var lockedTicket = await context.StudentExamTickets.FindAsync(ticket.Id);
        lockedTicket!.Status.Should().Be(ExamTicketStatus.Locked);
        (await context.AuditLogs.AnyAsync(a => a.Action == "TICKET_AUTO_LOCKED_BLUR_VIOLATION" && a.EntityId == ticket.Id)).Should().BeTrue();

        // Notifier called exactly 4 times (duplicate retry did NOT re-broadcast)
        mockNotifier.Verify(n => n.NotifyAntiCheatEventAsync(It.IsAny<ExamAntiCheatLogDto>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }
}
