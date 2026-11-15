using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Proctor;

internal static class ProctorAuthorization
{
    internal const string Forbidden = "FORBIDDEN: Bạn không có quyền thao tác ca thi này.";

    internal static async Task<bool> CanAccessShiftAsync(IApplicationDbContext db, Guid actorId, Guid shiftId, bool readOnly, CancellationToken ct)
    {
        if (actorId == Guid.Empty) return false;
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId && u.IsActive, ct);
        if (actor is null) return false;
        if (readOnly && actor.Role == UserRole.ExaminationOfficer) return true;
        return actor.Role == UserRole.Proctor && await db.RealExamSessionShifts.AsNoTracking()
            .AnyAsync(s => s.Id == shiftId && s.ProctorUserId == actorId, ct);
    }

    internal static Task<bool> CanSuspendAsync(IApplicationDbContext db, Guid actorId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(u => u.Id == actorId && u.IsActive && u.Role == UserRole.ExaminationOfficer, ct);
}
