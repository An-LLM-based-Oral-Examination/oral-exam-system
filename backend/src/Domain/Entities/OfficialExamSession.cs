using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class OfficialExamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid ExamStructureId { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly ExamDate { get; set; }
    public bool HasFollowUp { get; set; } = false;
    public int MaxFollowUpQuestions { get; set; } = 1;
    public string ExamInputMode { get; set; } = OralExamination.Domain.Enums.ExamInputMode.VoiceWithTranscriptEdit;
    public int TranscriptBufferSeconds { get; set; } = 60;
    public string Status { get; set; } = "scheduled";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual ExamStructure ExamStructure { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<RealExamSessionShift> Shifts { get; set; } = new List<RealExamSessionShift>();
}
