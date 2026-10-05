using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class ExamSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StructureId { get; set; }
    public Guid CourseId { get; set; }
    public string SetCode { get; set; } = null!;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public virtual ExamStructure Structure { get; set; } = null!;
    public virtual Course Course { get; set; } = null!;
    public virtual ICollection<ExamSetQuestion> ExamSetQuestions { get; set; } = new List<ExamSetQuestion>();
    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();
    public virtual ICollection<StudentExamTicket> StudentExamTickets { get; set; } = new List<StudentExamTicket>();
}
