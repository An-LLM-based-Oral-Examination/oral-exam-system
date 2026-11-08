using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.InClassShifts;

namespace OralExamination.Application.Features.InClassShifts.Commands.ExpirePersonalInClassExams;

public sealed class ExpirePersonalInClassExamsCommandHandler(
    IApplicationDbContext context,
    IGradingQueueChannel gradingQueue)
    : IRequestHandler<ExpirePersonalInClassExamsCommand, int>
{
    public Task<int> Handle(ExpirePersonalInClassExamsCommand request, CancellationToken cancellationToken) =>
        InClassShiftAutoSubmit.SubmitExpiredPersonalExamsAsync(context, gradingQueue, cancellationToken);
}
