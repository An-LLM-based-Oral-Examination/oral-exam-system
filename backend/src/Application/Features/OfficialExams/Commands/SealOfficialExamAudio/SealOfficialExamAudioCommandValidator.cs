using FluentValidation;
using System.Text.RegularExpressions;

namespace OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;

public sealed partial class SealOfficialExamAudioCommandValidator : AbstractValidator<SealOfficialExamAudioCommand>
{
    public SealOfficialExamAudioCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RemoteIpAddress).NotEmpty();
        RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Sha256)
            .NotEmpty()
            .Must(value => value is not null && Sha256Pattern().IsMatch(value))
            .WithMessage("SHA-256 phải gồm đúng 64 ký tự hexadecimal.");
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
