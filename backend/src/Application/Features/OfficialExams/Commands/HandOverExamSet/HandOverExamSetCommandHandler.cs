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

namespace OralExamination.Application.Features.OfficialExams.Commands.HandOverExamSet;

public sealed class HandOverExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<HandOverExamSetCommand, Result>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer,
        UserRole.Admin
    ];

    public async Task<Result> Handle(HandOverExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !AllowedRoles.Contains(request.ActorRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Status != ExamSetStatus.Official)
        {
            return Result.Failure("Chỉ có thể bàn giao bộ đề đang ở trạng thái chính thức (OFFICIAL).");
        }

        examSet.Status = ExamSetStatus.HandedOver;
        examSet.HandedOverAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
