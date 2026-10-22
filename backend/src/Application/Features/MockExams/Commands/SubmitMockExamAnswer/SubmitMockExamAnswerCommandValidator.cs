using FluentValidation;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;

/// <summary>
/// Validator cho SubmitMockExamAnswerCommand tuân thủ FluentValidation.
/// </summary>
public sealed class SubmitMockExamAnswerCommandValidator : AbstractValidator<SubmitMockExamAnswerCommand>
{
    public SubmitMockExamAnswerCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Mã ca thi thử không được để trống.");

        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Mã sinh viên không được để trống.");

        RuleFor(x => x.QuestionId)
            .NotEmpty().WithMessage("Mã câu hỏi không được để trống.");

        RuleFor(x => x.TimeTakenSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian làm bài không được là số âm.");

        RuleFor(x => x)
            .Must(x => (x.AudioStream != null && (!x.AudioStream.CanSeek || x.AudioStream.Length > 0)) || !string.IsNullOrWhiteSpace(x.DirectTranscript))
            .WithMessage("Bắt buộc phải cung cấp file âm thanh ghi âm (.webm, .mp3, .wav) hoặc transcript câu trả lời.");
    }
}
