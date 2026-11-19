using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals;

internal static class AppealAuthorization
{
    internal const string Forbidden = "FORBIDDEN: Bạn không có quyền truy cập hoặc xử lý đơn phúc khảo này.";

    internal static async Task<User?> ActiveClaimedActorAsync(
        IApplicationDbContext context,
        Guid? actorId,
        string? claimedRole,
        CancellationToken cancellationToken)
    {
        if (!actorId.HasValue || actorId.Value == Guid.Empty || string.IsNullOrWhiteSpace(claimedRole))
        {
            return null;
        }

        var actor = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == actorId.Value && user.IsActive, cancellationToken);
        return actor is not null && string.Equals(actor.Role, claimedRole, StringComparison.Ordinal)
            ? actor
            : null;
    }

    internal static async Task<bool> OwnsSessionCourseAsync(
        IApplicationDbContext context,
        Guid sessionId,
        Guid departmentHeadId,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions.AsNoTracking()
            .Include(item => item.Course)
                .ThenInclude(course => course.AcademicProgram)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        return session is not null && AcademicCourseOwnership.IsOwnedBy(session.Course, departmentHeadId);
    }

    internal static bool IsReadRole(string role) => role is
        UserRole.Student or UserRole.Lecturer or UserRole.DepartmentHead or UserRole.ExaminationOfficer;
}
