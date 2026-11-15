using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.InClassShifts.Commands.DeleteInClassShift;

public sealed record DeleteInClassShiftCommand(Guid ActorUserId, Guid ShiftId) : IRequest<Result>;

public sealed class DeleteInClassShiftCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteInClassShiftCommand, Result>
{
    public Task<Result> Handle(DeleteInClassShiftCommand request, CancellationToken cancellationToken) =>
        context.ExecuteShiftMutationAsync(request.ShiftId, false, async ct =>
        {
            var shift = await context.InClassExamShifts.Include(x => x.Class).ThenInclude(x => x.Course).ThenInclude(x => x.AcademicProgram)
                .FirstOrDefaultAsync(x => x.Id == request.ShiftId, ct);
            if (shift is null) return Result.Failure(InClassShiftErrors.ShiftNotFound);
            var error = await InClassShiftMutationGuard.AuthorizeAsync(context, request.ActorUserId, shift, ct)
                ?? await InClassShiftMutationGuard.CheckMutableAsync(context, shift, ct);
            if (error is not null) return Result.Failure(error);
            context.InClassExamShifts.Remove(shift);
            await context.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
}
