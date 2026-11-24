using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Common;

/// <summary>
/// Chuẩn hóa cách tính thời lượng gốc và mốc trần cộng bù tối đa khi tạm dừng (Pause/Resume)
/// cho ca thi thử MF-02, ngăn chặn khai thác kéo dài thời gian vô hạn qua paused_at.
/// </summary>
public static class MockExamClockHelper
{
    public const int MaxPauseExtensionMinutes = 15;
    public const int DefaultFallbackDurationMinutes = 15;

    public static int ResolveBaseDurationMinutes(MockExamSession session) =>
        ResolveBaseDurationMinutes(session, activeStructureDurationMinutes: null);

    public static int ResolveBaseDurationMinutes(MockExamSession session, int? activeStructureDurationMinutes)
    {
        var structureDuration = session.ExamSet?.Structure?.DurationMinutes ?? 0;
        if (structureDuration <= 0 && activeStructureDurationMinutes is > 0)
        {
            structureDuration = activeStructureDurationMinutes.Value;
        }

        var rawBase = structureDuration > 0
            ? structureDuration
            : (session.Course is { MockExamDurationMinutes: > 0 }
                ? session.Course.MockExamDurationMinutes
                : DefaultFallbackDurationMinutes);

        return session.HasFollowUp
            ? Math.Max(45, rawBase + 15)
            : rawBase;
    }

    public static async Task<int> ResolveBaseDurationMinutesAsync(
        IApplicationDbContext context,
        MockExamSession session,
        CancellationToken cancellationToken = default)
    {
        var structureDuration = session.ExamSet?.Structure?.DurationMinutes ?? 0;
        if (structureDuration <= 0)
        {
            structureDuration = await context.ExamStructures
                .AsNoTracking()
                .Where(s => s.CourseId == session.CourseId && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.DurationMinutes)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ResolveBaseDurationMinutes(session, structureDuration > 0 ? structureDuration : null);
    }

    public static async Task<Dictionary<Guid, int>> ResolveActiveStructureDurationsByCourseAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> courseIds,
        CancellationToken cancellationToken = default)
    {
        if (courseIds.Count == 0)
        {
            return [];
        }

        var distinctIds = courseIds.Distinct().ToList();
        var rows = await context.ExamStructures
            .AsNoTracking()
            .Where(s => distinctIds.Contains(s.CourseId) && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.CourseId, s.DurationMinutes })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var row in rows)
        {
            result.TryAdd(row.CourseId, row.DurationMinutes);
        }

        return result;
    }

    public static DateTime ResolveMaxAllowedExpiresAt(MockExamSession session) =>
        ResolveMaxAllowedExpiresAt(session, ResolveBaseDurationMinutes(session));

    public static DateTime ResolveMaxAllowedExpiresAt(MockExamSession session, int baseDurationMinutes) =>
        session.StartedAt.AddMinutes(baseDurationMinutes + MaxPauseExtensionMinutes);

    public static DateTime ComputeClampedResumeExpiresAt(MockExamSession session, DateTime pausedAt, DateTime nowUtc) =>
        ComputeClampedResumeExpiresAt(session, pausedAt, nowUtc, ResolveBaseDurationMinutes(session));

    public static DateTime ComputeClampedResumeExpiresAt(
        MockExamSession session,
        DateTime pausedAt,
        DateTime nowUtc,
        int baseDurationMinutes)
    {
        var pauseDelta = nowUtc > pausedAt ? nowUtc - pausedAt : TimeSpan.Zero;
        var candidateExpiresAt = session.ExpiresAt + pauseDelta;
        var maxAllowedExpiresAt = ResolveMaxAllowedExpiresAt(session, baseDurationMinutes);
        return candidateExpiresAt > maxAllowedExpiresAt ? maxAllowedExpiresAt : candidateExpiresAt;
    }
}
