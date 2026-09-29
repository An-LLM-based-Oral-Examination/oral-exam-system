using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class RealExamSessionShift
{
    public Guid ShiftId { get; set; }

    public Guid ExamSessionId { get; set; }

    public string ShiftName { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string LabRoom { get; set; } = null!;

    public virtual RealExamSession ExamSession { get; set; } = null!;

    public virtual ICollection<RealExamAssignment> RealExamAssignments { get; set; } = new List<RealExamAssignment>();
}
