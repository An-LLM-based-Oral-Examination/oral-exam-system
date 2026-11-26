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
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Domain.Enums;

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
            .Where(q => !q.IsDeleted && !q.IsPaperCopy && q.Course != null && q.Course.IsActive);

        var practiceQuery = _context.PracticeQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
            .AsNoTracking()
            .Where(q => !q.IsDeleted && q.Course != null && q.Course.IsActive);

        var isLecturer = !request.ApprovedBy.HasValue &&
            string.Equals(request.ActorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var actorId = request.ActorId;

        if (request.ApprovedBy.HasValue)
        {
            var approver = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.ApprovedBy.Value && u.IsActive, cancellationToken);
            if (approver == null || approver.Role != UserRole.DepartmentHead)
            {
                return Result<PagedResult<QuestionSummaryDto>>.Failure(
                    "FORBIDDEN: Chỉ Trưởng Bộ Môn đang hoạt động mới có danh sách câu hỏi đã duyệt.");
            }

            if (request.ActorId != Guid.Empty &&
                request.ActorId != request.ApprovedBy.Value)
            {
                return Result<PagedResult<QuestionSummaryDto>>.Failure(
                    "FORBIDDEN: Không được tra cứu danh sách câu hỏi đã duyệt của tài khoản khác.");
            }
        }
        else
        {
            if (request.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
                return Result<PagedResult<QuestionSummaryDto>>.Failure("FORBIDDEN: Thiếu định danh người tra cứu.");

            var allowedCourseIds = await QuestionCourseAccess
                .AllowedCourseIds(_context, request.ActorRole, request.ActorId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (request.CourseId.HasValue && !allowedCourseIds.Contains(request.CourseId.Value))
                return Result<PagedResult<QuestionSummaryDto>>.Failure("FORBIDDEN: Không được xem câu hỏi của môn học này.");

            examQuery = examQuery.Where(q => allowedCourseIds.Contains(q.CourseId));
            practiceQuery = practiceQuery.Where(q => allowedCourseIds.Contains(q.CourseId));

            if (isLecturer)
            {
                var sampleQuestionIds = allowedCourseIds.Count == 0
                    ? new List<Guid>()
                    : await _context.ExamSetQuestions
                        .AsNoTracking()
                        .Where(esq =>
                            allowedCourseIds.Contains(esq.ExamSet.CourseId) &&
                            esq.ExamSet.IsSampleSet &&
                            esq.ExamSet.IsActive &&
                            esq.ExamSet.Status != ExamSetStatus.Rejected)
                        .Select(esq => esq.ExamQuestionId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                examQuery = examQuery.Where(q =>
                    q.SubmittedBy == actorId ||
                    (q.ApprovalStatus == ApprovalStatus.Approved &&
                     q.IsActive &&
                     (q.BankCurationStatus == BankCurationStatus.BankReady ||
                      q.BankCurationStatus == BankCurationStatus.CuratedIntoBank) &&
                     !sampleQuestionIds.Contains(q.Id)));

                practiceQuery = practiceQuery.Where(q =>
                    q.SubmittedBy == actorId ||
                    (q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive));
            }
        }

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
            if (isLecturer && request.SubmittedBy.Value != actorId)
            {
                examQuery = examQuery.Where(_ => false);
                practiceQuery = practiceQuery.Where(_ => false);
            }
            else
            {
                examQuery = examQuery.Where(q => q.SubmittedBy == request.SubmittedBy.Value);
                practiceQuery = practiceQuery.Where(q => q.SubmittedBy == request.SubmittedBy.Value);
            }
        }

        if (request.ApprovedBy.HasValue)
        {
            examQuery = examQuery.Where(q => q.ApprovedBy == request.ApprovedBy.Value && q.ApprovalStatus == ApprovalStatus.Approved);
            practiceQuery = practiceQuery.Where(q => q.ApprovedBy == request.ApprovedBy.Value && q.ApprovalStatus == ApprovalStatus.Approved);
        }

        var pageNumber = Math.Clamp(request.PageNumber, 1, 10_000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var normalizedScope = (request.UsageScope ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedScope == "practice")
        {
            var practiceCount = await practiceQuery.CountAsync(cancellationToken);
            var practicePageItems = await practiceQuery
                .OrderByDescending(q => request.ApprovedBy.HasValue ? (q.ApprovedAt ?? DateTime.MinValue) : q.CreatedAt)
                .ThenBy(q => q.Id)
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
                    (!isLecturer || q.SubmittedBy == actorId) ? q.SubmittedBy : null,
                    q.ApprovedBy,
                    q.CreatedAt,
                    q.Rubric.TotalMaxScore,
                    q.ImageUrl,
                    q.ImageDescription,
                    q.CloId,
                    null,
                    q.ApprovedAt,
                    "practice"
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
                .OrderByDescending(q => request.ApprovedBy.HasValue ? (q.ApprovedAt ?? DateTime.MinValue) : q.CreatedAt)
                .ThenBy(q => q.Id)
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
                    (!isLecturer || q.SubmittedBy == actorId) ? q.SubmittedBy : null,
                    q.ApprovedBy,
                    q.CreatedAt,
                    q.Rubric.TotalMaxScore,
                    q.ImageUrl,
                    q.ImageDescription,
                    q.CloId,
                    q.StructureId,
                    q.ApprovedAt,
                    "exam"
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
            .OrderByDescending(q => request.ApprovedBy.HasValue ? (q.ApprovedAt ?? DateTime.MinValue) : q.CreatedAt)
            .ThenBy(q => q.Id)
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
                (!isLecturer || q.SubmittedBy == actorId) ? q.SubmittedBy : null,
                q.ApprovedBy,
                q.CreatedAt,
                q.Rubric.TotalMaxScore,
                q.ImageUrl,
                q.ImageDescription,
                q.CloId,
                q.StructureId,
                q.ApprovedAt,
                "exam"
            ))
            .ToListAsync(cancellationToken);

        var practiceOnlyItems = await practiceQuery
            .OrderByDescending(q => request.ApprovedBy.HasValue ? (q.ApprovedAt ?? DateTime.MinValue) : q.CreatedAt)
            .ThenBy(q => q.Id)
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
                (!isLecturer || q.SubmittedBy == actorId) ? q.SubmittedBy : null,
                q.ApprovedBy,
                q.CreatedAt,
                q.Rubric.TotalMaxScore,
                q.ImageUrl,
                q.ImageDescription,
                q.CloId,
                null,
                q.ApprovedAt,
                "practice"
            ))
            .ToListAsync(cancellationToken);

        var items = examItems
            .Concat(practiceOnlyItems)
            .OrderByDescending(q => request.ApprovedBy.HasValue ? (q.ApprovedAt ?? DateTime.MinValue) : q.CreatedAt)
            .ThenBy(q => q.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var result = new PagedResult<QuestionSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);

        return Result<PagedResult<QuestionSummaryDto>>.Success(result);
    }
}
