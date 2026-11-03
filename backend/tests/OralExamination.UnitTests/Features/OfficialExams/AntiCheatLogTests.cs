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
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 5, Status = "IN_PROGRESS", IpAddress = "192.168.1.105" };

        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var mockNotifier = new Mock<IAntiCheatNotifier>();
        var handler = new LogAntiCheatEventCommandHandler(context, mockNotifier.Object);

        // Lần 1: TAB_SWITCH
        var cmd1 = new LogAntiCheatEventCommand(ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Low, "Chuyển tab sang Chrome");
        var res1 = await handler.Handle(cmd1, CancellationToken.None);

        res1.IsSuccess.Should().BeTrue();
        res1.Value.ViolationCount.Should().Be(1);
        res1.Value.Severity.Should().Be(AntiCheatSeverity.Low);
        res1.Value.StudentCode.Should().Be("SE180001");

        // Lần 2: TAB_SWITCH tiếp tục lặp lại
        var cmd2 = new LogAntiCheatEventCommand(ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Medium, "Mở ứng dụng khác");
        var res2 = await handler.Handle(cmd2, CancellationToken.None);

        res2.IsSuccess.Should().BeTrue();
        res2.Value.ViolationCount.Should().Be(2);

        // Lần 3: TAB_SWITCH lặp lại >= 3 lần -> Tự động nâng cấp severity lên CRITICAL
        var cmd3 = new LogAntiCheatEventCommand(ticket.Id, shift.Id, student.Id, AntiCheatEventType.TabSwitch, AntiCheatSeverity.Low, "Tiếp tục chuyển tab lần 3");
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

        var shiftId = Guid.NewGuid();
        var student = new User { Id = Guid.NewGuid(), Email = "sv@fe.edu.vn", FullName = "Trần Thị B", StudentCode = "SE180002", Role = UserRole.Student, IsActive = true };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftId, StudentId = student.Id, SeatNumber = 10, Status = "IN_PROGRESS", IpAddress = "192.168.1.110" };

        context.Users.Add(student);
        context.StudentExamTickets.Add(ticket);

        var log1 = new ExamAntiCheatLog { Id = Guid.NewGuid(), TicketId = ticket.Id, ShiftId = shiftId, StudentId = student.Id, EventType = AntiCheatEventType.WindowBlur, Severity = AntiCheatSeverity.Medium, OccurredAt = DateTime.UtcNow.AddMinutes(-5), ViolationCount = 1 };
        var log2 = new ExamAntiCheatLog { Id = Guid.NewGuid(), TicketId = ticket.Id, ShiftId = shiftId, StudentId = student.Id, EventType = AntiCheatEventType.MultipleFaces, Severity = AntiCheatSeverity.High, OccurredAt = DateTime.UtcNow.AddMinutes(-1), ViolationCount = 1 };

        context.ExamAntiCheatLogs.AddRange(log1, log2);
        await context.SaveChangesAsync();

        var handler = new GetAntiCheatLogsQueryHandler(context);
        var result = await handler.Handle(new GetAntiCheatLogsQuery(shiftId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].EventType.Should().Be(AntiCheatEventType.MultipleFaces, "Sự kiện xảy ra sau (-1 min) phải đứng trước");
        result.Value[1].EventType.Should().Be(AntiCheatEventType.WindowBlur);
    }

    [Fact(DisplayName = "ANTICHEAT-03: Validator từ chối EventType hoặc Severity không hợp lệ")]
    public void Validator_Should_Reject_Invalid_EventType_Or_Severity()
    {
        var validator = new LogAntiCheatEventCommandValidator();

        var invalidEvent = new LogAntiCheatEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INVALID_EVENT", AntiCheatSeverity.Low, null);
        var res1 = validator.Validate(invalidEvent);
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().Contain(e => e.PropertyName == "EventType");

        var invalidSeverity = new LogAntiCheatEventCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AntiCheatEventType.TabSwitch, "EXTREME", null);
        var res2 = validator.Validate(invalidSeverity);
        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().Contain(e => e.PropertyName == "Severity");
    }
}
