using FluentValidation;

namespace OralExamination.Application.Features.Proctor.Commands.RecordProctorViolation;

public sealed class RecordProctorViolationCommandValidator : AbstractValidator<RecordProctorViolationCommand>
{
    public RecordProctorViolationCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId không được để trống.");

        RuleFor(x => x.ShiftId)
            .NotEmpty().WithMessage("ShiftId không được để trống.");

        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("Loại vi phạm (EventType) không được để trống.");

        RuleFor(x => x)
            .Must(x => (x.TicketId.HasValue && x.TicketId.Value != Guid.Empty) ||
                       (x.StudentId.HasValue && x.StudentId.Value != Guid.Empty))
            .WithMessage("Phải cung cấp ít nhất TicketId hoặc StudentId để định danh thí sinh.");
    }
}
