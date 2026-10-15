using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;

public sealed class CheckInExamTicketCommandValidator : AbstractValidator<CheckInExamTicketCommand>
{
    public CheckInExamTicketCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người điểm danh.");

        RuleFor(command => command.TicketId)
            .NotEmpty()
            .WithMessage("Vé thi không được để trống.");
    }
}
