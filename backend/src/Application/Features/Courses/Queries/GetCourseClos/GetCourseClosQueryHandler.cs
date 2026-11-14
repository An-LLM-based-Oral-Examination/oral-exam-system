using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Queries.GetCourseClos;

public sealed class GetCourseClosQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCourseClosQuery, Result<List<CourseCloDto>>>
{
    public async Task<Result<List<CourseCloDto>>> Handle(GetCourseClosQuery request, CancellationToken cancellationToken)
    {
        var (role, actorId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var course = await context.Courses.AsNoTracking().Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Result<List<CourseCloDto>>.Failure("NOT_FOUND: Môn học không tồn tại.");
        if (!AcademicCourseOwnership.CanManage(course, role, actorId))
            return Result<List<CourseCloDto>>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn phụ trách môn học hoặc Admin được quản lý CLO.");

        var query = context.CourseClos
            .AsNoTracking()
            .Where(c => c.CourseId == request.CourseId);

        if (request.ActiveOnly.HasValue && request.ActiveOnly.Value)
        {
            query = query.Where(c => c.IsActive);
        }

        var clos = await query
            .OrderBy(c => c.OrderIndex)
            .ThenBy(c => c.CloCode)
            .Select(c => new CourseCloDto
            {
                Id = c.Id,
                CourseId = c.CourseId,
                CloCode = c.CloCode,
                Description = c.Description,
                BloomLevel = c.BloomLevel,
                OrderIndex = c.OrderIndex,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<List<CourseCloDto>>.Success(clos);
    }
}
