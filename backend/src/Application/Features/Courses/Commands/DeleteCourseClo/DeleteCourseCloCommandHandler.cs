using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;

public sealed class DeleteCourseCloCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteCourseCloCommand, Result>
{
    public async Task<Result> Handle(DeleteCourseCloCommand request, CancellationToken cancellationToken)
    {
        var (actorRole, actorUserId) = ParseActorRoleAndId(request.ActorRole);

        if (!string.IsNullOrWhiteSpace(actorRole) &&
            !string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn hoặc Admin mới có quyền xóa CLO môn học.");
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
                return Result.Failure("FORBIDDEN: Trưởng bộ môn chỉ được quản lý CLO của môn học do mình phụ trách.");
            }
        }

        var clo = await context.CourseClos
            .FirstOrDefaultAsync(c => c.Id == request.CloId && c.CourseId == request.CourseId, cancellationToken);

        if (clo == null)
        {
            return Result.Failure("Chuẩn đầu ra (CLO) không tồn tại trong môn học này.");
        }

        context.CourseClos.Remove(clo);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
