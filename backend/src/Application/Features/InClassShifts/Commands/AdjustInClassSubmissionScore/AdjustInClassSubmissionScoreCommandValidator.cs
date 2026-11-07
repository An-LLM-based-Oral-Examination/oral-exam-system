using FluentValidation;

namespace OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;

public sealed class AdjustInClassSubmissionScoreCommandValidator : AbstractValidator<AdjustInClassSubmissionScoreCommand>
{
    public AdjustInClassSubmissionScoreCommandValidator()
    {
        RuleFor(x => x.ActorUserId)
            .NotEmpty().WithMessage("ActorUserId không được để trống.");

        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("SubmissionId không được để trống.");

        RuleFor(x => x.FinalScore)
            .InclusiveBetween(0.0m, 10.0m)
            .WithMessage(InClassShiftErrors.InvalidScoreRange);
    }
}
