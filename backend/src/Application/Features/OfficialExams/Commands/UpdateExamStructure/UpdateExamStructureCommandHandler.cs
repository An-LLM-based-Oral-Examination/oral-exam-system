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

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;

/// <summary>
/// Handler cập nhật cấu trúc đề thi của Trưởng Bộ Môn phụ trách môn.
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

        if (actor.Role != UserRole.DepartmentHead)
        {
            return Result<ExamStructureDto>.Failure(
                "FORBIDDEN: Chỉ Trưởng Bộ Môn phụ trách môn mới có quyền cấu hình đề thi.");
        }

        var structure = await _context.ExamStructures
            .Include(e => e.Course)
                .ThenInclude(course => course.AcademicProgram)
            .FirstOrDefaultAsync(e => e.Id == request.StructureId, cancellationToken);

        if (structure is null)
        {
            return Result<ExamStructureDto>.Failure("NOT_FOUND: Không tìm thấy cấu trúc đề thi.");
        }

        if (structure.Course is null || !AcademicCourseOwnership.IsOwnedBy(structure.Course, actor.Id))
        {
            return Result<ExamStructureDto>.Failure("FORBIDDEN: Môn học không thuộc phạm vi phụ trách.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 200 ||
            request.TotalQuestions is < 1 or > 20 ||
            request.DurationMinutes is < 5 or > 180)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Tên cấu trúc đề (1-200 ký tự), số lượng câu hỏi (1-20) hoặc thời lượng (5-180 phút) không hợp lệ.");
        }

        if (request.RequiredSetCount is < 1 or > 20)
        {
            return Result<ExamStructureDto>.Failure(
                "VALIDATION: Số lượng bộ đề cần soạn (RequiredSetCount) phải nằm trong khoảng từ 1 đến 20.");
        }

        string? validatedSlotDefinitions = null;
        if (request.SlotDefinitions is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SlotDefinitions))
            {
                validatedSlotDefinitions = "[]";
            }
            else
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

                validatedSlotDefinitions = trimmedSlots;
            }
        }

        var effectiveBloomDistribution = !string.IsNullOrWhiteSpace(request.BloomDistribution)
            ? request.BloomDistribution.Trim()
            : (structure.BloomDistribution ?? "{}").Trim();

        Dictionary<string, int>? parsedEffectiveBloom = null;
        if (!string.IsNullOrWhiteSpace(effectiveBloomDistribution) &&
            effectiveBloomDistribution != "{}")
        {
            Dictionary<string, int>? bloomMap;
            try
            {
                bloomMap = JsonSerializer.Deserialize<Dictionary<string, int>>(effectiveBloomDistribution);
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

            parsedEffectiveBloom = bloomMap;
        }

        if (await _context.ExamAuthoringAssignments.AsNoTracking().AnyAsync(x => x.StructureId == structure.Id, cancellationToken) ||
            await _context.ExamStructureClos.AsNoTracking().AnyAsync(x => x.StructureId == structure.Id, cancellationToken))
            return Result<ExamStructureDto>.Failure("CONFLICT: Cấu trúc theo kỳ thi là phiên bản cố định. Hãy tạo phiên bản mới trong hồ sơ chuẩn bị đề.");

        var isChangingTotalQuestions = request.TotalQuestions != structure.TotalQuestions;
        var isChangingBloomDistribution = !AreBloomDistributionsEquivalent(
            structure.BloomDistribution,
            effectiveBloomDistribution,
            parsedEffectiveBloom);

        if (isChangingTotalQuestions || isChangingBloomDistribution)
        {
            var hasLockedExamSet = await _context.ExamSets
                .AsNoTracking()
                .AnyAsync(es =>
                    es.StructureId == structure.Id
                    && es.IsActive
                    && (es.Status == ExamSetStatus.Official || es.Status == ExamSetStatus.HandedOver),
                    cancellationToken);

            if (hasLockedExamSet)
            {
                return Result<ExamStructureDto>.Failure(
                    "CONFLICT: Không thể thay đổi tổng số câu hỏi (TotalQuestions) hoặc phân bổ Bloom (BloomDistribution) vì cấu trúc đề đã có bộ đề chính thức (OFFICIAL) hoặc đã bàn giao (HANDED_OVER) đang hoạt động.");
            }
        }

        var oldValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};requiredSetCount={structure.RequiredSetCount};isActive={structure.IsActive}";

        structure.Name = request.Name.Trim();
        structure.TotalQuestions = request.TotalQuestions;
        structure.DurationMinutes = request.DurationMinutes;
        if (!string.IsNullOrWhiteSpace(request.BloomDistribution))
        {
            structure.BloomDistribution = request.BloomDistribution.Trim();
        }
        else if (string.IsNullOrWhiteSpace(structure.BloomDistribution))
        {
            structure.BloomDistribution = "{}";
        }
        structure.RequiredSetCount = request.RequiredSetCount;
        if (validatedSlotDefinitions is not null)
        {
            structure.SlotDefinitions = validatedSlotDefinitions;
        }
        else if (string.IsNullOrWhiteSpace(structure.SlotDefinitions))
        {
            structure.SlotDefinitions = "[]";
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
            NewValues = $"name={structure.Name};totalQuestions={structure.TotalQuestions};durationMinutes={structure.DurationMinutes};requiredSetCount={structure.RequiredSetCount};isActive={structure.IsActive}",
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
            structure.BloomDistribution ?? "{}",
            structure.IsActive,
            structure.CreatedAt,
            structure.RequiredSetCount,
            structure.SlotDefinitions));
    }

    private static bool AreBloomDistributionsEquivalent(
        string? existingJson,
        string requestedNormalizedJson,
        Dictionary<string, int>? parsedRequestedMap)
    {
        var currentNormalized = string.IsNullOrWhiteSpace(existingJson) ? "{}" : existingJson.Trim();
        if (string.Equals(currentNormalized, requestedNormalizedJson, StringComparison.Ordinal))
        {
            return true;
        }

        Dictionary<string, int>? currentMap;
        if (currentNormalized == "{}")
        {
            currentMap = [];
        }
        else
        {
            try
            {
                currentMap = JsonSerializer.Deserialize<Dictionary<string, int>>(currentNormalized);
            }
            catch
            {
                return false;
            }
        }

        var requestedMap = requestedNormalizedJson == "{}" ? [] : parsedRequestedMap;
        if (currentMap is null || requestedMap is null)
        {
            return false;
        }

        var canonicalCurrent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in currentMap)
        {
            if (kv.Value == 0) continue;
            if (!canonicalCurrent.TryAdd(kv.Key.Trim(), kv.Value))
            {
                return false;
            }
        }

        var canonicalRequested = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in requestedMap)
        {
            if (kv.Value == 0) continue;
            if (!canonicalRequested.TryAdd(kv.Key.Trim(), kv.Value))
            {
                return false;
            }
        }

        if (canonicalCurrent.Count != canonicalRequested.Count)
        {
            return false;
        }

        foreach (var (key, value) in canonicalCurrent)
        {
            if (!canonicalRequested.TryGetValue(key, out var requestedValue) || requestedValue != value)
            {
                return false;
            }
        }

        return true;
    }
}
