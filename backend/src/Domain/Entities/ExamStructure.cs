using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamStructure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Name { get; set; } = null!;
    public int TotalQuestions { get; set; } = 5;
    public int DurationMinutes { get; set; } = 30;
    public string BloomDistribution { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<ExamSet> ExamSets { get; set; } = new List<ExamSet>();
    public virtual ICollection<OfficialExamSession> OfficialExamSessions { get; set; } = new List<OfficialExamSession>();
}
