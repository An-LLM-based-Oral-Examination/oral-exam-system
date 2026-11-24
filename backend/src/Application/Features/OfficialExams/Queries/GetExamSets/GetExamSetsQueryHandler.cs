using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;

public sealed class GetExamSetsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamSetsQuery, Result<List<ExamSetDto>>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer
    ];

    public async Task<Result<List<ExamSetDto>>> Handle(GetExamSetsQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null
            || !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
        }

        var canAccessCourse = await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(
            context,
            request.CourseId,
            actor.Id,
            actor.Role,
            allowExaminationOfficer: true,
            cancellationToken);

        if (!canAccessCourse)
        {
            return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
        }

        var query = context.ExamSets
            .AsNoTracking()
            .Where(es => es.CourseId == request.CourseId);

        var isExamOfficer = string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isDeptHead = string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);

        if (isExamOfficer)
        {
            if (!string.IsNullOrWhiteSpace(request.Status)
                && !string.Equals(request.Status.Trim(), ExamSetStatus.HandedOver, StringComparison.OrdinalIgnoreCase))
            {
                return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
            }

            query = query.Where(es => es.Status == ExamSetStatus.HandedOver && !es.IsSampleSet);
        }
        else if (isLecturer)
        {
            query = query.Where(es =>
                es.CreatedBy == actor.Id
                || (es.IsSampleSet && es.IsActive && es.Status != ExamSetStatus.Rejected));
        }

        if (request.SessionId.HasValue)
        {
            query = query.Where(es => es.SessionId == request.SessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim();
            query = query.Where(es => es.Status == normalizedStatus);
        }

        var examSets = await query
            .OrderBy(es => es.SetCode)
            .Select(es => new ExamSetDto
            {
                Id = es.Id,
                StructureId = es.StructureId,
                CourseId = es.CourseId,
                CourseCode = es.Course.Code,
                SessionId = es.SessionId,
                CreatedBy = es.CreatedBy,
                SetCode = es.SetCode,
                Status = es.Status,
                ApprovedBy = es.ApprovedBy,
                ReviewNotes = es.ReviewNotes,
                SubmittedAt = es.SubmittedAt,
                ApprovedAt = es.ApprovedAt,
                HandedOverAt = es.HandedOverAt,
                GeneratedAt = es.GeneratedAt,
                IsSampleSet = es.IsSampleSet,
                IsActive = es.IsActive,
                OverlapSeverity = "NONE",
                YellowOverlapRatio = 0m,
                Questions = es.ExamSetQuestions
                    .OrderBy(esq => esq.OrderIndex)
                    .Select(esq => new ExamSetQuestionItemDto
                    {
                        QuestionId = esq.ExamQuestionId,
                        OrderIndex = esq.OrderIndex,
                        Title = esq.ExamQuestion.Title,
                        Content = esq.ExamQuestion.Content,
                        BloomLevel = esq.ExamQuestion.BloomLevel,
                        Difficulty = esq.ExamQuestion.Difficulty,
                        IsDuplicate = false,
                        DuplicateInSets = new List<string>(),
                        OverlapSeverity = "NONE",
                        BankCurationStatus = esq.ExamQuestion.BankCurationStatus,
                        CloId = esq.ExamQuestion.CloId
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (examSets.Count > 0 && (isDeptHead || isLecturer))
        {
            var structureIds = examSets
                .Where(s => !s.IsSampleSet)
                .Select(s => s.StructureId)
                .Distinct()
                .ToList();

            if (structureIds.Count > 0)
            {
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
                                    && (!u.CreatedBy.HasValue || u.CreatedBy.Value != actor.Id));

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
                    examSet.OverlapSeverity = redCount > 0
                        ? "RED_APPROVED"
                        : yellowCount > 0
                            ? "YELLOW_PENDING"
                            : "NONE";
                }
            }
        }

        return Result<List<ExamSetDto>>.Success(examSets);
    }
}
