using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.ActivateInClassShift;

public sealed class ActivateInClassShiftCommandHandler(
    IApplicationDbContext context)
    : IRequestHandler<ActivateInClassShiftCommand, Result<ActivateInClassShiftResponse>>
{
    public async Task<Result<ActivateInClassShiftResponse>> Handle(
        ActivateInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<ActivateInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<ActivateInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (actor.Role == UserRole.Lecturer)
        {
            bool isAssigned = shift.LecturerId == actor.Id ||
                shift.Class.LecturerId == actor.Id ||
                await context.CourseLecturers.AnyAsync(
                    cl => cl.CourseId == shift.Class.CourseId && cl.LecturerUserId == actor.Id,
                    cancellationToken);

            if (!isAssigned)
            {
                return Result<ActivateInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        if (shift.Status == "IN_PROGRESS")
        {
            return Result<ActivateInClassShiftResponse>.Success(
                new ActivateInClassShiftResponse(shift.Id, shift.ShiftName, shift.Status));
        }

        if (shift.IsLocked || shift.Status is "FINALIZED" or "CLOSED" or "ENDED")
        {
            return Result<ActivateInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotActivatable);
        }

        shift.Status = "IN_PROGRESS";
        shift.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return Result<ActivateInClassShiftResponse>.Success(
            new ActivateInClassShiftResponse(shift.Id, shift.ShiftName, shift.Status));
    }
}
