using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Abstraction interface cho DbContext phục vụ nguyên lý Dependency Inversion trong Clean Architecture.
/// Định nghĩa đủ 33 DbSets và SaveChangesAsync cho tầng Application.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<AcademicProgram> AcademicPrograms { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseOffering> CourseOfferings { get; }
    DbSet<CourseClo> CourseClos { get; }
    DbSet<CourseLecturer> CourseLecturers { get; }
    DbSet<Class> Classes { get; }
    DbSet<ClassEnrollment> ClassEnrollments { get; }
    DbSet<Rubric> Rubrics { get; }
    DbSet<RubricCriterion> RubricCriteria { get; }
    DbSet<PracticeQuestion> PracticeQuestions { get; }
    DbSet<ExamQuestion> ExamQuestions { get; }
    DbSet<PracticeSession> PracticeSessions { get; }
    DbSet<PracticeAnswer> PracticeAnswers { get; }
    DbSet<AiEvaluation> AiEvaluations { get; }
    DbSet<AiEvaluationDetail> AiEvaluationDetails { get; }
    DbSet<ExamStructure> ExamStructures { get; }
    DbSet<ExamSet> ExamSets { get; }
    DbSet<ExamSetQuestion> ExamSetQuestions { get; }
    DbSet<MockExamQuota> MockExamQuotas { get; }
    DbSet<MockExamSession> MockExamSessions { get; }
    DbSet<MockExamAnswer> MockExamAnswers { get; }
    DbSet<OfficialExamSession> OfficialExamSessions { get; }
    DbSet<RealExamSessionShift> RealExamSessionShifts { get; }
    DbSet<StudentExamTicket> StudentExamTickets { get; }
    DbSet<ExamQuestionSubmission> ExamQuestionSubmissions { get; }
    DbSet<ExamAntiCheatLog> ExamAntiCheatLogs { get; }
    DbSet<LecturerAudit> LecturerAudits { get; }
    DbSet<LecturerAuditDetail> LecturerAuditDetails { get; }
    DbSet<AppealRequest> AppealRequests { get; }
    DbSet<DeadLetterQueue> DeadLetterQueues { get; }
    DbSet<GradingJob> GradingJobs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<InClassExamShift> InClassExamShifts { get; }
    DbSet<InClassShiftPassword> InClassShiftPasswords { get; }
    DbSet<InClassShiftParticipant> InClassShiftParticipants { get; }
    DbSet<InClassExamSubmission> InClassExamSubmissions { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắt đầu phạm vi khóa phân bổ câu hỏi bằng Advisory Lock trên PostgreSQL (hoặc no-op nếu là InMemory).
    /// </summary>
    Task<IAsyncDisposable?> BeginShiftAllocationLockScopeAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default);
    /// <summary>Serialize and atomically persist a mock start for one student/course/Vietnam day.</summary>
    Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate, Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default);
    /// <summary>Serialize question allocation and idempotent participation for one in-class shift.</summary>
    Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join, CancellationToken cancellationToken = default);
    /// <summary>Serialize answer persistence and participant-state calculation for one student in one in-class shift.</summary>
    Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId, Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default);
    /// <summary>Serialize one appeal transition so concurrent requests cannot skip or duplicate a workflow step.</summary>
    Task<T> ExecuteAppealTransitionAsync<T>(Guid appealId, Func<CancellationToken, Task<T>> transition, CancellationToken cancellationToken = default);
    /// <summary>Serialize an appeal decision with its ticket aggregate to keep the locked audit score consistent.</summary>
    Task<T> ExecuteAppealDecisionAsync<T>(Guid appealId, Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default);
    /// <summary>Serialize appeal assignments and lecturer workload checks under the same transaction.</summary>
    Task<T> ExecuteAppealAssignmentAsync<T>(IReadOnlyCollection<Guid> appealIds, IReadOnlyCollection<Guid> lecturerIds, Func<CancellationToken, Task<T>> assignment, CancellationToken cancellationToken = default);
}
