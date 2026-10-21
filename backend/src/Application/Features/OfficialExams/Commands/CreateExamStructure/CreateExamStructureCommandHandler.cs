using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;

/// <summary>
/// Handler xử lý tạo mới cấu trúc đề thi (ExamStructure) của Trưởng Bộ Môn / Quản trị viên.
/// </summary>
public sealed class CreateExamStructureCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateExamStructureCommand, Result<ExamStructureDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<ExamStructureDto>> Handle(
        CreateExamStructureCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<ExamStructureDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == request.ActorUserId && u.IsActive,
                cancellationToken);

        if (actor is null)
        {
            return Result<ExamStructureDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");
        }

        if (actor.Role != UserRole.DepartmentHead && actor.Role != UserRole.Admin)
        {
            return Result<ExamStructureDto>.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn hoặc Quản trị viên mới có quyền cấu hình đề thi.");
        }

        var course = await _context.Courses.FirstOrDefaultAsync(
            c => c.Id == request.CourseId && c.IsActive,
            cancellationToken);
        if (course is null)
        {
            return Result<ExamStructureDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.TotalQuestions is < 1 or > 20 ||
            request.DurationMinutes is < 5 or > 180)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Tên cấu trúc đề, số lượng câu hỏi (1-20) hoặc thời lượng (5-180 phút) không hợp lệ.");
        }

        var now = DateTime.UtcNow;
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = actor.Id,
            Name = request.Name.Trim(),
            TotalQuestions = request.TotalQuestions,
            DurationMinutes = request.DurationMinutes,
            BloomDistribution = string.IsNullOrWhiteSpace(request.BloomDistribution)
                ? "{}"
                : request.BloomDistribution.Trim(),
            IsActive = request.IsActive,
            CreatedAt = now
        };

        _context.ExamStructures.Add(structure);
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "CREATE_EXAM_STRUCTURE",
            EntityName = "exam_structures",
            EntityId = structure.Id,
            NewValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};isActive={structure.IsActive}",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<ExamStructureDto>.Success(new ExamStructureDto(
            structure.Id,
            structure.CourseId,
            course.Code,
            structure.CreatedBy,
            structure.Name,
            structure.TotalQuestions,
            structure.DurationMinutes,
            structure.BloomDistribution,
            structure.IsActive,
            structure.CreatedAt));
    }
}
