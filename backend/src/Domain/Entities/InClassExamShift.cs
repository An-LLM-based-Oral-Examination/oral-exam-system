using System;
using System.Collections.Generic;
using OralExamination.Domain.Common;

namespace OralExamination.Domain.Entities;

public partial class InClassExamShift : ILockableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid LecturerId { get; set; }
    public string ShiftName { get; set; } = null!;
    public string? Description { get; set; }
    public string QuestionPoolMode { get; set; } = "ALL_COURSE_BANK";
    public string SelectedQuestionIdsJson { get; set; } = "[]";
    public int TotalQuestionsPerStudent { get; set; } = 1;
    public bool HasFollowUp { get; set; } = false;
    public int MaxFollowUpQuestions { get; set; } = 2;
    public bool IsAutoPublishGrades { get; set; } = true;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TotpSecretKey { get; set; } = null!;
    public string Status { get; set; } = "SCHEDULED";
    public bool IsLocked { get; set; } = false;
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;
    public virtual User Lecturer { get; set; } = null!;
    public virtual ICollection<InClassShiftPassword> Passwords { get; set; } = new List<InClassShiftPassword>();
    public virtual ICollection<InClassShiftParticipant> Participants { get; set; } = new List<InClassShiftParticipant>();
    public virtual ICollection<InClassExamSubmission> Submissions { get; set; } = new List<InClassExamSubmission>();
}
