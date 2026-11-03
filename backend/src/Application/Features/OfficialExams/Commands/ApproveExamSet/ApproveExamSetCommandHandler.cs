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
    private static readonly string[] AllowedRoles =
    [
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer,
        UserRole.Admin
    ];

    public async Task<Result<ApproveExamSetResultDto>> Handle(ApproveExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !AllowedRoles.Contains(request.ActorRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .Include(es => es.ExamSetQuestions)
                .ThenInclude(esq => esq.ExamQuestion)
            .FirstOrDefaultAsync(es => es.Id == request.ExamSetId, cancellationToken);

        if (examSet is null)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        if (examSet.Status == ExamSetStatus.Official)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyApproved);
        }

        if (examSet.Status == ExamSetStatus.HandedOver)
        {
            return Result<ApproveExamSetResultDto>.Failure(OfficialExamErrors.ExamSetAlreadyHandedOver);
        }

        var questionIds = examSet.ExamSetQuestions
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToList();

        // Check for duplicate questions in other official / handed over sets for this course
        var duplicateUsages = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq => questionIds.Contains(esq.ExamQuestionId) &&
                          esq.ExamSetId != examSet.Id &&
                          esq.ExamSet.CourseId == examSet.CourseId &&
                          (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver))
            .Select(esq => new
            {
                esq.ExamQuestionId,
                esq.ExamSet.SetCode,
                esq.ExamQuestion.Title
            })
            .ToListAsync(cancellationToken);

        var duplicateWarnings = new List<string>();
        var groupedDuplicates = duplicateUsages
            .GroupBy(d => d.ExamQuestionId)
            .ToList();

        foreach (var group in groupedDuplicates)
        {
            var title = group.First().Title;
            var setCodes = string.Join(", ", group.Select(g => g.SetCode).Distinct());
            duplicateWarnings.Add($"CẢNH BÁO TRÙNG LẶP: Câu hỏi '{title}' (ID: {group.Key}) đã được sử dụng trong các bộ đề chính thức: {setCodes}.");
        }

        // Cascade contained questions to APPROVED
        var cascadedCount = 0;
        foreach (var esq in examSet.ExamSetQuestions)
        {
            if (esq.ExamQuestion != null)
            {
                esq.ExamQuestion.ApprovalStatus = ApprovalStatus.Approved;
                esq.ExamQuestion.ApprovedBy = request.ActorUserId;
                cascadedCount++;
            }
        }

        examSet.Status = ExamSetStatus.Official;
        examSet.ApprovedBy = request.ActorUserId;
        examSet.ApprovedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            examSet.ReviewNotes = request.ReviewNotes.Trim();
        }

        await context.SaveChangesAsync(cancellationToken);

        var result = new ApproveExamSetResultDto
        {
            ExamSetId = examSet.Id,
            Status = examSet.Status,
            QuestionsCascadedCount = cascadedCount,
            HasDuplicates = groupedDuplicates.Count > 0,
            DuplicateQuestionCount = groupedDuplicates.Count,
            DuplicateWarnings = duplicateWarnings
        };

        return Result<ApproveExamSetResultDto>.Success(result);
    }
}
