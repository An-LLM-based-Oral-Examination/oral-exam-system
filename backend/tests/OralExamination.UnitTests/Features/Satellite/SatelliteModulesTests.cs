using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Admin.Commands.UpdateSystemConfig;
using OralExamination.Application.Features.Admin.Commands.UpdateUserRole;
using OralExamination.Application.Features.Admin.Commands.UpdateUserStatus;
using OralExamination.Application.Features.Admin.Queries.GetAuditLogs;
using OralExamination.Application.Features.Admin.Queries.GetSystemConfigs;
using OralExamination.Application.Features.Admin.Queries.GetUsers;
using OralExamination.Application.Features.Lecturer.Queries.GetLecturerDashboard;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;
using OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;
using OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;
using OralExamination.Application.Features.Semesters.Commands.CreateSemester;
using OralExamination.Application.Features.Semesters.Commands.UpdateSemester;
using OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;
using OralExamination.Application.Features.Semesters.Queries.GetSemesters;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Satellite;

public class SatelliteModulesTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "BE-4.4b: OverrideExamScore tạo LecturerAudit, cập nhật FinalScore & trạng thái AUDITED khi chưa khóa")]
    public async Task OverrideExamScore_WhenUnlocked_ShouldCreateLecturerAuditAndSetAuditedStatus()
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.audit@fpt.edu.vn",
            FullName = "ThS. Giảng Viên Hậu Kiểm",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv1@fpt.edu.vn",
            FullName = "Nguyễn Văn A",
            StudentCode = "SE170123",
            Role = UserRole.Student,
            IsActive = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.AiGraded,
            IsLocked = false
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 6.50m,
            FinalScore = 6.50m,
            GradingStatus = "graded"
        };

        context.Users.AddRange(lecturer, student);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var handler = new OverrideExamScoreCommandHandler(context);
        var result = await handler.Handle(
            new OverrideExamScoreCommand(
                lecturer.Id,
                ticket.Id,
                8.25m,
                "Sinh viên trình bày đúng bản chất Dependency Injection nhưng Whisper nhận diện thiếu thuật ngữ.",
                new List<OverrideCriterionScoreDto>
                {
                    new(Guid.NewGuid(), 4.25m, "Nắm vững nguyên lý SOLID", 3.0m)
                }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalAiScore.Should().Be(6.50m);
        result.Value.AuditedScore.Should().Be(8.25m);
        result.Value.TicketStatus.Should().Be(ExamTicketStatus.Audited);

        var updatedTicket = await context.StudentExamTickets
            .Include(t => t.LecturerAudit)
                .ThenInclude(a => a!.Details)
            .Include(t => t.Submissions)
            .FirstAsync(t => t.Id == ticket.Id);

        updatedTicket.Status.Should().Be(ExamTicketStatus.Audited);
        updatedTicket.LecturerAudit.Should().NotBeNull();
        updatedTicket.LecturerAudit!.AuditedScore.Should().Be(8.25m);
        updatedTicket.LecturerAudit.Details.Should().ContainSingle();
        updatedTicket.Submissions.First().FinalScore.Should().Be(8.25m);
    }

    [Fact(DisplayName = "BE-4.4b: OverrideExamScore chặn thao tác khi vé thi đã bị khóa một chiều (is_locked = true) hoặc lý do < 10 ký tự")]
    public async Task OverrideExamScore_WhenLockedOrShortReason_ShouldFail()
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.lock@fpt.edu.vn",
            FullName = "ThS. Giảng Viên",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var lockedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 2,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        context.Users.Add(lecturer);
        context.StudentExamTickets.Add(lockedTicket);
        await context.SaveChangesAsync();

        var handler = new OverrideExamScoreCommandHandler(context);

        var shortReasonResult = await handler.Handle(
            new OverrideExamScoreCommand(lecturer.Id, lockedTicket.Id, 7.5m, "Quá ngắn"),
            CancellationToken.None);
        shortReasonResult.IsSuccess.Should().BeFalse();
        shortReasonResult.Error.Should().StartWith("VALIDATION:");

        var lockedResult = await handler.Handle(
            new OverrideExamScoreCommand(lecturer.Id, lockedTicket.Id, 7.5m, "Lý do hợp lệ đủ trên 10 ký tự để kiểm thử khóa điểm"),
            CancellationToken.None);
        lockedResult.IsSuccess.Should().BeFalse();
        lockedResult.Error.Should().StartWith("LOCKED:");
    }

    [Fact(DisplayName = "BE-V05 / BE-V09b: Admin quản lý trạng thái & role User, thu hồi RefreshToken và ghi AuditLogs")]
    public async Task AdminUserManagement_ShouldUpdateRoleAndStatus_RevokeTokens_AndLogAudit()
    {
        await using var context = CreateDbContext();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "System Admin",
            Role = UserRole.Admin,
            IsActive = true
        };
        var targetUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "gv.moi@fpt.edu.vn",
            FullName = "Nguyễn Văn Giảng Viên",
            Role = UserRole.Student,
            IsActive = true
        };
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = targetUser.Id,
            TokenHash = "hash-123",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        context.Users.AddRange(admin, targetUser);
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        var roleHandler = new UpdateUserRoleCommandHandler(context);
        var roleResult = await roleHandler.Handle(
            new UpdateUserRoleCommand(admin.Id, targetUser.Id, UserRole.Lecturer),
            CancellationToken.None);

        roleResult.IsSuccess.Should().BeTrue();
        roleResult.Value.Role.Should().Be(UserRole.Lecturer);

        var statusHandler = new UpdateUserStatusCommandHandler(context);
        var selfLockResult = await statusHandler.Handle(
            new UpdateUserStatusCommand(admin.Id, admin.Id, false, "Tự khóa"),
            CancellationToken.None);
        selfLockResult.IsSuccess.Should().BeFalse();
        selfLockResult.Error.Should().StartWith("CONFLICT:");

        var lockTargetResult = await statusHandler.Handle(
            new UpdateUserStatusCommand(admin.Id, targetUser.Id, false, "Tạm khóa tài khoản hết học kỳ"),
            CancellationToken.None);
        lockTargetResult.IsSuccess.Should().BeTrue();
        lockTargetResult.Value.IsActive.Should().BeFalse();

        var revokedToken = await context.RefreshTokens.FirstAsync(t => t.Id == refreshToken.Id);
        revokedToken.IsRevoked.Should().BeTrue();

        var usersQueryHandler = new GetUsersQueryHandler(context);
        var usersPage = await usersQueryHandler.Handle(new GetUsersQuery("Giảng Viên", UserRole.Lecturer, false), CancellationToken.None);
        usersPage.IsSuccess.Should().BeTrue();
        usersPage.Value.TotalCount.Should().Be(1);

        var auditHandler = new GetAuditLogsQueryHandler(context);
        var logs = await auditHandler.Handle(new GetAuditLogsQuery("users", null, admin.Id), CancellationToken.None);
        logs.IsSuccess.Should().BeTrue();
        logs.Value.TotalCount.Should().Be(2);
    }

    [Fact(DisplayName = "BE-V08: UpdateSystemConfig kiểm tra chặt dải giá trị hợp lệ (TranscriptBufferSeconds 10-300s, Follow-up 1-5)")]
    public async Task SystemConfigs_ShouldEnforceInvariants_AndPersistChanges()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var updateHandler = new UpdateSystemConfigCommandHandler(context);

        var invalidBuffer = await updateHandler.Handle(
            new UpdateSystemConfigCommand(adminId, "TranscriptBufferSeconds", "500"),
            CancellationToken.None);
        invalidBuffer.IsSuccess.Should().BeFalse();
        invalidBuffer.Error.Should().StartWith("VALIDATION:");

        var validBuffer = await updateHandler.Handle(
            new UpdateSystemConfigCommand(adminId, "TranscriptBufferSeconds", "90", "Thời gian đệm chỉnh sửa thuật ngữ SE"),
            CancellationToken.None);
        validBuffer.IsSuccess.Should().BeTrue();
        validBuffer.Value.Value.Should().Be("90");

        var getHandler = new GetSystemConfigsQueryHandler(context);
        var listResult = await getHandler.Handle(new GetSystemConfigsQuery(), CancellationToken.None);
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().ContainSingle(c => c.Key == "TranscriptBufferSeconds" && c.Value == "90");
    }

    [Fact(DisplayName = "BE-V06: Quản lý Học kỳ (Create, Update, GetSemesters & GetAcademicClasses) chống trùng mã và kiểm tra ngày")]
    public async Task SemestersAndAcademicClasses_ShouldCreateUpdateAndQueryCleanly()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var createHandler = new CreateSemesterCommandHandler(context);

        var invalidDates = await createHandler.Handle(
            new CreateSemesterCommand(adminId, "FA26", "Fall 2026", new DateOnly(2026, 12, 31), new DateOnly(2026, 9, 1)),
            CancellationToken.None);
        invalidDates.IsSuccess.Should().BeFalse();

        var created = await createHandler.Handle(
            new CreateSemesterCommand(adminId, "fa26", "Fall 2026", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
            CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        created.Value.Code.Should().Be("FA26");

        var duplicate = await createHandler.Handle(
            new CreateSemesterCommand(adminId, "FA26", "Fall 2026 Duplicate", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
            CancellationToken.None);
        duplicate.IsSuccess.Should().BeFalse();
        duplicate.Error.Should().StartWith("CONFLICT:");

        var updateHandler = new UpdateSemesterCommandHandler(context);
        var updated = await updateHandler.Handle(
            new UpdateSemesterCommand(adminId, created.Value.Id, "FA26", "Fall 2026 Updated", new DateOnly(2026, 9, 5), new DateOnly(2026, 12, 31), true),
            CancellationToken.None);
        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Fall 2026 Updated");

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Software Development Project",
            SemesterId = created.Value.Id,
            IsActive = true
        };
        var academicClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1701",
            CourseId = course.Id,
            SemesterId = created.Value.Id,
            MaxStudents = 35,
            IsActive = true
        };
        context.Courses.Add(course);
        context.Classes.Add(academicClass);
        await context.SaveChangesAsync();

        var getSemestersHandler = new GetSemestersQueryHandler(context);
        var semesters = await getSemestersHandler.Handle(new GetSemestersQuery(true), CancellationToken.None);
        semesters.Value.Should().ContainSingle(s => s.Code == "FA26" && s.CoursesCount == 1 && s.ClassesCount == 1);

        var getClassesHandler = new GetAcademicClassesQueryHandler(context);
        var classes = await getClassesHandler.Handle(new GetAcademicClassesQuery(course.Id, created.Value.Id), CancellationToken.None);
        classes.Value.Should().ContainSingle(c => c.Code == "SE1701" && c.CourseCode == "SWP391");
    }

    [Fact(DisplayName = "BE-V07: In-App Notifications tổng hợp thông báo theo Role và đánh dấu đã đọc (MarkRead / MarkAllRead)")]
    public async Task Notifications_ShouldSynthesizeEvents_AndSupportMarkRead()
    {
        await using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.noti@fpt.edu.vn",
            FullName = "Lê Thị Thông Báo",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 2 - Chiều",
            RoomLab = "Lab 402",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "completed"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 12,
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            CheckedInAt = DateTime.UtcNow.AddHours(-2),
            SubmittedAt = DateTime.UtcNow.AddHours(-1)
        };

        context.Users.Add(student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        await context.SaveChangesAsync();

        var getHandler = new GetNotificationsQueryHandler(context);
        var initial = await getHandler.Handle(new GetNotificationsQuery(student.Id), CancellationToken.None);
        initial.IsSuccess.Should().BeTrue();
        initial.Value.TotalCount.Should().Be(2);
        initial.Value.UnreadCount.Should().Be(2);

        var firstNotiId = initial.Value.Items.First().Id;
        var markReadHandler = new MarkNotificationReadCommandHandler(context);
        var markOne = await markReadHandler.Handle(new MarkNotificationReadCommand(student.Id, firstNotiId), CancellationToken.None);
        markOne.IsSuccess.Should().BeTrue();

        var afterOne = await getHandler.Handle(new GetNotificationsQuery(student.Id), CancellationToken.None);
        afterOne.Value.UnreadCount.Should().Be(1);

        var markAllHandler = new MarkAllNotificationsReadCommandHandler(context);
        await markAllHandler.Handle(new MarkAllNotificationsReadCommand(student.Id), CancellationToken.None);

        var afterAll = await getHandler.Handle(new GetNotificationsQuery(student.Id), CancellationToken.None);
        afterAll.Value.UnreadCount.Should().Be(0);
    }

    [Fact(DisplayName = "BE-V10: Proctor Room Monitor trả về sơ đồ 40 ghế, cập nhật điểm danh và đình chỉ vé thi vi phạm Kiosk")]
    public async Task ProctorRoomMonitor_ShouldReturn40Seats_UpdateAttendance_AndSuspendTicket()
    {
        await using var context = CreateDbContext();
        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fpt.edu.vn",
            FullName = "Giám Thị Phòng Lab",
            Role = UserRole.Proctor,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.kiosk@fpt.edu.vn",
            FullName = "Trần Văn Kiosk",
            StudentCode = "SE170555",
            Role = UserRole.Student,
            IsActive = true
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1 - Lab 201",
            RoomLab = "Lab 201",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow.AddMinutes(45),
            ProctorUserId = proctor.Id,
            Status = "scheduled"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 5,
            IpAddress = "10.20.30.5",
            Status = ExamTicketStatus.Scheduled
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 7.0m,
            GradingStatus = "graded"
        };

        context.Users.AddRange(proctor, student);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var attendanceHandler = new UpdateTicketAttendanceCommandHandler(context);
        var checkInResult = await attendanceHandler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket.Id, true, "10.20.30.5", "Đã kiểm tra thẻ SV"),
            CancellationToken.None);
        checkInResult.IsSuccess.Should().BeTrue();
        checkInResult.Value.Status.Should().Be(ExamTicketStatus.InProgress);

        var suspendHandler = new SuspendExamTicketCommandHandler(context);
        var suspendResult = await suspendHandler.Handle(
            new SuspendExamTicketCommand(proctor.Id, ticket.Id, "Sinh viên cố tình thoát Fullscreen quá 3 lần trong ca thi", 4),
            CancellationToken.None);
        suspendResult.IsSuccess.Should().BeTrue();
        suspendResult.Value.IsSuspended.Should().BeTrue();
        suspendResult.Value.Status.Should().Be(ExamTicketStatus.Audited);

        var monitorHandler = new GetShiftAttendanceQueryHandler(context);
        var monitor = await monitorHandler.Handle(new GetShiftAttendanceQuery(shift.Id), CancellationToken.None);
        monitor.IsSuccess.Should().BeTrue();
        monitor.Value.TotalSeats.Should().Be(40);
        monitor.Value.Seats.Should().HaveCount(40);
        monitor.Value.SuspendedCount.Should().Be(1);

        var seat5 = monitor.Value.Seats.First(s => s.SeatNumber == 5);
        seat5.IsOccupied.Should().BeTrue();
        seat5.IsSuspended.Should().BeTrue();
        seat5.StudentCode.Should().Be("SE170555");
    }

    [Fact(DisplayName = "BE-V11: Lecturer Dashboard tổng hợp thống kê ngân hàng câu hỏi, ca thi cần hậu kiểm và đơn phúc khảo")]
    public async Task LecturerDashboard_ShouldReturnQuestionStats_ShiftAuditSummaries_AndAppeals()
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "gv.dashboard@fpt.edu.vn",
            FullName = "TS. Nguyễn Quang Giảng Viên",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications",
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        var qApproved = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Clean Architecture",
            Content = "Giải thích nguyên lý Dependency Inversion trong Clean Architecture.",
            ApprovalStatus = ApprovalStatus.Approved,
            SubmittedBy = lecturer.Id
        };
        var qNeedsRevision = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi CQRS MediatR",
            Content = "Trình bày luồng xử lý Command và Query trong MediatR.",
            ApprovalStatus = ApprovalStatus.NeedsRevision,
            ReviewNotes = "Cần bổ sung tiêu chí chấm điểm cho Validation Pipeline.",
            SubmittedBy = lecturer.Id
        };

        context.Users.Add(lecturer);
        context.Courses.Add(course);
        context.ExamQuestions.AddRange(qApproved, qNeedsRevision);
        await context.SaveChangesAsync();

        var handler = new GetLecturerDashboardQueryHandler(context);
        var dashboard = await handler.Handle(new GetLecturerDashboardQuery(lecturer.Id, course.Id), CancellationToken.None);

        dashboard.IsSuccess.Should().BeTrue();
        dashboard.Value.QuestionStats.TotalExamQuestions.Should().Be(2);
        dashboard.Value.QuestionStats.ApprovedCount.Should().Be(1);
        dashboard.Value.QuestionStats.NeedsRevisionCount.Should().Be(1);
        dashboard.Value.QuestionStats.ApprovalRatePercent.Should().Be(50.00m);
        dashboard.Value.QuestionsNeedingRevision.Should().ContainSingle(q => q.QuestionId == qNeedsRevision.Id);
    }

    [Fact(DisplayName = "BE-4.4b: OverrideExamScore hỗ trợ sửa điểm riêng từng câu (SubmissionId) trên vé thi nhiều câu và tính lại điểm trung bình vé")]
    public async Task OverrideExamScore_WithSpecificSubmissionId_ShouldUpdateOnlyTargetSubmissionAndRecalculateTicketAverage()
    {
        await using var context = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.sub@fpt.edu.vn",
            FullName = "ThS. Giảng Viên Chấm Lẻ",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 3,
            Status = ExamTicketStatus.AiGraded,
            IsLocked = false
        };
        var sub1 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 5.00m,
            FinalScore = 5.00m,
            GradingStatus = "graded"
        };
        var sub2 = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 8.00m,
            FinalScore = 8.00m,
            GradingStatus = "graded"
        };

        context.Users.Add(lecturer);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.AddRange(sub1, sub2);
        await context.SaveChangesAsync();

        var handler = new OverrideExamScoreCommandHandler(context);
        var result = await handler.Handle(
            new OverrideExamScoreCommand(
                lecturer.Id,
                ticket.Id,
                7.00m,
                "Sinh viên bổ sung đúng ví dụ thực tế cho câu số 1 trong đoạn ghi âm.",
                CriterionDetails: null,
                SubmissionId: sub1.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalAiScore.Should().Be(6.50m);
        result.Value.AuditedScore.Should().Be(7.50m);

        var updatedSub1 = await context.ExamQuestionSubmissions.FirstAsync(s => s.Id == sub1.Id);
        var updatedSub2 = await context.ExamQuestionSubmissions.FirstAsync(s => s.Id == sub2.Id);
        updatedSub1.FinalScore.Should().Be(7.00m);
        updatedSub1.GradingStatus.Should().Be("audited");
        updatedSub2.FinalScore.Should().Be(8.00m);
        updatedSub2.GradingStatus.Should().Be("graded");
    }
}
