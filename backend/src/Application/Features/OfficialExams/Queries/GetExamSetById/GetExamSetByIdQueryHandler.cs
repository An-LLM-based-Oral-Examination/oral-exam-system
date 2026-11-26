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

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamSetById;

public sealed class GetExamSetByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamSetByIdQuery, Result<ExamSetDto>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer
    ];

    public async Task<Result<ExamSetDto>> Handle(GetExamSetByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
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
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var examSet = await context.ExamSets
            .AsNoTracking()
            .Where(es => es.Id == request.ExamSetId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (examSet is null)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        var canAccessCourse = await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(
            context,
            examSet.CourseId,
            actor.Id,
            actor.Role,
            allowExaminationOfficer: true,
            cancellationToken);

        if (!canAccessCourse)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var isExamOfficer = string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var isDeptHead = string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);

        if (isExamOfficer)
        {
            if (examSet.Status != ExamSetStatus.HandedOver || examSet.IsSampleSet)
            {
                return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
            }

            if (!examSet.IsActive)
            {
                return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound);
            }
        }
        else if (isLecturer)
        {
            var canLecturerSee = examSet.CreatedBy == actor.Id
                || (examSet.IsSampleSet && examSet.IsActive && examSet.Status != ExamSetStatus.Rejected);
            if (!canLecturerSee)
            {
                return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
            }

            if (!examSet.IsActive && !request.IncludeInactive)
            {
                return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound);
            }
        }
        else if (!examSet.IsActive && !request.IncludeInactive)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNotFound);
        }

        await ExamSetOverlapEnricher.EnrichAsync(
            context,
            [examSet],
            actor.Id,
            isDeptHead,
            isLecturer,
            cancellationToken);

        return Result<ExamSetDto>.Success(examSet);
    }
}
