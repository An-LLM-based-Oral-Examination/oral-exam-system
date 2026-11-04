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
        var (actorRole, actorUserId) = ParseActorRoleAndId(request.ActorRole);

        if (!string.IsNullOrWhiteSpace(actorRole) &&
            !string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseCloDto>.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn hoặc Admin mới có quyền cập nhật CLO môn học.");
        }

        if (string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            actorUserId.HasValue &&
            actorUserId.Value != Guid.Empty)
        {
            var course = await context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

            if (course != null &&
                course.DepartmentHeadUserId.HasValue &&
                course.DepartmentHeadUserId.Value != actorUserId.Value)
            {
                return Result<CourseCloDto>.Failure("FORBIDDEN: Trưởng bộ môn chỉ được quản lý CLO của môn học do mình phụ trách.");
            }
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

    private static (string? Role, Guid? UserId) ParseActorRoleAndId(string? rawActorRole)
    {
        if (string.IsNullOrWhiteSpace(rawActorRole))
        {
            return (null, null);
        }

        var separatorIndex = rawActorRole.IndexOf(':');
        if (separatorIndex > 0)
        {
            var rolePart = rawActorRole[..separatorIndex].Trim();
            var idPart = rawActorRole[(separatorIndex + 1)..].Trim();
            if (Guid.TryParse(idPart, out var parsedId) && parsedId != Guid.Empty)
            {
                return (rolePart, parsedId);
            }
        }

        return (rawActorRole.Trim(), null);
    }
}
