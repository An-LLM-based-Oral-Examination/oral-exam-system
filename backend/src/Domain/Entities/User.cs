using System;
using System.Collections.Generic;

namespace OralExamination.Domain.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string FullName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<MockExamQuota> MockExamQuota { get; set; } = new List<MockExamQuota>();

    public virtual ICollection<MockExamSession> MockExamSessions { get; set; } = new List<MockExamSession>();

    public virtual ICollection<PracticeSession> PracticeSessions { get; set; } = new List<PracticeSession>();

    public virtual ICollection<RealExamAssignment> RealExamAssignments { get; set; } = new List<RealExamAssignment>();

    public virtual ICollection<RealExamResult> RealExamResults { get; set; } = new List<RealExamResult>();

    public virtual Role Role { get; set; } = null!;
}
