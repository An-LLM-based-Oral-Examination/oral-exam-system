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
    DbSet<QuestionReviewRequest> QuestionReviewRequests { get; }
    DbSet<QuestionReviewRequestItem> QuestionReviewRequestItems { get; }
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
    DbSet<OfficialExamCandidate> OfficialExamCandidates { get; }
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

    Task<T> ExecuteShiftAllocationAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> allocate, CancellationToken cancellationToken);

    /// <summary>Lock the shift row before fresh reads and persist one atomic management operation.</summary>
    Task<T> ExecuteShiftMutationAsync<T>(Guid shiftId, bool isOfficial, Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắt đầu phạm vi khóa phân bổ câu hỏi bằng Advisory Lock trên PostgreSQL (hoặc no-op nếu là InMemory).
    /// </summary>
    Task<IAsyncDisposable?> BeginShiftAllocationLockScopeAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default);
    /// <summary>Serialize and atomically persist a mock start for one student/course/Vietnam day.</summary>
    Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate, Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default);
    /// <summary>Serialize question allocation and idempotent participation for one in-class shift.</summary>
    Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join, CancellationToken cancellationToken = default);
    /// <summary>Serialize capacity checks and enrollment writes for one class across API instances.</summary>
    Task<T> ExecuteClassEnrollmentAsync<T>(Guid classId, Func<CancellationToken, Task<T>> enrollment, CancellationToken cancellationToken = default);
    /// <summary>Serialize answer persistence and participant-state calculation for one student in one in-class shift.</summary>
    Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId, Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default);
    /// <summary>Serialize one appeal transition so concurrent requests cannot skip or duplicate a workflow step.</summary>
    Task<T> ExecuteAppealTransitionAsync<T>(Guid appealId, Func<CancellationToken, Task<T>> transition, CancellationToken cancellationToken = default);
    /// <summary>Serialize an appeal decision with its ticket aggregate to keep the locked audit score consistent.</summary>
    Task<T> ExecuteAppealDecisionAsync<T>(Guid appealId, Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default);
    /// <summary>Serialize appeal assignments and lecturer workload checks under the same transaction.</summary>
    Task<T> ExecuteAppealAssignmentAsync<T>(IReadOnlyCollection<Guid> appealIds, IReadOnlyCollection<Guid> lecturerIds, Func<CancellationToken, Task<T>> assignment, CancellationToken cancellationToken = default);
    /// <summary>Serialize an all-or-nothing roster import for one official-exam shift.</summary>
    Task<T> ExecuteBulkExamTicketImportAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> import, CancellationToken cancellationToken = default);
    /// <summary>Serialize batch creation of official exam sessions within a semester.</summary>
    Task<T> ExecuteOfficialSemesterBatchAsync<T>(Guid semesterId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    /// <summary>Serialize batch creation of official exam shifts to prevent room/proctor/examiner time-overlap races.</summary>
    Task<T> ExecuteOfficialShiftScheduleBatchAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    /// <summary>Serialize candidate roster import/removal for one official exam session.</summary>
    Task<T> ExecuteOfficialSessionCandidatesAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    /// <summary>Serialize student auto-allocation across all shifts of an official exam session.</summary>
    Task<T> ExecuteOfficialSessionAllocationAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> allocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute the complete refresh-token lock, rotation and commit as one retriable operation.
    /// </summary>
    Task<T> ExecuteRefreshTokenRotationAsync<T>(Func<CancellationToken, Task<T>> rotate, CancellationToken cancellationToken = default);

    /// <summary>Acquire the per-token advisory lock inside the rotation execution strategy.</summary>
    Task<IRefreshTokenLockScope> BeginRefreshTokenLockScopeAsync(string tokenHash, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phạm vi khóa refresh token để serialize/claim token và phát hiện xung đột đồng thời.
/// </summary>
public interface IRefreshTokenLockScope : IAsyncDisposable
{
    bool HasLock { get; }

    /// <summary>
    /// Commit giao dịch chứa advisory lock và các thay đổi rotation refresh token.
    /// Bắt buộc gọi và chờ commit thành công trước khi phát hành access token mới ra ngoài.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback giao dịch nếu có lỗi phát sinh hoặc huỷ bỏ rotation.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
