using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Queries.GetManagedCloCourses;

public sealed record ManagedCloCourseDto(
    Guid Id, string Code, string Name, int Credits, string? Description, string? AcademicProgramName,
    int ActiveCloCount, int TotalCloCount, bool IsActive);
public sealed record GetManagedCloCoursesQuery(string? ActorRole) : IRequest<Result<List<ManagedCloCourseDto>>>;

public sealed class GetManagedCloCoursesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetManagedCloCoursesQuery, Result<List<ManagedCloCourseDto>>>
{
    public async Task<Result<List<ManagedCloCourseDto>>> Handle(GetManagedCloCoursesQuery request, CancellationToken cancellationToken)
    {
        var (role, actorId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var isAdmin = string.Equals(role, UserRole.Admin, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && (!string.Equals(role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) || !actorId.HasValue))
            return Result<List<ManagedCloCourseDto>>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn hoặc Admin được quản lý CLO.");
        var courses = context.Courses.AsNoTracking().Where(c => c.IsActive);
        if (!isAdmin)
            courses = courses.Where(c => c.AcademicProgram != null &&
                c.AcademicProgram.DepartmentHeadUserId == actorId);
        var items = await courses.OrderBy(c => c.Code).Select(c => new ManagedCloCourseDto(
                c.Id, c.Code, c.Name, c.Credits, c.Description,
                c.AcademicProgram == null ? null : c.AcademicProgram.Name,
                c.CourseClos.Count(clo => clo.IsActive), c.CourseClos.Count, c.IsActive))
            .ToListAsync(cancellationToken);
        return Result<List<ManagedCloCourseDto>>.Success(items);
    }
}
