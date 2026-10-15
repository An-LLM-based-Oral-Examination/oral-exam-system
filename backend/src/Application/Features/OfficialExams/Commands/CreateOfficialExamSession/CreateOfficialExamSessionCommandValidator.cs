using FluentValidation;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;

public sealed class CreateOfficialExamSessionCommandValidator : AbstractValidator<CreateOfficialExamSessionCommand>
{
    public CreateOfficialExamSessionCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người tạo kỳ thi.");

        RuleFor(command => command.CourseId)
            .NotEmpty()
            .WithMessage("Môn học không được để trống.");

        RuleFor(command => command.ExamStructureId)
            .NotEmpty()
            .WithMessage("Cấu trúc đề không được để trống.");

        RuleFor(command => command.Title)
            .NotEmpty()
            .WithMessage("Tên kỳ thi không được để trống.")
            .MaximumLength(255)
            .WithMessage("Tên kỳ thi không được dài quá 255 ký tự.");

        RuleFor(command => command.ExamDate)
            .Must(date => date != default)
            .WithMessage("Ngày thi không được để trống.");

        RuleFor(command => command.MaxFollowUpQuestions)
            .InclusiveBetween(OfficialExamRules.MinFollowUpQuestions, OfficialExamRules.MaxFollowUpQuestions)
            .WithMessage("Số câu hỏi phụ phải từ 1 đến 5.");

        RuleFor(command => command.ExamInputMode)
            .Must(mode => ExamInputMode.All.Contains(mode ?? string.Empty))
            .WithMessage("Phương thức làm bài chỉ được là VoiceOnly hoặc VoiceWithTranscriptEdit.");

        RuleFor(command => command.TranscriptBufferSeconds)
            .InclusiveBetween(OfficialExamRules.MinTranscriptBufferSeconds, OfficialExamRules.MaxTranscriptBufferSeconds)
            .WithMessage("Thời gian đệm phải từ 10 đến 300 giây.");
    }
}
