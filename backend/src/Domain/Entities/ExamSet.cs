using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? CreatedBy { get; set; }
    public string SetCode { get; set; } = null!;
    public string Status { get; set; } = OralExamination.Domain.Enums.ExamSetStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsSampleSet { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public virtual ExamStructure Structure { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
    public virtual OfficialExamSession? Session { get; set; }
    public virtual User? Creator { get; set; }
    public virtual User? Approver { get; set; }
    public virtual ICollection<ExamSetQuestion> ExamSetQuestions { get; set; } = new List<ExamSetQuestion>();
    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();
    public virtual ICollection<StudentExamTicket> StudentExamTickets { get; set; } = new List<StudentExamTicket>();
}
