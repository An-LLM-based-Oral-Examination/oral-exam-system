using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;

public sealed class CreateAudioUploadAuthorizationCommandValidator : AbstractValidator<CreateAudioUploadAuthorizationCommand>
{
    public CreateAudioUploadAuthorizationCommandValidator()
    {
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.RemoteIpAddress).NotEmpty();
        RuleFor(x => x.AtUtc).NotEmpty();
    }
}
