using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Appeals.Handlers;

public class ReviewAppealDecisionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(User Student, User DeptHead, User Lecturer, StudentExamTicket Ticket, ExamQuestionSubmission Submission, AppealRequest Appeal)> SeedDataAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.review@fpt.edu.vn",
            FullName = "Nguyễn Phúc Khảo",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead.review@fpt.edu.vn",
            FullName = "TS. Trưởng Bộ Môn Xét Duyệt",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.review@fpt.edu.vn",
            FullName = "ThS. Giảng Viên Bình Thường",
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
            Name = ".NET Architecture",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };

        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = Guid.NewGuid(),
            Title = "Ca thi Lab PRN231",
            ExamDate = new DateOnly(2026, 10, 10),
            Status = "concluded"
        };

        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-2),
            Status = "completed"
        };

        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = "PUBLISHED",
            IsLocked = true
        };

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Transcript câu trả lời thi vấn đáp",
            AiScore = 5.0m,
            FinalScore = 5.0m,
            GradingStatus = "audited"
        };

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            SubmissionId = submission.Id,
            Reason = "Kính mong thầy cô xem xét lại câu trả lời",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id,
            OriginalScore = 5.0m,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        context.Users.AddRange(student, deptHead, lecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.AppealRequests.Add(appeal);

        await context.SaveChangesAsync();

        return (student, deptHead, lecturer, ticket, submission, appeal);
    }

    [Fact(DisplayName = "1. Handle phê duyệt (APPROVED) đơn phúc khảo thành công và cập nhật điểm mới")]
    public async Task Handle_Should_Approve_Appeal_And_Update_Score()
    {
        using var context = CreateDbContext();
        var (_, deptHead, _, _, submission, appeal) = await SeedDataAsync(context);

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(
            AppealId: appeal.Id,
            ReviewerId: deptHead.Id,
            Decision: AppealStatus.Approved,
            ProposedScore: 8.5m,
            ReviewNotes: "Sau khi nghe lại ghi âm phòng thi, thí sinh trả lời rõ ý và chuẩn xác. Điều chỉnh điểm lên 8.5."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AppealStatus.Approved);
        result.Value.Decision.Should().Be(AppealStatus.Approved);
        result.Value.ProposedScore.Should().Be(8.5m);
        result.Value.ReviewedBy.Should().Be(deptHead.Id);
        result.Value.ResolvedAt.Should().NotBeNull();

        // Kiểm tra database
        var updatedAppeal = await context.AppealRequests.FindAsync(appeal.Id);
        updatedAppeal!.Status.Should().Be(AppealStatus.Approved);
        updatedAppeal.ProposedScore.Should().Be(8.5m);

        var updatedSubmission = await context.ExamQuestionSubmissions.FindAsync(submission.Id);
        updatedSubmission!.FinalScore.Should().Be(8.5m);
    }

    [Fact(DisplayName = "2. Handle từ chối (REJECTED) đơn phúc khảo thành công")]
    public async Task Handle_Should_Reject_Appeal_Successfully()
    {
        using var context = CreateDbContext();
        var (_, deptHead, _, _, _, appeal) = await SeedDataAsync(context);

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(
            AppealId: appeal.Id,
            ReviewerId: deptHead.Id,
            Decision: AppealStatus.Rejected,
            ProposedScore: null,
            ReviewNotes: "Hội đồng đã nghe lại băng ghi âm: Thí sinh không trả lời đúng trọng tâm câu hỏi. Giữ nguyên điểm."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AppealStatus.Rejected);
        result.Value.Decision.Should().Be(AppealStatus.Rejected);
        result.Value.ProposedScore.Should().BeNull();
        result.Value.ResolvedAt.Should().NotBeNull();

        var updatedAppeal = await context.AppealRequests.FindAsync(appeal.Id);
        updatedAppeal!.Status.Should().Be(AppealStatus.Rejected);
    }

    [Fact(DisplayName = "3. Handle trả về Failure khi đơn phúc khảo không tồn tại")]
    public async Task Handle_Should_Fail_When_Appeal_Not_Found()
    {
        using var context = CreateDbContext();
        var (_, deptHead, _, _, _, _) = await SeedDataAsync(context);

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(
            AppealId: Guid.NewGuid(),
            ReviewerId: deptHead.Id,
            Decision: AppealStatus.Approved,
            ProposedScore: 9.0m,
            ReviewNotes: "Ghi chú thẩm định hợp lệ."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Đơn phúc khảo không tồn tại");
    }

    [Fact(DisplayName = "4. Handle trả về Failure khi đơn phúc khảo đã có quyết định trước đó")]
    public async Task Handle_Should_Fail_When_Appeal_Already_Resolved()
    {
        using var context = CreateDbContext();
        var (_, deptHead, _, _, _, appeal) = await SeedDataAsync(context);

        appeal.Status = AppealStatus.Approved;
        appeal.Decision = AppealStatus.Approved;
        appeal.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(
            AppealId: appeal.Id,
            ReviewerId: deptHead.Id,
            Decision: AppealStatus.Rejected,
            ProposedScore: null,
            ReviewNotes: "Cố tình thẩm định lại đơn đã có quyết định."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã có quyết định xử lý trước đó");
    }

    [Fact(DisplayName = "5. Handle trả về Failure khi người thẩm định không có quyền (ví dụ Giảng viên thông thường)")]
    public async Task Handle_Should_Fail_When_Reviewer_Is_Not_DepartmentHead_Or_Admin()
    {
        using var context = CreateDbContext();
        var (_, _, lecturer, _, _, appeal) = await SeedDataAsync(context);

        var handler = new ReviewAppealDecisionCommandHandler(context);
        var command = new ReviewAppealDecisionCommand(
            AppealId: appeal.Id,
            ReviewerId: lecturer.Id,
            Decision: AppealStatus.Approved,
            ProposedScore: 8.0m,
            ReviewNotes: "Giảng viên cố tình tự ý thẩm định và sửa điểm đơn phúc khảo."
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có quyền thẩm định");
    }

    [Fact(DisplayName = "BE-12: Trưởng bộ môn phân công giảng viên và chuyển đơn sang IN_REVIEW")]
    public async Task AssignLecturer_Should_Move_Pending_Appeal_To_InReview()
    {
        using var context = CreateDbContext();
        var (_, departmentHead, lecturer, _, _, appeal) = await SeedDataAsync(context);

        var handler = new AssignAppealLecturerCommandHandler(context);
        var result = await handler.Handle(
            new AssignAppealLecturerCommand(appeal.Id, departmentHead.Id, lecturer.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(AppealStatus.InReview);
        result.Value.AssignedTo.Should().Be(lecturer.Id);
        appeal.ReviewedBy.Should().Be(departmentHead.Id);
    }

    [Fact(DisplayName = "BE-12: Chỉ giảng viên được phân công mới cập nhật được điểm chấm lại")]
    public async Task Regrade_Should_Update_Score_Only_For_Assigned_Lecturer()
    {
        using var context = CreateDbContext();
        var (_, departmentHead, lecturer, _, submission, appeal) = await SeedDataAsync(context);
        var assignHandler = new AssignAppealLecturerCommandHandler(context);
        await assignHandler.Handle(
            new AssignAppealLecturerCommand(appeal.Id, departmentHead.Id, lecturer.Id),
            CancellationToken.None);

        var otherLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "other.lecturer@fpt.edu.vn",
            FullName = "Giảng viên không được phân công",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(otherLecturer);
        await context.SaveChangesAsync();

        var handler = new RegradeAppealCommandHandler(context);
        var forbidden = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, otherLecturer.Id, 8m, "Không có quyền chấm đơn này."),
            CancellationToken.None);
        forbidden.IsSuccess.Should().BeFalse();
        submission.FinalScore.Should().Be(5m);

        var result = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, lecturer.Id, 8.25m, "Đã nghe lại audio và đối chiếu transcript."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(AppealStatus.Approved);
        submission.FinalScore.Should().Be(8.25m);
    }

    [Fact(DisplayName = "R2: Chấm lại đơn phúc khảo toàn vé (SubmissionId == null, không có LecturerAudit) vượt qua OneWayLockInterceptor và cập nhật FinalScore")]
    public async Task Regrade_WholeTicketAppeal_Without_LecturerAudit_Should_Update_Submissions_Under_OneWayLock()
    {
        var dbName = Guid.NewGuid().ToString();
        var seedOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        Guid appealId;
        Guid lecturerId;
        Guid submissionId;

        using (var seedContext = new OralExamDbContext(seedOptions))
        {
            var (_, deptHead, lecturer, ticket, submission, appeal) = await SeedDataAsync(seedContext);
            appeal.SubmissionId = null; // Đơn phúc khảo toàn vé thi (Nhóm 2 tin cậy cao, không có LecturerAudit)
            appeal.Status = AppealStatus.InReview;
            appeal.AssignedTo = lecturer.Id;
            appeal.ReviewedBy = deptHead.Id;
            await seedContext.SaveChangesAsync();

            appealId = appeal.Id;
            lecturerId = lecturer.Id;
            submissionId = submission.Id;
        }

        var lockedOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .AddInterceptors(new OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor())
            .Options;

        using var activeContext = new OralExamDbContext(lockedOptions);
        var handler = new RegradeAppealCommandHandler(activeContext);
        var result = await handler.Handle(
            new RegradeAppealCommand(appealId, lecturerId, 8.75m, "Chấm lại phúc khảo toàn vé thi Nhóm 2 sau khi nghe lại file ghi âm R2."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(AppealStatus.Approved);
        result.Value.ProposedScore.Should().Be(8.75m);

        var updatedSubmission = await activeContext.ExamQuestionSubmissions.FindAsync(submissionId);
        updatedSubmission!.FinalScore.Should().Be(8.75m);
    }

    [Fact(DisplayName = "R3: Chấm lại đơn phúc khảo 1 câu đơn lẻ trên vé thi nhiều câu sẽ tính lại trung bình cộng cho LecturerAudit.AuditedScore")]
    public async Task Regrade_SingleSubmissionAppeal_On_MultiQuestionTicket_Should_Recalculate_Average_AuditedScore()
    {
        using var context = CreateDbContext();
        var (_, deptHead, lecturer, ticket, sub1, appeal) = await SeedDataAsync(context);

        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            TranscriptWhisper = "Câu trả lời số 2 đạt điểm 8.0",
            AiScore = 8.0m,
            FinalScore = 8.0m,
            GradingStatus = "audited"
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = lecturer.Id,
            OriginalAiScore = 6.50m,
            AuditedScore = 6.50m,
            OverrideReason = "Điểm trung bình ban đầu (5.0 + 8.0) / 2 = 6.50",
            IsLocked = true,
            AuditedAt = DateTime.UtcNow.AddHours(-2)
        };
        context.ExamQuestionSubmissions.Add(sub2);
        context.LecturerAudits.Add(audit);

        appeal.Status = AppealStatus.InReview;
        appeal.AssignedTo = lecturer.Id;
        appeal.ReviewedBy = deptHead.Id;
        await context.SaveChangesAsync();

        var handler = new RegradeAppealCommandHandler(context);
        // Phúc khảo câu 1 từ 5.0 lên 7.0 -> trung bình vé thi phải là (7.0 + 8.0) / 2 = 7.50
        var result = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, lecturer.Id, 7.0m, "Đã nghe lại câu 1 và nâng điểm câu 1 lên 7.0."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub1.FinalScore.Should().Be(7.0m);
        sub2.FinalScore.Should().Be(8.0m);
        audit.AuditedScore.Should().Be(7.50m);
    }
}
