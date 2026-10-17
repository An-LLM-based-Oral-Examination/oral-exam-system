using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;

public sealed class GetStudentPracticeHistoryQueryHandler : IRequestHandler<GetStudentPracticeHistoryQuery, Result<List<PracticeHistoryItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetStudentPracticeHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PracticeHistoryItemDto>>> Handle(GetStudentPracticeHistoryQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _context.PracticeSessions
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .Where(s => s.StudentId == request.StudentId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        var result = sessions.Select(s =>
        {
            var answers = s.Answers ?? new List<OralExamination.Domain.Entities.PracticeAnswer>();
            var mainScoredAnswers = answers
                .Where(a => !a.IsFollowUp && a.AiEvaluations != null && a.AiEvaluations.Any())
                .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                .ToList();

            var latestScores = mainScoredAnswers.Any()
                ? mainScoredAnswers
                : answers
                    .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
                    .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                    .ToList();

            decimal? avgScore = latestScores.Any()
                ? Math.Round(latestScores.Average(), 2, MidpointRounding.AwayFromZero)
                : null;

            return new PracticeHistoryItemDto
            {
                SessionId = s.Id,
                CourseId = s.CourseId,
                CourseCode = s.Course?.Code ?? string.Empty,
                CourseName = s.Course?.Name ?? string.Empty,
                PracticeMode = s.PracticeMode,
                Status = s.Status,
                StartedAt = s.StartedAt,
                EndedAt = s.CompletedAt,
                TotalQuestions = answers.Count(a => !a.IsFollowUp),
                AnsweredQuestions = answers.Count,
                AverageScore = avgScore
            };
        }).ToList();

        return Result<List<PracticeHistoryItemDto>>.Success(result);
    }
}
