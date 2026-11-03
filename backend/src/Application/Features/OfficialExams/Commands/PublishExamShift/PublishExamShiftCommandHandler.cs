using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.PublishExamShift;

public sealed class PublishExamShiftCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PublishExamShiftCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.ExaminationOfficer,
        UserRole.DepartmentHead,
        UserRole.Admin
    ];

    public async Task<Result> Handle(PublishExamShiftCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !AllowedRoles.Contains(request.ActorRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var shift = await context.RealExamSessionShifts
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result.Failure(OfficialExamErrors.ShiftNotFound);
        }

        if (shift.IsExamPublished)
        {
            return Result.Failure(OfficialExamErrors.ShiftAlreadyPublished);
        }

        if (!string.Equals(shift.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.ShiftNotScheduledForPublish);
        }

        shift.IsExamPublished = true;
        shift.PublishedAt = DateTime.UtcNow;
        shift.ExaminerUserId = request.ActorUserId;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
