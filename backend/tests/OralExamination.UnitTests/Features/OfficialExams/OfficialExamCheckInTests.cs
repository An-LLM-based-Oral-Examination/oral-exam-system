using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class OfficialExamCheckInTests
{
    private static readonly DateTime InsideShift = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
    [Fact]
    public async Task CreateSession_ExaminationOfficer_StoresSafeDefaultsAndInputMode()
    {
        await using var context = CreateDbContext();
        var head = await SeedUser(context, UserRole.ExaminationOfficer);
        var course = await SeedCourse(context);
        var structure = await SeedStructure(context, course.Id, head.Id);

        var result = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new CreateOfficialExamSessionCommand(
                head.Id,
                course.Id,
                structure.Id,
                "Vấn đáp PRN231",
                new DateOnly(2026, 10, 20),
                false,
                2,
                ExamInputMode.VoiceOnly,
                60), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        result.Value.MaxFollowUpQuestions.Should().Be(2);
        result.Value.HasFollowUp.Should().BeFalse();
        (await context.OfficialExamSessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateSession_Lecturer_IsForbidden()
    {
        await using var context = CreateDbContext();
        var lecturer = await SeedUser(context, UserRole.Lecturer);
        var course = await SeedCourse(context);
        var structure = await SeedStructure(context, course.Id, lecturer.Id);

        var result = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new CreateOfficialExamSessionCommand(
                lecturer.Id,
                course.Id,
                structure.Id,
                "Vấn đáp PRN231",
                new DateOnly(2026, 10, 20),
                false,
                2,
                ExamInputMode.VoiceWithTranscriptEdit,
                60), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Fact]
    public async Task CreateSession_FollowUpOutsideRange_FailsValidation()
    {
        await using var context = CreateDbContext();
        var head = await SeedUser(context, UserRole.DepartmentHead);

        var result = await new CreateOfficialExamSessionCommandHandler(context, new CreateOfficialExamSessionCommandValidator())
            .Handle(new CreateOfficialExamSessionCommand(
                head.Id,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Vấn đáp",
                new DateOnly(2026, 10, 20),
                true,
                6,
                ExamInputMode.VoiceOnly,
                60), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("1 đến 5");
    }

    [Fact]
    public async Task OpenShift_AssignsLecturerProctor()
    {
        await using var context = CreateDbContext();
        var head = await SeedUser(context, UserRole.ExaminationOfficer);
        var proctor = await SeedUser(context, UserRole.Lecturer);
        var examiner = await SeedUser(context, UserRole.Lecturer);
        var session = await SeedSession(context, head.Id);

        var result = await new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator())
            .Handle(new OpenExamShiftCommand(
                head.Id,
                session.Id,
                "Ca sáng",
                "Lab 7.1",
                new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
                proctor.Id,
                examiner.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProctorUserId.Should().Be(proctor.Id);
        result.Value.ExaminerUserId.Should().Be(examiner.Id);
        result.Value.RoomLab.Should().Be("Lab 7.1");
        result.Value.MaxSeats.Should().Be(OfficialExamRules.DefaultShiftCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void OpenShift_CapacityOutsideHardLimit_FailsValidation(int maxSeats)
    {
        var command = new OpenExamShiftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Morning", "Lab",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1),
            Guid.NewGuid(), Guid.NewGuid(), maxSeats);

        var result = new OpenExamShiftCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(OpenExamShiftCommand.MaxSeats));
    }

    [Fact]
    public async Task AssignTicket_DuplicateSeat_Fails()
    {
        await using var context = CreateDbContext();
        var head = await SeedUser(context, UserRole.ExaminationOfficer);
        var first = await SeedUser(context, UserRole.Student);
        var second = await SeedUser(context, UserRole.Student);
        var session = await SeedSession(context, head.Id);
        var shift = await SeedShift(context, session.Id, head.Id);
        context.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = session.CourseId,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "HANDED-OVER-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var handler = new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator());

        var created = await handler.Handle(
            new AssignExamTicketCommand(head.Id, shift.Id, first.Id, 1, "192.168.1.101"),
            CancellationToken.None);
        var duplicate = await handler.Handle(
            new AssignExamTicketCommand(head.Id, shift.Id, second.Id, 1, "192.168.1.102"),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        duplicate.IsSuccess.Should().BeFalse();
        duplicate.Error.Should().Be(OfficialExamErrors.SeatTaken);
    }

    [Fact]
    public async Task AssignTicket_SeatAboveConfiguredCapacity_FailsWithoutCreatingTicket()
    {
        await using var context = CreateDbContext();
        var officer = await SeedUser(context, UserRole.ExaminationOfficer);
        var student = await SeedUser(context, UserRole.Student);
        var session = await SeedSession(context, officer.Id);
        var shift = await SeedShift(context, session.Id, officer.Id);
        shift.MaxSeats = 24;
        await context.SaveChangesAsync();

        var result = await new AssignExamTicketCommandHandler(context, new AssignExamTicketCommandValidator())
            .Handle(new AssignExamTicketCommand(officer.Id, shift.Id, student.Id, 25, "192.168.1.125"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("1 đến 24");
        (await context.StudentExamTickets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CheckIn_MatchingIp_SetsInProgress()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "::ffff:192.168.1.101", InsideShift), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ExamTicketStatus.InProgress);
        (await context.StudentExamTickets.SingleAsync()).Status.Should().Be(ExamTicketStatus.InProgress);
    }

    [Fact]
    public async Task CheckIn_DifferentIpSameStudent_SucceedsAsAuditOnly_WhileDifferentDeviceFailsDeviceMismatch()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");
        const string deviceA = "KIOSK-DEVICE-CHECKIN-A-0001";
        const string deviceB = "KIOSK-DEVICE-CHECKIN-B-0002";

        var handler = new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator());
        var checkIn = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.8", InsideShift, deviceA),
            CancellationToken.None);

        checkIn.IsSuccess.Should().BeTrue("BYOD mode treats IP as audit-only and locks on DeviceId");
        checkIn.Value.IpAddress.Should().Be("10.0.0.8");
        checkIn.Value.DeviceId.Should().Be(deviceA);
        (await context.StudentExamTickets.SingleAsync()).Status.Should().Be(ExamTicketStatus.InProgress);

        var differentDevice = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.8", InsideShift, deviceB),
            CancellationToken.None);

        differentDevice.IsSuccess.Should().BeFalse();
        differentDevice.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }

    [Fact]
    public async Task CheckIn_OtherStudent_IsForbidden()
    {
        await using var context = CreateDbContext();
        var owner = await SeedUser(context, UserRole.Student);
        var stranger = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, owner.Id, "192.168.1.101");

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(stranger.Id, ticket.Id, "192.168.1.101", InsideShift), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.ExaminationOfficer)]
    public async Task CheckIn_AdminOrOfficer_MissingTicket_ReturnsForbiddenBeforeNotFound(string role)
    {
        await using var context = CreateDbContext();
        var actor = await SeedUser(context, role);

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(actor.Id, Guid.NewGuid(), "192.168.1.101", InsideShift), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Fact]
    public async Task CheckIn_BeforeShift_StaysScheduled()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(
                student.Id,
                ticket.Id,
                "192.168.1.101",
                new DateTime(2026, 10, 20, 6, 59, 0, DateTimeKind.Utc)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ShiftNotOpen);
        (await context.StudentExamTickets.SingleAsync()).Status.Should().Be(ExamTicketStatus.Scheduled);
    }

    [Fact]
    public async Task CheckIn_AtShiftEnd_StaysScheduled()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(
                student.Id,
                ticket.Id,
                "192.168.1.101",
                new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ShiftNotOpen);
        (await context.StudentExamTickets.SingleAsync()).Status.Should().Be(ExamTicketStatus.Scheduled);
    }

    [Fact]
    public async Task CheckIn_CancelledShift_IsClosed()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");
        var shift = await context.RealExamSessionShifts.SingleAsync();
        shift.Status = ExamShiftWindow.Cancelled;
        await context.SaveChangesAsync();

        var result = await new CheckInExamTicketCommandHandler(context, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "192.168.1.101", InsideShift), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ShiftClosed);
    }

    [Fact]
    public async Task GetCurrentTicket_InsideShift_ReturnsSeat()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");

        var result = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "::ffff:192.168.1.101", InsideShift), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ticket.Id);
        result.Value.SeatNumber.Should().Be(1);
        result.Value.RoomLab.Should().Be("Lab 7.1");
        result.Value.SessionTitle.Should().Be("Kỳ thi mẫu");
    }

    [Fact]
    public async Task GetCurrentTicket_OutsideShift_Fails()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        await SeedTicket(context, student.Id, "192.168.1.101");

        var result = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(
                student.Id,
                "192.168.1.101",
                new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.ShiftNotOpen);
    }

    [Fact]
    public async Task GetCurrentTicket_DifferentIpSucceedsAuditOnly_WhileWrongBoundDeviceReturnsDeviceMismatch()
    {
        await using var context = CreateDbContext();
        var student = await SeedUser(context, UserRole.Student);
        var ticket = await SeedTicket(context, student.Id, "192.168.1.101");
        ticket.DeviceId = "BOUND-LAPTOP-DEVICE-0001";
        ticket.DeviceLockedAt = InsideShift;
        await context.SaveChangesAsync();

        var sameDeviceDifferentIp = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.8", InsideShift, "BOUND-LAPTOP-DEVICE-0001"), CancellationToken.None);

        sameDeviceDifferentIp.IsSuccess.Should().BeTrue("IP change on the same bound BYOD device must succeed");

        var wrongDevice = await new GetMyWorkstationTicketQueryHandler(context, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.8", InsideShift, "FOREIGN-LAPTOP-DEVICE-9999"), CancellationToken.None);

        wrongDevice.IsSuccess.Should().BeFalse();
        wrongDevice.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }

    private static async Task<User> SeedUser(OralExamDbContext context, string role)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Người dùng mẫu",
            Role = role,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Course> SeedCourse(OralExamDbContext context)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"FA{Guid.NewGuid().ToString("N")[..4]}",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        context.Semesters.Add(semester);

        var course = new Course
        {
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();
        return course;
    }

    private static async Task<ExamStructure> SeedStructure(OralExamDbContext context, Guid courseId, Guid createdBy)
    {
        var structure = new ExamStructure
        {
            CourseId = courseId,
            CreatedBy = createdBy,
            Name = "Cấu trúc vấn đáp",
            IsActive = true
        };
        context.ExamStructures.Add(structure);
        await context.SaveChangesAsync();
        return structure;
    }

    private static async Task<OfficialExamSession> SeedSession(OralExamDbContext context, Guid createdBy)
    {
        var course = await SeedCourse(context);
        var structure = await SeedStructure(context, course.Id, createdBy);
        var session = new OfficialExamSession
        {
            CourseId = course.Id,
            SemesterId = course.SemesterId,
            ExamStructureId = structure.Id,
            Title = "Kỳ thi mẫu",
            ExamDate = new DateOnly(2026, 10, 20),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            ExamInputMode = ExamInputMode.VoiceOnly,
            TranscriptBufferSeconds = 60,
            Status = "scheduled",
            CreatedBy = createdBy
        };
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    private static async Task<RealExamSessionShift> SeedShift(OralExamDbContext context, Guid sessionId, Guid proctorId)
    {
        var shift = new RealExamSessionShift
        {
            SessionId = sessionId,
            ShiftName = "Ca sáng",
            RoomLab = "Lab 7.1",
            StartTime = new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
            ProctorUserId = proctorId,
            Status = "scheduled"
        };
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();
        return shift;
    }

    private static async Task<StudentExamTicket> SeedTicket(OralExamDbContext context, Guid studentId, string ipAddress)
    {
        var head = await SeedUser(context, UserRole.DepartmentHead);
        var session = await SeedSession(context, head.Id);
        var shift = await SeedShift(context, session.Id, head.Id);
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = session.CourseId,
            StructureId = session.ExamStructureId!.Value,
            SessionId = session.Id,
            SetCode = "HANDED-SEED-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        context.ExamSets.Add(examSet);
        var ticket = new StudentExamTicket
        {
            ShiftId = shift.Id,
            StudentId = studentId,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            IpAddress = ipAddress,
            Status = ExamTicketStatus.Scheduled
        };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket;
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }
}
