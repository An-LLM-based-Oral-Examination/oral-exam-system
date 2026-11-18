using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.Features.InClassShifts;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

/// <summary>
/// Bộ kiểm thử tích hợp CSDL PostgreSQL 16 thật cho MF-01 On-Demand (TASK-008A).
/// Chạy trực tiếp trên oralexam_integration_test, kiểm chứng:
/// 1. BOLA Prevention: Student B không thể đọc câu hỏi tiếp theo, nộp bài hoặc kết thúc session của Student A (403 Forbidden thay vì 404).
/// 2. Question Bank Isolation: Chỉ cấp câu hỏi APPROVED từ practice_questions; không lộ DRAFT hoặc exam_questions.
/// 3. Anti-3-Consecutive & Pool Exhaustion: Khi 2 câu liền trước cùng độ khó và các mức khác cạn kiệt, trả hasMoreQuestions = false thay vì fallback mức cũ.
/// 4. Follow-Up Boundary [4.0, 8.0]: Chỉ cho phép nộp câu hỏi phụ khi điểm câu gốc trong [4.0, 8.0] và chưa vượt quá MaxPracticeFollowUpQuestions.
/// 5. Inactivity Timeout: Session quá 5 phút tự động kết thúc và từ chối các thao tác tiếp theo với mã lỗi 410 Gone.
/// 6. Concurrency Serialization & Idempotency: Không cho phép nộp trùng câu hỏi chính hoặc câu hỏi phụ.
/// 7. Zero-R2: Không lưu bất kỳ URL R2 nào cho dữ liệu âm thanh/bài làm luyện tập MF-01.
/// Tự động dọn dẹp sạch sẽ sau khi test hoàn tất, bảo đảm ZERO-POLLUTION (0 rows rác).
/// </summary>
[CollectionDefinition("PostgreSqlNonParallelSchemaCollection", DisableParallelization = true)]
public class PostgreSqlNonParallelSchemaCollectionDefinition
{
}

