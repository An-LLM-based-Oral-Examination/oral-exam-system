using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

/// <summary>Use persisted identity and canonical academic ownership, never an admin override.</summary>
public static class OfficialExamAuthorization
{
    public static Task<bool> IsActiveActorAsync(
        IApplicationDbContext context, Guid actorUserId, string requiredRole, CancellationToken cancellationToken) =>
        actorUserId == Guid.Empty
            ? Task.FromResult(false)
            : context.Users.AsNoTracking().AnyAsync(
                user => user.Id == actorUserId && user.IsActive && user.Role == requiredRole, cancellationToken);

    public static Task<bool> IsActiveClaimedActorAsync(
        IApplicationDbContext context, Guid actorUserId, string? claimedRole, string requiredRole,
        CancellationToken cancellationToken) =>
        !string.Equals(claimedRole, requiredRole, StringComparison.Ordinal)
            ? Task.FromResult(false)
            : IsActiveActorAsync(context, actorUserId, requiredRole, cancellationToken);

    public static Task<bool> IsActiveOfficerAsync(
        IApplicationDbContext context, Guid actorUserId, CancellationToken cancellationToken) =>
        IsActiveActorAsync(context, actorUserId, UserRole.ExaminationOfficer, cancellationToken);

    public static Task<bool> IsActiveEnrollmentEstimateRoleAsync(
        IApplicationDbContext context, Guid actorUserId, CancellationToken cancellationToken) =>
        actorUserId == Guid.Empty
            ? Task.FromResult(false)
            : context.Users.AsNoTracking().AnyAsync(
                user => user.Id == actorUserId
                    && user.IsActive
                    && (user.Role == UserRole.ExaminationOfficer || user.Role == UserRole.DepartmentHead),
                cancellationToken);

    public static async Task<bool> CanReadEnrollmentEstimateAsync(
        IApplicationDbContext context, Guid actorUserId, Guid courseId, CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        var actor = await context.Users.AsNoTracking()
            .Where(user => user.Id == actorUserId && user.IsActive)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return false;
        }

        if (actor.Role == UserRole.ExaminationOfficer)
        {
            return true;
        }

        if (actor.Role == UserRole.DepartmentHead)
        {
            return await OwnsAcademicCourseAsync(context, courseId, actorUserId, cancellationToken);
        }

        return false;
    }

    public static async Task<bool> CanAuditShiftAsync(
        IApplicationDbContext context,
        Guid actorUserId,
        RealExamSessionShift shift,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        var actor = await context.Users.AsNoTracking()
            .Where(user => user.Id == actorUserId && user.IsActive)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return false;
        }

        return actor.Role switch
        {
            UserRole.ExaminationOfficer => true,
            UserRole.Lecturer => shift.ExaminerUserId.HasValue && shift.ExaminerUserId.Value == actor.Id,
            UserRole.DepartmentHead => await OwnsShiftCourseAsync(context, shift, actor.Id, cancellationToken),
            _ => false
        };
    }

    public static async Task<bool> CanOverrideOrLockTicketAsync(
        IApplicationDbContext context,
        Guid actorUserId,
        StudentExamTicket ticket,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        var shift = ticket.Shift ?? await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == ticket.ShiftId, cancellationToken);
        if (shift is null)
        {
            return false;
        }

        var actor = await context.Users.AsNoTracking()
            .Where(user => user.Id == actorUserId && user.IsActive)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return false;
        }

        return actor.Role switch
        {
            UserRole.Lecturer => shift.ExaminerUserId.HasValue && shift.ExaminerUserId.Value == actor.Id,
            UserRole.DepartmentHead => await OwnsShiftCourseAsync(context, shift, actor.Id, cancellationToken),
            _ => false
        };
    }

    public static async Task<bool> CanMonitorShiftAntiCheatAsync(
        IApplicationDbContext context,
        Guid actorUserId,
        RealExamSessionShift shift,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        var actor = await context.Users.AsNoTracking()
            .Where(user => user.Id == actorUserId && user.IsActive)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return false;
        }

        return actor.Role switch
        {
            UserRole.ExaminationOfficer => true,
            UserRole.Proctor => shift.ProctorUserId.HasValue && shift.ProctorUserId.Value == actor.Id,
            UserRole.Lecturer => (shift.ExaminerUserId.HasValue && shift.ExaminerUserId.Value == actor.Id)
                || (shift.ProctorUserId.HasValue && shift.ProctorUserId.Value == actor.Id),
            UserRole.DepartmentHead => await OwnsShiftCourseAsync(context, shift, actor.Id, cancellationToken),
            _ => false
        };
    }

    public static async Task<bool> OwnsShiftCourseAsync(
        IApplicationDbContext context, RealExamSessionShift shift, Guid actorUserId, CancellationToken cancellationToken)
    {
        var courseId = shift.Session?.CourseId ?? await context.OfficialExamSessions
            .AsNoTracking()
            .Where(session => session.Id == shift.SessionId)
            .Select(session => (Guid?)session.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        return courseId.HasValue && await OwnsAcademicCourseAsync(context, courseId.Value, actorUserId, cancellationToken);
    }

    public static async Task<bool> OwnsAcademicCourseAsync(
        IApplicationDbContext context, Guid courseId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var course = await context.Courses.AsNoTracking().Include(course => course.AcademicProgram)
            .FirstOrDefaultAsync(course => course.Id == courseId, cancellationToken);
        return course is not null && AcademicCourseOwnership.IsOwnedBy(course, actorUserId);
    }

    public static async Task<bool> CanLecturerAccessCourseAsync(
        IApplicationDbContext context, Guid courseId, Guid lecturerUserId, CancellationToken cancellationToken)
    {
        if (courseId == Guid.Empty || lecturerUserId == Guid.Empty)
        {
            return false;
        }

        var hasCourseAssignment = await context.CourseLecturers
            .AsNoTracking()
            .AnyAsync(cl =>
                cl.CourseId == courseId
                && cl.LecturerUserId == lecturerUserId
                && cl.IsActive
                && cl.Course != null
                && cl.Course.IsActive
                && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled),
                cancellationToken);
        if (hasCourseAssignment)
        {
            return true;
        }

        return await context.Classes
            .AsNoTracking()
            .AnyAsync(c =>
                c.CourseId == courseId
                && c.LecturerId == lecturerUserId
                && c.IsActive
                && c.Course != null
                && c.Course.IsActive
                && (c.CourseOffering == null || c.CourseOffering.IsEnabled),
                cancellationToken);
    }

    public static async Task<bool> CanAccessCourseForExamSetAsync(
        IApplicationDbContext context,
        Guid courseId,
        Guid actorUserId,
        string actorRole,
        bool allowExaminationOfficer,
        CancellationToken cancellationToken)
    {
        if (courseId == Guid.Empty || actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorRole))
        {
            return false;
        }

        if (allowExaminationOfficer && string.Equals(actorRole, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            return await OwnsAcademicCourseAsync(context, courseId, actorUserId, cancellationToken);
        }

        if (string.Equals(actorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            return await CanLecturerAccessCourseAsync(context, courseId, actorUserId, cancellationToken);
        }

        return false;
    }
}
