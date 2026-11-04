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
using OralExamination.Domain.Entities;

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
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Clo)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Include(q => q.SubmittedByUser)
            .Include(q => q.ApprovedByUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        var practiceQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Clo)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Include(q => q.SubmittedByUser)
            .Include(q => q.ApprovedByUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (question != null)
        {
            var usageScope = practiceQuestion != null ? "both" : "exam";
            return Result<QuestionDetailDto>.Success(MapExamQuestionToDetailDto(question, usageScope));
        }

        if (practiceQuestion != null)
        {
            return Result<QuestionDetailDto>.Success(MapPracticeQuestionToDetailDto(practiceQuestion, "practice"));
        }

        return Result<QuestionDetailDto>.Failure("Không tìm thấy câu hỏi với mã tương ứng.");
    }

    private static QuestionDetailDto MapExamQuestionToDetailDto(ExamQuestion question, string usageScope)
    {
        var keyPoints = ParseKeyPoints(question.KeyPoints);
        var rubricDto = MapRubricDto(question.Rubric);

        return new QuestionDetailDto(
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
            rubricDto,
            question.ImageUrl,
            question.ImageDescription,
            question.CloId,
            question.Clo?.CloCode,
            question.StructureId,
            usageScope
        );
    }

    private static QuestionDetailDto MapPracticeQuestionToDetailDto(PracticeQuestion question, string usageScope)
    {
        var keyPoints = ParseKeyPoints(question.KeyPoints);
        var rubricDto = MapRubricDto(question.Rubric);

        return new QuestionDetailDto(
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
            rubricDto,
            question.ImageUrl,
            question.ImageDescription,
            question.CloId,
            question.Clo?.CloCode,
            null,
            usageScope
        );
    }

    private static List<string> ParseKeyPoints(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static RubricDetailDto MapRubricDto(Rubric? rubric)
    {
        var criteriaDto = rubric?.Criteria
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

        return new RubricDetailDto(
            rubric?.Id ?? Guid.Empty,
            rubric?.Name ?? string.Empty,
            rubric?.Description,
            rubric?.TotalMaxScore ?? 10.00m,
            criteriaDto
        );
    }
}
