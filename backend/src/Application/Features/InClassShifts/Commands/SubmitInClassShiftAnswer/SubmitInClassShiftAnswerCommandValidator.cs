using FluentValidation;

namespace OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;

public sealed class SubmitInClassShiftAnswerCommandValidator : AbstractValidator<SubmitInClassShiftAnswerCommand>
{
    public SubmitInClassShiftAnswerCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("StudentId không được để trống.");

        RuleFor(x => x.ShiftId)
            .NotEmpty().WithMessage("ShiftId không được để trống.");

        RuleFor(x => x.QuestionId)
            .NotEmpty().WithMessage("QuestionId không được để trống.");

        RuleFor(x => x.TranscriptText)
            .NotEmpty().WithMessage(InClassShiftErrors.EmptyTranscript);
    }
}
