using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class UpdateExamShiftStatusTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Officer, RealExamSessionShift Shift)> SeedOfficerAndShift(OralExamDbContext context)
    {
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer.test@fpt.edu.vn",
            FullName = "Cán bộ Khảo thí Test",
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
            Code = "PRN231",
            Name = "Cross-Platform Backend .NET",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.Courses.Add(course);

        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Cấu trúc đề PRN231",
            IsActive = true
        };
        context.ExamStructures.Add(structure);

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "Kỳ thi chính thức PRN231",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        context.OfficialExamSessions.Add(session);

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 Sáng",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2), // Shift has not ended in clock
            ProctorUserId = officer.Id,
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = false,
            MaxSeats = 24
        };
        context.RealExamSessionShifts.Add(shift);
        await context.SaveChangesAsync();

        return (officer, shift);
    }

    [Fact]
    public async Task UpdateStatus_ScheduledToInProgress_SucceedsAndSyncsExamPublished()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        var handler = new UpdateExamShiftStatusCommandHandler(context);
        var result = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift.Id, "in_progress"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("in_progress");

        var updatedShift = await context.RealExamSessionShifts.FindAsync(shift.Id);
        updatedShift!.Status.Should().Be("in_progress");
        updatedShift.IsExamPublished.Should().BeTrue();
        updatedShift.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStatus_InProgressToCompleted_Succeeds()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        shift.Status = ExamShiftWindow.InProgress;
        shift.IsExamPublished = true;
        shift.PublishedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var handler = new UpdateExamShiftStatusCommandHandler(context);
        var result = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift.Id, "completed"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("completed");

        var updatedShift = await context.RealExamSessionShifts.FindAsync(shift.Id);
        updatedShift!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task UpdateStatus_CompletedToScheduled_FailsConflict()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        shift.Status = ExamShiftWindow.Completed;
        await context.SaveChangesAsync();

        var handler = new UpdateExamShiftStatusCommandHandler(context);
        var result = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift.Id, "scheduled"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT");
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_FailsBadRequest()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        var handler = new UpdateExamShiftStatusCommandHandler(context);
        var result = await handler.Handle(
            new UpdateExamShiftStatusCommand(officer.Id, officer.Role, shift.Id, "unknown_status"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("BAD_REQUEST");
    }

    [Fact]
    public async Task UpdateStatus_NonOfficerOrAdmin_IsForbidden()
    {
        await using var context = CreateDbContext();
        var (_, shift) = await SeedOfficerAndShift(context);

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Sinh viên",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var handler = new UpdateExamShiftStatusCommandHandler(context);
        var result = await handler.Handle(
            new UpdateExamShiftStatusCommand(student.Id, student.Role, shift.Id, "in_progress"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Fact]
    public async Task PublishGrades_WhenShiftIsCompleted_SucceedsEvenBeforeEndTime()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        // Shift is marked completed by examination officer early
        shift.Status = ExamShiftWindow.Completed;
        shift.EndTime = DateTime.UtcNow.AddHours(2); // EndTime is still in the future!

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.score@fpt.edu.vn",
            FullName = "Sinh viên Chấm Xong",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Audited,
            IsLocked = false
        };
        context.StudentExamTickets.Add(ticket);

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = shift.Session.CourseId,
            Title = "Câu hỏi thi",
            Content = "Nội dung câu hỏi thi PRN231",
            SampleAnswer = "Đáp án mẫu",
            IsActive = true
        };
        context.ExamQuestions.Add(question);

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            AiScore = 8.5m,
            FinalScore = 8.5m,
            GradingStatus = "audited",
            SubmittedAt = DateTime.UtcNow
        };
        ticket.Submissions.Add(submission);
        context.ExamQuestionSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var result = await handler.Handle(
            new PublishGradesCommand(shift.Id, officer.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPublished.Should().Be(1);

        var refreshedTicket = await context.StudentExamTickets.FindAsync(ticket.Id);
        refreshedTicket!.Status.Should().Be(ExamTicketStatus.Published);
        refreshedTicket.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task PublishGrades_WhenShiftIsScheduled_FailsShiftNotEnded()
    {
        await using var context = CreateDbContext();
        var (officer, shift) = await SeedOfficerAndShift(context);

        shift.Status = ExamShiftWindow.Scheduled;

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.fail@fpt.edu.vn",
            FullName = "Sinh viên test fail",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Audited
        };
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var result = await handler.Handle(
            new PublishGradesCommand(shift.Id, officer.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(PublishGradesErrors.ShiftNotEnded);
    }
}
