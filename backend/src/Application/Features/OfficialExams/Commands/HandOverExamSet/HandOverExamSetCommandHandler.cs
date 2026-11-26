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
    public async Task<Result> Handle(HandOverExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(context, examSet.CourseId, request.ActorUserId, cancellationToken))
        {
            return Result.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.AssignmentId.HasValue)
        {
            if (examSet.SessionId.HasValue && await context.ExamPreparations.AsNoTracking().AnyAsync(x => x.SessionId == examSet.SessionId, cancellationToken))
                return Result.Failure("CONFLICT: Kỳ thi đã dùng luồng phân công chuẩn bị đề. Bộ đề cần gắn phân công hợp lệ.");
            return await new LegacyHandOverExamSetCommandHandler(context).Handle(request, cancellationToken);
        }

        if (examSet.IsSampleSet)
        {
            return Result.Failure("UNPROCESSABLE: Không thể bàn giao Bộ Đề Mẫu (Sample Set) sang phòng Khảo thí.");
        }

        if (examSet.Status != ExamSetStatus.Official)
        {
            return Result.Failure("Chỉ có thể bàn giao bộ đề đang ở trạng thái chính thức (OFFICIAL).");
        }

        if (!examSet.SessionId.HasValue)
            return Result.Failure("VALIDATION: Bộ đề chưa gắn kỳ thi.");
        var result = await new ExamPreparationCommandHandler(context).Handle(new ExamPreparationCommand(
            examSet.SessionId.Value, request.ActorUserId, request.ActorRole, PreparationAction.HandOver,
            Delivery: new DTOs.HandOverExamPreparation([examSet.Id])), cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }
}
