using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = null!;
    public string? StudentCode { get; set; }
    public string Role { get; set; } = "student";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Class> InstructedClasses { get; set; } = new List<Class>();
    public virtual ICollection<ClassEnrollment> Enrollments { get; set; } = new List<ClassEnrollment>();
    public virtual ICollection<ExamQuestion> ApprovedExamQuestions { get; set; } = new List<ExamQuestion>();
    public virtual ICollection<ExamStructure> CreatedExamStructures { get; set; } = new List<ExamStructure>();
    public virtual ICollection<PracticeSession> PracticeSessions { get; set; } = new List<PracticeSession>();
    public virtual ICollection<PracticeAnswer> PracticeAnswers { get; set; } = new List<PracticeAnswer>();
    public virtual ICollection<MockExamQuota> MockExamQuotas { get; set; } = new List<MockExamQuota>();
    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();
    public virtual ICollection<RealExamSessionShift> ProctoredShifts { get; set; } = new List<RealExamSessionShift>();
    public virtual ICollection<StudentExamTicket> ExamTickets { get; set; } = new List<StudentExamTicket>();
    public virtual ICollection<LecturerAudit> LecturerAudits { get; set; } = new List<LecturerAudit>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
