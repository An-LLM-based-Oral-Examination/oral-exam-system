using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

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
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Bạn không có quyền tạo Barem Rubric.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            (!string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Chỉ Giảng viên hoặc Trưởng bộ môn đang hoạt động mới được tạo Barem Rubric.");
        }

        var courseExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (!courseExists)
        {
            return Result<CreateRubricResponseDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        var canAccessCourse = await QuestionCourseAccess
            .AllowedCourseIds(_context, actor.Role, actor.Id)
            .AnyAsync(id => id == request.CourseId, cancellationToken);

        if (!canAccessCourse)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Bạn không phụ trách hoặc không được phân công giảng dạy môn học này.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > RubricNormalization.MaxNameLength)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "VALIDATION: Tên Barem Rubric không được để trống và tối đa 200 ký tự.");
        }

        if (request.Criteria == null || request.Criteria.Count < 2)
        {
            return Result<CreateRubricResponseDto>.Failure("INVARIANT: Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");
        }

        if (request.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName)))
        {
            return Result<CreateRubricResponseDto>.Failure("VALIDATION: Tên từng tiêu chí đánh giá con không được để trống.");
        }

        if (request.Criteria.Any(c => c.CriterionName.Trim().Length > RubricNormalization.MaxNameLength))
        {
            return Result<CreateRubricResponseDto>.Failure("VALIDATION: Tên từng tiêu chí đánh giá con không được vượt quá 200 ký tự.");
        }

        if (request.Criteria.Any(c => c.MaxScore <= 0))
        {
            return Result<CreateRubricResponseDto>.Failure("INVARIANT: Điểm tối đa của từng tiêu chí con phải lớn hơn 0.");
        }

        if (request.Criteria.Any(c => !RubricNormalization.HasValidScorePrecision(c.MaxScore)))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Điểm tối đa của từng tiêu chí con chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");
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
            var normalizedWeight = RubricNormalization.NormalizeWeight(c.Weight, c.MaxScore);

            rubric.Criteria.Add(new RubricCriterion
            {
                CriterionName = c.CriterionName.Trim(),
                Description = c.Description,
                MaxScore = c.MaxScore,
                Weight = normalizedWeight,
                BloomLevel = c.BloomLevel,
                OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : order,
                CreatedAt = now
            });
            order++;
        }

        _context.Rubrics.Add(rubric);

        if (request.ActorUserId != Guid.Empty)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.ActorUserId,
                Action = "CREATE_RUBRIC",
                EntityName = "rubrics",
                EntityId = rubric.Id,
                NewValues = rubric.Name,
                CreatedAt = now
            });
        }

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
