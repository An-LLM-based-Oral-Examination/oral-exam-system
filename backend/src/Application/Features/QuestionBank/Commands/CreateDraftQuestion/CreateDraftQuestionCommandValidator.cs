using System.Linq;
using FluentValidation;
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
            .WithMessage("Câu trả lời mẫu (Model Answer) khi cung cấp bắt buộc phải từ 50 ký tự trở lên.");

        RuleFor(v => v.Difficulty)
            .Must(d => QuestionDifficulty.All.Contains(d))
            .WithMessage("Độ khó phải là 'easy', 'medium', hoặc 'hard'.");

        RuleFor(v => v.BloomLevel)
            .Must(b => BloomLevel.All.Contains(b))
            .WithMessage("Bậc Bloom không hợp lệ.");

        RuleFor(v => v.Rubric)
            .NotNull().WithMessage("Barem Rubric không được để trống.");

        RuleFor(v => v.Rubric.Name)
            .NotEmpty().WithMessage("Tên Barem Rubric không được để trống.");

        RuleFor(v => v.Rubric.Criteria)
            .NotNull().Must(c => c != null && c.Count >= 2)
            .WithMessage("Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");

        // RÀNG BUỘC BẤT BIẾN: Tổng điểm các tiêu chí rubric con PHẢI BẰNG CHÍNH XÁC 10.00
        RuleFor(v => v.Rubric.Criteria)
            .Must(criteria => criteria != null && criteria.Sum(c => c.MaxScore) == 10.00m)
            .WithMessage("Tổng điểm tối đa của các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");

        RuleForEach(v => v.Rubric.Criteria).ChildRules(criterion =>
        {
            criterion.RuleFor(c => c.CriterionName)
                .NotEmpty().WithMessage("Tên tiêu chí con không được để trống.");
            criterion.RuleFor(c => c.MaxScore)
                .GreaterThan(0).WithMessage("Điểm tối đa của từng tiêu chí con phải lớn hơn 0.");
            criterion.RuleFor(c => c.Weight)
                .GreaterThan(0).WithMessage("Trọng số của từng tiêu chí con phải lớn hơn 0.");
        });
    }
}
