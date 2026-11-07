using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Proctor;

public class ProctorHardeningTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "R4.3-01: Proctor điểm danh ghế 1-40 thành công, KHÔNG được phép đổi IP và KHÔNG được mở ca thi")]
    public async Task UpdateAttendance_ShouldUpdateSeatAndPresence_ButNotChangeIpOrShiftStatus()
    {
        await using var context = CreateDbContext();
        var proctorId = Guid.NewGuid();
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1 - Phòng 501",
            RoomLab = "Lab 501",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(50),
            ProctorUserId = proctorId,
            Status = "scheduled" // Ca thi chưa được Khảo thí mở
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.proctor@fpt.edu.vn",
            FullName = "Sinh Viên Giám Thị Điểm Danh",
            StudentCode = "SE170111",
            Role = UserRole.Student,
            IsActive = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = "192.168.1.101",
            Status = ExamTicketStatus.Scheduled
        };

        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new UpdateTicketAttendanceCommandHandler(context);

        // Proctor cố tình truyền IpAddress mới và số ghế mới 15
        var command = new UpdateTicketAttendanceCommand(
            ActorUserId: proctorId,
            TicketId: ticket.Id,
            IsPresent: true,
            IpAddress: "10.99.99.99", // Cố tình đổi IP
            Note: "Đã kiểm tra thẻ sinh viên",
            SeatNumber: 15);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SeatNumber.Should().Be(15);
        result.Value.Status.Should().Be(ExamTicketStatus.InProgress);

        // Kiểm tra trong DB:
        var updatedTicket = await context.StudentExamTickets.FirstAsync(t => t.Id == ticket.Id);
        updatedTicket.SeatNumber.Should().Be(15);
        updatedTicket.CheckedInAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // CAM KẾT BẤT BIẾN & ĐIỀU CẤM KỴ 14:
        // 1. IP máy trạm KHÔNG bị thay đổi theo request của Proctor
        updatedTicket.IpAddress.Should().Be("192.168.1.101", "Proctor tuyệt đối không được phép sửa IP máy trạm");

        // 2. Ca thi KHÔNG bị Proctor tự ý mở sang in_progress (quyền thuộc Khảo thí)
        var updatedShift = await context.RealExamSessionShifts.FirstAsync(s => s.Id == shift.Id);
        updatedShift.Status.Should().Be("scheduled", "Proctor tuyệt đối không có quyền mở ca thi sang in_progress");
    }

    [Fact(DisplayName = "R4.3-02: UpdateAttendance chặn số ghế ngoài khoảng 1-40")]
    public async Task UpdateAttendance_WithInvalidSeatNumber_ShouldFailValidation()
    {
        await using var context = CreateDbContext();
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca thi kiểm thử",
            RoomLab = "Lab 101",
            Status = "scheduled"
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.invalidseat@fpt.edu.vn",
            FullName = "Sinh Viên Test Ghế",
            Role = UserRole.Student,
            IsActive = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 10,
            Status = ExamTicketStatus.Scheduled
        };
        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new UpdateTicketAttendanceCommandHandler(context);

        // Ghế số 0
        var zeroSeatResult = await handler.Handle(
            new UpdateTicketAttendanceCommand(Guid.NewGuid(), ticket.Id, true, SeatNumber: 0),
            CancellationToken.None);
        zeroSeatResult.IsSuccess.Should().BeFalse();
        zeroSeatResult.Error.Should().StartWith("VALIDATION:");

        // Ghế số 45
        var overSeatResult = await handler.Handle(
            new UpdateTicketAttendanceCommand(Guid.NewGuid(), ticket.Id, true, SeatNumber: 45),
            CancellationToken.None);
        overSeatResult.IsSuccess.Should().BeFalse();
        overSeatResult.Error.Should().StartWith("VALIDATION:");
    }

    [Fact(DisplayName = "R4.3-03: Proctor ghi biên bản vi phạm vào ExamAntiCheatLogs và tự động nâng mức Critical khi vi phạm >= 3")]
    public async Task RecordProctorViolation_ShouldPersistLog_AndEscalateSeverity()
    {
        await using var context = CreateDbContext();
        var proctorId = Guid.NewGuid();
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 2 - Lab 301",
            RoomLab = "Lab 301",
            Status = "in_progress"
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.cheat@fpt.edu.vn",
            FullName = "Nguyễn Văn Vi Phạm",
            StudentCode = "SE170222",
            Role = UserRole.Student,
            IsActive = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 7,
            Status = ExamTicketStatus.InProgress
        };
        context.RealExamSessionShifts.Add(shift);
        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var fakeNotifier = new FakeAntiCheatNotifier();
        var handler = new RecordProctorViolationCommandHandler(context, fakeNotifier);

        // Lần vi phạm 1: TAB_SWITCH (Medium)
        var res1 = await handler.Handle(
            new RecordProctorViolationCommand(
                proctorId,
                shift.Id,
                ticket.Id,
                student.Id,
                AntiCheatEventType.TabSwitch,
                AntiCheatSeverity.Medium,
                "Thí sinh chuyển tab trình duyệt lần 1"),
            CancellationToken.None);

        res1.IsSuccess.Should().BeTrue();
        res1.Value.ViolationCount.Should().Be(1);
        res1.Value.Severity.Should().Be(AntiCheatSeverity.Medium);
        fakeNotifier.CallCount.Should().Be(1);

        // Lần vi phạm 2: TAB_SWITCH (Medium)
        var res2 = await handler.Handle(
            new RecordProctorViolationCommand(
                proctorId,
                shift.Id,
                ticket.Id,
                student.Id,
                AntiCheatEventType.TabSwitch,
                AntiCheatSeverity.Medium,
                "Thí sinh chuyển tab trình duyệt lần 2"),
            CancellationToken.None);

        res2.IsSuccess.Should().BeTrue();
        res2.Value.ViolationCount.Should().Be(2);

        // Lần vi phạm 3: TAB_SWITCH -> BẮT BUỘC TỰ ĐỘNG NÂNG LÊN Critical
        var res3 = await handler.Handle(
            new RecordProctorViolationCommand(
                proctorId,
                shift.Id,
                ticket.Id,
                student.Id,
                AntiCheatEventType.TabSwitch,
                AntiCheatSeverity.Medium,
                "Thí sinh chuyển tab trình duyệt lần 3"),
            CancellationToken.None);

        res3.IsSuccess.Should().BeTrue();
        res3.Value.ViolationCount.Should().Be(3);
        res3.Value.Severity.Should().Be(AntiCheatSeverity.Critical, "Vi phạm từ lần 3 trở lên phải được tự động nâng lên Critical");

        // Kiểm tra bản ghi trong DB và AuditLogs
        var logs = await context.ExamAntiCheatLogs.Where(l => l.TicketId == ticket.Id).ToListAsync();
        logs.Should().HaveCount(3);
        logs.Last().Severity.Should().Be(AntiCheatSeverity.Critical);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PROCTOR_VIOLATION_RECORDED" && a.UserId == proctorId);
        audit.Should().NotBeNull();
    }

    [Fact(DisplayName = "R4.3-04: ProctorController.SuspendTicket chỉ cho phép examination_officer và admin; cấm proctor")]
    public void ProctorController_SuspendEndpoint_ShouldRestrictRoles_ExcludingProctor()
    {
        var controllerType = typeof(global::API.Controllers.v1.ProctorController);
        var suspendMethod = controllerType.GetMethod(nameof(global::API.Controllers.v1.ProctorController.SuspendTicket));
        suspendMethod.Should().NotBeNull();

        var authAttr = suspendMethod!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();

        var allowedRoles = authAttr!.Roles?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        allowedRoles.Should().Contain("examination_officer");
        allowedRoles.Should().Contain("admin");
        allowedRoles.Should().NotContain("proctor", "Proctor tuyệt đối không có quyền gọi endpoint đình chỉ điểm thi");
        allowedRoles.Should().NotContain("student");
        allowedRoles.Should().NotContain("lecturer");
    }

    [Fact(DisplayName = "R4.3-05: ProctorController.RecordViolation cho phép proctor và admin")]
    public void ProctorController_RecordViolationEndpoint_ShouldAllowProctorAndAdmin()
    {
        var controllerType = typeof(global::API.Controllers.v1.ProctorController);
        var violationMethod = controllerType.GetMethod(nameof(global::API.Controllers.v1.ProctorController.RecordViolation));
        violationMethod.Should().NotBeNull();

        var authAttr = violationMethod!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();

        var allowedRoles = authAttr!.Roles?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        allowedRoles.Should().Contain("proctor");
        allowedRoles.Should().Contain("admin");
    }

    private sealed class FakeAntiCheatNotifier : IAntiCheatNotifier
    {
        public int CallCount { get; private set; }

        public Task NotifyAntiCheatEventAsync(ExamAntiCheatLogDto logDto, CancellationToken ct = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
