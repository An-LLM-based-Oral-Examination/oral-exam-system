using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Abstraction interface cho DbContext phục vụ nguyên lý Dependency Inversion trong Clean Architecture.
/// Định nghĩa đủ 28 DbSets và SaveChangesAsync cho tầng Application.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<Course> Courses { get; }
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
    DbSet<LecturerAudit> LecturerAudits { get; }
    DbSet<LecturerAuditDetail> LecturerAuditDetails { get; }
    DbSet<AppealRequest> AppealRequests { get; }
    DbSet<DeadLetterQueue> DeadLetterQueues { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
