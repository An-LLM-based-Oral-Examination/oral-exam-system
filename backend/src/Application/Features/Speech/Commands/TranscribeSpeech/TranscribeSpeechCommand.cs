using FluentValidation;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Speech.Commands.TranscribeSpeech;

public sealed record TranscribeSpeechCommand(
    Stream AudioStream,
    string FileName,
    string? VocabularyHint = null,
    string? ClientTranscript = null
) : IRequest<Result<SpeechTranscriptionResult>>;

public sealed class TranscribeSpeechCommandValidator : AbstractValidator<TranscribeSpeechCommand>
{
    public TranscribeSpeechCommandValidator()
    {
        RuleFor(x => x.AudioStream)
            .NotNull().WithMessage("Luồng âm thanh không được để trống.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Tên tệp âm thanh không được để trống.");
    }
}

public sealed class TranscribeSpeechCommandHandler(ISpeechToTextService speechToTextService)
    : IRequestHandler<TranscribeSpeechCommand, Result<SpeechTranscriptionResult>>
{
    public async Task<Result<SpeechTranscriptionResult>> Handle(
        TranscribeSpeechCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AudioStream == null)
        {
            return Result<SpeechTranscriptionResult>.Failure("File ghi âm không hợp lệ hoặc rỗng.");
        }

        if (request.AudioStream.CanSeek && request.AudioStream.Length == 0)
        {
            return Result<SpeechTranscriptionResult>.Failure("File ghi âm không hợp lệ hoặc rỗng.");
        }

        try
        {
            var result = await speechToTextService.TranscribeDetailedAsync(
                request.AudioStream,
                string.IsNullOrWhiteSpace(request.FileName) ? "speech.webm" : request.FileName,
                request.VocabularyHint,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                return Result<SpeechTranscriptionResult>.Success(result);
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(request.ClientTranscript))
            {
                return Result<SpeechTranscriptionResult>.Failure($"Lỗi khi bóc băng âm thanh: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ClientTranscript))
        {
            var normalized = speechToTextService.NormalizeTranscript(request.ClientTranscript);
            var finalText = !string.IsNullOrWhiteSpace(normalized) ? normalized : request.ClientTranscript.Trim();
            return Result<SpeechTranscriptionResult>.Success(new SpeechTranscriptionResult(
                Text: finalText,
                RawText: request.ClientTranscript.Trim(),
                Provider: "BrowserWebSpeechHybrid",
                UsedFallback: true,
                TimestampsJson: null,
                ConfidenceScore: 0.90));
        }

        return Result<SpeechTranscriptionResult>.Failure("Không nhận diện được nội dung giọng nói trong bản ghi.");
    }
}
