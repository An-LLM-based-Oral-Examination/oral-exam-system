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

        SpeechTranscriptionResult? detailedResult = null;
        Exception? sttException = null;
        try
        {
            var result = detailedResult = await speechToTextService.TranscribeDetailedAsync(
                request.AudioStream,
                string.IsNullOrWhiteSpace(request.FileName) ? "speech.webm" : request.FileName,
                request.VocabularyHint,
                cancellationToken);

            var sanitizedServer = !string.IsNullOrWhiteSpace(result?.Text)
                ? TranscriptHelper.NormalizeOrFallback(result.Text, speechToTextService.NormalizeTranscript(result.Text))
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(sanitizedServer))
            {
                TranscriptHelper.RegisterVerifiedTranscript(sanitizedServer);
                return Result<SpeechTranscriptionResult>.Success(result! with { Text = sanitizedServer });
            }
        }
        catch (Exception ex)
        {
            sttException = ex;
            if (string.IsNullOrWhiteSpace(request.ClientTranscript))
            {
                return Result<SpeechTranscriptionResult>.Failure($"Lỗi khi bóc băng âm thanh: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ClientTranscript) && !TranscriptHelper.IsServerConfirmedSilence(detailedResult, sttException))
        {
            var normalized = speechToTextService.NormalizeTranscript(request.ClientTranscript);
            var finalText = TranscriptHelper.NormalizeOrFallback(request.ClientTranscript, normalized);

            if (!string.IsNullOrWhiteSpace(finalText))
            {
                return Result<SpeechTranscriptionResult>.Success(new SpeechTranscriptionResult(
                    Text: finalText,
                    RawText: request.ClientTranscript.Trim(),
                    Provider: "BrowserWebSpeechHybrid",
                    UsedFallback: true,
                    TimestampsJson: null,
                    ConfidenceScore: 0.65));
            }
        }

        return Result<SpeechTranscriptionResult>.Failure("Không nhận diện được nội dung giọng nói trong bản ghi.");
    }
}
