using System;
using System.Linq;
using FluentValidation;
using OralExamination.Application.Common.Validation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;

public sealed class CreateDraftQuestionCommandValidator : AbstractValidator<CreateDraftQuestionCommand>
{
    public CreateDraftQuestionCommandValidator()
    {
        RuleFor(v => v.CourseId).NotEmpty().WithMessage("Mã môn học không được để trống.");
        RuleFor(v => v.LecturerId).NotEmpty().WithMessage("Định danh giảng viên không được để trống.");
        RuleFor(v => v.Title).NotEmpty().MaximumLength(300).WithMessage("Tiêu đề không được để trống và <= 300 ký tự.");
        RuleFor(v => v.Content).NotEmpty().WithMessage("Nội dung câu hỏi không được để trống.");

        RuleFor(v => v.SampleAnswer)
            .Must(ans => string.IsNullOrEmpty(ans) || ans.Trim().Length >= 50)
            .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
            .WithMessage("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");

        RuleFor(v => v.Difficulty)
            .Must(d => !string.IsNullOrWhiteSpace(d) &&
                d.Trim().ToLowerInvariant() is QuestionDifficulty.Easy or QuestionDifficulty.Medium or QuestionDifficulty.Hard)
            .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
            .WithMessage("Độ khó phải là 'easy', 'medium', hoặc 'hard'.");

        RuleFor(v => v.Source)
            .Must(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length <= 50)
            .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
            .WithMessage("Nguồn câu hỏi (Source) không được vượt quá 50 ký tự.");

        RuleFor(v => v.BloomLevel)
            .Must(b => BloomLevel.All.Contains(b))
            .WithMessage("Bậc Bloom không hợp lệ.");

        RuleFor(v => v.ImageUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || (url.Trim().Length <= 1000 && QuestionImageUrlValidator.IsSafeImageUrl(url.Trim())))
            .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
            .WithMessage(QuestionImageUrlValidator.InvalidImageUrlError);

        RuleFor(v => v.Rubric)
            .NotNull().WithMessage("Barem Rubric không được để trống.")
            .When(v => !v.RubricId.HasValue || v.RubricId.Value == Guid.Empty);

        When(v => v.Rubric != null, () =>
        {
            RuleFor(v => v.Rubric.Name)
                .NotEmpty().WithMessage("Tên Barem Rubric không được để trống.")
                .MaximumLength(RubricNormalization.MaxNameLength)
                .WithMessage("Tên Barem Rubric không được vượt quá 200 ký tự.");

            RuleFor(v => v.Rubric.Criteria)
                .NotNull()
                .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                .WithMessage("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.")
                .Must(c => c != null && c.Count >= 2 && c.All(item => item != null))
                .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                .WithMessage("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

            // RÀNG BUỘC BẤT BIẾN: Tổng điểm các tiêu chí rubric con PHẢI BẰNG CHÍNH XÁC 10.00
            RuleFor(v => v.Rubric.Criteria)
                .Must(criteria => criteria != null && criteria.All(c => c != null) && criteria.Sum(c => c.MaxScore) == 10.00m)
                .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                .WithMessage("Tổng điểm tối đa của các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

            RuleForEach(v => v.Rubric.Criteria)
                .Where(c => c != null)
                .ChildRules(criterion =>
                {
                    criterion.RuleFor(c => c.CriterionName)
                        .NotEmpty().WithMessage("Tên tiêu chí con không được để trống.")
                        .MaximumLength(RubricNormalization.MaxNameLength)
                        .WithMessage("Tên tiêu chí con không được vượt quá 200 ký tự.");
                    criterion.RuleFor(c => c.MaxScore)
                        .GreaterThan(0)
                        .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                        .WithMessage("Điểm tối đa của từng tiêu chí con phải lớn hơn 0.")
                        .Must(RubricNormalization.HasValidScorePrecision)
                        .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                        .WithMessage("Điểm tối đa của từng tiêu chí con chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");
                    criterion.RuleFor(c => c.Weight)
                        .GreaterThan(0)
                        .WithErrorCode(ValidationErrorCodes.UnprocessableEntity)
                        .WithMessage("Trọng số của từng tiêu chí con phải lớn hơn 0.");
                });
        });
    }
}
