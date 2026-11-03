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
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !string.Equals(request.ActorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn, Cán bộ khảo thí hoặc Admin mới có quyền xóa CLO môn học.");
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
}
