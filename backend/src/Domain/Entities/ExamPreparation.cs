namespace OralExamination.Domain.Entities;

public sealed class ExamPreparation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int RequiredSetCount { get; set; }
    public DateTime Deadline { get; set; }
    public string? Notes { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandedOverAt { get; set; }
    public Guid? HandedOverBy { get; set; }
    public OfficialExamSession Session { get; set; } = null!;
}

public sealed class ExamAuthoringAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PreparationId { get; set; }
    public Guid StructureId { get; set; }
    public Guid LecturerId { get; set; }
    public Guid AssignedBy { get; set; }
    public int SetCount { get; set; }
    public DateTime Deadline { get; set; }
    public string? Notes { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public ExamPreparation Preparation { get; set; } = null!;
    public ExamStructure Structure { get; set; } = null!;
}

public sealed class ExamStructureClo
{
    public Guid StructureId { get; set; }
    public Guid CloId { get; set; }
    public ExamStructure Structure { get; set; } = null!;
    public CourseClo Clo { get; set; } = null!;
}

/// <summary>Append-only record of the complete paper at each workflow transition.</summary>
public sealed class ExamSetRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamSetId { get; set; }
    public int Sequence { get; set; }
    public string Action { get; set; } = null!;
    public Guid ActorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string Snapshot { get; set; } = "{}";
    public ExamSet ExamSet { get; set; } = null!;
}
