using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;

public sealed class UpdateCourseCloCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateCourseCloCommand, Result<CourseCloDto>>
{
    public async Task<Result<CourseCloDto>> Handle(UpdateCourseCloCommand request, CancellationToken cancellationToken)
    {
        var (role, actorId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var course = await context.Courses.AsNoTracking().Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Result<CourseCloDto>.Failure("NOT_FOUND: Môn học không tồn tại.");
        if (!AcademicCourseOwnership.CanManage(course, role, actorId))
            return Result<CourseCloDto>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn phụ trách môn học hoặc Admin được quản lý CLO.");

        var clo = await context.CourseClos.FirstOrDefaultAsync(c => c.Id == request.CloId && c.CourseId == request.CourseId, cancellationToken);
        if (clo is null) return Result<CourseCloDto>.Failure("NOT_FOUND: Chuẩn đầu ra (CLO) không tồn tại trong môn học này.");
        var normalizedCode = request.CloCode.Trim().ToUpperInvariant();
        if (await context.CourseClos.AsNoTracking().AnyAsync(c => c.CourseId == request.CourseId
                && c.Id != request.CloId && c.CloCode.Trim().ToUpper() == normalizedCode, cancellationToken))
            return Result<CourseCloDto>.Failure($"CONFLICT: Mã chuẩn đầu ra '{normalizedCode}' đã tồn tại trong môn học này.");

        // Legacy slot JSON may reference a code instead of an ID.
        if (!string.Equals(clo.CloCode.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase)
            && await CourseCloDependencies.HasSlotReferenceAsync(context, clo, cancellationToken, codesOnly: true))
            return Result<CourseCloDto>.Failure("CONFLICT: CLO_IN_USE: Không thể đổi mã CLO đang được cấu trúc đề tham chiếu bằng mã.");

        clo.CloCode = normalizedCode;
        clo.Description = request.Description.Trim();
        clo.BloomLevel = request.BloomLevel.Trim();
        clo.OrderIndex = request.OrderIndex;
        clo.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        return Result<CourseCloDto>.Success(CourseCloDto.FromEntity(clo));
    }
}
