using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class DistributeShiftTicketsTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Officer, RealExamSessionShift Shift, List<StudentExamTicket> Tickets)> SeedShiftWithTickets(
        OralExamDbContext context,
        int ticketCount = 3)
    {
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer.dist@fpt.edu.vn",
            FullName = "Khảo thí phân phối đề",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        context.Users.Add(officer);

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
            Code = "SWP391",
            Name = "Software Development Project",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.Courses.Add(course);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Kỳ thi SWP391",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        context.OfficialExamSessions.Add(session);

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 201",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            ProctorUserId = officer.Id,
            Status = ExamShiftWindow.Scheduled,
            MaxSeats = 24
        };
        context.RealExamSessionShifts.Add(shift);

        var tickets = new List<StudentExamTicket>();
        for (int i = 1; i <= ticketCount; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student{i}@fpt.edu.vn",
                StudentCode = $"SE17000{i}",
                FullName = $"Sinh viên {i}",
                Role = UserRole.Student,
                IsActive = true
            };
            context.Users.Add(student);

            var ticket = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                SeatNumber = i,
                Status = ExamTicketStatus.Scheduled,
                ExamSetId = null
            };
            tickets.Add(ticket);
            context.StudentExamTickets.Add(ticket);
        }

        await context.SaveChangesAsync();
        return (officer, shift, tickets);
    }

    private static async Task<List<ExamSet>> SeedHandedOverExamSets(OralExamDbContext context, Guid courseId, int count)
    {
        var sets = new List<ExamSet>();
        for (int i = 1; i <= count; i++)
        {
            var set = new ExamSet
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                SessionId = await context.OfficialExamSessions.Where(x => x.CourseId == courseId).Select(x => x.Id).SingleAsync(),
                SetCode = $"SET-OFFICIAL-{i:D2}",
                Status = ExamSetStatus.HandedOver,
                IsActive = true,
                HandedOverAt = DateTime.UtcNow.AddDays(-i)
            };
            sets.Add(set);
            context.ExamSets.Add(set);
        }
        await context.SaveChangesAsync();
        return sets;
    }

    [Fact]
    public async Task DistributeTickets_WithAvailableHandedOverSets_AllocatesWithoutDuplicate()
    {
        await using var context = CreateDbContext();
        var (officer, shift, tickets) = await SeedShiftWithTickets(context, 3);
        var handedOverSets = await SeedHandedOverExamSets(context, shift.Session.CourseId, 5);

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id, AllowRandomShuffle: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDistributed.Should().Be(3);
        result.Value.DistributedTickets.Should().HaveCount(3);

        var assignedSetIds = result.Value.DistributedTickets.Select(d => d.ExamSetId).ToList();
        assignedSetIds.Distinct().Should().HaveCount(3, "mỗi vé phải nhận một bộ đề duy nhất");

        var refreshedTickets = await context.StudentExamTickets
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync();
        refreshedTickets.All(t => t.ExamSetId.HasValue).Should().BeTrue();
    }

    [Fact]
    public async Task DistributeTickets_AllTicketsAssigned_ReturnsZeroDistributed()
    {
        await using var context = CreateDbContext();
        var (officer, shift, tickets) = await SeedShiftWithTickets(context, 2);
        var handedOverSets = await SeedHandedOverExamSets(context, shift.Session.CourseId, 2);

        tickets[0].ExamSetId = handedOverSets[0].Id;
        tickets[1].ExamSetId = handedOverSets[1].Id;
        await context.SaveChangesAsync();

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDistributed.Should().Be(0);
        result.Value.DistributedTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task DistributeTickets_InsufficientHandedOverSets_FailsUnprocessable()
    {
        await using var context = CreateDbContext();
        var (officer, shift, _) = await SeedShiftWithTickets(context, 4);
        // Only 2 sets available for 4 unset tickets
        await SeedHandedOverExamSets(context, shift.Session.CourseId, 2);

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("UNPROCESSABLE");
        result.Error.Should().Contain("cần cấp 4");
    }

    [Fact]
    public async Task DistributeTickets_DraftOrOfficialNonHandedOverSets_AreIgnored()
    {
        await using var context = CreateDbContext();
        var (officer, shift, _) = await SeedShiftWithTickets(context, 2);

        // Add 2 sets but not HANDED_OVER
        var draftSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = shift.Session.CourseId,
            SetCode = "SET-DRAFT",
            Status = ExamSetStatus.Draft,
            IsActive = true
        };
        var reviewSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = shift.Session.CourseId,
            SetCode = "SET-REVIEW",
            Status = ExamSetStatus.SubmittedForReview,
            IsActive = true
        };
        context.ExamSets.AddRange(draftSet, reviewSet);
        await context.SaveChangesAsync();

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id),
            CancellationToken.None);

        // Should fail because 0 handed-over sets available
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("UNPROCESSABLE");
    }

    [Fact]
    public async Task DistributeTickets_CompletedShift_FailsConflict()
    {
        await using var context = CreateDbContext();
        var (officer, shift, _) = await SeedShiftWithTickets(context, 2);
        shift.Status = ExamShiftWindow.Completed;
        await context.SaveChangesAsync();

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(officer.Id, officer.Role, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT");
    }

    [Theory]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.Admin)]
    public async Task DistributeTickets_NonOfficer_IsForbidden(string role)
    {
        await using var context = CreateDbContext();
        var (_, shift, _) = await SeedShiftWithTickets(context, 2);

        var actor = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{role}@fpt.edu.vn",
            FullName = $"Actor {role}",
            Role = role,
            IsActive = true
        };
        context.Users.Add(actor);
        await context.SaveChangesAsync();

        var handler = new DistributeShiftTicketsCommandHandler(context);
        var result = await handler.Handle(
            new DistributeShiftTicketsCommand(actor.Id, actor.Role, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }
}
