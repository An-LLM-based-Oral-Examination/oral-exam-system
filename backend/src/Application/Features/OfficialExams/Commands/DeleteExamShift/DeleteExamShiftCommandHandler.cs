using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;

public sealed class DeleteExamShiftCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteExamShiftCommand, Result>
{
    public async Task<Result> Handle(DeleteExamShiftCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(context, request.ActorUserId, request.ActorRole,
                UserRole.ExaminationOfficer, cancellationToken)) return Result.Failure(OfficialExamErrors.Forbidden);
        if (request.ShiftId == Guid.Empty) return Result.Failure("Mã ca thi không được để trống.");
        return await context.ExecuteShiftMutationAsync(request.ShiftId, true, async ct =>
        {
            var shift = await context.RealExamSessionShifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);
            if (shift is null) return Result.Failure(OfficialExamErrors.ShiftNotFound);
            var conflict = await ShiftMutationGuard.Conflict(context, shift, true, ct);
            if (conflict is not null) return Result.Failure(conflict);
            context.RealExamSessionShifts.Remove(shift);
            await context.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}
