using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Common;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;

/// <summary>
/// Handler tra cứu lịch sử thi thử của sinh viên (MF-02 - FE-02).
/// Hỗ trợ lọc theo môn học và phân trang tối ưu với AsNoTracking().
/// </summary>
public sealed class GetMockExamHistoryQueryHandler(
    IApplicationDbContext context
) : IRequestHandler<GetMockExamHistoryQuery, Result<MockExamHistoryListResponse>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<MockExamHistoryListResponse>> Handle(
        GetMockExamHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var expiredIds = await _context.MockExamSessions
            .AsNoTracking()
            .Where(s => s.StudentId == request.StudentId &&
                        s.Status == "in_progress" &&
                        s.PausedAt == null &&
                        nowUtc > s.ExpiresAt.AddSeconds(10))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var expiredId in expiredIds)
        {
            await _context.ExecuteMockExamGradingAsync(expiredId, async lockCt =>
            {
                var tracked = await _context.MockExamSessions
                    .AsTracking()
                    .Include(s => s.Answers)
                        .ThenInclude(a => a.AiEvaluations)
                            .ThenInclude(e => e.Details)
                                .ThenInclude(d => d.Criterion)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(s => s.Id == expiredId, lockCt);

                var lockNowUtc = DateTime.UtcNow;
                if (tracked is null
                    || !string.Equals(tracked.Status, "in_progress", StringComparison.OrdinalIgnoreCase)
                    || tracked.PausedAt.HasValue
                    || tracked.ExpiresAt > lockNowUtc
                    || lockNowUtc <= tracked.ExpiresAt.AddSeconds(10))
                {
                    return false;
                }

                tracked.SubmittedAt ??= tracked.ExpiresAt;
                var mainAnswers = tracked.Answers.Where(a => !a.IsFollowUp).ToList();
                var answeredMains = mainAnswers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var allAnswered = tracked.Answers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var hasAnsweredFollowUps = allAnswered.Any(a => a.IsFollowUp);

                if (allAnswered.Count == 0)
                {
                    // Sinh viên không trả lời câu nào trước khi mất mạng/tắt ngang -> đóng ca thi 0.0đ
                    tracked.Status = "graded";
                    tracked.TotalScore ??= 0.0m;
                }
                else if (answeredMains.Count > 0 && allAnswered.All(a =>
                             a.AiEvaluations != null &&
                             a.AiEvaluations.Any() &&
                             (!hasAnsweredFollowUps || string.Equals(a.Status, "graded", StringComparison.OrdinalIgnoreCase))))
                {
                    // Tất cả các câu đã trả lời (bao gồm cả câu chính lẫn Follow-Up) đều đã chấm xong -> tổng hợp điểm trung bình trên tổng số câu chính
                    int totalMainCount = Math.Max(1, mainAnswers.Count);
                    decimal sumMainScores = 0.0m;
                    foreach (var mainAns in answeredMains)
                    {
                        var mainEval = mainAns.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                        var followUpEvals = allAnswered
                            .Where(f => f.IsFollowUp &&
                                        ((f.ParentAnswerId.HasValue && f.ParentAnswerId == mainAns.Id) ||
                                         (!f.ParentAnswerId.HasValue && f.QuestionId == mainAns.QuestionId)))
                            .Select(f => f.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault())
                            .Where(e => e != null)
                            .Cast<AiEvaluation>()
                            .ToList();

                        var effectiveQuestionScore = FollowUpPromptBuilder.ComputeEffectiveQuestionScore(mainEval, followUpEvals);
                        if (mainEval != null && followUpEvals.Count > 0)
                        {
                            mainEval.TotalScore = effectiveQuestionScore;
                        }

                        sumMainScores += effectiveQuestionScore;
                    }

                    tracked.TotalScore = Math.Clamp(
                        Math.Round(sumMainScores / totalMainCount, 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        10.0m);
                    foreach (var ans in allAnswered)
                    {
                        ans.Status = "graded";
                    }
                    tracked.Status = "graded";
                }
                else
                {
                    // Sinh viên đã có câu trả lời (câu chính hoặc câu Follow-Up) được lưu nhưng chưa chấm/gộp điểm hết -> chuyển sang "submitted" để bảo toàn bài làm chờ chấm bù
                    tracked.Status = "submitted";
                }

                try
                {
                    await _context.SaveChangesAsync(lockCt);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }

                return true;
            }, cancellationToken);
        }

        var query = _context.MockExamSessions
            .AsNoTracking()
            .Where(s => s.StudentId == request.StudentId);

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(s => s.CourseId == request.CourseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(s => s.Status == request.Status);
        }

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);
        long rawSkip = (long)(pageIndex - 1) * pageSize;
        int skip = rawSkip > int.MaxValue ? int.MaxValue : (int)rawSkip;

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Skip(skip)
            .Take(pageSize)
            .Include(s => s.Course)
            .Include(s => s.ExamSet)
                .ThenInclude(es => es!.Structure)
            .Include(s => s.Answers)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var structureDurationsByCourse = await MockExamClockHelper.ResolveActiveStructureDurationsByCourseAsync(
            _context,
            sessions.Select(s => s.CourseId).Distinct().ToList(),
            cancellationToken);

        var items = sessions.Select(s =>
        {
            structureDurationsByCourse.TryGetValue(s.CourseId, out var structureDuration);
            int baseDuration = MockExamClockHelper.ResolveBaseDurationMinutes(
                s,
                structureDuration > 0 ? structureDuration : null);
            int computedDuration = s.ExpiresAt > s.StartedAt
                ? (int)Math.Round((s.ExpiresAt - s.StartedAt).TotalMinutes)
                : baseDuration;
            int durationMinutes = Math.Min(computedDuration, baseDuration);

            // Chỉ đếm câu gốc — không cộng câu hỏi phụ.
            int answeredQuestions = s.Answers.Count(a => !a.IsFollowUp && !string.IsNullOrWhiteSpace(a.AnswerText));
            int totalQuestions = s.Answers.Count(a => !a.IsFollowUp);

            return new MockExamHistoryItemDto(
                SessionId: s.Id,
                CourseId: s.CourseId,
                CourseCode: s.Course?.Code ?? string.Empty,
                CourseName: s.Course?.Name ?? string.Empty,
                StartedAt: s.StartedAt,
                DurationMinutes: durationMinutes,
                TotalScore: s.TotalScore,
                Status: s.Status,
                AnsweredQuestions: answeredQuestions,
                TotalQuestions: totalQuestions
            );
        }).ToList();

        int totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var response = new MockExamHistoryListResponse(
            Items: items,
            PageIndex: pageIndex,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages
        );

        return Result<MockExamHistoryListResponse>.Success(response);
    }
}
