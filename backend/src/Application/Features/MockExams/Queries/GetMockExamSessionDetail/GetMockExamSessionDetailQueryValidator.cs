using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;

/// <summary>
/// Validator cho GetMockExamSessionDetailQuery.
/// </summary>
public sealed class GetMockExamSessionDetailQueryValidator : AbstractValidator<GetMockExamSessionDetailQuery>
{
    public GetMockExamSessionDetailQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Mã ca thi thử không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên không được để trống.")
            .When(x => !x.IsElevatedRole);
    }
}
