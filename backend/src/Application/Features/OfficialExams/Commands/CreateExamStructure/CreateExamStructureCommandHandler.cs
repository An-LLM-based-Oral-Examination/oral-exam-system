using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;

/// <summary>
/// Handler xử lý tạo mới cấu trúc đề thi của Trưởng Bộ Môn phụ trách môn.
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

        if (actor.Role != UserRole.DepartmentHead)
        {
            return Result<ExamStructureDto>.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn phụ trách môn mới có quyền cấu hình đề thi.");
        }

        var course = await _context.Courses.Include(course => course.AcademicProgram).FirstOrDefaultAsync(
            c => c.Id == request.CourseId && c.IsActive,
            cancellationToken);
        if (course is null)
        {
            return Result<ExamStructureDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        if (!AcademicCourseOwnership.IsOwnedBy(course, actor.Id))
        {
            return Result<ExamStructureDto>.Failure("FORBIDDEN: Môn học không thuộc phạm vi phụ trách.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.TotalQuestions is < 1 or > 20 ||
            request.DurationMinutes is < 5 or > 180)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Tên cấu trúc đề, số lượng câu hỏi (1-20) hoặc thời lượng (5-180 phút) không hợp lệ.");
        }

        if (request.RequiredSetCount is < 1 or > 20)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Số lượng bộ đề cần soạn (RequiredSetCount) phải nằm trong khoảng từ 1 đến 20.");
        }

        var normalizedSlotDefinitions = "[]";
        if (!string.IsNullOrWhiteSpace(request.SlotDefinitions))
        {
            var trimmedSlots = request.SlotDefinitions.Trim();
            try
            {
                using var slotsDoc = JsonDocument.Parse(trimmedSlots);
                if (slotsDoc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Result<ExamStructureDto>.Failure(
                        "VALIDATION: Danh sách cấu hình vị trí câu hỏi (SlotDefinitions) phải là chuỗi JSON Array hợp lệ.");
                }
            }
            catch (JsonException)
            {
                return Result<ExamStructureDto>.Failure(
                    "VALIDATION: Danh sách cấu hình vị trí câu hỏi (SlotDefinitions) phải là chuỗi JSON Array hợp lệ.");
            }

            normalizedSlotDefinitions = trimmedSlots;
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

            var seenBloomKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in bloomMap.Keys)
            {
                var trimmedKey = key?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmedKey) ||
                    !BloomLevel.All.Contains(trimmedKey, StringComparer.OrdinalIgnoreCase))
                {
                    return Result<ExamStructureDto>.Failure(
                        $"VALIDATION: Mức độ nhận thức Bloom '{key}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", BloomLevel.All)}.");
                }

                if (!seenBloomKeys.Add(trimmedKey))
                {
                    return Result<ExamStructureDto>.Failure(
                        $"VALIDATION: Mức độ nhận thức Bloom '{trimmedKey}' bị khai báo trùng lặp trong BloomDistribution.");
                }
            }

            if (bloomMap.Values.Any(v => v < 0))
            {
                return Result<ExamStructureDto>.Failure(
                    "VALIDATION: Số lượng câu hỏi của từng mức độ Bloom không được là số âm.");
            }

            if (bloomMap.Count > 0 && bloomMap.Values.Sum() <= 0)
            {
                return Result<ExamStructureDto>.Failure(
                    "VALIDATION: Tổng số câu hỏi trong phân bổ Bloom (BloomDistribution) phải lớn hơn 0.");
            }

            if (bloomMap.Count > 0 && bloomMap.Values.Sum() > request.TotalQuestions)
            {
                return Result<ExamStructureDto>.Failure(
                    $"VALIDATION: Tổng số câu hỏi trong phân bổ Bloom ({bloomMap.Values.Sum()}) không được vượt quá tổng số câu hỏi của đề ({request.TotalQuestions}).");
            }
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
            SlotDefinitions = normalizedSlotDefinitions,
            RequiredSetCount = request.RequiredSetCount,
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
            NewValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};requiredSetCount={structure.RequiredSetCount};isActive={structure.IsActive}",
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
            structure.CreatedAt,
            structure.RequiredSetCount,
            structure.SlotDefinitions));
    }
}
