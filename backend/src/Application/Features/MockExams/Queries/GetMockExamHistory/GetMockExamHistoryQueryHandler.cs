using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

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
        var expiredSessions = await _context.MockExamSessions
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .Where(s => s.StudentId == request.StudentId &&
                        s.Status == "in_progress" &&
                        nowUtc > s.ExpiresAt.AddSeconds(10))
            .ToListAsync(cancellationToken);

        if (expiredSessions.Count > 0)
        {
            foreach (var expired in expiredSessions)
            {
                expired.SubmittedAt ??= expired.ExpiresAt;
                var mainAnswers = expired.Answers.Where(a => !a.IsFollowUp).ToList();
                var answeredMains = mainAnswers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var allAnswered = expired.Answers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var hasAnsweredFollowUps = allAnswered.Any(a => a.IsFollowUp);

                if (allAnswered.Count == 0)
                {
                    // Sinh viên không trả lời câu nào trước khi mất mạng/tắt ngang -> đóng ca thi 0.0đ
                    expired.Status = "graded";
                    expired.TotalScore ??= 0.0m;
                }
                else if (answeredMains.Count > 0 && allAnswered.All(a =>
                             a.AiEvaluations != null &&
                             a.AiEvaluations.Any() &&
                             (!hasAnsweredFollowUps || string.Equals(a.Status, "graded", StringComparison.OrdinalIgnoreCase))))
                {
                    // Tất cả các câu đã trả lời (bao gồm cả câu chính lẫn Follow-Up) đều đã chấm xong -> tổng hợp điểm trung bình trên tổng số câu chính
                    int totalMainCount = Math.Max(1, mainAnswers.Count);
                    decimal sumMainScores = answeredMains.Sum(a =>
                        a.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).First().TotalScore);
                    expired.TotalScore = Math.Clamp(
                        Math.Round(sumMainScores / totalMainCount, 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        10.0m);
                    foreach (var ans in allAnswered)
                    {
                        ans.Status = "graded";
                    }
                    expired.Status = "graded";
                }
                else
                {
                    // Sinh viên đã có câu trả lời (câu chính hoặc câu Follow-Up) được lưu nhưng chưa chấm/gộp điểm hết -> chuyển sang "submitted" để bảo toàn bài làm chờ chấm bù
                    expired.Status = "submitted";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
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

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(s => s.Course)
            .Include(s => s.Answers)
            .ToListAsync(cancellationToken);

        var items = sessions.Select(s =>
        {
            int durationMinutes = s.ExpiresAt > s.StartedAt
                ? (int)Math.Round((s.ExpiresAt - s.StartedAt).TotalMinutes)
                : 30;

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

        int totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / request.PageSize) : 0;

        var response = new MockExamHistoryListResponse(
            Items: items,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize,
            TotalCount: totalCount,
            TotalPages: totalPages
        );

        return Result<MockExamHistoryListResponse>.Success(response);
    }
}
