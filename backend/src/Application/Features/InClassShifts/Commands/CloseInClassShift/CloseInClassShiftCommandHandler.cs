using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.CloseInClassShift;

public sealed class CloseInClassShiftCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueue)
    : IRequestHandler<CloseInClassShiftCommand, Result<CloseInClassShiftResponse>>
{
    public async Task<Result<CloseInClassShiftResponse>> Handle(
        CloseInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotFound);
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
                return Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized);
            }
        }

        if (shift.Status is "CLOSED" or "ENDED")
        {
            return Result<CloseInClassShiftResponse>.Success(
                new CloseInClassShiftResponse(shift.Id, shift.ShiftName, "CLOSED"));
        }

        if (shift.IsLocked || shift.Status == "FINALIZED" || !InClassShiftLifecycle.IsClosable(shift.Status))
        {
            return Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotClosable);
        }

        shift.Status = "CLOSED";
        shift.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await InClassShiftAutoSubmit.SubmitMissingAsync(context, gradingQueue, cancellationToken, shift.Id);

        return Result<CloseInClassShiftResponse>.Success(
            new CloseInClassShiftResponse(shift.Id, shift.ShiftName, shift.Status));
    }
}
