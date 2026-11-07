using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.InClassShifts;

namespace OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;

public sealed class CloseDueInClassShiftsCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueue)
    : IRequestHandler<CloseDueInClassShiftsCommand, int>
{
    public async Task<int> Handle(CloseDueInClassShiftsCommand request, CancellationToken cancellationToken)
    {
        var closed = await InClassShiftLifecycle.CloseDueAsync(context, DateTime.UtcNow, cancellationToken);
        await InClassShiftAutoSubmit.SubmitMissingAsync(context, gradingQueue, cancellationToken);
        return closed;
    }
}
