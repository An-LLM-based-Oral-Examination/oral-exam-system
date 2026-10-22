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
            var isFullSession = string.Equals(s.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);
            var isCompleted = string.Equals(s.Status, "completed", StringComparison.OrdinalIgnoreCase);

            var plannedQuestionCount = s.SelectedQuestionIds.Count;
            var distinctMainQuestionIds = answers
                .Where(a => !a.IsFollowUp)
                .Select(a => a.QuestionId)
                .Distinct()
                .ToList();
            var answeredMainCount = distinctMainQuestionIds.Count;
            var totalQuestions = Math.Max(plannedQuestionCount, answeredMainCount);

            var mainScores = answers
                .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
                .GroupBy(a => a.QuestionId)
                .Select(g => g
                    .SelectMany(a => a.AiEvaluations)
                    .Max(e => e.TotalScore))
                .ToList();

            bool hasPendingAnswers = answers.Any(a => string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase));

            decimal? avgScore = null;
            if (isFullSession && isCompleted && totalQuestions > 0)
            {
                if (hasPendingAnswers)
                {
                    avgScore = null;
                }
                else if (mainScores.Count > 0)
                {
                    avgScore = Math.Round(mainScores.Sum() / totalQuestions, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    avgScore = 0.0m;
                }
            }
            else
            {
                var fallbackScores = mainScores.Count > 0
                    ? mainScores
                    : answers
                        .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
                        .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                        .ToList();

                if (fallbackScores.Count > 0)
                {
                    avgScore = Math.Round(fallbackScores.Average(), 2, MidpointRounding.AwayFromZero);
                }
            }

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
                TotalQuestions = totalQuestions,
                AnsweredQuestions = answeredMainCount,
                AverageScore = avgScore
            };
        }).ToList();

        return Result<List<PracticeHistoryItemDto>>.Success(result);
    }
}
