using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;

public sealed class CreateRubricCommandHandler
    : IRequestHandler<CreateRubricCommand, Result<CreateRubricResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateRubricCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreateRubricResponseDto>> Handle(
        CreateRubricCommand request,
        CancellationToken cancellationToken)
    {
        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (!courseExists)
        {
            return Result<CreateRubricResponseDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<CreateRubricResponseDto>.Failure("VALIDATION: Tên Barem Rubric không được để trống.");
        }

        if (request.Criteria == null || request.Criteria.Count < 2)
        {
            return Result<CreateRubricResponseDto>.Failure("INVARIANT: Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");
        }

        if (request.Criteria.Any(c => c.MaxScore <= 0))
        {
            return Result<CreateRubricResponseDto>.Failure("INVARIANT: Điểm tối đa của từng tiêu chí con phải lớn hơn 0.");
        }

        var totalMaxScore = request.Criteria.Sum(c => c.MaxScore);
        if (totalMaxScore != 10.00m)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");
        }

        var now = DateTime.UtcNow;
        var rubric = new Rubric
        {
            CourseId = request.CourseId,
            Name = request.Name.Trim(),
            Description = request.Description,
            TotalMaxScore = 10.00m,
            IsActive = true,
            CreatedAt = now
        };

        int order = 1;
        foreach (var c in request.Criteria)
        {
            rubric.Criteria.Add(new RubricCriterion
            {
                CriterionName = c.CriterionName.Trim(),
                Description = c.Description,
                MaxScore = c.MaxScore,
                Weight = c.Weight,
                BloomLevel = c.BloomLevel,
                OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : order,
                CreatedAt = now
            });
            order++;
        }

        _context.Rubrics.Add(rubric);
        await _context.SaveChangesAsync(cancellationToken);

        var criteriaDto = rubric.Criteria
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
            .ToList();

        var response = new CreateRubricResponseDto(
            rubric.Id,
            rubric.CourseId,
            rubric.Name,
            rubric.Description,
            rubric.TotalMaxScore,
            criteriaDto.Count,
            rubric.CreatedAt,
            criteriaDto
        );

        return Result<CreateRubricResponseDto>.Success(response);
    }
}
