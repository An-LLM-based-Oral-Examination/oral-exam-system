using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams.Commands.PublishGrades;

public class PublishGradesCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Lecturer, RealExamSessionShift Shift, List<StudentExamTicket> Tickets)> SeedShiftDataAsync(
        OralExamDbContext context,
        int ticketCount = 3,
        bool allScored = true)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.exam@fpt.edu.vn",
            FullName = "ThS. Giảng Viên Khảo Thí",
            Role = UserRole.Lecturer,
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
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Kỳ thi Vấn đáp Chính thức Lab PRN231",
            ExamDate = new DateOnly(2026, 10, 20),
            Status = "in_progress"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Sáng (Phòng Lab 301)",
            RoomLab = "Lab 301",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "in_progress"
        };

        context.Users.Add(lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);

        var tickets = new List<StudentExamTicket>();

        for (int i = 1; i <= ticketCount; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student{i}@fpt.edu.vn",
                FullName = $"Sinh Viên Thí Sinh {i}",
                StudentCode = $"SE17000{i}",
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
                IpAddress = $"192.168.1.{100 + i}",
                Status = "AUDITED",
                IsLocked = false
            };

            // Nếu allScored = true, tất cả đều có điểm. Nếu false, sinh viên cuối cùng chưa có điểm.
            bool isCurrentScored = allScored || (i < ticketCount);

            var submission = new ExamQuestionSubmission
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                ExamQuestionId = Guid.NewGuid(),
                AudioR2Url = $"https://pub-r2.oralexam.fpt.edu.vn/exams/{shift.Id}/{i}_{student.StudentCode}.webm",
                AudioHashSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                TranscriptWhisper = "Thí sinh trình bày về Clean Architecture và Repository Pattern.",
                AiScore = isCurrentScored ? 8.5m : null,
                FinalScore = isCurrentScored ? 8.5m : null,
                GradingStatus = isCurrentScored ? "audited" : "pending"
            };

            ticket.Submissions.Add(submission);
            tickets.Add(ticket);
            context.StudentExamTickets.Add(ticket);
            context.ExamQuestionSubmissions.Add(submission);
        }

        await context.SaveChangesAsync();

        return (lecturer, shift, tickets);
    }

    [Fact(DisplayName = "1. HARD VERIFICATION GATE: Công bố điểm THÀNH CÔNG khi 100% sinh viên ca thi có điểm")]
    public async Task Handle_Should_Publish_Successfully_When_100_Percent_Students_Scored()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftDataAsync(context, ticketCount: 3, allScored: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ShiftId.Should().Be(shift.Id);
        result.Value.TotalPublished.Should().Be(3);
        result.Value.Message.Should().Contain("Đã công bố điểm thành công cho toàn bộ 3/3 sinh viên");

        // Kiểm tra One-Way Lock đã được kích hoạt trong CSDL
        var updatedTickets = await context.StudentExamTickets
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync();

        updatedTickets.Should().HaveCount(3);
        updatedTickets.Should().OnlyContain(t => t.IsLocked == true);
        updatedTickets.Should().OnlyContain(t => t.Status == ExamTicketStatus.Published);

        var updatedShift = await context.RealExamSessionShifts.FindAsync(shift.Id);
        updatedShift!.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "2. HARD VERIFICATION GATE: Chặn công bố điểm khi CÒN SINH VIÊN THIẾU ĐIỂM")]
    public async Task Handle_Should_Fail_When_Any_Student_Lacks_Score()
    {
        using var context = CreateDbContext();
        // 3 sinh viên nhưng sinh viên thứ 3 chưa có điểm (allScored = false)
        var (lecturer, shift, tickets) = await SeedShiftDataAsync(context, ticketCount: 3, allScored: false);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Không thể công bố điểm. Còn sinh viên chưa có điểm hoàn chỉnh.");

        // Kiểm tra không có vé nào bị khóa sai
        var untouchedTickets = await context.StudentExamTickets
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync();

        untouchedTickets.Should().OnlyContain(t => t.IsLocked == false);
        untouchedTickets.Should().OnlyContain(t => t.Status != ExamTicketStatus.Published);
    }

    [Fact(DisplayName = "3. Handle trả về Failure khi ca thi không tồn tại")]
    public async Task Handle_Should_Fail_When_Shift_Not_Found()
    {
        using var context = CreateDbContext();
        var (lecturer, _, _) = await SeedShiftDataAsync(context, ticketCount: 1, allScored: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(Guid.NewGuid(), lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi không tồn tại trong hệ thống.");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi giảng viên không tồn tại")]
    public async Task Handle_Should_Fail_When_Lecturer_Not_Found()
    {
        using var context = CreateDbContext();
        var (_, shift, _) = await SeedShiftDataAsync(context, ticketCount: 1, allScored: true);

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Giảng viên không tồn tại trong hệ thống.");
    }

    [Fact(DisplayName = "5. Handle trả về Failure khi người thực hiện không có quyền (ví dụ: student)")]
    public async Task Handle_Should_Fail_When_User_Has_No_Permission()
    {
        using var context = CreateDbContext();
        var (_, shift, tickets) = await SeedShiftDataAsync(context, ticketCount: 1, allScored: true);

        // Lấy student của vé thi để thử publish
        var studentId = tickets[0].StudentId;

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, studentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Người dùng không có quyền công bố điểm ca thi");
    }

    [Fact(DisplayName = "6. Handle trả về Failure khi ca thi không có vé thi nào")]
    public async Task Handle_Should_Fail_When_Shift_Has_No_Tickets()
    {
        using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.empty@fpt.edu.vn",
            FullName = "GV Empty",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var emptyShift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Thi Rỗng",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Status = "scheduled"
        };

        context.Users.Add(lecturer);
        context.RealExamSessionShifts.Add(emptyShift);
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(emptyShift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi không có vé thi nào để công bố điểm.");
    }

    [Fact(DisplayName = "7. Handle trả về Failure khi ca thi đã được công bố điểm trước đó (Idempotent Guard)")]
    public async Task Handle_Should_Fail_When_Shift_Already_Locked()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftDataAsync(context, ticketCount: 2, allScored: true);

        // Khóa trước cả 2 vé thi
        foreach (var t in tickets)
        {
            t.IsLocked = true;
            t.Status = ExamTicketStatus.Published;
        }
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Ca thi này đã được công bố điểm trước đó và đã bị khóa một chiều.");
    }

    [Fact(DisplayName = "8. Sinh viên có LecturerAudit thẩm định ghi nhận điểm thành công ngay cả khi submission chưa có điểm")]
    public async Task Handle_Should_Pass_When_Student_Scored_Via_LecturerAudit()
    {
        using var context = CreateDbContext();
        var (lecturer, shift, tickets) = await SeedShiftDataAsync(context, ticketCount: 1, allScored: false);

        var ticket = tickets[0];
        // Thêm LecturerAudit cho vé thi này
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = lecturer.Id,
            OriginalAiScore = 0m,
            AuditedScore = 9.00m,
            OverrideReason = "Giảng viên đã nghe lại audio và chấm 9.00 điểm trực tiếp.",
            IsLocked = false
        };
        context.LecturerAudits.Add(audit);
        ticket.LecturerAudit = audit;
        await context.SaveChangesAsync();

        var handler = new PublishGradesCommandHandler(context);
        var command = new PublishGradesCommand(shift.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPublished.Should().Be(1);

        var updatedAudit = await context.LecturerAudits.FindAsync(audit.Id);
        updatedAudit!.IsLocked.Should().BeTrue();
    }
}
