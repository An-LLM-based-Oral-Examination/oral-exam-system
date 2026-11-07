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
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.OpenExamShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// ADVERSARIAL CHALLENGER TEST SUITE - MILESTONE 2 (R1 & R2)
/// Kiểm nghiệm đối kháng độc lập các ca biên:
/// 1. Mở ca thi khi ExaminerUserId == ProctorUserId -> Bắt buộc ném validation error / Bad Request.
/// 2. Cấu hình Follow-up thi thật bởi role không phải department_head, admin -> Bị từ chối Forbidden.
/// 3. Phân công Giảng viên chấm lại trùng shift.ExaminerUserId -> Bị chặn độc lập 100%.
/// 4. Thuật toán đề xuất phân công: tổng bài > N * 24 (chia đều) và <= N * 24 (cân bằng tải).
/// 5. Giảng viên gửi kết quả chấm lại: bảo toàn bất biến không đổi audit.AuditedScore và final_score.
/// 6. Cán bộ Khảo thí phê duyệt phúc khảo: kích hoạt ngoại lệ OneWayLockInterceptor cập nhật điểm thành công.
/// </summary>
public class AdversarialM2OfficialExamAndAppealChallengerTests
{
    private static OralExamDbContext CreateDbContextWithLock(string dbName)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new OneWayLockInterceptor())
            .Options;
        return new OralExamDbContext(options);
    }

    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Student, User DeptHead, User Officer, User InitialExaminer, User IndependentLecturer, OfficialExamSession Session, RealExamSessionShift Shift, StudentExamTicket Ticket, ExamQuestionSubmission Submission, LecturerAudit Audit)>
        SeedFullBaseDataAsync(OralExamDbContext context)
    {
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.adv@fe.edu.vn",
            FullName = "Sinh Viên Đối Kháng",
            StudentCode = "SE199999",
            Role = UserRole.Student,
            IsActive = true
        };
        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead.adv@fe.edu.vn",
            FullName = "Trưởng Bộ Môn Đối Kháng",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer.adv@fe.edu.vn",
            FullName = "Cán Bộ Khảo Thí Đối Kháng",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var initialExaminer = new User
        {
            Id = Guid.NewGuid(),
            Email = "examiner.initial@fe.edu.vn",
            FullName = "Giám Khảo Ban Đầu",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var independentLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.independent@fe.edu.vn",
            FullName = "Giảng Viên Độc Lập",
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
            Name = "Cross-Platform .NET",
            SemesterId = semester.Id,
            DepartmentHeadUserId = deptHead.Id,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Kỳ thi Lab Đối Kháng",
            ExamDate = new DateOnly(2026, 10, 10),
            Status = "published"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Lab 101",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            ExaminerUserId = initialExaminer.Id,
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
            TranscriptWhisper = "Adversarial test transcript",
            AiScore = 6.0m,
            FinalScore = 6.0m,
            GradingStatus = "audited"
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = initialExaminer.Id,
            OriginalAiScore = 6.0m,
            AuditedScore = 6.0m,
            OverrideReason = "Chấm ban đầu",
            IsLocked = true
        };

        context.Users.AddRange(student, deptHead, officer, initialExaminer, independentLecturer);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        context.LecturerAudits.Add(audit);
        await context.SaveChangesAsync();

        return (student, deptHead, officer, initialExaminer, independentLecturer, session, shift, ticket, submission, audit);
    }

    // =========================================================================
    // SECTION 1: CA BIÊN R1 - MỞ CA THI & XUNG ĐỘT EXAMINER == PROCTOR
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-01: Thử mở ca thi khi ExaminerUserId == ProctorUserId -> Bắt buộc ném validation error")]
    public async Task OpenExamShift_ExaminerSameAsProctor_Must_Fail_Validation_And_Handler()
    {
        var sameUserId = Guid.NewGuid();
        var validator = new OpenExamShiftCommandValidator();
        var command = new OpenExamShiftCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ca 1 - Phòng Lab AL-L201",
            "AL-L201",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            sameUserId,
            sameUserId);

        // 1. Kiểm tra mức Validator
        var validationResult = await validator.ValidateAsync(command);
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.ErrorMessage.Contains("không được trùng"));

        // 2. Kiểm tra mức Handler khi cố tình bypass validator
        await using var context = CreateDbContext();
        var actor = new User
        {
            Id = command.ActorUserId,
            Email = "officer@fe.edu.vn",
            FullName = "Khảo Thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var user = new User
        {
            Id = sameUserId,
            Email = "dual.role@fe.edu.vn",
            FullName = "Vừa Coi Vừa Chấm",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = command.SessionId,
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };
        context.Users.AddRange(actor, user);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new OpenExamShiftCommandHandler(context, validator);
        var handlerResult = await handler.Handle(command, CancellationToken.None);

        handlerResult.IsSuccess.Should().BeFalse();
        handlerResult.Error.Should().Contain("không được trùng");
    }

    [Theory(DisplayName = "ADV-M2-02: Thử mở ca thi với Examiner không phải Lecturer/DeptHead -> Bị từ chối")]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Proctor)]
    public async Task OpenExamShift_ExaminerInvalidRole_Must_Be_Rejected(string invalidRole)
    {
        await using var context = CreateDbContext();
        var actor = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fe.edu.vn",
            FullName = "Khảo Thí",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fe.edu.vn",
            FullName = "Giám thị",
            Role = UserRole.Proctor,
            IsActive = true
        };
        var invalidExaminer = new User
        {
            Id = Guid.NewGuid(),
            Email = "invalid@fe.edu.vn",
            FullName = "Examiner Không Hợp Lệ",
            Role = invalidRole,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        };
        context.Users.AddRange(actor, proctor, invalidExaminer);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new OpenExamShiftCommandHandler(context, new OpenExamShiftCommandValidator());
        var command = new OpenExamShiftCommand(
            actor.Id,
            session.Id,
            "Ca 1",
            "Lab 1",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            proctor.Id,
            invalidExaminer.Id);

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("phải là giảng viên đang hoạt động");
    }

    // =========================================================================
    // SECTION 2: CA BIÊN R1 - PHÂN QUYỀN CẤU HÌNH FOLLOW-UP THI THẬT
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-03: Role không phải department_head,admin gọi ConfigureOfficialExamFollowUp -> Bị từ chối Forbidden")]
    [InlineData(UserRole.ExaminationOfficer)]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Proctor)]
    public async Task ConfigureOfficialExamFollowUp_UnauthorizedRoles_Must_Be_Forbidden(string unauthorizedRole)
    {
        await using var context = CreateDbContext();
        var unauthorizedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = $"user_{unauthorizedRole}@fe.edu.vn",
            FullName = $"User {unauthorizedRole}",
            Role = unauthorizedRole,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Thi Thật",
            ExamDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))
        };
        context.Users.Add(unauthorizedUser);
        context.OfficialExamSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new ConfigureOfficialExamFollowUpCommandHandler(context, new ConfigureOfficialExamFollowUpCommandValidator());
        var command = new ConfigureOfficialExamFollowUpCommand(unauthorizedUser.Id, session.Id, true, 2);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OfficialExamErrors.Forbidden);
    }

    [Theory(DisplayName = "ADV-M2-04: Cấu hình Follow-up số câu vi phạm biên [1, 5] -> Báo lỗi validator")]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(99)]
    public async Task ConfigureOfficialExamFollowUp_OutOfRange_Must_Fail_Validation(int invalidQuestions)
    {
        var validator = new ConfigureOfficialExamFollowUpCommandValidator();
        var command = new ConfigureOfficialExamFollowUpCommand(Guid.NewGuid(), Guid.NewGuid(), true, invalidQuestions);

        var validationResult = await validator.ValidateAsync(command);
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(command.MaxFollowUpQuestions));
    }

    // =========================================================================
    // SECTION 3: CA BIÊN R2 - RÀNG BUỘC ĐỘC LẬP KHI PHÂN CÔNG GIẢNG VIÊN CHẤM LẠI
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-05: Khảo thí phân công Giảng viên chấm lại trùng shift.ExaminerUserId -> Bắt buộc bị chặn")]
    public async Task AssignAppealLecturer_MatchingInitialExaminer_Must_Be_Blocked()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, initialExaminer, _, session, shift, ticket, submission, _) = await SeedFullBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
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

        // Xác nhận trạng thái không bị đổi
        var appealInDb = await context.AppealRequests.FindAsync(appeal.Id);
        appealInDb!.Status.Should().Be(AppealStatus.Pending);
        appealInDb.AssignedTo.Should().Be(deptHead.Id);
    }

    [Fact(DisplayName = "ADV-M2-06: BatchAssignAppeals chứa gán trùng shift.ExaminerUserId -> Bị từ chối và Rollback")]
    public async Task BatchAssignAppeals_ContainingInitialExaminer_Must_Be_Rejected()
    {
        await using var context = CreateDbContext();
        var (student, deptHead, officer, initialExaminer, independentLecturer, session, shift, ticket, submission, _) = await SeedFullBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            SubmissionId = submission.Id,
            Reason = "Xin phúc khảo",
            Status = AppealStatus.Pending,
            AssignedTo = deptHead.Id
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new BatchAssignAppealsCommandHandler(context, new BatchAssignAppealsCommandValidator());
        var command = new BatchAssignAppealsCommand(officer.Id, new List<AppealAssignmentItemDto>
        {
            new() { AppealId = appeal.Id, LecturerId = initialExaminer.Id } // Gán trùng examiner ban đầu
        });

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã là Giám khảo chấm ban đầu");

        // Xác nhận trạng thái vẫn Pending
        var appealInDb = await context.AppealRequests.FindAsync(appeal.Id);
        appealInDb!.Status.Should().Be(AppealStatus.Pending);
    }

    // =========================================================================
    // SECTION 4: CA BIÊN R2 - THUẬT TOÁN ĐỀ XUẤT PHÂN BỔ (CÂN BẰNG TẢI & CHIA ĐỀU)
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-07: Thuật toán phân bổ khi tổng bài <= N * 24: Cân bằng tải triệt để (25 bài / 3 GV -> 9, 8, 8)")]
    public async Task AllocationProposal_UnderStandardQuota_Must_Balance_Load_Evenly()
    {
        await using var context = CreateDbContext();
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Lab",
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
            ExaminerUserId = Guid.NewGuid() // Khác 3 GV
        };
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);

        var lecturers = Enumerable.Range(1, 3).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"gv_bal_{i}@fe.edu.vn",
            FullName = $"Giảng viên {i}",
            Role = UserRole.Lecturer,
            IsActive = true
        }).ToList();
        context.Users.AddRange(lecturers);

        // Tạo 25 đơn phúc khảo
        for (int i = 1; i <= 25; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv_bal_{i}@fe.edu.vn",
                FullName = $"Sinh viên {i}",
                StudentCode = $"SE{191000 + i}",
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
        result.Value.TotalAppeals.Should().Be(25);
        result.Value.ExceedsStandardQuota.Should().BeFalse();

        // 25 bài chia cho 3 GV: ceiling(25/3) = 9. Phân bổ cân bằng: một GV 9 bài, 2 GV 8 bài
        var counts = result.Value.LecturerSummaries.Select(s => s.AssignedCount).ToList();
        counts.Sum().Should().Be(25);
        counts.Max().Should().BeLessOrEqualTo(9);
        counts.Min().Should().BeGreaterOrEqualTo(8);
        (counts.Max() - counts.Min()).Should().BeLessOrEqualTo(1);
    }

    [Fact(DisplayName = "ADV-M2-08: Thuật toán phân bổ khi tổng bài > N * 24: Chia đều toàn bộ bài (51 bài / 2 GV -> 26 và 25)")]
    public async Task AllocationProposal_ExceedsStandardQuota_Must_Divide_All_Appeals_Evenly()
    {
        await using var context = CreateDbContext();
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            Title = "Kỳ thi Quá Tải",
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

        // 2 Giảng viên -> Chuẩn tối đa = 2 * 24 = 48 bài
        var lecturers = Enumerable.Range(1, 2).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"gv_over_{i}@fe.edu.vn",
            FullName = $"Giảng viên Over {i}",
            Role = UserRole.Lecturer,
            IsActive = true
        }).ToList();
        context.Users.AddRange(lecturers);

        // Tạo 51 bài (> 48 bài)
        for (int i = 1; i <= 51; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv_over_{i}@fe.edu.vn",
                FullName = $"Sinh viên {i}",
                StudentCode = $"SE{192000 + i}",
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
        result.Value.TotalAppeals.Should().Be(51);
        result.Value.ExceedsStandardQuota.Should().BeTrue();

        // 51 bài chia đều cho 2 GV -> 26 bài và 25 bài
        var counts = result.Value.LecturerSummaries.Select(s => s.AssignedCount).ToList();
        counts.Sum().Should().Be(51);
        counts.Should().Contain(26);
        counts.Should().Contain(25);
        (counts.Max() - counts.Min()).Should().Be(1);
    }

    // =========================================================================
    // SECTION 5: CA BIÊN R2 - REGRADE GIẢNG VIÊN KHÔNG ĐỔI AUDITEDSCORE
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-09: RegradeAppealCommand của Giảng viên tuyệt đối không tự đổi audit.AuditedScore và FinalScore")]
    public async Task RegradeAppeal_Must_Not_Modify_AuditedScore_Or_FinalScore_Directly()
    {
        await using var context = CreateDbContext();
        var (student, _, _, _, independentLecturer, session, shift, ticket, submission, audit) = await SeedFullBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            SubmissionId = submission.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.InReview,
            AssignedTo = independentLecturer.Id,
            OriginalScore = 6.0m
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new RegradeAppealCommandHandler(context);
        var result = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, independentLecturer.Id, 9.0m, "Giải trình: Bài nói mạch lạc, xuất sắc."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProposedScore.Should().Be(9.0m);
        result.Value.Status.Should().Be(AppealStatus.InReview); // Vẫn là InReview, chưa được Approved!

        // Xác nhận trong CSDL: Điểm chính thức vẫn bảo toàn nguyên vẹn 6.0m!
        var auditInDb = await context.LecturerAudits.FindAsync(audit.Id);
        auditInDb!.AuditedScore.Should().Be(6.0m);

        var submissionInDb = await context.ExamQuestionSubmissions.FindAsync(submission.Id);
        submissionInDb!.FinalScore.Should().Be(6.0m);
    }

    [Fact(DisplayName = "ADV-M2-10: Giảng viên không được phân công gọi RegradeAppeal -> Bị từ chối")]
    public async Task RegradeAppeal_UnassignedLecturer_Must_Be_Rejected()
    {
        await using var context = CreateDbContext();
        var (student, _, _, initialExaminer, independentLecturer, session, shift, ticket, submission, _) = await SeedFullBaseDataAsync(context);

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = student.Id,
            SessionId = session.Id,
            Reason = "Xin chấm lại",
            Status = AppealStatus.InReview,
            AssignedTo = independentLecturer.Id // Gán cho independentLecturer
        };
        context.AppealRequests.Add(appeal);
        await context.SaveChangesAsync();

        var handler = new RegradeAppealCommandHandler(context);
        // initialExaminer cố tình gọi chấm lại đơn không được gán
        var result = await handler.Handle(
            new RegradeAppealCommand(appeal.Id, initialExaminer.Id, 9.0m, "Cố tình sửa"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không phải người được phân công");
    }

    // =========================================================================
    // SECTION 6: CA BIÊN R2 - KHẢO THÍ DUYỆT MỞ ONEWAYLOCKINTERCEPTOR CẬP NHẬT ĐIỂM
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-11: OneWayLockInterceptor chặn đứng mọi hành vi sửa trực tiếp điểm thi đã khóa")]
    public async Task OneWayLockInterceptor_DirectModification_Without_Approved_Appeal_Must_Throw_Exception()
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var seedContext = CreateDbContext(dbName))
        {
            var audit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = Guid.NewGuid(),
                LecturerId = Guid.NewGuid(),
                OriginalAiScore = 5.0m,
                AuditedScore = 5.0m,
                OverrideReason = "Khởi tạo ban đầu",
                IsLocked = true // Khóa điểm
            };
            seedContext.LecturerAudits.Add(audit);
            await seedContext.SaveChangesAsync();
        }

        // Mở context có gắn OneWayLockInterceptor
        await using var lockedContext = CreateDbContextWithLock(dbName);
        var lockedAudit = await lockedContext.LecturerAudits.FirstAsync();
        lockedAudit.AuditedScore = 10.0m; // Cố tình sửa trực tiếp

        var act = async () => await lockedContext.SaveChangesAsync();
        await act.Should().ThrowAsync<OneWayLockException>()
            .WithMessage("*khóa một chiều*");
    }

    [Fact(DisplayName = "ADV-M2-12: Cán bộ Khảo thí duyệt đơn phúc khảo kích hoạt ngoại lệ OneWayLockInterceptor cập nhật điểm thành công")]
    public async Task ReviewAppealDecision_OfficerApproval_Successfully_Passes_OneWayLockInterceptor()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid officerId;
        Guid appealId;
        Guid ticketId;
        Guid submissionId;

        // 1. Seed dữ liệu chuẩn có đầy đủ quan hệ và điểm thi đã bị khóa một chiều
        await using (var seedContext = CreateDbContext(dbName))
        {
            var (student, deptHead, officer, initialExaminer, independentLecturer, session, shift, ticket, submission, audit) = await SeedFullBaseDataAsync(seedContext);

            var appeal = new AppealRequest
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                StudentId = student.Id,
                SessionId = session.Id,
                SubmissionId = submission.Id,
                Reason = "Xin chấm lại",
                Status = AppealStatus.InReview,
                AssignedTo = independentLecturer.Id,
                OriginalScore = 6.0m,
                ProposedScore = 8.5m,
                ReviewNotes = "Giải trình: Trả lời tốt tiêu chí 2."
            };
            seedContext.AppealRequests.Add(appeal);
            await seedContext.SaveChangesAsync();

            officerId = officer.Id;
            appealId = appeal.Id;
            ticketId = ticket.Id;
            submissionId = submission.Id;
        }

        // 2. Mở DbContext CÓ GẮN ONEWAYLOCKINTERCEPTOR và thực hiện phê duyệt
        await using var activeContext = CreateDbContextWithLock(dbName);
        var handler = new ReviewAppealDecisionCommandHandler(activeContext);
        var command = new ReviewAppealDecisionCommand(
            appealId,
            officerId,
            AppealStatus.Approved,
            8.5m,
            "Cán bộ Khảo thí phê duyệt kết quả phúc khảo sau thẩm tra");

        // Gọi handler: Nếu OneWayLockInterceptor chặn thì SaveChangesAsync sẽ ném OneWayLockException!
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AppealStatus.Approved);
        result.Value.Decision.Should().Be(AppealStatus.Approved);

        // 3. Kiểm tra trong DB: Điểm đã được cập nhật thành công xuyên qua OneWayLockInterceptor!
        var auditInDb = await activeContext.LecturerAudits.FirstAsync(a => a.TicketId == ticketId);
        auditInDb.AuditedScore.Should().Be(8.5m);
        auditInDb.IsLocked.Should().BeTrue(); // Khóa vẫn giữ nguyên

        var subInDb = await activeContext.ExamQuestionSubmissions.FindAsync(submissionId);
        subInDb!.FinalScore.Should().Be(8.5m);

        var appealInDb = await activeContext.AppealRequests.FindAsync(appealId);
        appealInDb!.Status.Should().Be(AppealStatus.Approved);
        appealInDb.Decision.Should().Be(AppealStatus.Approved);
        appealInDb.ReviewedBy.Should().Be(officerId);
    }
}
