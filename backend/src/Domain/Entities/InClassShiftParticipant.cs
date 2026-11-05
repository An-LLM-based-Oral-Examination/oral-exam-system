using System;

namespace OralExamination.Domain.Entities;

public partial class InClassShiftParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? AssignedQuestionId { get; set; }
    public PracticeQuestion? AssignedQuestion { get; set; }
    public string AssignedQuestionIdsJson { get; set; } = "[]";
    public decimal? TotalAiScore { get; set; }
    public decimal? TotalFinalScore { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "JOINED";

    public virtual InClassExamShift Shift { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
}
