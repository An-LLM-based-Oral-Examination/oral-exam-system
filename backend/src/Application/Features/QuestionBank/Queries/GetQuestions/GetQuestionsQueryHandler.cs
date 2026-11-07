using System;
using System.Collections.Generic;
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
        var examQuery = _context.ExamQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
            .AsNoTracking()
            .AsQueryable();

        var practiceQuery = _context.PracticeQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
            .AsNoTracking()
            .AsQueryable();

        if (request.CourseId.HasValue)
        {
            examQuery = examQuery.Where(q => q.CourseId == request.CourseId.Value);
            practiceQuery = practiceQuery.Where(q => q.CourseId == request.CourseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ApprovalStatus))
        {
            examQuery = examQuery.Where(q => q.ApprovalStatus == request.ApprovalStatus);
            practiceQuery = practiceQuery.Where(q => q.ApprovalStatus == request.ApprovalStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            examQuery = examQuery.Where(q => q.Difficulty == request.Difficulty);
            practiceQuery = practiceQuery.Where(q => q.Difficulty == request.Difficulty);
        }

        if (!string.IsNullOrWhiteSpace(request.BloomLevel))
        {
            examQuery = examQuery.Where(q => q.BloomLevel == request.BloomLevel);
            practiceQuery = practiceQuery.Where(q => q.BloomLevel == request.BloomLevel);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            examQuery = examQuery.Where(q => q.Source == request.Source);
            practiceQuery = practiceQuery.Where(q => q.Source == request.Source);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            examQuery = examQuery.Where(q => q.Title.Contains(request.Search) || q.Content.Contains(request.Search));
            practiceQuery = practiceQuery.Where(q => q.Title.Contains(request.Search) || q.Content.Contains(request.Search));
        }

        if (request.SubmittedBy.HasValue && request.SubmittedBy.Value != Guid.Empty)
        {
            examQuery = examQuery.Where(q => q.SubmittedBy == request.SubmittedBy.Value);
            practiceQuery = practiceQuery.Where(q => q.SubmittedBy == request.SubmittedBy.Value);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var normalizedScope = (request.UsageScope ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedScope == "practice")
        {
            var practiceCount = await practiceQuery.CountAsync(cancellationToken);
            var practicePageItems = await practiceQuery
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
                    q.Rubric.TotalMaxScore,
                    q.ImageUrl,
                    q.ImageDescription,
                    q.CloId,
                    null
                ))
                .ToListAsync(cancellationToken);

            var practicePages = (int)Math.Ceiling(practiceCount / (double)pageSize);
            return Result<PagedResult<QuestionSummaryDto>>.Success(
                new PagedResult<QuestionSummaryDto>(practicePageItems, practiceCount, pageNumber, pageSize, practicePages));
        }

        if (normalizedScope is "exam" or "official_exam")
        {
            var examCount = await examQuery.CountAsync(cancellationToken);
            var examPageItems = await examQuery
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
                    q.Rubric.TotalMaxScore,
                    q.ImageUrl,
                    q.ImageDescription,
                    q.CloId,
                    q.StructureId
                ))
                .ToListAsync(cancellationToken);

            var examPages = (int)Math.Ceiling(examCount / (double)pageSize);
            return Result<PagedResult<QuestionSummaryDto>>.Success(
                new PagedResult<QuestionSummaryDto>(examPageItems, examCount, pageNumber, pageSize, examPages));
        }

        if (!string.IsNullOrEmpty(normalizedScope))
        {
            return Result<PagedResult<QuestionSummaryDto>>.Failure(
                "Phạm vi sử dụng câu hỏi không hợp lệ. Chỉ chấp nhận 'practice', 'exam' hoặc để trống.");
        }

        var examTotalCount = await examQuery.CountAsync(cancellationToken);
        var practiceTotalCount = await practiceQuery.CountAsync(cancellationToken);
        var totalCount = examTotalCount + practiceTotalCount;
        var windowLimit = pageNumber * pageSize;

        var examItems = await examQuery
            .OrderByDescending(q => q.CreatedAt)
            .Take(windowLimit)
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
                q.Rubric.TotalMaxScore,
                q.ImageUrl,
                q.ImageDescription,
                q.CloId,
                q.StructureId
            ))
            .ToListAsync(cancellationToken);

        var practiceOnlyItems = await practiceQuery
            .OrderByDescending(q => q.CreatedAt)
            .Take(windowLimit)
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
                q.Rubric.TotalMaxScore,
                q.ImageUrl,
                q.ImageDescription,
                q.CloId,
                null
            ))
            .ToListAsync(cancellationToken);

        var items = examItems
            .Concat(practiceOnlyItems)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var result = new PagedResult<QuestionSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);

        return Result<PagedResult<QuestionSummaryDto>>.Success(result);
    }
}
