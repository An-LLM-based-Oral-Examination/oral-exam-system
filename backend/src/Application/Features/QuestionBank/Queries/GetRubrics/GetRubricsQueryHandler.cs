using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;

public sealed class GetRubricsQueryHandler
    : IRequestHandler<GetRubricsQuery, Result<List<RubricDetailDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRubricsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<RubricDetailDto>>> Handle(
        GetRubricsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<List<RubricDetailDto>>.Failure(
                "FORBIDDEN: Bạn không có quyền truy cập danh sách Barem Rubric.");
        }

        var claimedRole = request.ActorRole.Trim();
        if (!string.Equals(claimedRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(claimedRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<RubricDetailDto>>.Failure(
                "FORBIDDEN: Chỉ Giảng viên hoặc Trưởng bộ môn đang hoạt động mới được xem Barem Rubric.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            (!string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase)) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<RubricDetailDto>>.Failure(
                "FORBIDDEN: Chỉ Giảng viên hoặc Trưởng bộ môn đang hoạt động mới được xem Barem Rubric.");
        }

        var allowedCourseIds = QuestionCourseAccess.AllowedCourseIds(_context, actor.Role, actor.Id);

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            var canAccessCourse = await allowedCourseIds
                .AnyAsync(id => id == request.CourseId.Value, cancellationToken);
            if (!canAccessCourse)
            {
                return Result<List<RubricDetailDto>>.Failure(
                    "FORBIDDEN: Bạn không phụ trách hoặc không được phân công giảng dạy môn học này.");
            }
        }

        if (request.RubricId.HasValue && request.RubricId.Value != Guid.Empty)
        {
            var targetCourseId = await _context.Rubrics
                .AsNoTracking()
                .Where(r => r.Id == request.RubricId.Value && r.IsActive)
                .Select(r => (Guid?)r.CourseId)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetCourseId.HasValue)
            {
                var canAccessRubricCourse = await allowedCourseIds
                    .AnyAsync(id => id == targetCourseId.Value, cancellationToken);
                if (!canAccessRubricCourse)
                {
                    return Result<List<RubricDetailDto>>.Failure(
                        "FORBIDDEN: Bạn không có quyền xem Barem Rubric của môn học này.");
                }
            }
        }

        var query = _context.Rubrics
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Where(r => r.IsActive && allowedCourseIds.Contains(r.CourseId) &&
                !_context.ExamQuestions.Any(q => q.IsPaperCopy && q.RubricId == r.Id));

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            query = query.Where(r => r.CourseId == request.CourseId.Value);
        }

        if (request.RubricId.HasValue && request.RubricId.Value != Guid.Empty)
        {
            query = query.Where(r => r.Id == request.RubricId.Value);
        }

        var rubrics = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = rubrics.Select(r => new RubricDetailDto(
            r.Id,
            r.Name,
            r.Description,
            r.TotalMaxScore,
            r.Criteria
                .OrderBy(c => c.OrderIndex)
                .Select(c => new RubricCriterionDetailDto(
                    c.Id,
                    c.CriterionName,
                    c.Description,
                    c.MaxScore,
                    c.Weight,
                    c.BloomLevel,
                    c.OrderIndex
                ))
                .ToList()
        )).ToList();

        return Result<List<RubricDetailDto>>.Success(items);
    }
}