[Trait("Category", "Integration")]
[Collection("PostgreSqlNonParallelSchemaCollection")]
public class PracticeOnDemandPostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = new();
    private readonly List<Guid> _createdSemesterIds = new();
    private readonly List<Guid> _createdCourseIds = new();
    private readonly List<Guid> _createdRubricIds = new();
    private readonly List<Guid> _createdRubricCriterionIds = new();
    private readonly List<Guid> _createdPracticeQuestionIds = new();
    private readonly List<Guid> _createdExamQuestionIds = new();
    private readonly List<Guid> _createdPracticeSessionIds = new();
    private readonly List<Guid> _createdPracticeAnswerIds = new();
    private readonly List<Guid> _createdAiEvaluationIds = new();
    private readonly List<Guid> _createdAiEvaluationDetailIds = new();

    private OralExamDbContext? _context;

    public async Task InitializeAsync()
    {
        _context = CreatePostgresDbContext();
        await _context.Database.MigrateAsync();
        await PostgresSchemaReconciler.ReconcilePostMigrationAsync(_context);
    }

    private static OralExamDbContext CreatePostgresDbContext(params IInterceptor[] interceptors)
    {
        var connStr = PostgresIntegrationTestDatabase.GetPostgresConnectionString();
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(connStr);
        if (interceptors != null && interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }
        var options = builder.Options;
        var context = new OralExamDbContext(options);
        context.Database.CanConnect().Should().BeTrue("PostgreSQL test database phải kết nối được tại localhost:5432");
        return context;
    }

    /// <summary>
    /// Interceptor thiết lập Barrier để ép 2 connection đồng thời hoàn thành truy vấn pre-check SELECT
    /// trước khi bất kỳ connection nào thực thi INSERT. Bảo đảm race condition thực sự ở tầng CSDL PostgreSQL.
    /// T8A-F017: Bắt buộc fail-safe khi timeout và xác nhận đủ 2 participant rendezvous.
    /// </summary>
    private sealed class ConcurrencyPrecheckBarrierInterceptor : DbCommandInterceptor
    {
        private readonly Barrier _barrier;
        private int _precheckParticipantCount = 0;

        public int PrecheckParticipantCount => _precheckParticipantCount;

        public ConcurrencyPrecheckBarrierInterceptor(int participantCount = 2)
        {
            _barrier = new Barrier(participantCount);
        }

        private static bool IsPrecheckDuplicateQuery(string? sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return false;
            }

            if (!sql.Contains("practice_answers", StringComparison.OrdinalIgnoreCase) ||
                !sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Duplicate precheck query trong SubmitPracticeAnswerCommandHandler (sử dụng AnyAsync)
            bool isDuplicatePrecheck = sql.TrimStart().StartsWith("SELECT EXISTS", StringComparison.OrdinalIgnoreCase);
            bool isMainDuplicateCheck = isDuplicatePrecheck &&
                                        sql.Contains("is_follow_up", StringComparison.OrdinalIgnoreCase) &&
                                        sql.Contains("question_id", StringComparison.OrdinalIgnoreCase) &&
                                        !sql.Contains("parent_answer_id", StringComparison.OrdinalIgnoreCase);

            bool isFollowUpDuplicateCheck = isDuplicatePrecheck &&
                                            sql.Contains("parent_answer_id", StringComparison.OrdinalIgnoreCase);

            return isMainDuplicateCheck || isFollowUpDuplicateCheck;
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            if (IsPrecheckDuplicateQuery(command.CommandText))
            {
                Interlocked.Increment(ref _precheckParticipantCount);
                bool arrived = _barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                if (!arrived)
                {
                    throw new TimeoutException("Concurrency barrier timeout: Không đủ participant rendezvous tại pre-check query!");
                }
            }
            return base.ReaderExecuted(command, eventData, result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsPrecheckDuplicateQuery(command.CommandText))
            {
                Interlocked.Increment(ref _precheckParticipantCount);
                bool arrived = await Task.Run(() => _barrier.SignalAndWait(TimeSpan.FromSeconds(5)), cancellationToken);
                if (!arrived)
                {
                    throw new TimeoutException("Concurrency barrier timeout: Không đủ participant rendezvous tại pre-check query!");
                }
            }
            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            try
            {
                // A failed assertion may leave pending inserts in the tracked context. Cleanup reads committed rows afresh.
                _context.ChangeTracker.Clear();
                // 1. Dọn dẹp AiEvaluationDetails
                if (_createdAiEvaluationDetailIds.Count > 0)
                {
                    var details = await _context.AiEvaluationDetails
                        .Where(d => _createdAiEvaluationDetailIds.Contains(d.Id))
                        .ToListAsync();
                    if (details.Count > 0)
                    {
                        _context.AiEvaluationDetails.RemoveRange(details);
                        await _context.SaveChangesAsync();
                    }
                }

                // 2. Dọn dẹp AiEvaluations
                if (_createdAiEvaluationIds.Count > 0)
                {
                    var evals = await _context.AiEvaluations
                        .Where(e => _createdAiEvaluationIds.Contains(e.Id))
                        .ToListAsync();
                    if (evals.Count > 0)
                    {
                        _context.AiEvaluations.RemoveRange(evals);
                        await _context.SaveChangesAsync();
                    }
                }

                // 3. Dọn tất cả answers thuộc sessions test, kể cả answer đã commit trước khi assertion fail.
                if (_createdPracticeAnswerIds.Count > 0 || _createdPracticeSessionIds.Count > 0)
                {
                    var answers = await _context.PracticeAnswers
                        .Where(a => _createdPracticeAnswerIds.Contains(a.Id) || _createdPracticeSessionIds.Contains(a.SessionId))
                        .ToListAsync();
                    var followUps = answers.Where(a => a.IsFollowUp).ToList();
                    if (followUps.Count > 0)
                    {
                        _context.PracticeAnswers.RemoveRange(followUps);
                        await _context.SaveChangesAsync();
                    }
                    var mainAnswers = answers.Where(a => !a.IsFollowUp).ToList();
                    if (mainAnswers.Count > 0)
                    {
                        _context.PracticeAnswers.RemoveRange(mainAnswers);
                        await _context.SaveChangesAsync();
                    }
                }

                // 4. Dọn dẹp PracticeSessions
                if (_createdPracticeSessionIds.Count > 0)
                {
                    var sessions = await _context.PracticeSessions
                        .Where(s => _createdPracticeSessionIds.Contains(s.Id))
                        .ToListAsync();
                    if (sessions.Count > 0)
                    {
                        _context.PracticeSessions.RemoveRange(sessions);
                        await _context.SaveChangesAsync();
                    }
                }

                // 5. Dọn dẹp ExamQuestions
                if (_createdExamQuestionIds.Count > 0)
                {
                    var eqList = await _context.ExamQuestions
                        .Where(q => _createdExamQuestionIds.Contains(q.Id))
                        .ToListAsync();
                    if (eqList.Count > 0)
                    {
                        _context.ExamQuestions.RemoveRange(eqList);
                        await _context.SaveChangesAsync();
                    }
                }

                // 6. Dọn dẹp PracticeQuestions
                if (_createdPracticeQuestionIds.Count > 0)
                {
                    var pqList = await _context.PracticeQuestions
                        .Where(q => _createdPracticeQuestionIds.Contains(q.Id))
                        .ToListAsync();
                    if (pqList.Count > 0)
                    {
                        _context.PracticeQuestions.RemoveRange(pqList);
                        await _context.SaveChangesAsync();
                    }
                }

                // 7. Dọn dẹp RubricCriteria
                if (_createdRubricCriterionIds.Count > 0)
                {
                    var criteria = await _context.RubricCriteria
                        .Where(c => _createdRubricCriterionIds.Contains(c.Id))
                        .ToListAsync();
                    if (criteria.Count > 0)
                    {
                        _context.RubricCriteria.RemoveRange(criteria);
                        await _context.SaveChangesAsync();
                    }
                }

                // 8. Dọn dẹp Rubrics
                if (_createdRubricIds.Count > 0)
                {
                    var rubrics = await _context.Rubrics
                        .Where(r => _createdRubricIds.Contains(r.Id))
                        .ToListAsync();
                    if (rubrics.Count > 0)
                    {
                        _context.Rubrics.RemoveRange(rubrics);
                        await _context.SaveChangesAsync();
                    }
                }

                // 9. Dọn dẹp Courses
                if (_createdCourseIds.Count > 0)
                {
                    var courses = await _context.Courses
                        .Where(c => _createdCourseIds.Contains(c.Id))
                        .ToListAsync();
                    if (courses.Count > 0)
                    {
                        _context.Courses.RemoveRange(courses);
                        await _context.SaveChangesAsync();
                    }
                }

                // 10. Dọn dẹp Semesters
                if (_createdSemesterIds.Count > 0)
                {
                    var semesters = await _context.Semesters
                        .Where(s => _createdSemesterIds.Contains(s.Id))
                        .ToListAsync();
                    if (semesters.Count > 0)
                    {
                        _context.Semesters.RemoveRange(semesters);
                        await _context.SaveChangesAsync();
                    }
                }

                // 11. Dọn dẹp Users
                if (_createdUserIds.Count > 0)
                {
                    var users = await _context.Users
                        .Where(u => _createdUserIds.Contains(u.Id))
                        .ToListAsync();
                    if (users.Count > 0)
                    {
                        _context.Users.RemoveRange(users);
                        await _context.SaveChangesAsync();
                    }
                }

                // T8A-F009 / T8A-F014 Post-cleanup assertion: Xác minh qua một DbContext độc lập hoàn toàn rằng 0 marker rows còn sót lại trên toàn bộ 11 bảng hierarchy đã seed
                using var cleanupVerifyContext = CreatePostgresDbContext();

                var remainingDetails = await cleanupVerifyContext.AiEvaluationDetails
                    .CountAsync(d => _createdAiEvaluationDetailIds.Contains(d.Id));
                remainingDetails.Should().Be(0, "Post-cleanup assertion: Toàn bộ AiEvaluationDetails tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingEvals = await cleanupVerifyContext.AiEvaluations
                    .CountAsync(e => _createdAiEvaluationIds.Contains(e.Id));
                remainingEvals.Should().Be(0, "Post-cleanup assertion: Toàn bộ AiEvaluations tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingAnswers = await cleanupVerifyContext.PracticeAnswers
                    .CountAsync(a => _createdPracticeAnswerIds.Contains(a.Id) || _createdPracticeSessionIds.Contains(a.SessionId));
                remainingAnswers.Should().Be(0, "Post-cleanup assertion: Toàn bộ PracticeAnswer tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingSessions = await cleanupVerifyContext.PracticeSessions
                    .CountAsync(s => _createdPracticeSessionIds.Contains(s.Id));
                remainingSessions.Should().Be(0, "Post-cleanup assertion: Toàn bộ PracticeSession tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingExamQuestions = await cleanupVerifyContext.ExamQuestions
                    .CountAsync(q => _createdExamQuestionIds.Contains(q.Id));
                remainingExamQuestions.Should().Be(0, "Post-cleanup assertion: Toàn bộ ExamQuestions tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingPracticeQuestions = await cleanupVerifyContext.PracticeQuestions
                    .CountAsync(q => _createdPracticeQuestionIds.Contains(q.Id));
                remainingPracticeQuestions.Should().Be(0, "Post-cleanup assertion: Toàn bộ PracticeQuestions tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingCriteria = await cleanupVerifyContext.RubricCriteria
                    .CountAsync(c => _createdRubricCriterionIds.Contains(c.Id));
                remainingCriteria.Should().Be(0, "Post-cleanup assertion: Toàn bộ RubricCriteria tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingRubrics = await cleanupVerifyContext.Rubrics
                    .CountAsync(r => _createdRubricIds.Contains(r.Id));
                remainingRubrics.Should().Be(0, "Post-cleanup assertion: Toàn bộ Rubrics tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingCourses = await cleanupVerifyContext.Courses
                    .CountAsync(c => _createdCourseIds.Contains(c.Id));
                remainingCourses.Should().Be(0, "Post-cleanup assertion: Toàn bộ Courses tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingSemesters = await cleanupVerifyContext.Semesters
                    .CountAsync(s => _createdSemesterIds.Contains(s.Id));
                remainingSemesters.Should().Be(0, "Post-cleanup assertion: Toàn bộ Semesters tạo trong test phải được dọn dẹp sạch sẽ.");

                var remainingUsers = await cleanupVerifyContext.Users
                    .CountAsync(u => _createdUserIds.Contains(u.Id));
                remainingUsers.Should().Be(0, "Post-cleanup assertion: Toàn bộ User tạo trong test phải được dọn dẹp sạch sẽ.");
            }
            finally
            {
                await _context.DisposeAsync();
            }
        }
    }

    private async Task<(User StudentA, User StudentB, Course Course, PracticeQuestion Question)> SeedBasicHierarchyAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        var studentA = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student_a_{tag}@fpt.edu.vn",
            FullName = $"Student A {tag}",
            PasswordHash = "hashed_pass",
            Role = "student",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var studentB = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student_b_{tag}@fpt.edu.vn",
            FullName = $"Student B {tag}",
            PasswordHash = "hashed_pass",
            Role = "student",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _createdUserIds.Add(studentA.Id);
        _createdUserIds.Add(studentB.Id);
        _context!.Users.AddRange(studentA, studentB);

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = $"FA26_{tag}",
            Name = $"Fall 2026 {tag}",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdSemesterIds.Add(semester.Id);
        _context.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"SWP_{tag}",
            Name = $"Software Project {tag}",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdCourseIds.Add(course.Id);
        _context.Courses.Add(course);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = $"Rubric {tag}",
            TotalMaxScore = 10.0m
        };
        _createdRubricIds.Add(rubric.Id);
        _context.Rubrics.Add(rubric);

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức",
            MaxScore = 10.0m,
            Weight = 1.0m,
            OrderIndex = 1
        };
        _createdRubricCriterionIds.Add(criterion.Id);
        _context.RubricCriteria.Add(criterion);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = $"Câu hỏi kiến thức {tag}",
            Content = $"Nội dung câu hỏi kiến thức {tag}?",
            Difficulty = "medium",
            ApprovalStatus = "APPROVED",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdPracticeQuestionIds.Add(question.Id);
        _context.PracticeQuestions.Add(question);

        await _context.SaveChangesAsync();
        return (studentA, studentB, course, question);
    }

    [Fact(DisplayName = "1. BOLA Prevention: Student B bị từ chối với mã lỗi FORBIDDEN khi thao tác trên session của Student A")]
    public async Task Bola_Prevention_StudentB_Rejected_With_Forbidden()
    {
        var (studentA, studentB, course, question) = await SeedBasicHierarchyAsync();

        var sessionA = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(sessionA.Id);
        _context!.PracticeSessions.Add(sessionA);
        await _context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();

        // 1. NextQuestion: Student B gọi session của Student A
        var nextHandler = new NextQuestionCommandHandler(_context);
        var nextResult = await nextHandler.Handle(new NextQuestionCommand(sessionA.Id, studentB.Id), CancellationToken.None);
        nextResult.IsSuccess.Should().BeFalse();
        nextResult.Error.Should().StartWith("FORBIDDEN:");

        // 2. SubmitAnswer: Student B nộp bài vào session của Student A
        var submitHandler = new SubmitPracticeAnswerCommandHandler(_context, queueMock.Object);
        var submitResult = await submitHandler.Handle(
            new SubmitPracticeAnswerCommand(sessionA.Id, question.Id, studentB.Id, "Hacker answer", false, null),
            CancellationToken.None);
        submitResult.IsSuccess.Should().BeFalse();
        submitResult.Error.Should().StartWith("FORBIDDEN:");

        // 3. CompleteSession: Student B hoàn thành session của Student A
        var completeHandler = new CompletePracticeSessionCommandHandler(_context);
        var completeResult = await completeHandler.Handle(
            new CompletePracticeSessionCommand(sessionA.Id, studentB.Id),
            CancellationToken.None);
        completeResult.IsSuccess.Should().BeFalse();
        completeResult.Error.Should().StartWith("FORBIDDEN:");

        // 4. Non-existent session: phải trả NOT_FOUND
        var notFoundResult = await nextHandler.Handle(new NextQuestionCommand(Guid.NewGuid(), studentA.Id), CancellationToken.None);
        notFoundResult.IsSuccess.Should().BeFalse();
        notFoundResult.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "2. Question Bank Isolation: Chỉ cấp câu hỏi APPROVED của practice_questions, không lộ DRAFT hoặc exam_questions")]
    public async Task QuestionBank_Isolation_Only_Approved_Practice_Questions_Delivered()
    {
        var (studentA, _, course, approvedQuestion) = await SeedBasicHierarchyAsync();

        // Tạo câu hỏi DRAFT trong practice_questions
        var draftQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = approvedQuestion.RubricId,
            Title = "Câu hỏi nháp chưa duyệt",
            Content = "Câu hỏi nháp chưa duyệt",
            Difficulty = "medium",
            ApprovalStatus = "DRAFT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdPracticeQuestionIds.Add(draftQuestion.Id);
        _context!.PracticeQuestions.Add(draftQuestion);

        // Tạo câu hỏi trong exam_questions (kho thi thật)
        var examQuestion = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = approvedQuestion.RubricId,
            Title = "Câu hỏi thi thật tuyệt mật",
            Content = "Câu hỏi thi thật tuyệt mật",
            Difficulty = "medium",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdExamQuestionIds.Add(examQuestion.Id);
        _context.ExamQuestions.Add(examQuestion);

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(_context);
        var result = await handler.Handle(new NextQuestionCommand(session.Id, studentA.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Question.Should().NotBeNull();
        result.Value.Question!.Id.Should().Be(approvedQuestion.Id);
        result.Value.Question.Id.Should().NotBe(draftQuestion.Id);
        result.Value.Question.Id.Should().NotBe(examQuestion.Id);
    }

    [Fact(DisplayName = "3. Anti-3-Consecutive & Pool Exhaustion: Trả hasMoreQuestions = false khi cạn kho thay vì fallback mức cũ")]
    public async Task Anti3Consecutive_And_CandidatePool_Exhaustion()
    {
        var (studentA, _, course, approvedQuestion) = await SeedBasicHierarchyAsync();

        // Tạo 2 câu hỏi Dễ (easy) đã APPROVED
        var qEasy1 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = approvedQuestion.RubricId,
            Title = "Câu hỏi dễ 1",
            Content = "Câu hỏi dễ 1",
            Difficulty = "easy",
            ApprovalStatus = "APPROVED",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var qEasy2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = approvedQuestion.RubricId,
            Title = "Câu hỏi dễ 2",
            Content = "Câu hỏi dễ 2",
            Difficulty = "easy",
            ApprovalStatus = "APPROVED",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdPracticeQuestionIds.Add(qEasy1.Id);
        _createdPracticeQuestionIds.Add(qEasy2.Id);
        _context!.PracticeQuestions.AddRange(qEasy1, qEasy2);

        // Đảm bảo không còn câu hỏi medium nào khả dụng ngoài các câu đã seed
        var existingMedium = await _context.PracticeQuestions
            .Where(q => q.CourseId == course.Id && q.Difficulty == "medium")
            .ToListAsync();
        foreach (var m in existingMedium)
        {
            m.IsActive = false; // Vô hiệu hóa để làm cạn pool medium
        }

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "easy", "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context.PracticeSessions.Add(session);

        // Tạo 2 câu trả lời liền trước có cùng độ khó "easy"
        var ans1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = qEasy1.Id,
            StudentId = studentA.Id,
            OrderIndex = 1,
            AnswerText = "Trả lời dễ 1",
            IsFollowUp = false,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-4)
        };
        var ans2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = qEasy2.Id,
            StudentId = studentA.Id,
            OrderIndex = 2,
            AnswerText = "Trả lời dễ 2",
            IsFollowUp = false,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        _createdPracticeAnswerIds.Add(ans1.Id);
        _createdPracticeAnswerIds.Add(ans2.Id);
        _context.PracticeAnswers.AddRange(ans1, ans2);

        await _context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(_context);
        var result = await handler.Handle(new NextQuestionCommand(session.Id, studentA.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // 2 câu liền trước là easy -> loại trừ easy. Mức medium đã cạn sạch -> Tuân thủ Anti-3-Consecutive cấm cấp câu easy thứ 3, trả hasMoreQuestions = false
        result.Value.HasMoreQuestions.Should().BeFalse("Khi 2 câu liền trước cùng độ khó và các mức khác cạn sạch, cấm fallback về độ khó cũ.");
        result.Value.Question.Should().BeNull();
    }

    [Fact(DisplayName = "4. Follow-Up Boundary: Kiểm chứng đúng 4 biên điểm 3.99, 4.00, 8.00, 8.01 và duplicate reject")]
    public async Task FollowUp_Boundary_Only_Allowed_When_Score_Between_4_And_8()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);

        var mainAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            OrderIndex = 1,
            AnswerText = "Câu trả lời chính",
            IsFollowUp = false,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _createdPracticeAnswerIds.Add(mainAnswer.Id);
        _context.PracticeAnswers.Add(mainAnswer);

        var eval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = mainAnswer.Id,
            TotalScore = 3.99m,
            ConfidenceScore = 0.95m,
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-4)
        };
        _createdAiEvaluationIds.Add(eval.Id);
        _context.AiEvaluations.Add(eval);
        await _context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeAnswerCommandHandler(_context, queueMock.Object);

        // 1. Biên 3.99 (dưới 4.00) -> BỊ TỪ CHỐI
        var fail399 = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Follow-up 3.99", true, mainAnswer.Id),
            CancellationToken.None);
        fail399.IsSuccess.Should().BeFalse();
        fail399.Error.Should().Contain("[4.0, 8.0]");

        // 2. Biên 8.01 (trên 8.00) -> BỊ TỪ CHỐI
        eval.TotalScore = 8.01m;
        await _context.SaveChangesAsync();

        var fail801 = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Follow-up 8.01", true, mainAnswer.Id),
            CancellationToken.None);
        fail801.IsSuccess.Should().BeFalse();
        fail801.Error.Should().Contain("[4.0, 8.0]");

        // 3. Biên 4.00 (ngưỡng tối thiểu) -> CHO PHÉP
        eval.TotalScore = 4.00m;
        await _context.SaveChangesAsync();

        var success400 = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Follow-up 4.00 valid", true, mainAnswer.Id),
            CancellationToken.None);
        success400.IsSuccess.Should().BeTrue();
        _createdPracticeAnswerIds.Add(success400.Value);

        // 4. Thử nộp trùng follow-up cho cùng parent -> BỊ TỪ CHỐI CONFLICT (409)
        var dupResult = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Follow-up duplicate", true, mainAnswer.Id),
            CancellationToken.None);
        dupResult.IsSuccess.Should().BeFalse();
        dupResult.Error.Should().StartWith("CONFLICT:");

        // 5. Biên 8.00 (ngưỡng tối đa) -> Tạo main answer 2 để kiểm chứng
        var q2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = question.RubricId,
            Title = "Câu hỏi 2",
            Content = "Nội dung 2",
            Difficulty = "medium",
            ApprovalStatus = "APPROVED",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdPracticeQuestionIds.Add(q2.Id);
        _context.PracticeQuestions.Add(q2);

        var mainAnswer2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q2.Id,
            StudentId = studentA.Id,
            OrderIndex = 2,
            AnswerText = "Câu trả lời chính 2",
            IsFollowUp = false,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-3)
        };
        _createdPracticeAnswerIds.Add(mainAnswer2.Id);
        _context.PracticeAnswers.Add(mainAnswer2);

        var eval2 = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = mainAnswer2.Id,
            TotalScore = 8.00m,
            ConfidenceScore = 0.95m,
            EvaluatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        _createdAiEvaluationIds.Add(eval2.Id);
        _context.AiEvaluations.Add(eval2);
        await _context.SaveChangesAsync();

        var success800 = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, q2.Id, studentA.Id, "Follow-up 8.00 valid", true, mainAnswer2.Id),
            CancellationToken.None);
        success800.IsSuccess.Should().BeTrue("Biên 8.00 nằm trong [4.0, 8.0] cho phép hỏi tiếp.");
        _createdPracticeAnswerIds.Add(success800.Value);
    }

    [Fact(DisplayName = "5. Inactivity Timeout: Session không tương tác quá 5 phút bị tự động đóng và trả lỗi 410 Gone")]
    public async Task Inactivity_Timeout_AutoCloses_Session_And_Rejects_Actions()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        // Tạo session với LastActivityAt = 6 phút trước
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-6),
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        var nextHandler = new NextQuestionCommandHandler(_context);
        var nextResult = await nextHandler.Handle(new NextQuestionCommand(session.Id, studentA.Id), CancellationToken.None);
        nextResult.IsSuccess.Should().BeFalse();
        nextResult.Error.Should().StartWith("SESSION_TIMEOUT_410:");

        // Kiểm tra session trong DB đã được cập nhật completed
        var updatedSession = await _context.PracticeSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == session.Id);
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "6. Race Invariant: Nộp đồng thời Main Answer qua 2 connection độc lập có barrier - đúng 1 bài thành công, 1 gặp 23505 CONFLICT")]
    public async Task Concurrency_TwoIndependentConnections_HandledCleanly_And_Idempotent()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        // T8A-F014: Khởi tạo barrier chung cho 2 context để đảm bảo cả hai request đều vượt qua precheck trước khi insert
        var barrierInterceptor = new ConcurrencyPrecheckBarrierInterceptor(2);
        using var context1 = CreatePostgresDbContext(barrierInterceptor);
        using var context2 = CreatePostgresDbContext(barrierInterceptor);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler1 = new SubmitPracticeAnswerCommandHandler(context1, queueMock.Object);
        var handler2 = new SubmitPracticeAnswerCommandHandler(context2, queueMock.Object);

        var task1 = handler1.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Nộp đồng thời qua Connection 1", false, null),
            CancellationToken.None);
        var task2 = handler2.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Nộp đồng thời qua Connection 2", false, null),
            CancellationToken.None);

        await Task.WhenAll(task1, task2);

        // T8A-F017: Chứng minh cả 2 connection đều đã vượt qua precheck query trước khi insert
        barrierInterceptor.PrecheckParticipantCount.Should().Be(2, "Cả hai connection bắt buộc phải vượt qua precheck query đồng thời trước khi ghi dữ liệu.");

        var r1 = task1.Result;
        var r2 = task2.Result;

        // Bắt buộc đúng 1 request thành công, 1 request gặp CONFLICT
        (r1.IsSuccess ^ r2.IsSuccess).Should().BeTrue("Đúng một trong hai request đồng thời được ghi nhận thành công, request còn lại phải bị từ chối.");

        var successResult = r1.IsSuccess ? r1 : r2;
        var conflictResult = r1.IsSuccess ? r2 : r1;

        successResult.Value.Should().NotBeEmpty();
        _createdPracticeAnswerIds.Add(successResult.Value);

        conflictResult.IsSuccess.Should().BeFalse();
        conflictResult.Error.Should().StartWith("CONFLICT:", "Request thất bại do race condition trên PostgreSQL partial unique index phải trả mã CONFLICT.");

        // Kiểm tra qua connection độc lập thứ 3 trong DB: chỉ có ĐÚNG 1 row
        using var verifyContext = CreatePostgresDbContext();
        var answerCount = await verifyContext.PracticeAnswers
            .CountAsync(a => a.SessionId == session.Id && a.QuestionId == question.Id && !a.IsFollowUp);
        answerCount.Should().Be(1, "PostgreSQL partial unique constraint uq_practice_answers_session_question_main bảo đảm chỉ có đúng 1 câu trả lời chính.");

        // Queue chấm điểm chỉ enqueue đúng 1 lần
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Once,
            "Chỉ enqueue chấm bài đúng 1 lần duy nhất cho câu trả lời thành công.");
    }

    [Fact(DisplayName = "6B. T8A-F009 / T8A-F014: Physical PostgreSQL Unique Invariant 23505 trên Main Answer (uq_practice_answers_session_question_main)")]
    public async Task PhysicalPostgreSql_UniqueViolation23505_MainAnswer()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        using var context1 = CreatePostgresDbContext();
        using var context2 = CreatePostgresDbContext();

        var ans1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Ans 1 from Context 1",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(ans1.Id);
        context1.PracticeAnswers.Add(ans1);
        await context1.SaveChangesAsync();

        var ans2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Ans 2 from Context 2 (Duplicate Main)",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(ans2.Id);
        context2.PracticeAnswers.Add(ans2);

        var act = async () => await context2.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>("PostgreSQL phải từ chối bản ghi trùng lặp trên unique index.");
        var pgEx = ex.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>(
            "PostgreSQL phải ném PostgresException khi vi phạm ràng buộc unique.").Subject;
        pgEx.SqlState.Should().Be("23505", "PostgreSQL error code cho unique_violation là 23505.");
        pgEx.ConstraintName.Should().Be("uq_practice_answers_session_question_main", "Constraint name phải là uq_practice_answers_session_question_main.");
    }

    [Fact(DisplayName = "6C. T8A-F009 / T8A-F014: Physical PostgreSQL Unique Invariant 23505 trên Follow-Up Answer (uq_practice_answers_session_parent)")]
    public async Task PhysicalPostgreSql_UniqueViolation23505_FollowUpAnswer()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);

        var mainAns = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Main Answer",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "graded"
        };
        _createdPracticeAnswerIds.Add(mainAns.Id);
        _context.PracticeAnswers.Add(mainAns);
        await _context.SaveChangesAsync();

        using var context1 = CreatePostgresDbContext();
        using var context2 = CreatePostgresDbContext();

        var fu1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "FollowUp 1",
            IsFollowUp = true,
            ParentAnswerId = mainAns.Id,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(fu1.Id);
        context1.PracticeAnswers.Add(fu1);
        await context1.SaveChangesAsync();

        var fu2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "FollowUp 2 (Duplicate Parent)",
            IsFollowUp = true,
            ParentAnswerId = mainAns.Id,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(fu2.Id);
        context2.PracticeAnswers.Add(fu2);

        var act = async () => await context2.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>("PostgreSQL phải từ chối bản ghi trùng lặp trên unique index.");
        var pgEx = ex.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>(
            "PostgreSQL phải ném PostgresException khi vi phạm ràng buộc unique.").Subject;
        pgEx.SqlState.Should().Be("23505", "PostgreSQL error code cho unique_violation là 23505.");
        pgEx.ConstraintName.Should().Be("uq_practice_answers_session_parent", "Constraint name phải là uq_practice_answers_session_parent.");
    }

    [Fact(DisplayName = "6D. T8A-F007 / T8A-F014: Check Constraint ck_practice_answers_follow_up_parent từ chối tổ hợp main+parent hoặc follow-up thiếu parent với SqlState 23514")]
    public async Task PhysicalPostgreSql_CheckConstraint_RejectsInconsistentFollowUpParent()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);

        // Tạo một parent answer hợp lệ trong DB để kiểm chứng vi phạm Check Constraint 23514 mà KHÔNG bị lỗi Foreign Key 23503
        var validParent = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Valid parent answer for FK validity",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "graded"
        };
        _createdPracticeAnswerIds.Add(validParent.Id);
        _context.PracticeAnswers.Add(validParent);
        await _context.SaveChangesAsync();

        using var testContext = CreatePostgresDbContext();

        // 1. IsFollowUp = false nhưng ParentAnswerId != null (sử dụng validParent.Id để vượt qua FK)
        var invalidMain = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Invalid Main with ParentId",
            IsFollowUp = false,
            ParentAnswerId = validParent.Id,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(invalidMain.Id);
        testContext.PracticeAnswers.Add(invalidMain);

        var act1 = async () => await testContext.SaveChangesAsync();
        var ex1 = await act1.Should().ThrowAsync<DbUpdateException>("PostgreSQL phải từ chối is_follow_up = false có parent_answer_id qua ck_practice_answers_follow_up_parent.");
        var pgEx1 = ex1.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>(
            "PostgreSQL phải ném PostgresException khi vi phạm check constraint.").Subject;
        pgEx1.SqlState.Should().Be("23514", "PostgreSQL error code cho check_violation là 23514.");
        pgEx1.ConstraintName.Should().Be("ck_practice_answers_follow_up_parent");

        // Clear tracker
        testContext.ChangeTracker.Clear();

        // 2. IsFollowUp = true nhưng ParentAnswerId == null
        var invalidFollowUp = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Invalid FollowUp without ParentId",
            IsFollowUp = true,
            ParentAnswerId = null,
            Status = "pending"
        };
        _createdPracticeAnswerIds.Add(invalidFollowUp.Id);
        testContext.PracticeAnswers.Add(invalidFollowUp);

        var act2 = async () => await testContext.SaveChangesAsync();
        var ex2 = await act2.Should().ThrowAsync<DbUpdateException>("PostgreSQL phải từ chối is_follow_up = true không có parent_answer_id qua ck_practice_answers_follow_up_parent.");
        var pgEx2 = ex2.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>(
            "PostgreSQL phải ném PostgresException khi vi phạm check constraint.").Subject;
        pgEx2.SqlState.Should().Be("23514", "PostgreSQL error code cho check_violation là 23514.");
        pgEx2.ConstraintName.Should().Be("ck_practice_answers_follow_up_parent");
    }

    [Fact(DisplayName = "6E. T8A-F014: Race Invariant: Nộp đồng thời Follow-Up Answer qua 2 connection độc lập có barrier - đúng 1 bài thành công, 1 gặp 23505 CONFLICT")]
    public async Task SubmitPracticeAnswer_ConcurrentRace_FollowUpWithBarrier_ExactlyOneSucceeds_OneReturnsConflict()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);

        var mainAns = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            AnswerText = "Main Answer for Follow-up race",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "graded"
        };
        _createdPracticeAnswerIds.Add(mainAns.Id);
        _context.PracticeAnswers.Add(mainAns);

        var mainEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "practice",
            PracticeAnswerId = mainAns.Id,
            TotalScore = 5.0m, // Thuộc [4.0, 8.0] cho phép follow-up
            ConfidenceScore = 0.95m,
            EvaluatedAt = DateTime.UtcNow
        };
        _createdAiEvaluationIds.Add(mainEval.Id);
        _context.AiEvaluations.Add(mainEval);
        await _context.SaveChangesAsync();

        var barrierInterceptor = new ConcurrencyPrecheckBarrierInterceptor(2);
        using var context1 = CreatePostgresDbContext(barrierInterceptor);
        using var context2 = CreatePostgresDbContext(barrierInterceptor);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler1 = new SubmitPracticeAnswerCommandHandler(context1, queueMock.Object);
        var handler2 = new SubmitPracticeAnswerCommandHandler(context2, queueMock.Object);

        var task1 = handler1.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Nộp follow-up đồng thời qua Connection 1", true, mainAns.Id),
            CancellationToken.None);
        var task2 = handler2.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Nộp follow-up đồng thời qua Connection 2", true, mainAns.Id),
            CancellationToken.None);

        await Task.WhenAll(task1, task2);

        // T8A-F017: Chứng minh cả 2 connection đều đã vượt qua precheck query trước khi insert
        barrierInterceptor.PrecheckParticipantCount.Should().Be(2, "Cả hai connection bắt buộc phải vượt qua precheck query đồng thời trước khi ghi dữ liệu.");

        var r1 = task1.Result;
        var r2 = task2.Result;

        (r1.IsSuccess ^ r2.IsSuccess).Should().BeTrue("Đúng một trong hai request follow-up đồng thời được ghi nhận thành công, request còn lại phải bị từ chối.");

        var successResult = r1.IsSuccess ? r1 : r2;
        var conflictResult = r1.IsSuccess ? r2 : r1;

        successResult.Value.Should().NotBeEmpty();
        _createdPracticeAnswerIds.Add(successResult.Value);

        conflictResult.IsSuccess.Should().BeFalse();
        conflictResult.Error.Should().StartWith("CONFLICT:", "Request follow-up thất bại do race condition trên PostgreSQL partial unique index phải trả mã CONFLICT.");

        using var verifyContext = CreatePostgresDbContext();
        var answerCount = await verifyContext.PracticeAnswers
            .CountAsync(a => a.SessionId == session.Id && a.ParentAnswerId == mainAns.Id && a.IsFollowUp);
        answerCount.Should().Be(1, "PostgreSQL partial unique constraint uq_practice_answers_session_parent bảo đảm chỉ có đúng 1 câu trả lời phụ.");

        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Once,
            "Chỉ enqueue chấm bài đúng 1 lần duy nhất cho câu trả lời phụ thành công.");
    }

    [Fact(DisplayName = "7. Zero-R2: Structural & Architecture Assertion chứng minh MF-01 không phụ thuộc và không lưu trữ trên R2")]
    public async Task Zero_R2_Audit_PracticeAnswers_Do_Not_Use_R2_Storage()
    {
        // 1. Structural Architecture Assertion: Domain Entity PracticeAnswer tuyệt đối không có thuộc tính AudioUrl hay AudioKey
        typeof(PracticeAnswer).GetProperty("AudioUrl").Should().BeNull("Domain Entity PracticeAnswer không được chứa thuộc tính AudioUrl.");
        typeof(PracticeAnswer).GetProperty("AudioFileKey").Should().BeNull("Domain Entity PracticeAnswer không được chứa thuộc tính AudioFileKey.");

        // 2. Dependency Audit: SubmitPracticeAnswerCommandHandler không inject bất kỳ storage service nào
        var ctorParams = typeof(SubmitPracticeAnswerCommandHandler).GetConstructors()[0].GetParameters();
        ctorParams.Should().NotContain(p => p.ParameterType.Name.Contains("Storage", StringComparison.OrdinalIgnoreCase),
            "SubmitPracticeAnswerCommandHandler tuyệt đối không được phụ thuộc vào Storage service.");

        // 3. Execution Data Assertion: Lưu câu trả lời vào PostgreSQL thành công mà không có trường liên kết R2
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeAnswerCommandHandler(_context, queueMock.Object);

        var result = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, question.Id, studentA.Id, "Zero-R2 verification answer text", false, null),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        _createdPracticeAnswerIds.Add(result.Value);

        var savedAnswer = await _context.PracticeAnswers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == result.Value);
        savedAnswer.Should().NotBeNull();
        savedAnswer!.AnswerText.Should().Be("Zero-R2 verification answer text");
        savedAnswer.PromptText.Should().BeNull();
    }

    [Fact(DisplayName = "8. Submit Question Approval Validation: Câu hỏi DRAFT hoặc REJECTED bị từ chối kể cả khi session có selected list rỗng")]
    public async Task SubmitAnswer_NonApprovedQuestions_Should_Be_Rejected()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        // Tạo câu hỏi DRAFT và REJECTED
        var draftQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = question.RubricId,
            Title = "Câu hỏi Draft",
            Content = "Nội dung Draft",
            Difficulty = "medium",
            ApprovalStatus = "DRAFT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var rejectedQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = question.RubricId,
            Title = "Câu hỏi Rejected",
            Content = "Nội dung Rejected",
            Difficulty = "medium",
            ApprovalStatus = "REJECTED",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _createdPracticeQuestionIds.Add(draftQuestion.Id);
        _createdPracticeQuestionIds.Add(rejectedQuestion.Id);
        _context!.PracticeQuestions.AddRange(draftQuestion, rejectedQuestion);

        // Session legacy có SelectedQuestionIds rỗng
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedQuestionIds = new List<Guid>() // Rỗng
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeAnswerCommandHandler(_context, queueMock.Object);

        // 1. Nộp câu hỏi DRAFT -> Bị từ chối
        var draftResult = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, draftQuestion.Id, studentA.Id, "Trả lời câu draft", false, null),
            CancellationToken.None);
        draftResult.IsSuccess.Should().BeFalse();
        draftResult.Error.Should().Contain("chưa được phê duyệt");

        // 2. Nộp câu hỏi REJECTED -> Bị từ chối
        var rejectedResult = await handler.Handle(
            new SubmitPracticeAnswerCommand(session.Id, rejectedQuestion.Id, studentA.Id, "Trả lời câu rejected", false, null),
            CancellationToken.None);
        rejectedResult.IsSuccess.Should().BeFalse();
        rejectedResult.Error.Should().Contain("chưa được phê duyệt");
    }

    [Fact(DisplayName = "9. T8A-F012 / T8A-F013 / T8A-F016: Preflight phát hiện dữ liệu vi phạm check hoặc trùng lặp sẽ ném FAIL CLOSED")]
    public async Task Preflight_DetectsInconsistentOrDuplicateData_ThrowsFailClosed()
    {
        var (studentA, _, course, question) = await SeedBasicHierarchyAsync();

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = studentA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "medium" }
        };
        _createdPracticeSessionIds.Add(session.Id);
        _context!.PracticeSessions.Add(session);

        // T8A-F016: Tạo parent answer hợp lệ trước để tránh lỗi FK 23503 khi chèn row bẩn
        var validParent = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = studentA.Id,
            OrderIndex = 1,
            AnswerText = "Valid Parent Answer for FK",
            IsFollowUp = false,
            ParentAnswerId = null,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow
        };
        _createdPracticeAnswerIds.Add(validParent.Id);
        _context.PracticeAnswers.Add(validParent);
        await _context.SaveChangesAsync();

        var badAnswerId = Guid.NewGuid();
        _createdPracticeAnswerIds.Add(badAnswerId);

        using var rawContext = CreatePostgresDbContext();

        try
        {
            // Drop constraint tạm thời để chèn dữ liệu legacy bẩn
            await rawContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;");

            // Chèn dòng vi phạm: is_follow_up = false nhưng parent_answer_id trỏ tới validParent.Id (không vướng lỗi 23503 FK)
            await rawContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO practice_answers (id, session_id, question_id, student_id, order_index, answer_text, is_follow_up, parent_answer_id, status, submitted_at) " +
                "VALUES ({0}, {1}, {2}, {3}, 2, 'Corrupted Legacy Answer', false, {4}, 'pending', NOW());",
                badAnswerId, session.Id, question.Id, studentA.Id, validParent.Id);

            // Gọi Preflight: BẮT BUỘC phải ném InvalidOperationException chứa [FAIL CLOSED]
            var act = async () => await PostgresSchemaReconciler.ValidatePracticeAnswersUniquePreflightAsync(rawContext);
            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should().Contain("[FAIL CLOSED]", "Preflight phải ném exception có prefix [FAIL CLOSED] để dừng startup API.");
            ex.Which.Message.Should().Contain("ck_practice_answers_follow_up_parent", "Thông báo lỗi phải chỉ rõ vi phạm ràng buộc cha-con.");
        }
        finally
        {
            // T8A-F017: Dọn dẹp dòng bẩn và khôi phục constraint trong finally để đảm bảo an toàn tuyệt đối
            await rawContext.Database.ExecuteSqlRawAsync("DELETE FROM practice_answers WHERE id = {0};", badAnswerId);
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK ((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL));
                """);
        }

        // T8A-F017: Dùng context độc lập xác nhận schema và data đã hoàn toàn sạch
        using var verifyCleanContext = CreatePostgresDbContext();
        var verifyAct = async () => await PostgresSchemaReconciler.ValidatePracticeAnswersUniquePreflightAsync(verifyCleanContext);
        await verifyAct.Should().NotThrowAsync("Sau khi dọn dẹp và khôi phục trong finally, preflight trên context độc lập phải pass 100%.");
    }

    [Fact(DisplayName = "10. T8A-F012 / T8A-F013 / T8A-F017: Probe phát hiện thiếu unique index sẽ ném FAIL CLOSED")]
    public async Task StartupProbe_MissingOrCorruptedSchema_ThrowsFailClosed()
    {
        using var rawContext = CreatePostgresDbContext();

        try
        {
            // Giả lập drop một trong hai unique index
            await rawContext.Database.ExecuteSqlRawAsync(
                "DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");

            var act = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should().Contain("[FAIL CLOSED]", "Startup Verification Probe phải ném exception [FAIL CLOSED] khi thiếu index bắt buộc.");
        }
        finally
        {
            // T8A-F017: Khôi phục lại index trong finally
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id)
                WHERE is_follow_up = false AND parent_answer_id IS NULL;
                """);
        }

        // T8A-F017: Dùng context độc lập xác nhận schema đã sạch và probe pass
        using var verifyCleanContext = CreatePostgresDbContext();
        var verifyAct = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(verifyCleanContext);
        await verifyAct.Should().NotThrowAsync("Sau khi khôi phục trong finally, probe trên context độc lập phải pass.");
    }

    [Fact(DisplayName = "10B. T8A-F015 / T8A-F018: Probe phát hiện schema SAI NGHĨA hoặc nới lỏng bằng OR TRUE / mệnh đề dư sẽ FAIL CLOSED")]
    public async Task StartupProbe_SemanticallyInvalidSchemaWithSufficientTokens_ThrowsFailClosed()
    {
        using var rawContext = CreatePostgresDbContext();

        try
        {
            // Case 1 (T8A-F018): Check constraint chứa đầy đủ contract nhưng bị nới lỏng bằng OR TRUE
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK (((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL)) OR TRUE);
                """);

            var actOrTrue = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exOrTrue = await actOrTrue.Should().ThrowAsync<InvalidOperationException>();
            exOrTrue.Which.Message.Should().Contain("[FAIL CLOSED]", "Check constraint bị nới lỏng bằng OR TRUE phải bị probe từ chối [FAIL CLOSED].");
            exOrTrue.Which.Message.Should().Contain("phải có đúng 2 nhánh nối bằng OR");

            // Case 2 (T8A-F018): Check constraint chứa mệnh đề dư thừa trong nhánh (AND TRUE)
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK (((is_follow_up = false AND parent_answer_id IS NULL AND TRUE) OR (is_follow_up = true AND parent_answer_id IS NOT NULL)));
                """);

            var actAndTrue = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exAndTrue = await actAndTrue.Should().ThrowAsync<InvalidOperationException>();
            exAndTrue.Which.Message.Should().Contain("[FAIL CLOSED]");
            exAndTrue.Which.Message.Should().Contain("phải có đúng 2 điều kiện nối bằng AND");

            // Case 3: Check constraint sai semantic (dùng OR thay vì 2 nhánh AND riêng biệt) dù vẫn chứa 'is_follow_up' và 'parent_answer_id'
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK (is_follow_up OR parent_answer_id IS NULL);
                """);

            var actCheck = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exCheck = await actCheck.Should().ThrowAsync<InvalidOperationException>();
            exCheck.Which.Message.Should().Contain("[FAIL CLOSED]");

            // Khôi phục check constraint đúng để kiểm tra tiếp index
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK ((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL));
                """);

            // Case 4 (T8A-F018): Index main predicate bị nới lỏng bằng OR TRUE dù chứa đầy đủ contract
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id)
                WHERE (is_follow_up = false AND parent_answer_id IS NULL) OR TRUE;
                """);

            var actMainOrTrue = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exMainOrTrue = await actMainOrTrue.Should().ThrowAsync<InvalidOperationException>();
            exMainOrTrue.Which.Message.Should().Contain("[FAIL CLOSED]");
            exMainOrTrue.Which.Message.Should().Contain("không được chứa toán tử OR");

            // Case 5 (T8A-F018): Index main predicate có mệnh đề dư thừa (AND TRUE)
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id)
                WHERE is_follow_up = false AND parent_answer_id IS NULL AND TRUE;
                """);

            var actMainAndTrue = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exMainAndTrue = await actMainAndTrue.Should().ThrowAsync<InvalidOperationException>();
            exMainAndTrue.Which.Message.Should().Contain("[FAIL CLOSED]");
            exMainAndTrue.Which.Message.Should().Contain("phải có đúng 2 điều kiện nối bằng AND");

            // Case 6: Index main có 3 cột (session_id, question_id, student_id) thay vì đúng 2 cột
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id, student_id)
                WHERE is_follow_up = false AND parent_answer_id IS NULL;
                """);

            var actMainIdx = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exMainIdx = await actMainIdx.Should().ThrowAsync<InvalidOperationException>();
            exMainIdx.Which.Message.Should().Contain("[FAIL CLOSED]");
            exMainIdx.Which.Message.Should().Contain("phải có đúng 2 key columns");

            // Khôi phục index main đúng
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_question_main;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id)
                WHERE is_follow_up = false AND parent_answer_id IS NULL;
                """);

            // Case 7 (T8A-F018): Index follow-up predicate bị nới lỏng bằng OR TRUE
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_parent;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_parent
                ON practice_answers (session_id, parent_answer_id)
                WHERE (is_follow_up = true AND parent_answer_id IS NOT NULL) OR TRUE;
                """);

            var actFuOrTrue = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exFuOrTrue = await actFuOrTrue.Should().ThrowAsync<InvalidOperationException>();
            exFuOrTrue.Which.Message.Should().Contain("[FAIL CLOSED]");
            exFuOrTrue.Which.Message.Should().Contain("không được chứa toán tử OR");

            // Case 8: Index follow-up có predicate đảo ngược (is_follow_up = false) dù vẫn chứa token 'is_follow_up'
            await rawContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS uq_practice_answers_session_parent;");
            await rawContext.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX uq_practice_answers_session_parent
                ON practice_answers (session_id, parent_answer_id)
                WHERE is_follow_up = false AND parent_answer_id IS NOT NULL;
                """);

            var actFuIdx = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(rawContext);
            var exFuIdx = await actFuIdx.Should().ThrowAsync<InvalidOperationException>();
            exFuIdx.Which.Message.Should().Contain("[FAIL CLOSED]");
            exFuIdx.Which.Message.Should().Contain("sai predicate filter");
        }
        finally
        {
            // Luôn luôn khôi phục schema về chuẩn trong finally ngay cả khi test fail
            await rawContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE practice_answers DROP CONSTRAINT IF EXISTS ck_practice_answers_follow_up_parent;
                ALTER TABLE practice_answers ADD CONSTRAINT ck_practice_answers_follow_up_parent
                    CHECK ((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL));

                DROP INDEX IF EXISTS uq_practice_answers_session_question_main;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_practice_answers_session_question_main
                ON practice_answers (session_id, question_id)
                WHERE is_follow_up = false AND parent_answer_id IS NULL;

                DROP INDEX IF EXISTS uq_practice_answers_session_parent;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_practice_answers_session_parent
                ON practice_answers (session_id, parent_answer_id)
                WHERE is_follow_up = true AND parent_answer_id IS NOT NULL;
                """);
        }

        // T8A-F017 / T8A-F018: Dùng context độc lập xác nhận schema đã hoàn toàn sạch và probe pass 100%
        using var verifyCleanContext = CreatePostgresDbContext();
        var verifyAct = async () => await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(verifyCleanContext);
        await verifyAct.Should().NotThrowAsync("Sau khi dọn dẹp và khôi phục trong finally, probe trên context độc lập phải pass 100%.");
    }
}
