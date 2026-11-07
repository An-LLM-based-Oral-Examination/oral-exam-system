using MediatR;

namespace OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;

public sealed record CloseDueInClassShiftsCommand : IRequest<int>;
