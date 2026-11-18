using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

/// <summary>Use persisted identity and canonical academic ownership, never an admin override.</summary>
internal static class OfficialExamAuthorization
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

    public static Task<bool> IsActiveOfficerOrAdminAsync(
        IApplicationDbContext context, Guid actorUserId, CancellationToken cancellationToken) =>
        actorUserId == Guid.Empty
            ? Task.FromResult(false)
            : context.Users.AsNoTracking().AnyAsync(
                user => user.Id == actorUserId
                    && user.IsActive
                    && (user.Role == UserRole.ExaminationOfficer || user.Role == UserRole.Admin),
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

        if (actor.Role == UserRole.ExaminationOfficer || actor.Role == UserRole.Admin)
        {
            return true;
        }

        if (actor.Role == UserRole.DepartmentHead)
        {
            return await OwnsAcademicCourseAsync(context, courseId, actorUserId, cancellationToken);
        }

        return false;
    }

    public static async Task<bool> OwnsAcademicCourseAsync(
        IApplicationDbContext context, Guid courseId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var course = await context.Courses.AsNoTracking().Include(course => course.AcademicProgram)
            .FirstOrDefaultAsync(course => course.Id == courseId, cancellationToken);
        return course is not null && AcademicCourseOwnership.IsOwnedBy(course, actorUserId);
    }
}
