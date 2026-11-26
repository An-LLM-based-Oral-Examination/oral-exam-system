using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;

public sealed class ApproveExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ApproveExamSetCommand, Result<ApproveExamSetResultDto>>
{
    public Task<Result<ApproveExamSetResultDto>> Handle(ApproveExamSetCommand request, CancellationToken cancellationToken) =>
        context.ExecuteOfficialSemesterBatchAsync(Guid.Empty, ct => Approve(request, ct), cancellationToken);

    private async Task<Result<ApproveExamSetResultDto>> Approve(ApproveExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context, request.ActorUserId, request.ActorRole, UserRole.DepartmentHead, cancellationToken))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (request.ReviewNotes?.Length > 2000)
            return Result<ApproveExamSetResultDto>.Failure("VALIDATION: Nhận xét tối đa 2000 ký tự.");

        var examSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion).ThenInclude(q => q.Rubric).ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(context, examSet.CourseId, request.ActorUserId, cancellationToken))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!examSet.AssignmentId.HasValue)
        {
            if (examSet.SessionId.HasValue && await context.ExamPreparations.AsNoTracking().AnyAsync(x => x.SessionId == examSet.SessionId, cancellationToken))
                return Result<ApproveExamSetResultDto>.Failure("CONFLICT: Kỳ thi đã dùng luồng phân công chuẩn bị đề. Bộ đề cần gắn phân công hợp lệ.");
            return await new LegacyApproveExamSetCommandHandler(context).Handle(request, cancellationToken);
        }
        if (!examSet.IsActive || examSet.IsSampleSet)
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetStatusInvalid);
        if (examSet.CreatedBy == request.ActorUserId)
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);

        if (examSet.Status == ExamSetStatus.Official)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyApproved);
        }

        if (examSet.Status == ExamSetStatus.HandedOver)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyHandedOver);
        }

        if (examSet.Status != ExamSetStatus.SubmittedForReview)
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetStatusInvalid);
        var validation = await ExamPaperRules.Validate(context, examSet, cancellationToken);
        if (validation is not null) return Result<ApproveExamSetResultDto>.Failure(validation);
        var duplicates = await ExamPaperRules.Duplicates(context, examSet, cancellationToken);
        var groupedDuplicates = duplicates.GroupBy(x => x.QuestionId).ToList();
        var duplicateWarnings = duplicates.Select(x => $"Câu {x.QuestionTitle} ({x.QuestionId}): bộ đề {x.SetCode} · {x.SessionTitle} · {x.SemesterCode}").ToList();

        // Cascade contained questions to APPROVED
        var cascadedCount = 0;
        var approvedAt = DateTime.UtcNow;
        foreach (var esq in examSet.ExamSetQuestions)
        {
            if (esq.ExamQuestion != null)
            {
                esq.ExamQuestion.ApprovalStatus = ApprovalStatus.Approved;
                esq.ExamQuestion.ApprovedBy = request.ActorUserId;
                esq.ExamQuestion.ApprovedAt = approvedAt;
                cascadedCount++;
            }
        }

        examSet.Status = ExamSetStatus.Official;
        examSet.ApprovedBy = request.ActorUserId;
        examSet.ApprovedAt = approvedAt;
        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            examSet.ReviewNotes = request.ReviewNotes.Trim();
        }

        await ExamPaperRules.Record(context, examSet, request.ActorUserId, "OFFICIAL", request.ReviewNotes, cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<ApproveExamSetResultDto>.Failure("CONFLICT: Bộ đề vừa được xử lý đồng thời. Vui lòng tải lại."); }

        var result = new ApproveExamSetResultDto
        {
            ExamSetId = examSet.Id,
            Status = examSet.Status,
            QuestionsCascadedCount = cascadedCount,
            HasDuplicates = groupedDuplicates.Count > 0,
            DuplicateQuestionCount = groupedDuplicates.Count,
            DuplicateUsages = duplicates,
            DuplicateWarnings = duplicateWarnings
        };

        return Result<ApproveExamSetResultDto>.Success(result);
    }
}
