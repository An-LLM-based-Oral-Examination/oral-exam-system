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
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;

/// <summary>
/// Handler cập nhật thông tin cấu trúc đề thi (ExamStructure) của Trưởng Bộ Môn / Quản trị viên.
/// </summary>
public sealed class UpdateExamStructureCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateExamStructureCommand, Result<ExamStructureDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<ExamStructureDto>> Handle(
        UpdateExamStructureCommand request,
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

        var structure = await _context.ExamStructures
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == request.StructureId, cancellationToken);

        if (structure is null)
        {
            return Result<ExamStructureDto>.Failure("NOT_FOUND: Không tìm thấy cấu trúc đề thi.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.TotalQuestions is < 1 or > 20 ||
            request.DurationMinutes is < 5 or > 180)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Tên cấu trúc đề, số lượng câu hỏi (1-20) hoặc thời lượng (5-180 phút) không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(request.BloomDistribution) &&
            request.BloomDistribution.Trim() != "{}")
        {
            Dictionary<string, int>? bloomMap;
            try
            {
                bloomMap = JsonSerializer.Deserialize<Dictionary<string, int>>(request.BloomDistribution.Trim());
            }
            catch
            {
                bloomMap = null;
            }

            if (bloomMap is null)
            {
                return Result<ExamStructureDto>.Failure(
                    "VALIDATION: Phân bổ mức độ nhận thức Bloom (BloomDistribution) phải là chuỗi JSON hợp lệ.");
            }

            if (bloomMap.Values.Any(v => v < 0))
            {
                return Result<ExamStructureDto>.Failure(
                    "VALIDATION: Số lượng câu hỏi của từng mức độ Bloom không được là số âm.");
            }

            if (bloomMap.Count > 0 && bloomMap.Values.Sum() > request.TotalQuestions)
            {
                return Result<ExamStructureDto>.Failure(
                    $"VALIDATION: Tổng số câu hỏi trong phân bổ Bloom ({bloomMap.Values.Sum()}) không được vượt quá tổng số câu hỏi của đề ({request.TotalQuestions}).");
            }
        }

        var oldValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};isActive={structure.IsActive}";

        structure.Name = request.Name.Trim();
        structure.TotalQuestions = request.TotalQuestions;
        structure.DurationMinutes = request.DurationMinutes;
        if (!string.IsNullOrWhiteSpace(request.BloomDistribution))
        {
            structure.BloomDistribution = request.BloomDistribution.Trim();
        }
        structure.IsActive = request.IsActive;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "UPDATE_EXAM_STRUCTURE",
            EntityName = "exam_structures",
            EntityId = structure.Id,
            OldValues = oldValues,
            NewValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};isActive={structure.IsActive}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<ExamStructureDto>.Success(new ExamStructureDto(
            structure.Id,
            structure.CourseId,
            structure.Course?.Code ?? string.Empty,
            structure.CreatedBy,
            structure.Name,
            structure.TotalQuestions,
            structure.DurationMinutes,
            structure.BloomDistribution,
            structure.IsActive,
            structure.CreatedAt));
    }
}
