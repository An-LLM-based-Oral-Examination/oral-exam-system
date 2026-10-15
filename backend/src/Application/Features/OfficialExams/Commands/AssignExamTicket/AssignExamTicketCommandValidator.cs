using FluentValidation;
using OralExamination.Application.Features.OfficialExams;

namespace OralExamination.Application.Features.OfficialExams.Commands.AssignExamTicket;

public sealed class AssignExamTicketCommandValidator : AbstractValidator<AssignExamTicketCommand>
{
    public AssignExamTicketCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người gán vé thi.");

        RuleFor(command => command.ShiftId)
            .NotEmpty()
            .WithMessage("Ca thi không được để trống.");

        RuleFor(command => command.StudentId)
            .NotEmpty()
            .WithMessage("Sinh viên không được để trống.");

        RuleFor(command => command.SeatNumber)
            .InclusiveBetween(OfficialExamRules.MinSeat, OfficialExamRules.MaxSeat)
            .WithMessage(OfficialExamErrors.SeatOutOfRange);

        RuleFor(command => command.IpAddress)
            .Must(address => WorkstationAddress.TryNormalize(address, out _))
            .WithMessage(OfficialExamErrors.IpInvalid);
    }
}
