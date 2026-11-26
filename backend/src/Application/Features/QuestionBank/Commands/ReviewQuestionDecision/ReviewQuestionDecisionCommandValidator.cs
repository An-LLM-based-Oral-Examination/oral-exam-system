using System;
using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;

public sealed class ReviewQuestionDecisionCommandValidator : AbstractValidator<ReviewQuestionDecisionCommand>
{
    private static readonly string[] AllowedDecisions = { "APPROVED", "NEEDS_REVISION", "REJECTED" };

    public ReviewQuestionDecisionCommandValidator()
    {
        RuleFor(v => v.QuestionId).NotEmpty().WithMessage("Mã câu hỏi không được để trống.");
        RuleFor(v => v.ReviewerId).NotEmpty().WithMessage("Định danh cán bộ thẩm định không được để trống.");

        RuleFor(v => v.Decision)
            .Must(d => !string.IsNullOrWhiteSpace(d) && AllowedDecisions.Contains(d.Trim(), StringComparer.Ordinal))
            .WithMessage("Quyết định thẩm định không hợp lệ. Chỉ chấp nhận APPROVED, NEEDS_REVISION hoặc REJECTED.");

        RuleFor(v => v.ReviewNotes)
            .NotEmpty().WithMessage("Lý do hoặc nhận xét thẩm định không được để trống.")
            .MinimumLength(10).WithMessage("Nhận xét thẩm định bắt buộc từ 10 ký tự trở lên để hướng dẫn giảng viên.")
            .Must(notes => string.IsNullOrWhiteSpace(notes) || notes.Trim().Length >= 10)
            .WithMessage("Nhận xét thẩm định bắt buộc từ 10 ký tự trở lên để hướng dẫn giảng viên.")
            .When(v => RequiresReviewNotes(v.Decision));

        RuleFor(v => v.ReviewNotes)
            .MaximumLength(2000).WithMessage("Nhận xét thẩm định không được vượt quá 2000 ký tự.")
            .When(v => !string.IsNullOrEmpty(v.ReviewNotes));
    }

    private static bool RequiresReviewNotes(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return false;
        }

        var trimmed = decision.Trim();
        return string.Equals(trimmed, ApprovalStatus.NeedsRevision, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, ApprovalStatus.Rejected, StringComparison.OrdinalIgnoreCase);
    }
}
