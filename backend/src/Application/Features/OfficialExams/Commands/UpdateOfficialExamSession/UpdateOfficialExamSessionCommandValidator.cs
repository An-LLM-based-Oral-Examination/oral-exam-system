using System;
using System.Linq;
using FluentValidation;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateOfficialExamSession;

public sealed class UpdateOfficialExamSessionCommandValidator : AbstractValidator<UpdateOfficialExamSessionCommand>
{
    public UpdateOfficialExamSessionCommandValidator()
    {
        RuleFor(command => command.ActorUserId)
            .NotEmpty()
            .WithMessage("Không xác định được người cập nhật kỳ thi.");

        RuleFor(command => command.SessionId)
            .NotEmpty()
            .WithMessage("Mã kỳ thi không được để trống.");

        RuleFor(command => command.Title)
            .NotEmpty()
            .WithMessage("Tên kỳ thi không được để trống.")
            .MaximumLength(255)
            .WithMessage("Tên kỳ thi không được dài quá 255 ký tự.");

        RuleFor(command => command.ExamDate)
            .Must(date => date != default)
            .WithMessage("Ngày thi không được để trống.");

        RuleFor(command => command.ExamInputMode)
            .Must(mode => ExamInputMode.All.Contains(mode ?? string.Empty))
            .WithMessage("Phương thức làm bài chỉ được là VoiceOnly hoặc VoiceWithTranscriptEdit.");

        RuleFor(command => command.TranscriptBufferSeconds)
            .InclusiveBetween(OfficialExamRules.MinTranscriptBufferSeconds, OfficialExamRules.MaxTranscriptBufferSeconds)
            .WithMessage("Thời gian đệm phải từ 10 đến 300 giây.");
    }
}
