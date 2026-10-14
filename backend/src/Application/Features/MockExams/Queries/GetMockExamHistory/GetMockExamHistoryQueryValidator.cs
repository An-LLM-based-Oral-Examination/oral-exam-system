using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;

/// <summary>
/// Validator cho GetMockExamHistoryQuery.
/// </summary>
public sealed class GetMockExamHistoryQueryValidator : AbstractValidator<GetMockExamHistoryQuery>
{
    public GetMockExamHistoryQueryValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên không được để trống.");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Chỉ số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
    }
}
