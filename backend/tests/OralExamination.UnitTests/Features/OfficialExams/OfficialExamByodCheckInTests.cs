using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class OfficialExamByodCheckInTests
{
    private static readonly DateTime InsideShiftTime = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Student, User Officer, RealExamSessionShift Shift, StudentExamTicket Ticket)> SeedByodTicket(
        OralExamDbContext context,
        string? initialIpAddress = null)
    {
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer.byod@fpt.edu.vn",
            FullName = "Khảo thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.byod@fpt.edu.vn",
            StudentCode = "SE179999",
            FullName = "Nguyễn Văn Sinh Viên",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.AddRange(officer, student);

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        context.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Backend .NET",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.Courses.Add(course);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Cấu trúc đề",
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            SemesterId = semester.Id,
            ExamStructureId = structure.Id,
            Title = "Kỳ thi PRN231",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        context.OfficialExamSessions.Add(session);

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca Sáng BYOD",
            RoomLab = "Lab 301",
            StartTime = new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
            ProctorUserId = officer.Id,
            Status = ExamShiftWindow.Scheduled,
            MaxSeats = 24
        };
        context.RealExamSessionShifts.Add(shift);

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "SET-PRN231-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        context.ExamSets.Add(examSet);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = initialIpAddress, // Null in BYOD mode!
            Status = ExamTicketStatus.Scheduled,
            ExamSetId = examSet.Id
        };
        context.StudentExamTickets.Add(ticket);

        await context.SaveChangesAsync();
        return (student, officer, shift, ticket);
    }

    [Fact]
    public async Task CheckIn_WithNullIpAddress_SucceedsAndBindsClientIp()
    {
        await using var context = CreateDbContext();
        var (student, _, _, ticket) = await SeedByodTicket(context, initialIpAddress: null);

        var handler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        const string deviceId = "LAPTOP-BYOD-NULL-IP-0001";
        var result = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "192.168.1.55", InsideShiftTime, deviceId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ExamTicketStatus.InProgress);
        result.Value.IpAddress.Should().Be("192.168.1.55");
        result.Value.DeviceId.Should().Be(deviceId);

        var updatedTicket = await context.StudentExamTickets.FindAsync(ticket.Id);
        updatedTicket!.Status.Should().Be(ExamTicketStatus.InProgress);
        updatedTicket.IpAddress.Should().Be("192.168.1.55", "địa chỉ IP phải được ghi nhận động tại thời điểm check-in");
    }

    [Fact]
    public async Task CheckIn_AfterClientIpChanged_SameOrNewByodDevice_AllowsIpChangeAsAuditOnlyAndBlocksDifferentDevice()
    {
        await using var context = CreateDbContext();
        var (student, _, _, ticket) = await SeedByodTicket(context, initialIpAddress: "192.168.1.55");
        const string laptopA = "LAPTOP-BYOD-DEVICE-AAA-0001";
        const string laptopB = "LAPTOP-BYOD-DEVICE-BBB-0002";

        var handler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var firstCheckIn = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.99", InsideShiftTime, laptopA),
            CancellationToken.None);

        firstCheckIn.IsSuccess.Should().BeTrue("Wi-Fi/NAT IP changes must NOT block BYOD check-in; IP is audit-only");
        firstCheckIn.Value.IpAddress.Should().Be("10.0.0.99");
        firstCheckIn.Value.DeviceId.Should().Be(laptopA);
        firstCheckIn.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();

        var secondDeviceCheckIn = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.99", InsideShiftTime, laptopB),
            CancellationToken.None);

        secondDeviceCheckIn.IsSuccess.Should().BeFalse();
        secondDeviceCheckIn.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }

    [Fact]
    public async Task GetMyWorkstationTicket_WithNullTicketIpAddress_InsideWindow_ReturnsTicket()
    {
        await using var context = CreateDbContext();
        var (student, _, _, ticket) = await SeedByodTicket(context, initialIpAddress: null);

        var handler = new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator());
        var result = await handler.Handle(
            new GetMyWorkstationTicketQuery(student.Id, "192.168.1.77", InsideShiftTime),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ticket.Id);
        result.Value.SeatNumber.Should().Be(ticket.SeatNumber);
    }

    [Fact]
    public async Task AssignTicket_WithoutIpAddress_SucceedsWithNullIp()
    {
        await using var context = CreateDbContext();
        var (student, officer, shift, _) = await SeedByodTicket(context);

        var newStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "student2.byod@fpt.edu.vn",
            StudentCode = "SE178888",
            FullName = "Sinh viên thứ hai",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(newStudent);
        var session = await context.OfficialExamSessions.FirstAsync(s => s.Id == shift.SessionId);
        context.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = session.CourseId,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "SET-PRN231-02",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var validator = new AssignExamTicketCommandValidator();
        var handler = new AssignExamTicketCommandHandler(context, validator);

        var command = new AssignExamTicketCommand(
            officer.Id,
            shift.Id,
            newStudent.Id,
            SeatNumber: 2,
            IpAddress: null); // Null IP in BYOD manual assign

        var validation = await validator.ValidateAsync(command);
        validation.IsValid.Should().BeTrue("validator phải cho phép IpAddress là null trong mô hình BYOD");

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.IpAddress.Should().BeNull();
        result.Value.SeatNumber.Should().Be(2);

        var assignedTicket = await context.StudentExamTickets
            .FirstOrDefaultAsync(t => t.StudentId == newStudent.Id);
        assignedTicket.Should().NotBeNull();
        assignedTicket!.IpAddress.Should().BeNull();
    }

    [Fact]
    public async Task AssignTicket_WithInvalidIpAddress_FailsValidation()
    {
        var validator = new AssignExamTicketCommandValidator();
        var command = new AssignExamTicketCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SeatNumber: 2,
            IpAddress: "invalid_not_an_ip");

        var validation = await validator.ValidateAsync(command);
        validation.IsValid.Should().BeFalse();
        validation.Errors[0].ErrorMessage.Should().Be(OfficialExamErrors.IpInvalid);
    }

    [Fact]
    public async Task GetShiftAttendance_ReturnsExamSetIdAndExamSetCode()
    {
        await using var context = CreateDbContext();
        var (_, officer, shift, ticket) = await SeedByodTicket(context);

        var handler = new GetShiftAttendanceQueryHandler(context);
        var result = await handler.Handle(new GetShiftAttendanceQuery(shift.Id, officer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var seat1 = result.Value.Seats.FirstOrDefault(s => s.SeatNumber == 1);
        seat1.Should().NotBeNull();
        seat1!.IsOccupied.Should().BeTrue();
        seat1.ExamSetId.Should().Be(ticket.ExamSetId);
        seat1.ExamSetCode.Should().Be("SET-PRN231-01", "mã bộ đề phải được nạp và chiếu ra trong DTO");
    }
}
