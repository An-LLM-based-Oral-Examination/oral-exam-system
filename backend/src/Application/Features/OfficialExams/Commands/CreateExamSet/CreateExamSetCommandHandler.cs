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
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;

public sealed class CreateExamSetCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateExamSetCommand, Result<ExamSetDto>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead,
        UserRole.ExaminationOfficer,
        UserRole.Admin
    ];

    public async Task<Result<ExamSetDto>> Handle(CreateExamSetCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !AllowedRoles.Contains(request.ActorRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var course = await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.CourseNotFound);
        }

        var structure = await context.ExamStructures
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StructureId && s.CourseId == request.CourseId, cancellationToken);
        if (structure is null)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.StructureNotFound);
        }

        if (request.QuestionIds == null || request.QuestionIds.Count == 0)
        {
            return Result<ExamSetDto>.Failure(OfficialExamErrors.ExamSetNoQuestions);
        }

        var normalizedCode = request.SetCode.Trim().ToUpperInvariant();
        var codeExists = await context.ExamSets
            .AsNoTracking()
            .AnyAsync(es => es.CourseId == request.CourseId && es.SetCode == normalizedCode, cancellationToken);
        if (codeExists)
        {
            return Result<ExamSetDto>.Failure($"Mã bộ đề '{normalizedCode}' đã tồn tại trong môn học này.");
        }

        var questions = await context.ExamQuestions
            .AsNoTracking()
            .Where(q => request.QuestionIds.Contains(q.Id) && q.CourseId == request.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        if (questions.Count != request.QuestionIds.Distinct().Count())
        {
            return Result<ExamSetDto>.Failure("Một hoặc nhiều câu hỏi không tồn tại hoặc không thuộc môn học này.");
        }

        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            StructureId = request.StructureId,
            CourseId = request.CourseId,
            SessionId = request.SessionId,
            CreatedBy = request.ActorUserId != Guid.Empty ? request.ActorUserId : null,
            SetCode = normalizedCode,
            Status = ExamSetStatus.Draft,
            GeneratedAt = DateTime.UtcNow,
            IsActive = true
        };

        var order = 1;
        foreach (var qId in request.QuestionIds)
        {
            examSet.ExamSetQuestions.Add(new ExamSetQuestion
            {
                Id = Guid.NewGuid(),
                ExamSetId = examSet.Id,
                ExamQuestionId = qId,
                OrderIndex = order++
            });
        }

        context.ExamSets.Add(examSet);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new ExamSetDto
        {
            Id = examSet.Id,
            StructureId = examSet.StructureId,
            CourseId = examSet.CourseId,
            CourseCode = course.Code,
            SessionId = examSet.SessionId,
            CreatedBy = examSet.CreatedBy,
            SetCode = examSet.SetCode,
            Status = examSet.Status,
            GeneratedAt = examSet.GeneratedAt,
            IsActive = examSet.IsActive,
            Questions = [.. questions
                .OrderBy(q => request.QuestionIds.IndexOf(q.Id))
                .Select((q, idx) => new ExamSetQuestionItemDto
                {
                    QuestionId = q.Id,
                    OrderIndex = idx + 1,
                    Title = q.Title,
                    Content = q.Content,
                    BloomLevel = q.BloomLevel,
                    Difficulty = q.Difficulty,
                    IsDuplicate = false,
                    DuplicateInSets = []
                })]
        };

        return Result<ExamSetDto>.Success(dto);
    }
}
