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

        if (string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(request.Status)
                && !string.Equals(request.Status.Trim(), ExamSetStatus.HandedOver, StringComparison.OrdinalIgnoreCase))
            {
                return Result<List<ExamSetDto>>.Failure(OfficialExamErrors.Forbidden);
            }

            query = query.Where(es => es.Status == ExamSetStatus.HandedOver);
        }
        else if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(es => es.CreatedBy == actor.Id);
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
                IsActive = es.IsActive,
                Questions = es.ExamSetQuestions
                    .OrderBy(esq => esq.OrderIndex)
                    .Select(esq => new ExamSetQuestionItemDto
                    {
                        QuestionId = esq.ExamQuestionId,
                        SourceQuestionId = esq.SourceQuestionId,
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
                        DuplicateInSets = new List<string>()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Result<List<ExamSetDto>>.Success(examSets);
    }
}
