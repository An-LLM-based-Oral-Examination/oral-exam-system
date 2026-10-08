using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Domain;

public class DomainEntitiesAndDbContextTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. OralExamDbContext khởi tạo thành công và chứa đủ 29 thực thể trong Model")]
    public void DbContext_Must_Contain_Exactly_28_Entity_Types()
    {
        using var context = CreateDbContext();
        var entityTypes = context.Model.GetEntityTypes().ToList();

        entityTypes.Should().HaveCount(29, "Hệ thống chuẩn hóa 29 bảng CSDL phân tách Practice - Exam kèm Phúc khảo nội bộ và Cấu hình hệ thống");

        var expectedEntities = new[]
        {
            typeof(User),
            typeof(Semester),
            typeof(Course),
            typeof(Class),
            typeof(ClassEnrollment),
            typeof(Rubric),
            typeof(RubricCriterion),
            typeof(PracticeQuestion),
            typeof(ExamQuestion),
            typeof(PracticeSession),
            typeof(PracticeAnswer),
            typeof(AiEvaluation),
            typeof(AiEvaluationDetail),
            typeof(ExamStructure),
            typeof(ExamSet),
            typeof(ExamSetQuestion),
            typeof(MockExamQuota),
            typeof(MockExamSession),
            typeof(MockExamAnswer),
            typeof(OfficialExamSession),
            typeof(RealExamSessionShift),
            typeof(StudentExamTicket),
            typeof(ExamQuestionSubmission),
            typeof(LecturerAudit),
            typeof(LecturerAuditDetail),
            typeof(AppealRequest),
            typeof(DeadLetterQueue),
            typeof(AuditLog),
            typeof(SystemConfig)
        };

        foreach (var type in expectedEntities)
        {
            var entity = context.Model.FindEntityType(type);
            entity.Should().NotBeNull($"Thực thể {type.Name} bắt buộc phải được đăng ký trong Model");
        }
    }

    [Fact(DisplayName = "2. 29 Entities ánh xạ chính xác 100% tên bảng PostgreSQL theo chuẩn snake_case")]
    public void Entities_Must_Map_To_Correct_Database_Tables()
    {
        using var context = CreateDbContext();

        var tableMap = new (Type EntityType, string ExpectedTable)[]
        {
            (typeof(User), "users"),
            (typeof(Semester), "semesters"),
            (typeof(Course), "courses"),
            (typeof(Class), "classes"),
            (typeof(ClassEnrollment), "class_enrollments"),
            (typeof(Rubric), "rubrics"),
            (typeof(RubricCriterion), "rubric_criteria"),
            (typeof(PracticeQuestion), "practice_questions"),
            (typeof(ExamQuestion), "exam_questions"),
            (typeof(PracticeSession), "practice_sessions"),
            (typeof(PracticeAnswer), "practice_answers"),
            (typeof(AiEvaluation), "ai_evaluations"),
            (typeof(AiEvaluationDetail), "ai_evaluation_details"),
            (typeof(ExamStructure), "exam_structures"),
            (typeof(ExamSet), "exam_sets"),
            (typeof(ExamSetQuestion), "exam_set_questions"),
            (typeof(MockExamQuota), "mock_exam_quotas"),
            (typeof(MockExamSession), "mock_exam_sessions"),
            (typeof(MockExamAnswer), "mock_exam_answers"),
            (typeof(OfficialExamSession), "official_exam_sessions"),
            (typeof(RealExamSessionShift), "real_exam_session_shifts"),
            (typeof(StudentExamTicket), "student_exam_tickets"),
            (typeof(ExamQuestionSubmission), "exam_question_submissions"),
            (typeof(LecturerAudit), "lecturer_audits"),
            (typeof(LecturerAuditDetail), "lecturer_audit_details"),
            (typeof(AppealRequest), "appeal_requests"),
            (typeof(DeadLetterQueue), "dead_letter_queues"),
            (typeof(AuditLog), "audit_logs"),
            (typeof(SystemConfig), "system_configs")
        };

        foreach (var (entityType, expectedTable) in tableMap)
        {
            var efEntity = context.Model.FindEntityType(entityType);
            efEntity.Should().NotBeNull();
            efEntity!.GetTableName().Should().Be(expectedTable, $"Entity {entityType.Name} phải ánh xạ bảng {expectedTable}");
        }
    }

    [Fact(DisplayName = "3. Ràng buộc bất biến Composite Unique Indexes trên các thực thể cốt lõi")]
    public void Core_Entities_Must_Configure_Composite_Unique_Indexes()
    {
        using var context = CreateDbContext();

        // 1. class_enrollments (class_id, student_id)
        var enrollmentType = context.Model.FindEntityType(typeof(ClassEnrollment))!;
        var enrollmentUnique = enrollmentType.GetIndexes().FirstOrDefault(i => i.IsUnique && i.Properties.Count == 2);
        enrollmentUnique.Should().NotBeNull("Mỗi sinh viên chỉ được ghi danh 1 lần vào mỗi lớp");

        // 2. exam_set_questions (exam_set_id, exam_question_id)
        var esqType = context.Model.FindEntityType(typeof(ExamSetQuestion))!;
        var esqUnique = esqType.GetIndexes().FirstOrDefault(i => i.IsUnique && i.Properties.Count == 2);
        esqUnique.Should().NotBeNull("Một câu hỏi không được trùng lặp trong cùng một bộ đề");

        // 3. mock_exam_quotas (student_id, course_id, quota_date)
        var quotaType = context.Model.FindEntityType(typeof(MockExamQuota))!;
        var quotaUnique = quotaType.GetIndexes().FirstOrDefault(i => i.IsUnique && i.Properties.Count == 3);
        quotaUnique.Should().NotBeNull("Mỗi sinh viên chỉ có 1 hạn ngạch mỗi môn theo ngày");

        // 4. student_exam_tickets (shift_id, seat_number) và (shift_id, student_id)
        var ticketType = context.Model.FindEntityType(typeof(StudentExamTicket))!;
        var ticketSeatUnique = ticketType.GetIndexes().FirstOrDefault(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(StudentExamTicket.SeatNumber)));
        ticketSeatUnique.Should().NotBeNull("Mỗi số máy tại kíp thi chỉ cấp cho duy nhất 1 thí sinh");

        // 5. exam_question_submissions (ticket_id, exam_question_id)
        var submissionType = context.Model.FindEntityType(typeof(ExamQuestionSubmission))!;
        var subUnique = submissionType.GetIndexes().FirstOrDefault(i => i.IsUnique && i.Properties.Count == 2);
        subUnique.Should().NotBeNull("Mỗi vé thi chỉ có 1 bản nộp duy nhất cho mỗi câu hỏi");
    }

    [Fact(DisplayName = "4. Ràng buộc Check Constraints trong 01_schema.sql cho Rubric 10.0 và Quota thi thử động UsedCount >= 0")]
    public void Schema_Must_Configure_Domain_Check_Constraints()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? schemaPath = null;

        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "infra", "postgres", "init", "01_schema.sql");
            if (File.Exists(candidate))
            {
                schemaPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        schemaPath.Should().NotBeNull("File 01_schema.sql phải được tìm thấy từ thư mục giải pháp");
        File.Exists(schemaPath).Should().BeTrue();

        var sql = File.ReadAllText(schemaPath!);

        // 1. Rubric TotalMaxScore = 10.0
        sql.Should().Contain("CONSTRAINT ck_rubrics_total_score CHECK (total_max_score = 10.00)");

        // 2. RubricCriterion MaxScore > 0 AND <= 10.0
        sql.Should().Contain("CONSTRAINT ck_rubric_criteria_score CHECK (max_score > 0 AND max_score <= 10.00)");

        // 3. MockExamQuota UsedCount >= 0 (Hạn ngạch động theo môn)
        sql.Should().Contain("CONSTRAINT ck_mock_exam_quotas_count CHECK (used_count >= 0)");

        // 4. StudentExamTicket Seat 1..40
        sql.Should().Contain("CONSTRAINT ck_tickets_seat CHECK (seat_number >= 1 AND seat_number <= 40)");

        // 5. DeadLetterQueue status check
        sql.Should().Contain("CONSTRAINT ck_dlq_status CHECK (status IN ('pending', 'resolved', 'abandoned'))");
    }

    [Fact(DisplayName = "5. Thực thể có thể lưu trữ và truy vấn in-memory mà không xung đột")]
    public void Entities_Can_Be_Added_And_Persisted_In_Memory()
    {
        using var context = CreateDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "reviewer_test@fpt.edu.vn",
            FullName = "Reviewer Test User",
            Role = "student",
            IsActive = true
        };

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = true,
            IsActive = true
        };

        context.Users.Add(user);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.SaveChanges();

        var retrievedCourse = context.Courses.FirstOrDefault(c => c.Code == "PRN231");
        retrievedCourse.Should().NotBeNull();
        retrievedCourse!.HasFollowUp.Should().BeTrue();
    }

    [Fact(DisplayName = "6. Khóa ngoại và Quan hệ điều hướng giữa các thực thể cốt lõi hợp lệ")]
    public void ForeignKeys_And_Navigations_Must_Be_Valid()
    {
        using var context = CreateDbContext();

        // Course -> Rubrics
        var courseType = context.Model.FindEntityType(typeof(Course))!;
        var rubricNav = courseType.FindNavigation(nameof(Course.Rubrics));
        rubricNav.Should().NotBeNull();
        rubricNav!.IsCollection.Should().BeTrue();

        // PracticeAnswer -> PracticeSession & PracticeQuestion
        var answerType = context.Model.FindEntityType(typeof(PracticeAnswer))!;
        var sessionNav = answerType.FindNavigation(nameof(PracticeAnswer.Session));
        sessionNav.Should().NotBeNull();

        // OfficialExamSession -> RealExamSessionShifts (Shifts)
        var officialSessionType = context.Model.FindEntityType(typeof(OfficialExamSession))!;
        var shiftsNav = officialSessionType.FindNavigation(nameof(OfficialExamSession.Shifts));
        shiftsNav.Should().NotBeNull();
        shiftsNav!.IsCollection.Should().BeTrue();

        // StudentExamTicket -> ExamQuestionSubmissions (Submissions)
        var ticketType = context.Model.FindEntityType(typeof(StudentExamTicket))!;
        var submissionsNav = ticketType.FindNavigation(nameof(StudentExamTicket.Submissions));
        submissionsNav.Should().NotBeNull();
        submissionsNav!.IsCollection.Should().BeTrue();
    }

    [Fact(DisplayName = "7. Dữ liệu mẫu (Seed Data) trong 02_seed.sql sử dụng 100% UUID hợp lệ theo chuẩn RFC 4122")]
    public void SeedData_Must_Only_Use_Valid_Hexadecimal_UUIDs()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? seedPath = null;

        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "infra", "postgres", "init", "02_seed.sql");
            if (File.Exists(candidate))
            {
                seedPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        seedPath.Should().NotBeNull("File 02_seed.sql phải được tìm thấy");
        File.Exists(seedPath).Should().BeTrue();

        var sql = File.ReadAllText(seedPath!);
        var matches = Regex.Matches(sql, @"'([0-9a-zA-Z]{8}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{4}-[0-9a-zA-Z]{12})'");

        matches.Count.Should().BeGreaterThan(0, "02_seed.sql phải có chứa các giá trị UUID");

        foreach (Match m in matches)
        {
            var uuidStr = m.Groups[1].Value;
            var isValid = Guid.TryParse(uuidStr, out _);
            isValid.Should().BeTrue($"Chuỗi UUID '{uuidStr}' trong 02_seed.sql bắt buộc phải là số hex hợp lệ để tránh crash PostgreSQL");
        }
    }

    [Fact(DisplayName = "8. OralExamDbContext triển khai đúng chuẩn IApplicationDbContext cho Dependency Inversion")]
    public void OralExamDbContext_Must_Implement_IApplicationDbContext()
    {
        using var context = CreateDbContext();
        context.Should().BeAssignableTo<OralExamination.Application.Common.Interfaces.IApplicationDbContext>();

        var appDb = (OralExamination.Application.Common.Interfaces.IApplicationDbContext)context;
        appDb.Users.Should().NotBeNull();
        appDb.Courses.Should().NotBeNull();
        appDb.Semesters.Should().NotBeNull();
        appDb.Classes.Should().NotBeNull();
        appDb.ClassEnrollments.Should().NotBeNull();
        appDb.Rubrics.Should().NotBeNull();
        appDb.RubricCriteria.Should().NotBeNull();
        appDb.PracticeQuestions.Should().NotBeNull();
        appDb.ExamQuestions.Should().NotBeNull();
        appDb.PracticeSessions.Should().NotBeNull();
        appDb.PracticeAnswers.Should().NotBeNull();
        appDb.AiEvaluations.Should().NotBeNull();
        appDb.AiEvaluationDetails.Should().NotBeNull();
        appDb.ExamStructures.Should().NotBeNull();
        appDb.ExamSets.Should().NotBeNull();
        appDb.ExamSetQuestions.Should().NotBeNull();
        appDb.MockExamQuotas.Should().NotBeNull();
        appDb.MockExamSessions.Should().NotBeNull();
        appDb.MockExamAnswers.Should().NotBeNull();
        appDb.OfficialExamSessions.Should().NotBeNull();
        appDb.RealExamSessionShifts.Should().NotBeNull();
        appDb.StudentExamTickets.Should().NotBeNull();
        appDb.ExamQuestionSubmissions.Should().NotBeNull();
        appDb.LecturerAudits.Should().NotBeNull();
        appDb.LecturerAuditDetails.Should().NotBeNull();
        appDb.AppealRequests.Should().NotBeNull();
        appDb.DeadLetterQueues.Should().NotBeNull();
        appDb.AuditLogs.Should().NotBeNull();
    }

    [Fact(DisplayName = "9. Service Collection đăng ký đầy đủ Application và Infrastructure Services")]
    public void ServiceCollection_Must_Register_Application_And_Infrastructure()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=oral_exam;Username=postgres;Password=postgres"
            })
            .Build();

        OralExamination.Application.DependencyInjection.AddApplication(services);
        OralExamination.Infrastructure.DependencyInjection.AddInfrastructure(services, configuration);

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetService<OralExamDbContext>();
        dbContext.Should().NotBeNull("OralExamDbContext phải được đăng ký trong DI");

        var appDbContext = scope.ServiceProvider.GetService<OralExamination.Application.Common.Interfaces.IApplicationDbContext>();
        appDbContext.Should().NotBeNull("IApplicationDbContext phải được đăng ký dưới dạng Scoped trong DI");
    }

    [Fact(DisplayName = "10. OralExamDbContext Model chứa đầy đủ các chỉ mục phức hợp và CheckedInAt bắt buộc")]
    public void Model_Must_Contain_Required_Composite_Indexes_And_CheckedInAt()
    {
        using var context = CreateDbContext();

        // 1. StudentExamTicket.CheckedInAt is NOT NULL
        var ticketEntity = context.Model.FindEntityType(typeof(StudentExamTicket))!;
        var checkedInAtProp = ticketEntity.FindProperty(nameof(StudentExamTicket.CheckedInAt))!;
        checkedInAtProp.IsNullable.Should().BeFalse("checked_in_at trong student_exam_tickets phải là NOT NULL");

        // 2. PracticeQuestion composite index on (course_id, difficulty, is_active)
        var pqEntity = context.Model.FindEntityType(typeof(PracticeQuestion))!;
        var pqIndex = pqEntity.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_practice_questions_course");
        pqIndex.Should().NotBeNull("Chỉ mục ix_practice_questions_course phải được cấu hình trên practice_questions");

        // 3. ExamQuestion composite index on (course_id, difficulty, is_active)
        var eqEntity = context.Model.FindEntityType(typeof(ExamQuestion))!;
        var eqIndex = eqEntity.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_exam_questions_course");
        eqIndex.Should().NotBeNull("Chỉ mục ix_exam_questions_course phải được cấu hình trên exam_questions");

        // 4. DeadLetterQueue composite index on (status, created_at)
        var dlqEntity = context.Model.FindEntityType(typeof(DeadLetterQueue))!;
        var dlqIndex = dlqEntity.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_dead_letter_queues_status");
        dlqIndex.Should().NotBeNull("Chỉ mục ix_dead_letter_queues_status phải được cấu hình trên dead_letter_queues");

        // 5. AuditLog composite index on (entity_name, entity_id)
        var alEntity = context.Model.FindEntityType(typeof(AuditLog))!;
        var alIndex = alEntity.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_audit_logs_entity");
        alIndex.Should().NotBeNull("Chỉ mục ix_audit_logs_entity phải được cấu hình trên audit_logs");
    }

    [Fact(DisplayName = "11. DomainEnums UserRole và AppealStatus chứa đầy đủ hằng số chuẩn")]
    public void DomainEnums_UserRole_And_AppealStatus_Must_Contain_Standard_Values()
    {
        // 1. UserRole constants
        UserRole.Admin.Should().Be("admin");
        UserRole.DepartmentHead.Should().Be("department_head");
        UserRole.Lecturer.Should().Be("lecturer");
        UserRole.Proctor.Should().Be("proctor");
        UserRole.Student.Should().Be("student");
        UserRole.All.Should().BeEquivalentTo(new[] { "admin", "department_head", "lecturer", "proctor", "student" });

        // 2. AppealStatus constants
        AppealStatus.Pending.Should().Be("PENDING");
        AppealStatus.InReview.Should().Be("IN_REVIEW");
        AppealStatus.Approved.Should().Be("APPROVED");
        AppealStatus.Rejected.Should().Be("REJECTED");
        AppealStatus.Cancelled.Should().Be("CANCELLED");
        AppealStatus.All.Should().BeEquivalentTo(new[] { "PENDING", "IN_REVIEW", "APPROVED", "REJECTED", "CANCELLED" });
    }

    [Fact(DisplayName = "12. Cấu hình động Course và CotTrace AiEvaluation lưu trữ và ánh xạ chính xác")]
    public void Course_Dynamic_Config_And_AiEvaluation_CotTrace_Must_Persist()
    {
        using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392_TEST",
            Name = "Software Architecture and Design Test",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            HasFollowUp = true,
            TranscriptBufferSeconds = 90,
            MaxFollowUpQuestions = 3,
            IsActive = true
        };

        var aiEval = new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerType = "exam",
            TotalScore = 8.50m,
            Feedback = "Bài làm lập luận tốt",
            ConfidenceScore = 0.95m,
            CotTrace = "Step 1: Analyzed architecture principles. Step 2: Evaluated Clean Architecture layering."
        };

        course.TranscriptBufferSeconds.Should().Be(90);
        course.MaxFollowUpQuestions.Should().Be(3);
        aiEval.CotTrace.Should().Contain("Step 1: Analyzed architecture principles");

        var courseEntity = context.Model.FindEntityType(typeof(Course))!;
        courseEntity.FindProperty(nameof(Course.TranscriptBufferSeconds))!.GetColumnName().Should().Be("transcript_buffer_seconds");
        courseEntity.FindProperty(nameof(Course.MaxFollowUpQuestions))!.GetColumnName().Should().Be("max_follow_up_questions");

        var aiEvalEntity = context.Model.FindEntityType(typeof(AiEvaluation))!;
        aiEvalEntity.FindProperty(nameof(AiEvaluation.CotTrace))!.GetColumnName().Should().Be("cot_trace");
    }

    [Fact(DisplayName = "13. Thực thể AppealRequest ánh xạ đầy đủ khóa ngoại, chỉ mục và trạng thái mặc định")]
    public void AppealRequest_Entity_And_Navigations_Must_Be_Properly_Configured()
    {
        using var context = CreateDbContext();

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Reason = "Xin phúc khảo do ồn micro khi trả lời câu hỏi 2",
            AssignedTo = Guid.NewGuid()
        };

        appeal.Status.Should().Be("PENDING");
        appeal.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var appealType = context.Model.FindEntityType(typeof(AppealRequest))!;
        appealType.Should().NotBeNull();
        appealType.GetTableName().Should().Be("appeal_requests");

        // Indexes
        var ticketIndex = appealType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_appeal_requests_ticket");
        ticketIndex.Should().NotBeNull();
        var studentIndex = appealType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_appeal_requests_student");
        studentIndex.Should().NotBeNull();
        var sessionIndex = appealType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_appeal_requests_session");
        sessionIndex.Should().NotBeNull();
        var assignedIndex = appealType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_appeal_requests_assigned");
        assignedIndex.Should().NotBeNull();

        // Foreign keys
        appealType.FindNavigation(nameof(AppealRequest.Ticket)).Should().NotBeNull();
        appealType.FindNavigation(nameof(AppealRequest.Student)).Should().NotBeNull();
        appealType.FindNavigation(nameof(AppealRequest.Session)).Should().NotBeNull();
        appealType.FindNavigation(nameof(AppealRequest.AssignedToUser)).Should().NotBeNull();
    }

    [Fact(DisplayName = "14. Ràng buộc Check Constraints cho appeal_requests và cấu hình động trong 01_schema.sql")]
    public void Schema_Must_Configure_AppealRequests_And_DynamicConfig_Constraints()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        string? schemaPath = null;

        while (currentDir != null)
        {
            var candidate = Path.Combine(currentDir.FullName, "infra", "postgres", "init", "01_schema.sql");
            if (File.Exists(candidate))
            {
                schemaPath = candidate;
                break;
            }
            currentDir = currentDir.Parent;
        }

        schemaPath.Should().NotBeNull();
        var sql = File.ReadAllText(schemaPath!);

        // 1. courses constraints
        sql.Should().Contain("CONSTRAINT ck_courses_transcript_buffer CHECK (transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300)");
        sql.Should().Contain("CONSTRAINT ck_courses_max_follow_up CHECK (max_follow_up_questions >= 1 AND max_follow_up_questions <= 5)");

        // 2. appeal_requests constraints
        sql.Should().Contain("CONSTRAINT ck_appeal_requests_status");
        sql.Should().Contain("CHECK (status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED'))");
        sql.Should().Contain("CONSTRAINT ck_appeal_requests_scores");

        // 3. ai_evaluations cot_trace
        sql.Should().Contain("cot_trace           TEXT");

        // 4. mock_exam_answers -> practice_questions
        sql.Should().Contain("question_id         UUID NOT NULL REFERENCES practice_questions (id) ON DELETE RESTRICT");
    }

    [Fact(DisplayName = "15. MockExamAnswer ánh xạ chính xác tới PracticeQuestion thay vì ExamQuestion theo 01_schema.sql")]
    public void MockExamAnswer_Must_Reference_PracticeQuestion_Properly()
    {
        using var context = CreateDbContext();

        var mockAnswerType = context.Model.FindEntityType(typeof(MockExamAnswer))!;
        var questionNav = mockAnswerType.FindNavigation(nameof(MockExamAnswer.Question));
        questionNav.Should().NotBeNull("MockExamAnswer phải điều hướng tới PracticeQuestion");
        questionNav!.TargetEntityType.ClrType.Should().Be(typeof(PracticeQuestion));

        var questionFk = mockAnswerType.FindProperty(nameof(MockExamAnswer.QuestionId));
        questionFk.Should().NotBeNull();
        questionFk!.GetColumnName().Should().Be("question_id");

        var practiceType = context.Model.FindEntityType(typeof(PracticeQuestion))!;
        var mockAnswersNav = practiceType.FindNavigation(nameof(PracticeQuestion.MockExamAnswers));
        mockAnswersNav.Should().NotBeNull("PracticeQuestion phải có collection điều hướng MockExamAnswers");
        mockAnswersNav!.IsCollection.Should().BeTrue();
    }

    [Fact(DisplayName = "16. Hợp đồng IAiGradingService chuẩn hóa đầu vào chỉ nhận Transcript text, không nhận Audio")]
    public void IAiGradingService_Must_Enforce_TranscriptOnly_Input_Contract()
    {
        var serviceType = typeof(OralExamination.Application.Common.Interfaces.IAiGradingService);
        serviceType.Should().NotBeNull();
        serviceType.IsInterface.Should().BeTrue();

        var gradeMethod = serviceType.GetMethod(nameof(OralExamination.Application.Common.Interfaces.IAiGradingService.GradeAnswerAsync));
        gradeMethod.Should().NotBeNull();

        var requestType = typeof(OralExamination.Application.Common.Interfaces.AiGradingRequest);
        var transcriptProp = requestType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingRequest.Transcript));
        transcriptProp.Should().NotBeNull("AiGradingRequest phải có trường Transcript dạng string");
        transcriptProp!.PropertyType.Should().Be(typeof(string));

        // RÀNG BUỘC BẤT BIẾN: Tuyệt đối KHÔNG có thuộc tính Audio trong AiGradingRequest
        var audioProps = requestType.GetProperties()
            .Where(p => p.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        audioProps.Should().BeEmpty("Đầu vào chấm điểm của AI chỉ là bản transcript nhận từ hệ thống, TUYỆT ĐỐI không nhận audio.");

        var resultType = typeof(OralExamination.Application.Common.Interfaces.AiGradingResult);
        resultType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingResult.Score)).Should().NotBeNull();
        resultType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingResult.ConfidenceScore)).Should().NotBeNull();
        resultType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingResult.IsSuspicious)).Should().NotBeNull();
        resultType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingResult.SuspiciousReason)).Should().NotBeNull();
        resultType.GetProperty(nameof(OralExamination.Application.Common.Interfaces.AiGradingResult.CotTrace)).Should().NotBeNull();
    }
}


