using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace OralExamination.Application.Features.Notifications.DTOs;

public sealed record NotificationItemDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? RelatedEntityName { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public IReadOnlyDictionary<string, string?>? Metadata { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record NotificationsSummaryResponseDto
{
    public IReadOnlyList<NotificationItemDto> Items { get; init; } = Array.Empty<NotificationItemDto>();
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public static class NotificationIdHelper
{
    public static Guid CreateDeterministicId(Guid userId, string type, Guid entityId)
    {
        var raw = $"{userId:D}:{type}:{entityId:D}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
