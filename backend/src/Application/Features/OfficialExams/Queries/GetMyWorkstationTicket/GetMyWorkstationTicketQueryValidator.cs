using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;

public sealed class GetMyWorkstationTicketQueryValidator : AbstractValidator<GetMyWorkstationTicketQuery>
{
    public GetMyWorkstationTicketQueryValidator()
    {
        RuleFor(query => query.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được sinh viên.");
    }
}
