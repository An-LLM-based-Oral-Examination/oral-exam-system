using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.CreateCourseClo;

public sealed class CreateCourseCloCommandHandler : IRequestHandler<CreateCourseCloCommand, Result<CourseCloDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateCourseCloCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CourseCloDto>> Handle(CreateCourseCloCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorRole) &&
            !string.Equals(request.ActorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseCloDto>.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn, Cán bộ khảo thí hoặc Admin mới có quyền tạo CLO môn học.");
        }

        var courseExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CourseId, cancellationToken);

        if (!courseExists)
        {
            return Result<CourseCloDto>.Failure("Môn học không tồn tại trong hệ thống.");
        }

        var normalizedCode = request.CloCode.Trim().ToUpperInvariant();
        var codeExists = await _context.CourseClos
            .AsNoTracking()
            .AnyAsync(c => c.CourseId == request.CourseId && c.CloCode == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return Result<CourseCloDto>.Failure($"Mã chuẩn đầu ra '{normalizedCode}' đã tồn tại trong môn học này.");
        }

        var clo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            CloCode = normalizedCode,
            Description = request.Description.Trim(),
            BloomLevel = request.BloomLevel.Trim(),
            OrderIndex = request.OrderIndex,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.CourseClos.Add(clo);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new CourseCloDto
        {
            Id = clo.Id,
            CourseId = clo.CourseId,
            CloCode = clo.CloCode,
            Description = clo.Description,
            BloomLevel = clo.BloomLevel,
            OrderIndex = clo.OrderIndex,
            IsActive = clo.IsActive,
            CreatedAt = clo.CreatedAt
        };

        return Result<CourseCloDto>.Success(dto);
    }
}
