using MediatR;

namespace OralExamination.Application.Features.InClassShifts.Commands.ExpirePersonalInClassExams;

public sealed record ExpirePersonalInClassExamsCommand : IRequest<int>;
