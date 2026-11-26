using System;
using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

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
            .NotEmpty().WithMessage("Loại vi phạm (EventType) không được để trống.")
            .Must(et => !string.IsNullOrWhiteSpace(et) && AntiCheatEventType.ProctorAllowed.Contains(et.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            .WithMessage($"Loại vi phạm (EventType) không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AntiCheatEventType.ProctorAllowed)}.");

        RuleFor(x => x.Severity)
            .Must(sev => AntiCheatSeverity.All.Contains(sev.Trim().ToLowerInvariant(), StringComparer.Ordinal))
            .When(x => !string.IsNullOrWhiteSpace(x.Severity))
            .WithMessage($"Mức độ vi phạm (Severity) không hợp lệ. Chỉ chấp nhận: {string.Join(", ", AntiCheatSeverity.All)}.");

        RuleFor(x => x.Details)
            .MaximumLength(2000).WithMessage("Chi tiết vi phạm (Details) tối đa 2000 ký tự.")
            .When(x => x.Details is not null);

        RuleFor(x => x)
            .Must(x => (x.TicketId.HasValue && x.TicketId.Value != Guid.Empty) ||
                       (x.StudentId.HasValue && x.StudentId.Value != Guid.Empty))
            .WithMessage("Phải cung cấp ít nhất TicketId hoặc StudentId để định danh thí sinh.");

        RuleFor(x => x.ClientEventId)
            .MaximumLength(128).WithMessage("ClientEventId tối đa 128 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ClientEventId));
    }
}
