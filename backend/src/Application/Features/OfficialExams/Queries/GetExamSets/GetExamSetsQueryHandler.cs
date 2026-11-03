using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;

public sealed class GetExamSetsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamSetsQuery, Result<List<ExamSetDto>>>
{
    public async Task<Result<List<ExamSetDto>>> Handle(GetExamSetsQuery request, CancellationToken cancellationToken)
    {
        var query = context.ExamSets
            .AsNoTracking()
            .Where(es => es.CourseId == request.CourseId);

        if (request.SessionId.HasValue)
        {
            query = query.Where(es => es.SessionId == request.SessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(es => es.Status == request.Status);
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
                IsActive = es.IsActive,
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
                        DuplicateInSets = new List<string>()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Result<List<ExamSetDto>>.Success(examSets);
    }
}
