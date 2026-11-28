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
using OralExamination.Application.Features.OfficialExams.Common;
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

        var hasCourseId = request.CourseId.HasValue && request.CourseId.Value != Guid.Empty;
        var hasStructureId = request.StructureId.HasValue && request.StructureId.Value != Guid.Empty;
        if (!hasCourseId && !hasStructureId)
        {
            return Result<List<ExamSetDto>>.Failure(
                "VALIDATION: Cần cung cấp ít nhất CourseId hoặc StructureId để tra cứu bộ đề.");
        }

        if (hasCourseId)
        {
            var canAccessCourse = await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(
                context,
                request.CourseId!.Value,
                actor.Id,
                actor.Role,
                allowExaminationOfficer: true,
                cancellationToken);

            if (!canAccessCourse)
            {
                return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
            }
        }

        Guid effectiveCourseId;
        if (hasStructureId)
        {
            var structure = await context.ExamStructures
                .AsNoTracking()
                .Where(s => s.Id == request.StructureId!.Value)
                .Select(s => new { s.Id, s.CourseId })
                .FirstOrDefaultAsync(cancellationToken);

            if (structure is null || (hasCourseId && structure.CourseId != request.CourseId!.Value))
            {
                return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.StructureNotFound);
            }

            effectiveCourseId = hasCourseId ? request.CourseId!.Value : structure.CourseId;

            if (!hasCourseId)
            {
                var canAccessResolvedCourse = await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(
                    context,
                    effectiveCourseId,
                    actor.Id,
                    actor.Role,
                    allowExaminationOfficer: true,
                    cancellationToken);

                if (!canAccessResolvedCourse)
                {
                    return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
                }
            }
        }
        else
        {
            effectiveCourseId = request.CourseId!.Value;
        }

        var query = context.ExamSets
            .AsNoTracking()
            .Where(es => es.CourseId == effectiveCourseId);

        if (hasStructureId)
        {
            query = query.Where(es => es.StructureId == request.StructureId!.Value);
        }

        var isExamOfficer = string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isDeptHead = string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);

        if (!request.IncludeInactive || isExamOfficer)
        {
            query = query.Where(es => es.IsActive);
        }

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

        if (isDeptHead)
        {
            query = query.Where(es =>
                (es.IsSampleSet && es.IsActive && es.Status != ExamSetStatus.Rejected)
                || (!es.IsSampleSet && es.Status != ExamSetStatus.Draft));
        }

        if (request.SessionId.HasValue)
        {
            if (hasStructureId)
            {
                query = query.Where(es =>
                    es.SessionId == request.SessionId.Value
                    || (es.IsSampleSet && es.SessionId == null));
            }
            else
            {
                var sessionStructureId = await context.OfficialExamSessions
                    .AsNoTracking()
                    .Where(s => s.Id == request.SessionId.Value)
                    .Select(s => s.ExamStructureId)
                    .FirstOrDefaultAsync(cancellationToken);

                query = query.Where(es =>
                    es.SessionId == request.SessionId.Value
                    || (es.IsSampleSet
                        && es.SessionId == null
                        && sessionStructureId.HasValue
                        && es.StructureId == sessionStructureId.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToUpperInvariant();
            query = query.Where(es => es.Status == normalizedStatus);
        }

        var examSets = await query
            .OrderBy(es => es.SetCode)
            .Select(es => new ExamSetDto
            {
                Id = es.Id,
                AssignmentId = es.AssignmentId,
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
                        SourceQuestionId = esq.SourceQuestionId ?? esq.ExamQuestion.OriginQuestionId,
                        Source = esq.ExamQuestion.Source,
                        IsBankQuestion = esq.IsBankQuestion,
                        KeyPoints = esq.ExamQuestion.KeyPoints,
                        OrderIndex = esq.OrderIndex,
                        Title = esq.ExamQuestion.Title,
                        Content = esq.ExamQuestion.Content,
                        BloomLevel = esq.ExamQuestion.BloomLevel,
                        Difficulty = esq.ExamQuestion.Difficulty,
                        CloId = esq.ExamQuestion.CloId,
                        SampleAnswer = esq.ExamQuestion.SampleAnswer,
                        ImageUrl = esq.ExamQuestion.ImageUrl,
                        ImageDescription = esq.ExamQuestion.ImageDescription,
                        RubricDescription = esq.ExamQuestion.Rubric.Description,
                        RubricName = esq.ExamQuestion.Rubric.Name,
                        TotalMaxScore = esq.ExamQuestion.Rubric.TotalMaxScore,
                        Criteria = esq.ExamQuestion.Rubric.Criteria.OrderBy(r => r.OrderIndex).Select(r => new PaperCriterionDto(
                            r.CriterionName, r.Description, r.MaxScore, r.Weight, r.BloomLevel, r.OrderIndex)).ToList(),
                        IsDuplicate = false,
                        DuplicateInSets = new List<string>(),
                        OverlapSeverity = "NONE",
                        BankCurationStatus = esq.ExamQuestion.BankCurationStatus
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        await ExamSetOverlapEnricher.EnrichAsync(
            context,
            examSets,
            actor.Id,
            isDeptHead,
            isLecturer,
            cancellationToken);

        return Result<List<ExamSetDto>>.Success(examSets);
    }
}
