using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Queries.GetManagedCloCourses;

public sealed record ManagedCloCourseDto(Guid Id, string Code, string Name);
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
            courses = courses.Where(c => (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId.HasValue
                    && c.AcademicProgram.DepartmentHeadUserId != Guid.Empty)
                ? c.AcademicProgram.DepartmentHeadUserId == actorId
                : c.DepartmentHeadUserId == actorId);
        var items = await courses.OrderBy(c => c.Code).Select(c => new ManagedCloCourseDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);
        return Result<List<ManagedCloCourseDto>>.Success(items);
    }
}
