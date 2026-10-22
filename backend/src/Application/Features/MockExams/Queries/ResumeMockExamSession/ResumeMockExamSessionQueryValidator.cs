using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Queries.ResumeMockExamSession;

public sealed class ResumeMockExamSessionQueryValidator : AbstractValidator<ResumeMockExamSessionQuery>
{
    public ResumeMockExamSessionQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
    }
}
