using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamSession
{
    public Guid ExamSessionId { get; set; }

    public int ClassId { get; set; }

    public int StructureId { get; set; }

    public string ExamMode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateOnly ExamDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<RealExamSessionShift> RealExamSessionShifts { get; set; } = new List<RealExamSessionShift>();

    public virtual ExamStructure Structure { get; set; } = null!;
}
