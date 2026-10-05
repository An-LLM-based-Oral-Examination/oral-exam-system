using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals.Handlers;

public class CreateAppealCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Student, User DeptHead, StudentExamTicket Ticket, ExamQuestionSubmission Submission)> SeedDataAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.test@fpt.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            StudentCode = "SE170123",
            Role = UserRole.Student,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead.test@fpt.edu.vn",
            FullName = "TS. Trưởng Bộ Môn KTPM",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = true,
            IsActive = true
        };

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric PRN231 Barem 10.0",
            TotalMaxScore = 10.00m,
            IsActive = true
        };

        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi kiến trúc Clean Architecture",
            Content = "Phân tích 4 tầng trong Clean Architecture .NET 8.",
            Difficulty = "hard",
            BloomLevel = BloomLevel.Analyze,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Ca thi Vấn đáp Chính thức PRN231",
            ExamDate = new DateOnly(2026, 10, 10),
            Status = "grading"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Sáng",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "completed"
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 12,
            IpAddress = "192.168.1.102",
            Status = ExamTicketStatus.Published,
            IsLocked = false
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            TranscriptWhisper = "Clean Architecture gồm 4 tầng Domain, Application, Infrastructure và API.",
            AiScore = 6.50m,
            FinalScore = 6.50m,
            GradingStatus = "audited"
        };

        context.Users.AddRange(student, deptHead);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.ExamQuestions.Add(question);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);

        await context.SaveChangesAsync();

        return (student, deptHead, ticket, submission);
    }

    [Fact(DisplayName = "1. Handle nộp đơn phúc khảo thành công và tự động gán Trưởng Bộ Môn (department_head)")]
    public async Task Handle_Should_Create_Appeal_And_Auto_Assign_DepartmentHead()
    {
        using var context = CreateDbContext();
        var (student, deptHead, ticket, submission) = await SeedDataAsync(context);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Em đã phân tích đầy đủ 4 tầng Clean Architecture nhưng chỉ được 6.5 điểm, kính mong thầy cô chấm phúc khảo."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TicketId.Should().Be(ticket.Id);
        result.Value.StudentId.Should().Be(student.Id);
        result.Value.Status.Should().Be(AppealStatus.Pending);
        result.Value.AssignedTo.Should().Be(deptHead.Id);
        result.Value.AssignedToName.Should().Be(deptHead.FullName);
        result.Value.OriginalScore.Should().Be(6.50m);

        var savedAppeal = await context.AppealRequests.FirstOrDefaultAsync(a => a.Id == result.Value.Id);
        savedAppeal.Should().NotBeNull();
        savedAppeal!.Status.Should().Be(AppealStatus.Pending);
        savedAppeal.AssignedTo.Should().Be(deptHead.Id);
    }

    [Fact(DisplayName = "2. Handle trả về Failure khi vé thi không tồn tại")]
    public async Task Handle_Should_Fail_When_Ticket_Not_Found()
    {
        using var context = CreateDbContext();
        var (student, _, _, _) = await SeedDataAsync(context);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: Guid.NewGuid(),
            SubmissionId: null,
            Reason: "Đơn phúc khảo với mã vé thi không tồn tại."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Vé thi không tồn tại");
    }

    [Fact(DisplayName = "3. Handle trả về Failure khi sinh viên không sở hữu vé thi này")]
    public async Task Handle_Should_Fail_When_Student_Does_Not_Own_Ticket()
    {
        using var context = CreateDbContext();
        var (_, _, ticket, _) = await SeedDataAsync(context);

        var otherStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@fpt.edu.vn",
            FullName = "Sinh Viên Khác",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(otherStudent);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: otherStudent.Id,
            TicketId: ticket.Id,
            SubmissionId: null,
            Reason: "Cố tình nộp đơn phúc khảo cho vé thi của bạn khác."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Sinh viên không sở hữu vé thi này");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi đã có đơn phúc khảo đang PENDING")]
    public async Task Handle_Should_Fail_When_Pending_Appeal_Already_Exists()
    {
        using var context = CreateDbContext();
        var (student, deptHead, ticket, submission) = await SeedDataAsync(context);

        var existingAppeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Reason = "Đơn phúc khảo lần 1",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(existingAppeal);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp lại đơn phúc khảo lần 2 trong khi đơn 1 đang chờ duyệt."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã tồn tại và đang chờ xử lý");
    }

    [Fact(DisplayName = "4b. Handle trả về Failure khi đã có đơn phúc khảo đang IN_REVIEW")]
    public async Task Handle_Should_Fail_When_InReview_Appeal_Already_Exists()
    {
        using var context = CreateDbContext();
        var (student, deptHead, ticket, submission) = await SeedDataAsync(context);

        var existingAppeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Reason = "Đơn phúc khảo lần 1 đang được thẩm định",
            Status = AppealStatus.InReview,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(existingAppeal);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp lại đơn phúc khảo lần 2 trong khi đơn 1 đang ở trạng thái IN_REVIEW."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã tồn tại và đang chờ xử lý");
    }

    [Fact(DisplayName = "5. Handle tự động gán Quản trị viên (admin) nếu hệ thống chưa có Trưởng Bộ Môn")]
    public async Task Handle_Should_Fallback_To_Admin_When_No_DepartmentHead()
    {
        using var context = CreateDbContext();
        var (student, deptHead, ticket, _) = await SeedDataAsync(context);

        // Xóa hoặc đổi role deptHead thành lecturer
        deptHead.Role = UserRole.Lecturer;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin.test@fpt.edu.vn",
            FullName = "Quản Trị Viên Hệ Thống",
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: null,
            Reason: "Kính mong phúc khảo tổng thể điểm bài thi vấn đáp."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(admin.Id);
        result.Value.AssignedToName.Should().Be(admin.FullName);
    }

    [Theory(DisplayName = "6. Handle trả về Failure khi vé thi chưa ở trạng thái PUBLISHED hoặc LOCKED")]
    [InlineData("SCHEDULED")]
    [InlineData("IN_PROGRESS")]
    [InlineData("SUBMITTED")]
    [InlineData("AI_GRADED")]
    [InlineData("AUDITED")]
    public async Task Handle_Should_Fail_When_Ticket_Status_Is_Not_Published_Or_Locked(string unreleasedStatus)
    {
        using var context = CreateDbContext();
        var (student, _, ticket, submission) = await SeedDataAsync(context);

        ticket.Status = unreleasedStatus;
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp đơn phúc khảo khi ca thi chưa được công bố điểm chính thức."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("PUBLISHED hoặc LOCKED");
    }

    [Fact(DisplayName = "7. Handle nộp đơn phúc khảo thành công khi vé thi ở trạng thái LOCKED")]
    public async Task Handle_Should_Succeed_When_Ticket_Status_Is_Locked()
    {
        using var context = CreateDbContext();
        var (student, _, ticket, submission) = await SeedDataAsync(context);

        ticket.Status = ExamTicketStatus.Locked;
        ticket.IsLocked = true;
        await context.SaveChangesAsync();

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(
            StudentId: student.Id,
            TicketId: ticket.Id,
            SubmissionId: submission.Id,
            Reason: "Nộp đơn phúc khảo sau khi điểm thi đã bị khóa một chiều (LOCKED)."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TicketId.Should().Be(ticket.Id);
    }
}
