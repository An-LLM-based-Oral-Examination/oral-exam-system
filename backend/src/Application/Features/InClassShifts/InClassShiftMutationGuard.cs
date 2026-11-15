using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts;

internal static class InClassShiftMutationGuard
{
    public static async Task<string?> AuthorizeAsync(IApplicationDbContext context, Guid actorId,
        InClassExamShift shift, CancellationToken cancellationToken)
    {
        var actor = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
        if (actor is null || !actor.IsActive) return InClassShiftErrors.LecturerNotAuthorized;
        if (actor.Role == UserRole.Admin) return null;
        if (actor.Role == UserRole.DepartmentHead)
            return AcademicCourseOwnership.IsOwnedBy(shift.Class.Course, actor.Id) ? null : InClassShiftErrors.LecturerNotAuthorized;
        if (actor.Role != UserRole.Lecturer) return InClassShiftErrors.LecturerNotAuthorized;
        if (shift.LecturerId == actor.Id || shift.Class.LecturerId == actor.Id ||
            await context.CourseLecturers.AnyAsync(x => x.CourseId == shift.Class.CourseId && x.LecturerUserId == actor.Id, cancellationToken))
            return null;
        return InClassShiftErrors.LecturerNotAuthorized;
    }

    public static async Task<string?> CheckMutableAsync(IApplicationDbContext context,
        InClassExamShift shift, CancellationToken cancellationToken)
    {
        if (shift.FinalizedAt.HasValue || shift.Status == "FINALIZED") return ShiftMutationErrors.Finalized;
        if (shift.IsLocked) return ShiftMutationErrors.Locked;
        if (shift.Status != "SCHEDULED") return ShiftMutationErrors.NotScheduled;
        if (shift.StartTime <= DateTime.UtcNow) return ShiftMutationErrors.AlreadyStarted;
        if (await context.InClassExamSubmissions.AnyAsync(x => x.ShiftId == shift.Id, cancellationToken)) return ShiftMutationErrors.HasSubmissions;
        if (await context.InClassShiftParticipants.AnyAsync(x => x.ShiftId == shift.Id, cancellationToken)) return ShiftMutationErrors.HasParticipants;
        if (await context.InClassShiftPasswords.AnyAsync(x => x.ShiftId == shift.Id, cancellationToken)) return ShiftMutationErrors.HasPasswords;
        return null;
    }
}
