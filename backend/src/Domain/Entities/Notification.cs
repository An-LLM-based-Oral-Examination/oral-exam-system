using System;

namespace OralExamination.Domain.Entities;

public partial class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? MetadataJson { get; set; }

    public virtual User User { get; set; } = null!;
}
