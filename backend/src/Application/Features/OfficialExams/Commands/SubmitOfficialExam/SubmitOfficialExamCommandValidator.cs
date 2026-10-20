using System.Text.Json;
using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;

public sealed class SubmitOfficialExamCommandValidator : AbstractValidator<SubmitOfficialExamCommand>
{
    public SubmitOfficialExamCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RemoteIpAddress).NotEmpty();
        RuleFor(x => x.Answers).NotNull().NotEmpty();
        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(x => x.ExamQuestionId).NotEmpty();
            answer.RuleFor(x => x.TranscriptWhisper)
                .NotEmpty()
                .MaximumLength(50_000);
            answer.RuleFor(x => x.TimeSpentSeconds).InclusiveBetween(0, 14_400);
            answer.RuleFor(x => x.TimestampsWhisper)
                .Must(BeJsonOrEmpty)
                .WithMessage("Timestamps Whisper phải là JSON hợp lệ.");
        });
    }

    private static bool BeJsonOrEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
