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

        RuleFor(command => command)
            .Must(command => command.StudentId != Guid.Empty || !string.IsNullOrWhiteSpace(command.StudentCode))
            .WithMessage("Sinh viên (studentId hoặc studentCode) không được để trống.");

        RuleFor(command => command.StudentCode)
            .MaximumLength(20)
            .When(command => !string.IsNullOrWhiteSpace(command.StudentCode))
            .WithMessage("Mã sinh viên không được vượt quá 20 ký tự.");

        RuleFor(command => command.SeatNumber)
            .InclusiveBetween(OfficialExamRules.MinSeat, OfficialExamRules.MaxSeat)
            .WithMessage(OfficialExamErrors.SeatOutOfRange);

        RuleFor(command => command.IpAddress)
            .Must(address => WorkstationAddress.TryNormalize(address, out _))
            .When(command => !string.IsNullOrWhiteSpace(command.IpAddress))
            .WithMessage(OfficialExamErrors.IpInvalid);
    }
}
