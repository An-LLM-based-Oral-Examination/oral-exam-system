using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;

/// <summary>
/// Validator cho lệnh thẩm định đơn phúc khảo:
/// - AppealId, ReviewerId không rỗng.
/// - Decision phải là 'APPROVED' hoặc 'REJECTED'.
/// - ReviewNotes không rỗng, tối thiểu 10 ký tự.
/// - Khi Decision == 'APPROVED': ProposedScore không null và nằm trong khoảng [0, 10.00].
/// </summary>
public sealed class ReviewAppealDecisionCommandValidator : AbstractValidator<ReviewAppealDecisionCommand>
{
    public ReviewAppealDecisionCommandValidator()
    {
        RuleFor(v => v.AppealId)
            .NotEmpty()
            .WithMessage("Mã đơn phúc khảo không được để trống.");

        RuleFor(v => v.ReviewerId)
            .NotEmpty()
            .WithMessage("Mã người thẩm định không được để trống.");

        RuleFor(v => v.Decision)
            .NotEmpty()
            .WithMessage("Quyết định thẩm định không được để trống.")
            .Must(d => d == AppealStatus.Approved || d == AppealStatus.Rejected)
            .WithMessage("Quyết định thẩm định chỉ được là APPROVED hoặc REJECTED.");

        RuleFor(v => v.ReviewNotes)
            .NotEmpty()
            .WithMessage("Ghi chú thẩm định không được để trống.")
            .MinimumLength(10)
            .WithMessage("Ghi chú thẩm định phải có tối thiểu 10 ký tự.");

        When(v => v.Decision == AppealStatus.Approved, () =>
        {
            RuleFor(v => v.ProposedScore)
                .NotNull()
                .WithMessage("Điểm đề xuất mới là bắt buộc khi phê duyệt đơn phúc khảo.")
                .InclusiveBetween(0m, 10.00m)
                .WithMessage("Điểm đề xuất mới phải nằm trong khoảng từ 0.00 đến 10.00 điểm.");
        });
    }
}
