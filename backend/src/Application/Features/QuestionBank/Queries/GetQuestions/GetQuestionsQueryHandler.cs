using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;

public sealed class GetQuestionsQueryHandler 
    : IRequestHandler<GetQuestionsQuery, Result<PagedResult<QuestionSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetQuestionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedResult<QuestionSummaryDto>>> Handle(
        GetQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ExamQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
            .AsNoTracking()
            .AsQueryable();

        if (request.CourseId.HasValue)
            query = query.Where(q => q.CourseId == request.CourseId.Value);

        if (!string.IsNullOrWhiteSpace(request.ApprovalStatus))
            query = query.Where(q => q.ApprovalStatus == request.ApprovalStatus);

        if (!string.IsNullOrWhiteSpace(request.Difficulty))
            query = query.Where(q => q.Difficulty == request.Difficulty);

        if (!string.IsNullOrWhiteSpace(request.BloomLevel))
            query = query.Where(q => q.BloomLevel == request.BloomLevel);

        if (!string.IsNullOrWhiteSpace(request.Source))
            query = query.Where(q => q.Source == request.Source);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(q => q.Title.Contains(request.Search) || q.Content.Contains(request.Search));

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuestionSummaryDto(
                q.Id,
                q.CourseId,
                q.Course.Code,
                q.Title,
                q.Difficulty,
                q.BloomLevel,
                q.Source,
                q.ApprovalStatus,
                q.SubmittedBy,
                q.ApprovedBy,
                q.CreatedAt,
                q.Rubric.TotalMaxScore
            ))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var result = new PagedResult<QuestionSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);

        return Result<PagedResult<QuestionSummaryDto>>.Success(result);
    }
}
