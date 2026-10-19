using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Speech.Queries.GetTtsVoices;

public sealed record GetTtsVoicesQuery : IRequest<Result<IReadOnlyList<TtsVoiceOptionDto>>>;

public sealed class GetTtsVoicesQueryHandler(ITextToSpeechService textToSpeechService)
    : IRequestHandler<GetTtsVoicesQuery, Result<IReadOnlyList<TtsVoiceOptionDto>>>
{
    public Task<Result<IReadOnlyList<TtsVoiceOptionDto>>> Handle(
        GetTtsVoicesQuery request,
        CancellationToken cancellationToken)
    {
        var voices = textToSpeechService.GetAvailableVoices();
        return Task.FromResult(Result<IReadOnlyList<TtsVoiceOptionDto>>.Success(voices));
    }
}
