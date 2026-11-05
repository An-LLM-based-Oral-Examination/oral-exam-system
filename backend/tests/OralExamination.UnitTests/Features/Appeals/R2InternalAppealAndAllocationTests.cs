using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals;

public class R2InternalAppealAndAllocationTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Student, User DeptHead, User Officer, User Lecturer, StudentExamTicket Ticket, ExamQuestionSubmission Submission, Course Course)>
        SeedBaseDataAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fe.edu.vn",
            FullName = "Nguyễn Văn Sinh Viên",
            StudentCode = "SE170001",
            Role = UserRole.Student,
            IsActive = true
        };
        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead@fe.edu.vn",
            FullName = "Trưởng Bộ Môn KTPM",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fe.edu.vn",
            FullName = "Cán bộ Khảo thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fe.edu.vn",
            FullName = "Giảng viên Chấm lại",
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
            Name = "Cross-Platform Back-End with .NET",
            SemesterId = semester.Id,
            DepartmentHeadUserId = deptHead.Id,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Kỳ thi Vấn đáp PRN231",
            ExamDate = new DateOnly(2026, 10, 10),
            Status = "published"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Sáng",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-1),
            ExaminerUserId = Guid.NewGuid(), // Initial examiner khác với lecturer
            Status = "completed"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Clean Architecture 4 tầng",
            AiScore = 6.0m,
            FinalScore = 6.0m,
            GradingStatus = "audited"
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = shift.ExaminerUserId.Value,
            OriginalAiScore = 6.0m,
            AuditedScore = 6.0m,
            OverrideReason = "Điểm ban đầu",
            IsLocked = true
        };

        context.Users.AddRange(student, deptHead, officer, lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        return (student, deptHead, officer, lecturer, ticket, submission, course);
    }

    [Fact(DisplayName = "R2-01: Sinh viên nộp đơn phúc khảo tạo thông báo in-app cho Cán bộ Khảo thí và Trưởng Bộ Môn")]
    public async Task CreateAppeal_Should_Create_InApp_Notifications_For_ExaminationOfficers_And_DepartmentHead()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, _, ticket, submission, _) = await SeedBaseDataAsync(context);

        var handler = new CreateAppealCommandHandler(context);
        var command = new CreateAppealCommand(student.Id, ticket.Id, submission.Id, "Xin phúc khảo câu 1.");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var notifications = await context.Notifications.ToListAsync();
        notifications.Should().HaveCountGreaterOrEqualTo(2);

        // Kiểm tra có thông báo cho Cán bộ Khảo thí
        notifications.Should().Contain(n => n.UserId == officer.Id && n.Type == "APPEAL_SUBMITTED");

        // Kiểm tra có thông báo cho Trưởng Bộ Môn
        notifications.Should().Contain(n => n.UserId == deptHead.Id && n.Type == "APPEAL_SUBMITTED");
    }

    [Fact(DisplayName = "R2-02: Cán bộ Khảo thí phân công giảng viên chấm lại thành công")]
    public async Task AssignAppealLecturer_ExaminationOfficer_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, lecturer, ticket, submission, _) = await SeedBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            SubmissionId = submission.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new AssignAppealLecturerCommandHandler(context);
        var command = new AssignAppealLecturerCommand(appeal.Id, officer.Id, lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AppealStatus.InReview);
        result.Value.AssignedTo.Should().Be(lecturer.Id);
        result.Value.ReviewedBy.Should().Be(officer.Id);
    }

    [Fact(DisplayName = "R2-03: Phân công giảng viên chấm lại trùng Giám khảo chấm ban đầu bị từ chối (Ràng buộc Độc lập)")]
    public async Task AssignAppealLecturer_MatchingInitialExaminer_Should_Fail_Independent_Constraint()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, _, ticket, submission, _) = await SeedBaseDataAsync(context);

        // Tạo giảng viên chính là Giám khảo ban đầu của shift
        var initialExaminerId = ticket.Shift!.ExaminerUserId!.Value;
        var initialExaminer = new User
        {
            Id = initialExaminerId,
            Email = "initial.examiner@fe.edu.vn",
            FullName = "Giám Khảo Ban Đầu",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(initialExaminer);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            SubmissionId = submission.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new AssignAppealLecturerCommandHandler(context);
        // Cố tình phân công chính Giám khảo ban đầu
        var command = new AssignAppealLecturerCommand(appeal.Id, officer.Id, initialExaminer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không được trùng với Giám khảo");
    }

    [Fact(DisplayName = "R2-04: Thuật toán đề xuất phân bổ cân bằng định mức (30 bài cho 3 GV chia 10-10-10, không dồn 24-6-0)")]
    public async Task GetAppealAllocationProposal_BalancedAllocation_StandardCapacity()
    {
        await using var context = CreateDbContext();

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab Cân bằng tải",
            ExamDate = new DateOnly(2026, 10, 10)
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            ExaminerUserId = Guid.NewGuid() // Giám khảo khác 3 GV dưới
        };
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);

        // Tạo 3 Giảng viên hoạt động
        var lecturers = Enumerable.Range(1, 3).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"gv{i}@fe.edu.vn",
            FullName = $"Giảng viên {i}",
            Role = UserRole.Lecturer,
            IsActive = true
        }).ToList();
        context.Users.AddRange(lecturers);

        // Tạo 30 đơn phúc khảo Pending
        for (int i = 1; i <= 30; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv{i}@fe.edu.vn",
                FullName = $"Sinh viên {i}",
                StudentCode = $"SE{170000 + i}",
                Role = UserRole.Student,
                IsActive = true
            };
            var ticket = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                SeatNumber = i,
                Status = ExamTicketStatus.Published
            };
            var appeal = new AppealRequest
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                StudentId = student.Id,
                SessionId = session.Id,
                Reason = $"Phúc khảo #{i}",
                Status = AppealStatus.Pending,
                AssignedTo = lecturers[0].Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            };
            context.Users.Add(student);
            context.StudentExamTickets.Add(ticket);
            context.AppealRequests.Add(appeal);
        }
        await context.SaveChangesAsync();

        var handler = new GetAppealAllocationProposalQueryHandler(context);
        var result = await handler.Handle(new GetAppealAllocationProposalQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAppeals.Should().Be(30);
        result.Value.TotalLecturers.Should().Be(3);
        result.Value.ExceedsStandardQuota.Should().BeFalse();

        // Kiểm tra phân bổ cân bằng: Mỗi GV nhận đúng 10 bài (30 / 3)
        result.Value.LecturerSummaries.Should().HaveCount(3);
        result.Value.LecturerSummaries.Should().OnlyContain(s => s.AssignedCount == 10);
    }

    [Fact(DisplayName = "R2-05: Thuật toán đề xuất phân bổ chia đều khi vượt định mức (50 bài cho 2 GV chia 25-25)")]
    public async Task GetAppealAllocationProposal_ExceedsStandardQuota_Divides_Evenly()
    {
        await using var context = CreateDbContext();

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab Vượt Định Mức",
            ExamDate = new DateOnly(2026, 10, 10)
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            ExaminerUserId = Guid.NewGuid()
        };
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);

        // Tạo 2 Giảng viên (Dung lượng chuẩn = 2 * 24 = 48 bài)
        var lecturers = Enumerable.Range(1, 2).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"gv_overload_{i}@fe.edu.vn",
            FullName = $"Giảng viên Overload {i}",
            Role = UserRole.Lecturer,
            IsActive = true
        }).ToList();
        context.Users.AddRange(lecturers);

        // Tạo 50 đơn phúc khảo Pending (50 > 48)
        for (int i = 1; i <= 50; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv_overload_{i}@fe.edu.vn",
                FullName = $"Sinh viên {i}",
                StudentCode = $"SE{180000 + i}",
                Role = UserRole.Student,
                IsActive = true
            };
            var ticket = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                SeatNumber = (i % 40) + 1,
                Status = ExamTicketStatus.Published
            };
            var appeal = new AppealRequest
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                StudentId = student.Id,
                SessionId = session.Id,
                Reason = $"Phúc khảo #{i}",
                Status = AppealStatus.Pending,
                AssignedTo = lecturers[0].Id,
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            };
            context.Users.Add(student);
            context.StudentExamTickets.Add(ticket);
            context.AppealRequests.Add(appeal);
        }
        await context.SaveChangesAsync();

        var handler = new GetAppealAllocationProposalQueryHandler(context);
        var result = await handler.Handle(new GetAppealAllocationProposalQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAppeals.Should().Be(50);
        result.Value.ExceedsStandardQuota.Should().BeTrue();

        // 50 bài chia cho 2 GV -> mỗi GV nhận 25 bài
        result.Value.LecturerSummaries.Should().HaveCount(2);
        result.Value.LecturerSummaries.Should().OnlyContain(s => s.AssignedCount == 25);
    }

    [Fact(DisplayName = "R2-06: Thuật toán đề xuất phân bổ loại trừ Giám khảo chấm ban đầu")]
    public async Task GetAppealAllocationProposal_IndependentConstraint_Excludes_Initial_Examiner()
    {
        await using var context = CreateDbContext();

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Độc lập",
            ExamDate = new DateOnly(2026, 10, 10)
        };
        context.OfficialExamSessions.Add(session);

        // GV 1 là Giám khảo chấm ban đầu của ca thi này
        var examinerLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "examiner@fe.edu.vn",
            FullName = "ThS. Giám khảo 1",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        // GV 2 là Giảng viên độc lập
        var independentLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "independent@fe.edu.vn",
            FullName = "TS. Giảng viên 2",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.AddRange(examinerLecturer, independentLecturer);

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            ExaminerUserId = examinerLecturer.Id // Gán examiner là GV 1
        };
        context.RealExamSessionShifts.Add(shift);

        // Tạo 5 đơn phúc khảo
        for (int i = 1; i <= 5; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv_indep_{i}@fe.edu.vn",
                FullName = $"Sinh viên {i}",
                StudentCode = $"SE{190000 + i}",
                Role = UserRole.Student,
                IsActive = true
            };
            var ticket = new StudentExamTicket
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                SeatNumber = i,
                Status = ExamTicketStatus.Published
            };
            var appeal = new AppealRequest
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                StudentId = student.Id,
                SessionId = session.Id,
                Reason = "Phúc khảo",
                Status = AppealStatus.Pending,
                AssignedTo = examinerLecturer.Id
            };
            context.Users.Add(student);
            context.StudentExamTickets.Add(ticket);
            context.AppealRequests.Add(appeal);
        }
        await context.SaveChangesAsync();

        var handler = new GetAppealAllocationProposalQueryHandler(context);
        var result = await handler.Handle(new GetAppealAllocationProposalQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // 100% 5 bài phải được gán cho independentLecturer, examinerLecturer nhận 0 bài!
        var examinerSummary = result.Value.LecturerSummaries.First(s => s.LecturerId == examinerLecturer.Id);
        var independentSummary = result.Value.LecturerSummaries.First(s => s.LecturerId == independentLecturer.Id);

        examinerSummary.AssignedCount.Should().Be(0);
        independentSummary.AssignedCount.Should().Be(5);
        result.Value.Items.Should().OnlyContain(item => item.ProposedLecturerId == independentLecturer.Id);
    }

    [Fact(DisplayName = "R2-07: BatchAssignAppeals lưu đồng loạt và chuyển toàn bộ đơn sang IN_REVIEW")]
    public async Task BatchAssignAppeals_ExaminationOfficer_Should_Assign_All_Pending()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, lecturer, ticket, submission, _) = await SeedBaseDataAsync(context);

        var appeal1 = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            SubmissionId = submission.Id,
            Reason = "Phúc khảo 1",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        var appeal2 = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            Reason = "Phúc khảo 2",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.AddRange(appeal1, appeal2);
        await context.SaveChangesAsync();

        var handler = new BatchAssignAppealsCommandHandler(context, new BatchAssignAppealsCommandValidator());
        var command = new BatchAssignAppealsCommand(officer.Id, new List<AppealAssignmentItemDto>
        {
            new() { AppealId = appeal1.Id, LecturerId = lecturer.Id },
            new() { AppealId = appeal2.Id, LecturerId = lecturer.Id }
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAssigned.Should().Be(2);

        var updated1 = await context.AppealRequests.FindAsync(appeal1.Id);
        var updated2 = await context.AppealRequests.FindAsync(appeal2.Id);
        updated1!.Status.Should().Be(AppealStatus.InReview);
        updated1.AssignedTo.Should().Be(lecturer.Id);
        updated2!.Status.Should().Be(AppealStatus.InReview);
        updated2.AssignedTo.Should().Be(lecturer.Id);
    }

    [Fact(DisplayName = "R2-08: Giảng viên chấm lại chỉ nhập ProposedScore và ReviewNotes, không đổi điểm Audit và không đổi Status Approved")]
    public async Task RegradeAppeal_Should_Only_Propose_Score_And_Not_Change_Audit_Score()
    {
        await using var context = CreateDbContext();
        var (student, _, _, lecturer, ticket, submission, _) = await SeedBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            SubmissionId = submission.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.InReview,
            AssignedTo = lecturer.Id,
            OriginalScore = 6.0m
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new RegradeAppealCommandHandler(context);
        var result = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, lecturer.Id, 8.5m, "Giải trình: Bài nói giải thích chi tiết."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProposedScore.Should().Be(8.5m);
        result.Value.Status.Should().Be(AppealStatus.InReview); // Vẫn ở InReview

        // Kiểm tra trong DB: Điểm Audit và Submission chưa hề bị thay đổi
        var auditInDb = await context.LecturerAudits.FirstOrDefaultAsync(a => a.TicketId == ticket.Id);
        auditInDb!.AuditedScore.Should().Be(6.0m);

        var subInDb = await context.ExamQuestionSubmissions.FindAsync(submission.Id);
        subInDb!.FinalScore.Should().Be(6.0m);
    }

    [Fact(DisplayName = "R2-09: Cán bộ Khảo thí phê duyệt quyết định cuối cùng kích hoạt cập nhật điểm chính thức")]
    public async Task ReviewAppealDecision_Approved_Should_Apply_Score_To_Audit_And_Submission()
    {
        await using var context = CreateDbContext();
        var (student, _, officer, lecturer, ticket, submission, _) = await SeedBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = ticket.Shift!.SessionId,
            SubmissionId = submission.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.InReview,
            AssignedTo = lecturer.Id,
            ProposedScore = 8.5m,
            ReviewNotes = "Đề xuất 8.5m",
            OriginalScore = 6.0m
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(appeal.Id, officer.Id, AppealStatus.Approved, 8.5m, "Khảo thí phê duyệt nâng điểm");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AppealStatus.Approved);
        result.Value.Decision.Should().Be(AppealStatus.Approved);

        // Sau khi Khảo thí duyệt, điểm audit và submission được cập nhật
        var auditInDb = await context.LecturerAudits.FirstOrDefaultAsync(a => a.TicketId == ticket.Id);
        auditInDb!.AuditedScore.Should().Be(8.5m);

        var subInDb = await context.ExamQuestionSubmissions.FindAsync(submission.Id);
        subInDb!.FinalScore.Should().Be(8.5m);
    }
}
