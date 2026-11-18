using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.Application.Features.DeadLetterQueue;

internal static class DlqAuthorization
{
    internal const string Forbidden = "FORBIDDEN: Bạn không có quyền xử lý tác vụ DLQ này.";

    internal static async Task<User?> ActorAsync(IApplicationDbContext db, Guid? id, string? claimedRole, bool requireRole, CancellationToken ct)
    {
        if (!id.HasValue || id == Guid.Empty || (requireRole && string.IsNullOrWhiteSpace(claimedRole))) return null;
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        if (actor is null || actor.Role is not (UserRole.Admin or UserRole.ExaminationOfficer)) return null;
        return claimedRole is not null && !string.Equals(claimedRole, actor.Role, StringComparison.Ordinal) ? null : actor;
    }

    // TaskType is canonical for legacy rows whose FlowType kept its old default.
    internal static string? Flow(DlqEntity item) => item.TaskType switch
    {
        "PracticeGrading" => "PRACTICE",
        "MockExamGrading" => "MOCK_EXAM",
        "OfficialExamGrading" => "OFFICIAL_EXAM",
        "InClassGrading" => "IN_CLASS_EXAM",
        _ => null
    };

    internal static bool CanManageFlow(string role, string? flow) => role switch
    {
        UserRole.Admin => flow is "PRACTICE" or "MOCK_EXAM" or "IN_CLASS_EXAM",
        UserRole.ExaminationOfficer => flow == "OFFICIAL_EXAM",
        _ => false
    };

    internal static Task<bool> CanAccessAsync(IApplicationDbContext db, User actor, DlqEntity item, CancellationToken ct)
    {
        return Task.FromResult(CanManageFlow(actor.Role, Flow(item)));
    }

    internal static async Task<InClassExamSubmission?> InClassSubmissionAsync(IApplicationDbContext db, DlqEntity item, CancellationToken ct)
    {
        GradingTask? payload = null;
        try { payload = JsonSerializer.Deserialize<GradingTask>(item.PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { /* Legacy corrupt payload can be reconstructed only from canonical correlation. */ }

        var payloadId = payload?.InClassSubmissionId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var correlation = item.CorrelationId is { } c && c != Guid.Empty ? c : (Guid?)null;
        if (payloadId.HasValue && correlation.HasValue && payloadId != correlation) return null;
        var submissionId = payloadId ?? correlation;
        if (!submissionId.HasValue) return null;
        var submission = await db.InClassExamSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId.Value, ct);
        if (submission is null) return null;
        if (payload is not null &&
            ((payload.SessionId != Guid.Empty && payload.SessionId != submission.ShiftId) ||
             (payload.AnswerId != Guid.Empty && payload.AnswerId != submission.Id) ||
             (payload.StudentId != Guid.Empty && payload.StudentId != submission.StudentId) ||
             (payload.QuestionId != Guid.Empty && payload.QuestionId != submission.QuestionId))) return null;
        return submission;
    }
}
