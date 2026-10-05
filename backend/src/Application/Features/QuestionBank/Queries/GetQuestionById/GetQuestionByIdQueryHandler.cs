using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;

public sealed class GetQuestionByIdQueryHandler : IRequestHandler<GetQuestionByIdQuery, Result<QuestionDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetQuestionByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionDetailDto>> Handle(GetQuestionByIdQuery request, CancellationToken cancellationToken)
    {
        var question = await _context.ExamQuestions
            .Include(q => q.Course)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Include(q => q.SubmittedByUser)
            .Include(q => q.ApprovedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (question == null)
        {
            return Result<QuestionDetailDto>.Failure("Không tìm thấy câu hỏi với mã tương ứng.");
        }

        List<string> keyPoints;
        try
        {
            keyPoints = JsonSerializer.Deserialize<List<string>>(question.KeyPoints) ?? new List<string>();
        }
        catch
        {
            keyPoints = new List<string>();
        }

        var criteriaDto = question.Rubric?.Criteria
            .OrderBy(c => c.OrderIndex)
            .Select(c => new RubricCriterionDetailDto(
                c.Id,
                c.CriterionName,
                c.Description,
                c.MaxScore,
                c.Weight,
                c.BloomLevel,
                c.OrderIndex
            ))
            .ToList() ?? new List<RubricCriterionDetailDto>();

        var rubricDto = new RubricDetailDto(
            question.Rubric?.Id ?? Guid.Empty,
            question.Rubric?.Name ?? string.Empty,
            question.Rubric?.Description,
            question.Rubric?.TotalMaxScore ?? 10.00m,
            criteriaDto
        );

        var detailDto = new QuestionDetailDto(
            question.Id,
            question.CourseId,
            question.Course.Code,
            question.RubricId,
            question.Title,
            question.Content,
            question.SampleAnswer,
            keyPoints,
            question.Difficulty,
            question.BloomLevel,
            question.Source,
            question.ApprovalStatus,
            question.SubmittedBy,
            question.SubmittedByUser?.FullName,
            question.ApprovedBy,
            question.ApprovedByUser?.FullName,
            question.ReviewNotes,
            question.IsActive,
            question.CreatedAt,
            rubricDto
        );

        return Result<QuestionDetailDto>.Success(detailDto);
    }
}
