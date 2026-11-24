using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;

public sealed class LogAntiCheatEventCommandValidator : AbstractValidator<LogAntiCheatEventCommand>
{
    public LogAntiCheatEventCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId không được để trống.");

        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("TicketId không được để trống.");

        RuleFor(x => x.ShiftId)
            .NotEmpty().WithMessage("ShiftId không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("EventType không được để trống.")
            .Must(type => AntiCheatEventType.All.Contains(type))
            .WithMessage($"EventType không hợp lệ. Cho phép: {string.Join(", ", AntiCheatEventType.All)}");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity không được để trống.")
            .Must(sev => AntiCheatSeverity.All.Contains(sev))
            .WithMessage($"Severity không hợp lệ. Cho phép: {string.Join(", ", AntiCheatSeverity.All)}");

        RuleFor(x => x.Details)
            .MaximumLength(2000).WithMessage("Chi tiết sự kiện an ninh (Details) tối đa 2000 ký tự.")
            .When(x => x.Details is not null);

        RuleFor(x => x.ClientEventId)
            .MaximumLength(128).WithMessage("ClientEventId tối đa 128 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ClientEventId));
    }
}
