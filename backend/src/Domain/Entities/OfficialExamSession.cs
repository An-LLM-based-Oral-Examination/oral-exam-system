using System;
using System.Collections.Generic;
using OralExamination.Domain.Enums;

namespace OralExamination.Domain.Entities;

public partial class OfficialExamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? ExamStructureId { get; set; }
    public string Title { get; set; } = null!;
    public string ExamAttemptType { get; set; } = OfficialExamAttemptType.FinalExam;
    public DateOnly ExamDate { get; set; }
    public bool HasFollowUp { get; set; } = false;
    public int MaxFollowUpQuestions { get; set; } = 2;
    public string ExamInputMode { get; set; } = OralExamination.Domain.Enums.ExamInputMode.VoiceWithTranscriptEdit;
    public int TranscriptBufferSeconds { get; set; } = 60;
    public string Status { get; set; } = "scheduled";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Course Course { get; set; } = null!;
    public virtual Semester? Semester { get; set; }
    public virtual ExamStructure? ExamStructure { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<RealExamSessionShift> Shifts { get; set; } = new List<RealExamSessionShift>();
    public virtual ICollection<ExamSet> ExamSets { get; set; } = new List<ExamSet>();
    public virtual ICollection<OfficialExamCandidate> Candidates { get; set; } = new List<OfficialExamCandidate>();
}
