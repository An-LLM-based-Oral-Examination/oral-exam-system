using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;
using OralExamination.Application.Features.Admin.Commands.CreateAdminNotification;
using OralExamination.Application.Features.Admin.Commands.DeleteAdminNotification;
using OralExamination.Application.Features.Admin.Commands.ResetUserPassword;
using OralExamination.Application.Features.Admin.Queries.GetAdminNotifications;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Admin;

public sealed class AdminNotificationsAndPasswordResetTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"AdminCompletionTests_{Guid.NewGuid()}")
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public void AdminController_MustEnforceAdminRoleAtControllerLevel()
    {
        var authorizeAttr = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull();
        authorizeAttr!.Roles.Should().Be("admin");
    }

    [Fact]
    public async Task ResetUserPassword_ValidTemporaryPassword_BumpsAuthVersion_RevokesTokens_ForcesChange_WithoutLoggingPlaintext()
    {
        using var db = CreateDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var adminId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = studentId,
            Email = "sv01@fpt.edu.vn",
            FullName = "Nguyen Van SV",
            PasswordHash = hasher.Hash("OldSecret123!"),
            Role = "student",
            StudentCode = "SE180101",
            IsActive = true,
            MustChangePassword = false,
            AuthVersion = 2
        });
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = studentId,
            TokenHash = "active-refresh-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ResetUserPasswordCommandHandler(db, hasher);
        var tempSecret = "TempPass#2026";
        var result = await handler.Handle(
            new ResetUserPasswordCommand(adminId, studentId, tempSecret, "Quen mat khau"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MustChangePassword.Should().BeTrue();

        var updatedUser = await db.Users.Include(u => u.RefreshTokens).SingleAsync(u => u.Id == studentId);
        updatedUser.AuthVersion.Should().Be(3);
        updatedUser.MustChangePassword.Should().BeTrue();
        hasher.Verify(tempSecret, updatedUser.PasswordHash!).Should().BeTrue();
        updatedUser.RefreshTokens.Should().OnlyContain(t => t.RevokedAt != null);

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "USER_PASSWORD_RESET");
        audit.UserId.Should().Be(adminId);
        audit.EntityId.Should().Be(studentId);
        audit.OldValues.Should().Be("must_change_password=false;auth_version=2");
        audit.NewValues.Should().NotContain(tempSecret);
    }

    [Fact]
    public async Task ResetUserPassword_NotFoundOrOverlongReason_FailsSafely()
    {
        using var db = CreateDbContext();
        var handler = new ResetUserPasswordCommandHandler(db, new Pbkdf2PasswordHasher());

        var notFound = await handler.Handle(
            new ResetUserPasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "ValidTemp#2026"),
            CancellationToken.None);
        notFound.IsSuccess.Should().BeFalse();
        notFound.Error.Should().StartWith("NOT_FOUND:");

        var longReason = new string('R', 501);
        var invalidReason = await handler.Handle(
            new ResetUserPasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "ValidTemp#2026", longReason),
            CancellationToken.None);
        invalidReason.IsSuccess.Should().BeFalse();
        invalidReason.Error.Should().StartWith("VALIDATION:");
    }

    [Fact]
    public async Task ResetUserPassword_TooShortPassword_FailsValidation()
    {
        using var db = CreateDbContext();
        var handler = new ResetUserPasswordCommandHandler(db, new Pbkdf2PasswordHasher());

        var result = await handler.Handle(
            new ResetUserPasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "short"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
    }

    [Fact]
    public async Task AdminNotifications_DispatchBroadcastAndRole_SkipsInactiveUsers_QueriesUnread_AndDeletesWithAudit()
    {
        using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var activeStudentId = Guid.NewGuid();
        var lockedStudentId = Guid.NewGuid();
        var activeLecturerId = Guid.NewGuid();

        db.Users.AddRange(
            new User
            {
                Id = adminId,
                Email = "admin@fpt.edu.vn",
                FullName = "System Admin",
                Role = "admin",
                IsActive = true
            },
            new User
            {
                Id = activeStudentId,
                Email = "active.sv@fpt.edu.vn",
                FullName = "Active Student",
                Role = "student",
                StudentCode = "SE180001",
                IsActive = true
            },
            new User
            {
                Id = lockedStudentId,
                Email = "locked.sv@fpt.edu.vn",
                FullName = "Locked Student",
                Role = "student",
                StudentCode = "SE180002",
                IsActive = false
            },
            new User
            {
                Id = activeLecturerId,
                Email = "gv@fpt.edu.vn",
                FullName = "Active Lecturer",
                Role = "lecturer",
                IsActive = true
            });
        await db.SaveChangesAsync();

        var createHandler = new CreateAdminNotificationCommandHandler(db);

        // 1. Broadcast ALL_ACTIVE -> hits 3 active users (admin + activeStudent + activeLecturer), skips lockedStudent
        var broadcastResult = await createHandler.Handle(
            new CreateAdminNotificationCommand(
                adminId,
                "ALL_ACTIVE",
                "Bảo trì hệ thống",
                "Hệ thống bảo trì lúc 23:00.",
                "MAINTENANCE_ALERT"),
            CancellationToken.None);

        broadcastResult.IsSuccess.Should().BeTrue();
        broadcastResult.Value!.CreatedCount.Should().Be(3);
        (await db.Notifications.AnyAsync(n => n.UserId == lockedStudentId)).Should().BeFalse();

        // 2. Role-targeted -> student -> only 1 active student
        var roleResult = await createHandler.Handle(
            new CreateAdminNotificationCommand(
                adminId,
                "ROLE",
                "Mở ca thi thử",
                "Đã mở hạn ngạch thi thử học kỳ FA26.",
                "ACADEMIC_NOTICE",
                TargetRole: "student"),
            CancellationToken.None);

        roleResult.IsSuccess.Should().BeTrue();
        roleResult.Value!.CreatedCount.Should().Be(1);

        // 2b. Single USER scope: rejects locked & NOT_FOUND, sets EntityId == created Notification.Id for active user
        var lockedUserResult = await createHandler.Handle(
            new CreateAdminNotificationCommand(
                adminId,
                "USER",
                "Thông báo cá nhân",
                "Nội dung riêng.",
                "SYSTEM_ANNOUNCEMENT",
                TargetUserId: lockedStudentId),
            CancellationToken.None);
        lockedUserResult.IsSuccess.Should().BeFalse();
        lockedUserResult.Error.Should().StartWith("VALIDATION:");

        var missingUserResult = await createHandler.Handle(
            new CreateAdminNotificationCommand(
                adminId,
                "USER",
                "Thông báo cá nhân",
                "Nội dung riêng.",
                "SYSTEM_ANNOUNCEMENT",
                TargetUserId: Guid.NewGuid()),
            CancellationToken.None);
        missingUserResult.IsSuccess.Should().BeFalse();
        missingUserResult.Error.Should().StartWith("NOT_FOUND:");

        var singleUserResult = await createHandler.Handle(
            new CreateAdminNotificationCommand(
                adminId,
                "USER",
                "Nhắc lịch cá nhân",
                "Vui lòng kiểm tra ca thi.",
                "ACADEMIC_NOTICE",
                TargetUserId: activeLecturerId),
            CancellationToken.None);
        singleUserResult.IsSuccess.Should().BeTrue();
        var lecturerNotif = await db.Notifications.SingleAsync(n => n.UserId == activeLecturerId && n.Title == "Nhắc lịch cá nhân");
        var singleDispatchAudit = await db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .FirstAsync(a => a.Action == "NOTIFICATION_DISPATCHED" && a.NewValues!.Contains("scope=USER"));
        singleDispatchAudit.EntityId.Should().Be(lecturerNotif.Id);

        var broadcastAudit = await db.AuditLogs
            .FirstAsync(a => a.Action == "NOTIFICATION_DISPATCHED" && a.NewValues!.Contains("scope=ALL_ACTIVE"));
        broadcastAudit.EntityId.Should().BeNull();

        // 3. Query filtered by role=student
        var queryHandler = new GetAdminNotificationsQueryHandler(db);
        var queried = await queryHandler.Handle(
            new GetAdminNotificationsQuery(Role: "student", Page: 1, PageSize: 10),
            CancellationToken.None);

        queried.IsSuccess.Should().BeTrue();
        queried.Value!.TotalCount.Should().Be(2);
        queried.Value.UnreadTotalCount.Should().Be(2);

        // 4. Delete 1 notification & verify audit log
        var targetNotifId = queried.Value.Items[0].Id;
        var deleteHandler = new DeleteAdminNotificationCommandHandler(db);
        var deleted = await deleteHandler.Handle(
            new DeleteAdminNotificationCommand(adminId, targetNotifId),
            CancellationToken.None);

        deleted.IsSuccess.Should().BeTrue();
        (await db.Notifications.AnyAsync(n => n.Id == targetNotifId)).Should().BeFalse();
        (await db.AuditLogs.CountAsync(a => a.Action == "NOTIFICATION_DISPATCHED")).Should().Be(3);
        (await db.AuditLogs.CountAsync(a => a.Action == "NOTIFICATION_DELETED")).Should().Be(1);
    }

    [Fact]
    public async Task CourseMutations_CreateUpdateUnassignHeadAndToggleOffering_WriteAccurateAuditLogs()
    {
        using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();

        db.Semesters.Add(new Semester
        {
            Id = semesterId,
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        });
        db.AcademicPrograms.Add(new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = deptHeadId,
            IsActive = true
        });
        db.Users.Add(new User
        {
            Id = deptHeadId,
            Email = "head@fpt.edu.vn",
            FullName = "Truong Bo Mon",
            Role = "department_head",
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Explicitly pass Guid.Empty on create -> must keep DepartmentHeadUserId == null despite SE AcademicProgram fallback
        var created = await new CreateCourseCommandHandler(db).Handle(
            new CreateCourseCommand("PRN231", "Cross Platform .NET", 3, "Desc", Guid.Empty, 4, 30, 5, true, semesterId, adminId),
            CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        created.Value!.DepartmentHeadUserId.Should().BeNull();

        var assigned = await new AssignDepartmentHeadCommandHandler(db, new AssignDepartmentHeadCommandValidator()).Handle(
            new AssignDepartmentHeadCommand(created.Value.Id, deptHeadId, "admin", adminId),
            CancellationToken.None);
        assigned.IsSuccess.Should().BeTrue();

        // Update: unassign DepartmentHead via Guid.Empty AND toggle IsActive = false -> syncs CourseOffering.IsEnabled = false & logs pre-mutation OldValues
        var updated = await new UpdateCourseCommandHandler(db).Handle(
            new UpdateCourseCommand(created.Value.Id, "PRN231", "Cross Platform .NET 8", 3, "Updated", Guid.Empty, 5, 30, 5, false, semesterId, adminId),
            CancellationToken.None);
        updated.IsSuccess.Should().BeTrue();
        updated.Value!.DepartmentHeadUserId.Should().BeNull();
        updated.Value.IsActive.Should().BeFalse();

        var offering = await db.CourseOfferings.SingleAsync(co => co.CourseId == created.Value.Id && co.SemesterId == semesterId);
        offering.IsEnabled.Should().BeFalse();

        var updateAudit = await db.AuditLogs.SingleAsync(a => a.Action == "COURSE_UPDATED");
        updateAudit.OldValues.Should().Contain($"departmentHeadUserId={deptHeadId}");
        updateAudit.OldValues.Should().Contain("isActive=True");
        updateAudit.NewValues.Should().Contain("departmentHeadUserId=null");
        updateAudit.NewValues.Should().Contain("isActive=False");

        var actions = await db.AuditLogs.Select(a => a.Action).ToListAsync();
        actions.Should().Contain(new[] { "COURSE_CREATED", "COURSE_UPDATED", "COURSE_DEPARTMENT_HEAD_ASSIGNED" });
    }
}
