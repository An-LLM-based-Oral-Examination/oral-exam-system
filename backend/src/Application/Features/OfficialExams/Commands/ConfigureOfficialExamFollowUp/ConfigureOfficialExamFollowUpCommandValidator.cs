using FluentValidation;

namespace OralExamination.Application.Features.OfficialExams.Commands.ConfigureOfficialExamFollowUp;

public sealed class ConfigureOfficialExamFollowUpCommandValidator : AbstractValidator<ConfigureOfficialExamFollowUpCommand>
{
    public ConfigureOfficialExamFollowUpCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người cấu hình.");

        RuleFor(command => command.SessionId)
            .NotEmpty()
            .WithMessage("Kỳ thi không được để trống.");

        RuleFor(command => command.MaxFollowUpQuestions)
            .InclusiveBetween(1, 5)
            .WithMessage("Số câu hỏi follow-up phải từ 1 đến 5.");
    }
}
