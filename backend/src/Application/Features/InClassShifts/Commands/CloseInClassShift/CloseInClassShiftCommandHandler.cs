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
        var outcome = await context.ExecuteShiftMutationAsync(
            request.ShiftId,
            isOfficial: false,
            async ct =>
            {
                var actor = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, ct);

                if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
                {
                    return new CloseShiftOutcome(Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized), false);
                }

                var shift = await context.InClassExamShifts
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

                if (shift is null)
                {
                    return new CloseShiftOutcome(Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotFound), false);
                }

                if (actor.Role == UserRole.Lecturer)
                {
                    bool isAssigned = shift.Class.LecturerId == actor.Id;

                    if (!isAssigned)
                    {
                        return new CloseShiftOutcome(Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized), false);
                    }
                }

                if (shift.Status is "CLOSED" or "ENDED")
                {
                    return new CloseShiftOutcome(
                        Result<CloseInClassShiftResponse>.Success(new CloseInClassShiftResponse(shift.Id, shift.ShiftName, "CLOSED")),
                        false);
                }

                if (shift.IsLocked || shift.Status == "FINALIZED" || !InClassShiftLifecycle.IsClosable(shift.Status))
                {
                    return new CloseShiftOutcome(Result<CloseInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotClosable), false);
                }

                shift.Status = "CLOSED";
                shift.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);

                return new CloseShiftOutcome(
                    Result<CloseInClassShiftResponse>.Success(new CloseInClassShiftResponse(shift.Id, shift.ShiftName, shift.Status)),
                    true);
            },
            cancellationToken);

        if (outcome.ShouldAutoSubmit)
        {
            await InClassShiftAutoSubmit.SubmitMissingAsync(
                context,
                gradingQueue,
                cancellationToken,
                request.ShiftId,
                respectClosedGrace: false);
        }

        return outcome.Result;
    }

    private sealed record CloseShiftOutcome(Result<CloseInClassShiftResponse> Result, bool ShouldAutoSubmit);
}
