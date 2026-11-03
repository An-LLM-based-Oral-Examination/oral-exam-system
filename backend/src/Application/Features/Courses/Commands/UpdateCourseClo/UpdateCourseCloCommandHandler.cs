using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;

public sealed class UpdateCourseCloCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateCourseCloCommand, Result<CourseCloDto>>
{
    public async Task<Result<CourseCloDto>> Handle(UpdateCourseCloCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !string.Equals(request.ActorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseCloDto>.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn, Cán bộ khảo thí hoặc Admin mới có quyền cập nhật CLO môn học.");
        }

        var clo = await context.CourseClos
            .FirstOrDefaultAsync(c => c.Id == request.CloId && c.CourseId == request.CourseId, cancellationToken);

        if (clo == null)
        {
            return Result<CourseCloDto>.Failure("Chuẩn đầu ra (CLO) không tồn tại trong môn học này.");
        }

        var normalizedCode = request.CloCode.Trim().ToUpperInvariant();
        if (!string.Equals(clo.CloCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await context.CourseClos
                .AsNoTracking()
                .AnyAsync(c => c.CourseId == request.CourseId && c.CloCode == normalizedCode && c.Id != request.CloId, cancellationToken);

            if (codeExists)
            {
                return Result<CourseCloDto>.Failure($"Mã chuẩn đầu ra '{normalizedCode}' đã tồn tại trong môn học này.");
            }
        }

        clo.CloCode = normalizedCode;
        clo.Description = request.Description.Trim();
        clo.BloomLevel = request.BloomLevel.Trim();
        clo.OrderIndex = request.OrderIndex;
        clo.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        var dto = new CourseCloDto
        {
            Id = clo.Id,
            CourseId = clo.CourseId,
            CloCode = clo.CloCode,
            Description = clo.Description,
            BloomLevel = clo.BloomLevel,
            OrderIndex = clo.OrderIndex,
            IsActive = clo.IsActive,
            CreatedAt = clo.CreatedAt
        };

        return Result<CourseCloDto>.Success(dto);
    }
}
