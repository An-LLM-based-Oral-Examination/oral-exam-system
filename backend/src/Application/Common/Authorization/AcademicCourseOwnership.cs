using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Common.Authorization;

/// <summary>Program ownership is canonical; legacy course ownership is a fallback only.</summary>
public static class AcademicCourseOwnership
{
    public static Guid? EffectiveDepartmentHead(Course course) =>
        course.AcademicProgram?.DepartmentHeadUserId is { } programOwner && programOwner != Guid.Empty
            ? programOwner
            : course.DepartmentHeadUserId is { } legacyOwner && legacyOwner != Guid.Empty ? legacyOwner : null;

    public static bool IsOwnedBy(Course course, Guid actorUserId) =>
        actorUserId != Guid.Empty && EffectiveDepartmentHead(course) == actorUserId;

    public static bool CanManage(Course course, string? actorRole, Guid? actorUserId) =>
        string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase)
        || (string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase)
            && actorUserId.HasValue && IsOwnedBy(course, actorUserId.Value));

    public static (string? Role, Guid? UserId) ParseActor(string? roleAndId)
    {
        if (string.IsNullOrWhiteSpace(roleAndId)) return (null, null);
        var parts = roleAndId.Split(':', 2, StringSplitOptions.TrimEntries);
        return (parts[0], parts.Length == 2 && Guid.TryParse(parts[1], out var id) && id != Guid.Empty ? id : null);
    }
}
