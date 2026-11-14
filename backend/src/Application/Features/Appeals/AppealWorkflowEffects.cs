using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Appeals;

internal static class AppealWorkflowEffects
{
    internal const int MaxActiveAppealsPerLecturer = 24;

    internal static void AddTransitionAudit(IApplicationDbContext context, Guid actorId, AppealRequest appeal,
        string? fromStatus, string toStatus, string? reason = null)
    {
        context.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "APPEAL_STATUS_CHANGED",
            EntityName = nameof(AppealRequest),
            EntityId = appeal.Id,
            OldValues = JsonSerializer.Serialize(new { status = fromStatus }),
            NewValues = JsonSerializer.Serialize(new
            {
                status = toStatus,
                appeal.AssignedTo,
                appeal.ProposedScore,
                appeal.Decision,
                reason
            })
        });
    }

    internal static async Task AddNotificationIfMissingAsync(IApplicationDbContext context, Guid userId,
        AppealRequest appeal, string type, string title, string message, CancellationToken cancellationToken)
    {
        var exists = await context.Notifications.AnyAsync(n => n.UserId == userId && n.Type == type &&
            n.RelatedEntityName == nameof(AppealRequest) && n.RelatedEntityId == appeal.Id, cancellationToken);
        if (exists) return;

        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityName = nameof(AppealRequest),
            RelatedEntityId = appeal.Id
        });
    }
}
