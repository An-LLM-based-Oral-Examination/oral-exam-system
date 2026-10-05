using FluentValidation;

namespace OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;

public sealed class BatchSubmitQuestionsForReviewCommandValidator : AbstractValidator<BatchSubmitQuestionsForReviewCommand>
{
    public BatchSubmitQuestionsForReviewCommandValidator()
    {
        RuleFor(v => v.CourseId).NotEmpty().WithMessage("Mã môn học không được để trống.");
        RuleFor(v => v.LecturerId).NotEmpty().WithMessage("Định danh giảng viên không được để trống.");
        RuleFor(v => v.QuestionIds)
            .NotNull().NotEmpty().WithMessage("Danh sách câu hỏi đệ trình không được để trống.");
        RuleFor(v => v.SubmissionNotes)
            .MaximumLength(1000).WithMessage("Ghi chú đệ trình tối đa 1000 ký tự.");
    }
}
