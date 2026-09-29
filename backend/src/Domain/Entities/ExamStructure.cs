using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamStructure
{
    public int StructureId { get; set; }

    public int CourseId { get; set; }

    public string StructureName { get; set; } = null!;

    public string ExamMode { get; set; } = null!;

    public int EasyCount { get; set; }

    public int MediumCount { get; set; }

    public int HardCount { get; set; }

    public int DurationMinutes { get; set; }

    public int? MaxFollowups { get; set; }

    public int? FollowupTimeoutSec { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();

    public virtual ICollection<RealExamSession> RealExamSessions { get; set; } = new List<RealExamSession>();
}
