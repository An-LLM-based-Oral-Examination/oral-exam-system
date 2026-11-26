using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal static class ExamSetOverlapEnricher
{
    public static async Task EnrichAsync(
        IApplicationDbContext context,
        IReadOnlyList<ExamSetDto> examSets,
        Guid actorId,
        bool isDeptHead,
        bool isLecturer,
        CancellationToken cancellationToken)
    {
        if (examSets.Count == 0 || (!isDeptHead && !isLecturer))
        {
            return;
        }

        var structureIds = examSets
            .Where(s => !s.IsSampleSet)
            .Select(s => s.StructureId)
            .Distinct()
            .ToList();

        if (structureIds.Count == 0)
        {
            return;
        }

        var siblingUsages = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                structureIds.Contains(esq.ExamSet.StructureId)
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet
                && (esq.ExamSet.Status == ExamSetStatus.SubmittedForReview
                    || esq.ExamSet.Status == ExamSetStatus.Official
                    || esq.ExamSet.Status == ExamSetStatus.HandedOver))
            .Select(esq => new
            {
                esq.ExamSet.StructureId,
                esq.ExamSetId,
                esq.ExamSet.SetCode,
                esq.ExamSet.Status,
                esq.ExamSet.CreatedBy,
                esq.ExamQuestionId
            })
            .ToListAsync(cancellationToken);

        var usagesByStructureAndQuestion = siblingUsages
            .GroupBy(u => (u.StructureId, u.ExamQuestionId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var examSet in examSets)
        {
            if (examSet.IsSampleSet || examSet.Questions.Count == 0)
            {
                continue;
            }

            var redCount = 0;
            var yellowCount = 0;

            foreach (var question in examSet.Questions)
            {
                if (!usagesByStructureAndQuestion.TryGetValue((examSet.StructureId, question.QuestionId), out var usages))
                {
                    continue;
                }

                var otherSets = usages.Where(u => u.ExamSetId != examSet.Id).ToList();
                if (otherSets.Count == 0)
                {
                    continue;
                }

                if (isDeptHead)
                {
                    var approvedSetCodes = otherSets
                        .Where(u => u.Status is ExamSetStatus.Official or ExamSetStatus.HandedOver)
                        .Select(u => u.SetCode)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var pendingSetCodes = otherSets
                        .Where(u => u.Status == ExamSetStatus.SubmittedForReview)
                        .Select(u => u.SetCode)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (approvedSetCodes.Count > 0)
                    {
                        question.IsDuplicate = true;
                        question.OverlapSeverity = "RED_APPROVED";
                        question.DuplicateInSets = approvedSetCodes
                            .Concat(pendingSetCodes)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        redCount++;
                    }
                    else if (pendingSetCodes.Count > 0)
                    {
                        question.IsDuplicate = true;
                        question.OverlapSeverity = "YELLOW_PENDING";
                        question.DuplicateInSets = pendingSetCodes;
                        yellowCount++;
                    }
                }
                else if (isLecturer)
                {
                    // Bảo mật ẩn danh tuyệt đối cho Giảng viên: KHÔNG lộ SetCode hay danh tính GV khác.
                    var hasApprovedConflict = examSet.Status is ExamSetStatus.Draft or ExamSetStatus.Rejected or ExamSetStatus.SubmittedForReview
                        && otherSets.Any(u => u.Status is ExamSetStatus.Official or ExamSetStatus.HandedOver);

                    if (hasApprovedConflict)
                    {
                        question.IsDuplicate = true;
                        question.OverlapSeverity = "RED_APPROVED";
                        question.DuplicateInSets = [];
                        redCount++;
                    }
                    else
                    {
                        var hasOtherLecturerPending = otherSets.Any(u =>
                            u.Status == ExamSetStatus.SubmittedForReview
                            && (!u.CreatedBy.HasValue || u.CreatedBy.Value != actorId));

                        if (hasOtherLecturerPending)
                        {
                            question.IsDuplicate = true;
                            question.OverlapSeverity = "YELLOW_PENDING";
                            question.DuplicateInSets = [];
                            yellowCount++;
                        }
                    }
                }
            }

            examSet.RedOverlapQuestionCount = redCount;
            examSet.YellowOverlapQuestionCount = yellowCount;
            examSet.YellowOverlapRatio = examSet.Questions.Count > 0
                ? Math.Round((decimal)yellowCount / examSet.Questions.Count, 4, MidpointRounding.AwayFromZero)
                : 0m;
            examSet.YellowOverlapPercent = Math.Round(
                examSet.YellowOverlapRatio * 100m,
                1,
                MidpointRounding.AwayFromZero);
            examSet.OverlapSeverity = redCount > 0
                ? "RED_APPROVED"
                : yellowCount > 0
                    ? "YELLOW_PENDING"
                    : "NONE";
        }
    }
}
