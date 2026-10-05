using System.Linq;
using FluentValidation;

namespace OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;

public sealed class ReviewQuestionDecisionCommandValidator : AbstractValidator<ReviewQuestionDecisionCommand>
{
    private static readonly string[] AllowedDecisions = { "APPROVED", "NEEDS_REVISION", "REJECTED" };

    public ReviewQuestionDecisionCommandValidator()
    {
        RuleFor(v => v.QuestionId).NotEmpty().WithMessage("Mã câu hỏi không được để trống.");
        RuleFor(v => v.ReviewerId).NotEmpty().WithMessage("Định danh cán bộ thẩm định không được để trống.");

        RuleFor(v => v.Decision)
            .Must(d => AllowedDecisions.Contains(d))
            .WithMessage("Quyết định thẩm định không hợp lệ. Chỉ chấp nhận APPROVED, NEEDS_REVISION hoặc REJECTED.");

        RuleFor(v => v.ReviewNotes)
            .NotEmpty().WithMessage("Lý do hoặc nhận xét thẩm định không được để trống.")
            .MinimumLength(10).WithMessage("Nhận xét thẩm định bắt buộc từ 10 ký tự trở lên để hướng dẫn giảng viên.");
    }
}
