using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Practice.Queries.GetPracticeRuntimeConfig;

public sealed record PracticeRuntimeConfigDto(
    int MinMixedPracticeQuestions,
    int MaxMixedPracticeQuestions,
    int MaxPracticeQuestionsPerSession,
    int MaxPracticeFollowUpQuestions,
    int SessionInactivityTimeoutMinutes,
    int TranscriptBufferSeconds,
    Guid? CourseId = null);

public sealed record GetPracticeRuntimeConfigQuery(Guid? CourseId = null)
    : IRequest<Result<PracticeRuntimeConfigDto>>;

public sealed class GetPracticeRuntimeConfigQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPracticeRuntimeConfigQuery, Result<PracticeRuntimeConfigDto>>
{
    private static readonly string[] ConfigKeys =
    [
        "MinMixedPracticeQuestions",
        "MaxMixedPracticeQuestions",
        "MaxPracticeQuestionsPerSession",
        "MaxPracticeFollowUpQuestions",
        "SessionInactivityTimeoutMinutes",
        "TranscriptBufferSeconds"
    ];

    public async Task<Result<PracticeRuntimeConfigDto>> Handle(
        GetPracticeRuntimeConfigQuery request,
        CancellationToken cancellationToken)
    {
        var configs = await context.SystemConfigs
            .AsNoTracking()
            .Where(c => ConfigKeys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        var minMixed = ReadInt(configs, "MinMixedPracticeQuestions", defaultValue: 3, min: 1, max: 10);
        var maxMixed = ReadInt(configs, "MaxMixedPracticeQuestions", defaultValue: 10, min: minMixed, max: 20);
        var maxPerSession = ReadInt(configs, "MaxPracticeQuestionsPerSession", defaultValue: 10, min: 1, max: 20);
        var maxFollowUps = ReadInt(configs, "MaxPracticeFollowUpQuestions", defaultValue: 2, min: 1, max: 5);
        var timeoutMinutes = ReadInt(configs, "SessionInactivityTimeoutMinutes", defaultValue: 5, min: 1, max: 60);

        int bufferSeconds;
        if (configs.TryGetValue("TranscriptBufferSeconds", out var rawBuffer) && int.TryParse(rawBuffer, out var parsedBuffer))
        {
            bufferSeconds = Math.Clamp(parsedBuffer, 10, 300);
        }
        else if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            var courseBuffer = await context.Courses
                .AsNoTracking()
                .Where(c => c.Id == request.CourseId.Value && c.IsActive)
                .Select(c => (int?)c.TranscriptBufferSeconds)
                .FirstOrDefaultAsync(cancellationToken);

            bufferSeconds = Math.Clamp(courseBuffer is > 0 ? courseBuffer.Value : 60, 10, 300);
        }
        else
        {
            bufferSeconds = 60;
        }

        return Result<PracticeRuntimeConfigDto>.Success(new PracticeRuntimeConfigDto(
            minMixed,
            maxMixed,
            maxPerSession,
            maxFollowUps,
            timeoutMinutes,
            bufferSeconds,
            request.CourseId is { } cid && cid != Guid.Empty ? cid : null));
    }

    private static int ReadInt(
        System.Collections.Generic.IReadOnlyDictionary<string, string> configs,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        if (configs.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        return Math.Clamp(defaultValue, min, max);
    }
}
