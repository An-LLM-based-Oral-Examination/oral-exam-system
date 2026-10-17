using FluentValidation;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Speech.Queries.SynthesizeSpeech;

public sealed record SynthesizeSpeechQuery(
    string Text,
    string? VoiceName = null,
    double SpeakingRate = 1.0
) : IRequest<Result<SpeechSynthesisResult>>;

public sealed class SynthesizeSpeechQueryValidator : AbstractValidator<SynthesizeSpeechQuery>
{
    public SynthesizeSpeechQueryValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Nội dung văn bản cần đọc không được để trống.")
            .MaximumLength(4000).WithMessage("Nội dung văn bản cần đọc không được vượt quá 4000 ký tự.");

        RuleFor(x => x.SpeakingRate)
            .InclusiveBetween(0.75, 1.35)
            .WithMessage("Tốc độ đọc phải nằm trong khoảng từ 0.75x đến 1.35x.");
    }
}

public sealed class SynthesizeSpeechQueryHandler(ITextToSpeechService textToSpeechService)
    : IRequestHandler<SynthesizeSpeechQuery, Result<SpeechSynthesisResult>>
{
    public async Task<Result<SpeechSynthesisResult>> Handle(
        SynthesizeSpeechQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Result<SpeechSynthesisResult>.Failure("Nội dung văn bản cần đọc không được để trống.");
        }

        var clampedRate = Math.Clamp(request.SpeakingRate, 0.75, 1.35);

        try
        {
            var synthesisResult = await textToSpeechService.SynthesizeSpeechAsync(
                request.Text.Trim(),
                request.VoiceName,
                clampedRate,
                cancellationToken);

            return Result<SpeechSynthesisResult>.Success(synthesisResult);
        }
        catch (Exception ex)
        {
            return Result<SpeechSynthesisResult>.Failure($"Không thể tổng hợp giọng đọc AI: {ex.Message}");
        }
    }
}
