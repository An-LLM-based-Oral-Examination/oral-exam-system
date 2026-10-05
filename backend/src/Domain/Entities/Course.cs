using System;
using System.Collections.Generic;
using OralExamination.Domain.Enums;

namespace OralExamination.Domain.Entities;

public partial class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Credits { get; set; } = 3;
    public Guid SemesterId { get; set; }
    public bool HasFollowUp { get; set; } = false;
    public int TranscriptBufferSeconds { get; set; } = 60;
    public int MaxFollowUpQuestions { get; set; } = 2;
    public string ExamInputMode { get; set; } = OralExamination.Domain.Enums.ExamInputMode.VoiceAndTextInput;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Semester Semester { get; set; } = null!;
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
    public virtual ICollection<PracticeQuestion> PracticeQuestions { get; set; } = new List<PracticeQuestion>();
    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public virtual ICollection<ExamStructure> ExamStructures { get; set; } = new List<ExamStructure>();
    public virtual ICollection<ExamSet> ExamSets { get; set; } = new List<ExamSet>();
    public virtual ICollection<MockExamQuota> MockExamQuotas { get; set; } = new List<MockExamQuota>();
    public virtual ICollection<PracticeSession> PracticeSessions { get; set; } = new List<PracticeSession>();
    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();
    public virtual ICollection<OfficialExamSession> OfficialExamSessions { get; set; } = new List<OfficialExamSession>();
}
