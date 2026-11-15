using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Queries.SearchOfferingLecturers;

public sealed record LecturerDirectoryItemDto(Guid Id, string FullName, string Email);
public sealed record SearchOfferingLecturersQuery(Guid ActorUserId, Guid CourseOfferingId, string? Search)
    : IRequest<Result<IReadOnlyList<LecturerDirectoryItemDto>>>;

public sealed class SearchOfferingLecturersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchOfferingLecturersQuery, Result<IReadOnlyList<LecturerDirectoryItemDto>>>
{
    public async Task<Result<IReadOnlyList<LecturerDirectoryItemDto>>> Handle(SearchOfferingLecturersQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        var offering = await context.CourseOfferings.AsNoTracking().Include(o => o.Course).ThenInclude(c => c.AcademicProgram)
            .FirstOrDefaultAsync(o => o.Id == request.CourseOfferingId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.DepartmentHead || offering is null ||
            !AcademicCourseOwnership.IsOwnedBy(offering.Course, actor.Id))
            return Result<IReadOnlyList<LecturerDirectoryItemDto>>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn phụ trách được tìm giảng viên.");
        var search = request.Search?.Trim() ?? string.Empty;
        if (search.Length < 2 || search.Length > 100)
            return Result<IReadOnlyList<LecturerDirectoryItemDto>>.Failure("VALIDATION: Nhập từ 2 đến 100 ký tự để tìm giảng viên.");
        if (!offering.IsEnabled || !offering.Course.IsActive)
            return Result<IReadOnlyList<LecturerDirectoryItemDto>>.Failure("CONFLICT: Môn học trong học kỳ đã bị vô hiệu hóa.");
        var normalized = search.ToLowerInvariant();
        var lecturers = await context.Users.AsNoTracking().Where(u => u.IsActive && u.Role == UserRole.Lecturer &&
                (u.FullName.ToLower().Contains(normalized) || u.Email.ToLower().Contains(normalized)))
            .OrderBy(u => u.FullName).ThenBy(u => u.Id).Take(20)
            .Select(u => new LecturerDirectoryItemDto(u.Id, u.FullName, u.Email)).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LecturerDirectoryItemDto>>.Success(lecturers);
    }
}
