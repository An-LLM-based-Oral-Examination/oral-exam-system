using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Courses;

internal static class CourseCloDependencies
{
    // Inventory: PracticeQuestion.CloId, ExamQuestion.CloId, ExamStructure.SlotDefinitions, InClassExamShift.SelectedCloIdsJson.
    public static async Task<bool> HasSlotReferenceAsync(IApplicationDbContext context, CourseClo clo,
        CancellationToken cancellationToken, bool codesOnly = false)
    {
        var definitions = await context.ExamStructures.AsNoTracking().Where(s => s.CourseId == clo.CourseId)
            .Select(s => s.SlotDefinitions).ToListAsync(cancellationToken);
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition)) continue;
            try
            {
                using var document = JsonDocument.Parse(definition);
                if (References(document.RootElement, clo, codesOnly)) return true;
            }
            catch (JsonException)
            {
                // Malformed legacy JSON cannot prove that the CLO is unused.
                return true;
            }
        }
        return false;
    }

    public static async Task<bool> HasInClassShiftReferenceAsync(
        IApplicationDbContext context,
        CourseClo clo,
        CancellationToken cancellationToken)
    {
        var classIds = await context.Classes
            .AsNoTracking()
            .Where(c => c.CourseId == clo.CourseId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // Lọc trên DB theo cột quan hệ chuẩn (tránh gọi strpos/LIKE trên cột jsonb của PostgreSQL 16)
        // rồi xác minh chính xác từng Guid bằng JSON parse để loại trừ khớp chuỗi con giả.
        var query = context.InClassExamShifts.AsNoTracking();
        query = classIds.Count > 0
            ? query.Where(s => classIds.Contains(s.ClassId) || s.QuestionPoolMode == "SELECTED_CLOS")
            : query.Where(s => s.QuestionPoolMode == "SELECTED_CLOS");

        var selectedCloJsonRows = await query
            .Select(s => s.SelectedCloIdsJson)
            .ToListAsync(cancellationToken);

        foreach (var rawJson in selectedCloJsonRows)
        {
            if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Trim() == "[]")
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                if (Matches(document.RootElement, null, clo.Id))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // Malformed legacy JSON cannot prove that the CLO is unused.
                if (rawJson.Contains(clo.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool References(JsonElement node, CourseClo clo, bool codesOnly)
    {
        if (node.ValueKind == JsonValueKind.Array)
            return node.EnumerateArray().Any(child => References(child, clo, codesOnly));
        if (node.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in node.EnumerateObject())
        {
            var key = property.Name.Replace("_", "").ToLowerInvariant();
            if ((key is "clocode" or "clocodes") && Matches(property.Value, clo.CloCode, null)) return true;
            if (!codesOnly && (key is "cloid" or "cloids") && Matches(property.Value, null, clo.Id)) return true;
            if (References(property.Value, clo, codesOnly)) return true;
        }
        return false;
    }

    private static bool Matches(JsonElement value, string? code, Guid? id) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Any(child => Matches(child, code, id)),
        JsonValueKind.String => id.HasValue
            ? Guid.TryParse(value.GetString(), out var parsed) && parsed == id.Value
            : string.Equals(value.GetString()?.Trim(), code?.Trim(), StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
