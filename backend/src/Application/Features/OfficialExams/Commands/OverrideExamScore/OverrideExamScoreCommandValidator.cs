using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;

public sealed class OverrideExamScoreCommandValidator : AbstractValidator<OverrideExamScoreCommand>
{
    public OverrideExamScoreCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("Định danh giảng viên hậu kiểm không được để trống.");

        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Mã vé thi (TicketId) không được để trống.");

        RuleFor(x => x.AuditedScore)
            .InclusiveBetween(0.0m, 10.0m)
            .WithMessage("Điểm hậu kiểm phải nằm trong thang điểm từ 0.0 đến 10.0.");

        RuleFor(x => x.OverrideReason)
            .NotEmpty().WithMessage("Bắt buộc phải nhập lý do ghi đè điểm (override_reason).")
            .Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 10)
            .WithMessage("Lý do ghi đè điểm (override_reason) phải có tối thiểu 10 ký tự.");
    }
}
