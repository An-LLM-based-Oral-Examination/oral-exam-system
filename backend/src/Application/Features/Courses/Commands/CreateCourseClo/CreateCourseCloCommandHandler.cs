using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Courses.Commands.CreateCourseClo;

public sealed class CreateCourseCloCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateCourseCloCommand, Result<CourseCloDto>>
{
    public async Task<Result<CourseCloDto>> Handle(CreateCourseCloCommand request, CancellationToken cancellationToken)
    {
        var (role, actorId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var course = await context.Courses.AsNoTracking().Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Result<CourseCloDto>.Failure("NOT_FOUND: Môn học không tồn tại.");
        if (!AcademicCourseOwnership.CanManage(course, role, actorId))
            return Result<CourseCloDto>.Failure("FORBIDDEN: Chỉ Trưởng bộ môn phụ trách môn học hoặc Admin được quản lý CLO.");

        var normalizedCode = request.CloCode.Trim().ToUpperInvariant();
        if (await context.CourseClos.AsNoTracking().AnyAsync(c => c.CourseId == request.CourseId
                && c.CloCode.Trim().ToUpper() == normalizedCode, cancellationToken))
            return Result<CourseCloDto>.Failure($"CONFLICT: Mã chuẩn đầu ra '{normalizedCode}' đã tồn tại trong môn học này.");

        var clo = new CourseClo
        {
            CourseId = course.Id, CloCode = normalizedCode, Description = request.Description.Trim(),
            BloomLevel = request.BloomLevel.Trim(), OrderIndex = request.OrderIndex, IsActive = true,
        };
        context.CourseClos.Add(clo);
        await context.SaveChangesAsync(cancellationToken);
        return Result<CourseCloDto>.Success(CourseCloDto.FromEntity(clo));
    }
}
