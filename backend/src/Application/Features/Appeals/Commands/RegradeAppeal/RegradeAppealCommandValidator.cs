using FluentValidation;

namespace OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;

public sealed class RegradeAppealCommandValidator : AbstractValidator<RegradeAppealCommand>
{
    public RegradeAppealCommandValidator()
    {
        RuleFor(x => x.AppealId).NotEmpty();
        RuleFor(x => x.LecturerId).NotEmpty();
        RuleFor(x => x.NewScore).InclusiveBetween(0m, 10m);
        RuleFor(x => x.ReviewNotes).NotEmpty().MaximumLength(2000);
    }
}
