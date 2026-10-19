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

            int answeredQuestions = s.Answers.Count(a => !string.IsNullOrWhiteSpace(a.AnswerText) && (a.Status == "answered" || a.Status == "graded"));
            int totalQuestions = s.Answers.Count;

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
