using System;

namespace OralExamination.Domain.Entities;

public partial class InClassShiftPassword
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public string PlainCode { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual InClassExamShift Shift { get; set; } = null!;
}
