using System;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext : DbContext, IApplicationDbContext
{
    public OralExamDbContext(DbContextOptions<OralExamDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Semester> Semesters { get; set; }
    public virtual DbSet<Course> Courses { get; set; }
    public virtual DbSet<Class> Classes { get; set; }
    public virtual DbSet<ClassEnrollment> ClassEnrollments { get; set; }
    public virtual DbSet<Rubric> Rubrics { get; set; }
    public virtual DbSet<RubricCriterion> RubricCriteria { get; set; }
    public virtual DbSet<PracticeQuestion> PracticeQuestions { get; set; }
    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }
    public virtual DbSet<PracticeSession> PracticeSessions { get; set; }
    public virtual DbSet<PracticeAnswer> PracticeAnswers { get; set; }
    public virtual DbSet<AiEvaluation> AiEvaluations { get; set; }
    public virtual DbSet<AiEvaluationDetail> AiEvaluationDetails { get; set; }
    public virtual DbSet<ExamStructure> ExamStructures { get; set; }
    public virtual DbSet<ExamSet> ExamSets { get; set; }
    public virtual DbSet<ExamSetQuestion> ExamSetQuestions { get; set; }
    public virtual DbSet<MockExamQuota> MockExamQuotas { get; set; }
    public virtual DbSet<MockExamSession> MockExamSessions { get; set; }
    public virtual DbSet<MockExamAnswer> MockExamAnswers { get; set; }
    public virtual DbSet<OfficialExamSession> OfficialExamSessions { get; set; }
    public virtual DbSet<RealExamSessionShift> RealExamSessionShifts { get; set; }
    public virtual DbSet<StudentExamTicket> StudentExamTickets { get; set; }
    public virtual DbSet<ExamQuestionSubmission> ExamQuestionSubmissions { get; set; }
    public virtual DbSet<LecturerAudit> LecturerAudits { get; set; }
    public virtual DbSet<LecturerAuditDetail> LecturerAuditDetails { get; set; }
    public virtual DbSet<AppealRequest> AppealRequests { get; set; }
    public virtual DbSet<DeadLetterQueue> DeadLetterQueues { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("ix_users_email");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.StudentCode).HasColumnName("student_code").HasMaxLength(20);
            entity.HasIndex(e => e.StudentCode).IsUnique().HasDatabaseName("ix_users_student_code");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).HasDefaultValue("student").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // 2. semesters
        modelBuilder.Entity<Semester>(entity =>
        {
            entity.ToTable("semesters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ix_semesters_code");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // 3. courses
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ix_courses_code");
            entity.HasIndex(e => e.SemesterId).HasDatabaseName("ix_courses_semester");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Credits).HasColumnName("credits").HasDefaultValue(3);
            entity.Property(e => e.SemesterId).HasColumnName("semester_id").IsRequired();
            entity.Property(e => e.HasFollowUp).HasColumnName("has_follow_up").HasDefaultValue(false);
            entity.Property(e => e.TranscriptBufferSeconds).HasColumnName("transcript_buffer_seconds").HasDefaultValue(60);
            entity.Property(e => e.MaxFollowUpQuestions).HasColumnName("max_follow_up_questions").HasDefaultValue(2);
            entity.Property(e => e.ExamInputMode)
                .HasColumnName("exam_input_mode")
                .HasMaxLength(30)
                .HasDefaultValue("VoiceAndTextInput")
                .IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Semester)
                .WithMany(p => p.Courses)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. classes
        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ix_classes_code");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_classes_course");
            entity.HasIndex(e => e.SemesterId).HasDatabaseName("ix_classes_semester");
            entity.HasIndex(e => e.LecturerId).HasDatabaseName("ix_classes_lecturer");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.SemesterId).HasColumnName("semester_id").IsRequired();
            entity.Property(e => e.LecturerId).HasColumnName("lecturer_id");
            entity.Property(e => e.MaxStudents).HasColumnName("max_students").HasDefaultValue(35);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.Classes)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Semester)
                .WithMany(p => p.Classes)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Lecturer)
                .WithMany(p => p.InstructedClasses)
                .HasForeignKey(d => d.LecturerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 5. class_enrollments
        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.ToTable("class_enrollments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ClassId).HasColumnName("class_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.EnrolledAt).HasColumnName("enrolled_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("enrolled");
            entity.HasIndex(e => e.StudentId).HasDatabaseName("ix_class_enrollments_student");
            entity.HasIndex(e => new { e.ClassId, e.StudentId }).IsUnique().HasDatabaseName("uq_class_enrollments");

            entity.HasOne(d => d.Class)
                .WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. rubrics
        modelBuilder.Entity<Rubric>(entity =>
        {
            entity.ToTable("rubrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.TotalMaxScore).HasColumnName("total_max_score").HasPrecision(4, 2).HasDefaultValue(10.00m);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_rubrics_course");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.Rubrics)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 7. rubric_criteria
        modelBuilder.Entity<RubricCriterion>(entity =>
        {
            entity.ToTable("rubric_criteria");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RubricId).HasColumnName("rubric_id").IsRequired();
            entity.Property(e => e.CriterionName).HasColumnName("criterion_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.MaxScore).HasColumnName("max_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.Weight).HasColumnName("weight").HasPrecision(3, 2).IsRequired();
            entity.Property(e => e.BloomLevel).HasColumnName("bloom_level").HasMaxLength(20);
            entity.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(1);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.RubricId).HasDatabaseName("ix_rubric_criteria_rubric");

            entity.HasOne(d => d.Rubric)
                .WithMany(p => p.Criteria)
                .HasForeignKey(d => d.RubricId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 8. practice_questions
        modelBuilder.Entity<PracticeQuestion>(entity =>
        {
            entity.ToTable("practice_questions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.RubricId).HasColumnName("rubric_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.SampleAnswer).HasColumnName("sample_answer");
            entity.Property(e => e.KeyPoints).HasColumnName("key_points").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.Difficulty).HasColumnName("difficulty").HasMaxLength(20).HasDefaultValue("medium");
            entity.Property(e => e.BloomLevel).HasColumnName("bloom_level").HasMaxLength(20).HasDefaultValue("Understand");
            entity.Property(e => e.HasFollowUp).HasColumnName("has_follow_up").HasDefaultValue(false);
            entity.Property(e => e.FollowUpPrompt).HasColumnName("follow_up_prompt");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.CourseId, e.Difficulty, e.IsActive }).HasDatabaseName("ix_practice_questions_course");
            entity.HasIndex(e => e.RubricId).HasDatabaseName("ix_practice_questions_rubric");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.PracticeQuestions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Rubric)
                .WithMany(p => p.PracticeQuestions)
                .HasForeignKey(d => d.RubricId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. exam_questions
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.ToTable("exam_questions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.RubricId).HasColumnName("rubric_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.SampleAnswer).HasColumnName("sample_answer");
            entity.Property(e => e.KeyPoints).HasColumnName("key_points").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.Difficulty).HasColumnName("difficulty").HasMaxLength(20).HasDefaultValue("medium");
            entity.Property(e => e.BloomLevel).HasColumnName("bloom_level").HasMaxLength(20).HasDefaultValue("Understand");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50).HasDefaultValue("manual");
            entity.Property(e => e.ApprovalStatus).HasColumnName("approval_status").HasMaxLength(30).HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");
            entity.Property(e => e.ReviewNotes).HasColumnName("review_notes");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.CourseId, e.Difficulty, e.IsActive }).HasDatabaseName("ix_exam_questions_course");
            entity.HasIndex(e => e.RubricId).HasDatabaseName("ix_exam_questions_rubric");
            entity.HasIndex(e => e.ApprovedBy).HasDatabaseName("ix_exam_questions_approved");
            entity.HasIndex(e => e.ApprovalStatus).HasDatabaseName("ix_exam_questions_approval_status");
            entity.HasIndex(e => e.SubmittedBy).HasDatabaseName("ix_exam_questions_submitted");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Rubric)
                .WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.RubricId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ApprovedByUser)
                .WithMany(p => p.ApprovedExamQuestions)
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.SubmittedByUser)
                .WithMany()
                .HasForeignKey(d => d.SubmittedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 10. practice_sessions
        modelBuilder.Entity<PracticeSession>(entity =>
        {
            entity.ToTable("practice_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.PracticeMode).HasColumnName("practice_mode").HasMaxLength(20).HasDefaultValue("per_question");
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("in_progress");
            entity.HasIndex(e => new { e.StudentId, e.StartedAt }).HasDatabaseName("ix_practice_sessions_student");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_practice_sessions_course");

            entity.HasOne(d => d.Student)
                .WithMany(p => p.PracticeSessions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.PracticeSessions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. practice_answers
        modelBuilder.Entity<PracticeAnswer>(entity =>
        {
            entity.ToTable("practice_answers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.AnswerText).HasColumnName("answer_text").IsRequired();
            entity.Property(e => e.AudioUrl).HasColumnName("audio_url");
            entity.Property(e => e.IsFollowUp).HasColumnName("is_follow_up").HasDefaultValue(false);
            entity.Property(e => e.ParentAnswerId).HasColumnName("parent_answer_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.SessionId).HasDatabaseName("ix_practice_answers_session");
            entity.HasIndex(e => e.QuestionId).HasDatabaseName("ix_practice_answers_question");
            entity.HasIndex(e => e.StudentId).HasDatabaseName("ix_practice_answers_student");
            entity.HasIndex(e => e.ParentAnswerId).HasDatabaseName("ix_practice_answers_parent");

            entity.HasOne(d => d.Session)
                .WithMany(p => p.Answers)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Question)
                .WithMany(p => p.PracticeAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Student)
                .WithMany(p => p.PracticeAnswers)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ParentAnswer)
                .WithMany(p => p.FollowUpAnswers)
                .HasForeignKey(d => d.ParentAnswerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 12. ai_evaluations
        modelBuilder.Entity<AiEvaluation>(entity =>
        {
            entity.ToTable("ai_evaluations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AnswerType).HasColumnName("answer_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.PracticeAnswerId).HasColumnName("practice_answer_id");
            entity.Property(e => e.ExamSubmissionId).HasColumnName("exam_submission_id");
            entity.Property(e => e.TotalScore).HasColumnName("total_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.Feedback).HasColumnName("feedback");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score").HasPrecision(3, 2);
            entity.Property(e => e.CotTrace).HasColumnName("cot_trace");
            entity.Property(e => e.EvaluatedAt).HasColumnName("evaluated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.PracticeAnswerId).HasDatabaseName("ix_ai_evaluations_practice_answer");
            entity.HasIndex(e => e.ExamSubmissionId).HasDatabaseName("ix_ai_evaluations_exam_submission");

            entity.HasOne(d => d.PracticeAnswer)
                .WithMany(p => p.AiEvaluations)
                .HasForeignKey(d => d.PracticeAnswerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamSubmission)
                .WithMany(p => p.AiEvaluations)
                .HasForeignKey(d => d.ExamSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. ai_evaluation_details
        modelBuilder.Entity<AiEvaluationDetail>(entity =>
        {
            entity.ToTable("ai_evaluation_details");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.EvaluationId).HasColumnName("evaluation_id").IsRequired();
            entity.Property(e => e.CriterionId).HasColumnName("criterion_id").IsRequired();
            entity.Property(e => e.Score).HasColumnName("score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.HasIndex(e => e.EvaluationId).HasDatabaseName("ix_ai_eval_details_evaluation");
            entity.HasIndex(e => e.CriterionId).HasDatabaseName("ix_ai_eval_details_criterion");

            entity.HasOne(d => d.Evaluation)
                .WithMany(p => p.Details)
                .HasForeignKey(d => d.EvaluationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Criterion)
                .WithMany(p => p.AiEvaluationDetails)
                .HasForeignKey(d => d.CriterionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 14. exam_structures
        modelBuilder.Entity<ExamStructure>(entity =>
        {
            entity.ToTable("exam_structures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.TotalQuestions).HasColumnName("total_questions").HasDefaultValue(5);
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes").HasDefaultValue(30);
            entity.Property(e => e.BloomDistribution).HasColumnName("bloom_distribution").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_exam_structures_course");
            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("ix_exam_structures_created_by");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.ExamStructures)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany(p => p.CreatedExamStructures)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 15. exam_sets
        modelBuilder.Entity<ExamSet>(entity =>
        {
            entity.ToTable("exam_sets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StructureId).HasColumnName("structure_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.SetCode).HasColumnName("set_code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.GeneratedAt).HasColumnName("generated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(e => e.StructureId).HasDatabaseName("ix_exam_sets_structure");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_exam_sets_course");

            entity.HasOne(d => d.Structure)
                .WithMany(p => p.ExamSets)
                .HasForeignKey(d => d.StructureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.ExamSets)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 16. exam_set_questions
        modelBuilder.Entity<ExamSetQuestion>(entity =>
        {
            entity.ToTable("exam_set_questions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ExamSetId).HasColumnName("exam_set_id").IsRequired();
            entity.Property(e => e.ExamQuestionId).HasColumnName("exam_question_id").IsRequired();
            entity.Property(e => e.OrderIndex).HasColumnName("order_index").HasDefaultValue(1);
            entity.HasIndex(e => e.ExamSetId).HasDatabaseName("ix_exam_set_questions_set");
            entity.HasIndex(e => e.ExamQuestionId).HasDatabaseName("ix_exam_set_questions_question");
            entity.HasIndex(e => new { e.ExamSetId, e.ExamQuestionId }).IsUnique().HasDatabaseName("uq_exam_set_questions");

            entity.HasOne(d => d.ExamSet)
                .WithMany(p => p.ExamSetQuestions)
                .HasForeignKey(d => d.ExamSetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamQuestion)
                .WithMany(p => p.ExamSetQuestions)
                .HasForeignKey(d => d.ExamQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 17. mock_exam_quotas
        modelBuilder.Entity<MockExamQuota>(entity =>
        {
            entity.ToTable("mock_exam_quotas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.QuotaDate).HasColumnName("quota_date").IsRequired();
            entity.Property(e => e.UsedCount).HasColumnName("used_count").HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => new { e.StudentId, e.CourseId, e.QuotaDate }).IsUnique().HasDatabaseName("ix_mock_exam_quotas_lookup");

            entity.HasOne(d => d.Student)
                .WithMany(p => p.MockExamQuotas)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.MockExamQuotas)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 18. mock_exam_sessions
        modelBuilder.Entity<MockExamSession>(entity =>
        {
            entity.ToTable("mock_exam_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ExamSetId).HasColumnName("exam_set_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.HasFollowUp).HasColumnName("has_follow_up").HasDefaultValue(false);
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("in_progress");
            entity.Property(e => e.TotalScore).HasColumnName("total_score").HasPrecision(4, 2);
            entity.HasIndex(e => new { e.StudentId, e.StartedAt }).HasDatabaseName("ix_mock_exam_sessions_student");
            entity.HasIndex(e => e.ExamSetId).HasDatabaseName("ix_mock_exam_sessions_set");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_mock_exam_sessions_course");

            entity.HasOne(d => d.Student)
                .WithMany(p => p.MockExamSessions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamSet)
                .WithMany(p => p.MockExamSessions)
                .HasForeignKey(d => d.ExamSetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Course)
                .WithMany(p => p.MockExamSessions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 19. mock_exam_answers
        modelBuilder.Entity<MockExamAnswer>(entity =>
        {
            entity.ToTable("mock_exam_answers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.AnswerText).HasColumnName("answer_text");
            entity.Property(e => e.AudioUrl).HasColumnName("audio_url");
            entity.Property(e => e.TimeTakenSeconds).HasColumnName("time_taken_seconds").HasDefaultValue(0);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("answered");
            entity.Property(e => e.AnsweredAt).HasColumnName("answered_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.SessionId).HasDatabaseName("ix_mock_exam_answers_session");
            entity.HasIndex(e => e.QuestionId).HasDatabaseName("ix_mock_exam_answers_question");

            entity.HasOne(d => d.Session)
                .WithMany(p => p.Answers)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Question)
                .WithMany(p => p.MockExamAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 20. official_exam_sessions
        modelBuilder.Entity<OfficialExamSession>(entity =>
        {
            entity.ToTable("official_exam_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.ExamStructureId).HasColumnName("exam_structure_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ExamDate).HasColumnName("exam_date").IsRequired();
            entity.Property(e => e.HasFollowUp).HasColumnName("has_follow_up").HasDefaultValue(false);
            entity.Property(e => e.MaxFollowUpQuestions).HasColumnName("max_follow_up_questions").HasDefaultValue(1);
            entity.Property(e => e.ExamInputMode).HasColumnName("exam_input_mode").HasMaxLength(30).HasDefaultValue("VoiceAndTextInput").IsRequired();
            entity.Property(e => e.TranscriptBufferSeconds).HasColumnName("transcript_buffer_seconds").HasDefaultValue(60);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("scheduled");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_official_exam_sessions_course");
            entity.HasIndex(e => e.ExamStructureId).HasDatabaseName("ix_official_exam_sessions_structure");
            entity.HasIndex(e => e.CreatedBy).HasDatabaseName("ix_official_exam_sessions_creator");

            entity.HasOne(d => d.Course)
                .WithMany(p => p.OfficialExamSessions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamStructure)
                .WithMany(p => p.OfficialExamSessions)
                .HasForeignKey(d => d.ExamStructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 21. real_exam_session_shifts
        modelBuilder.Entity<RealExamSessionShift>(entity =>
        {
            entity.ToTable("real_exam_session_shifts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.ShiftName).HasColumnName("shift_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RoomLab).HasColumnName("room_lab").HasMaxLength(50).IsRequired();
            entity.Property(e => e.StartTime).HasColumnName("start_time").IsRequired();
            entity.Property(e => e.EndTime).HasColumnName("end_time").IsRequired();
            entity.Property(e => e.ProctorUserId).HasColumnName("proctor_user_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("scheduled");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.SessionId).HasDatabaseName("ix_real_exam_shifts_session");
            entity.HasIndex(e => e.ProctorUserId).HasDatabaseName("ix_real_exam_shifts_proctor");

            entity.HasOne(d => d.Session)
                .WithMany(p => p.Shifts)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ProctorUser)
                .WithMany(p => p.ProctoredShifts)
                .HasForeignKey(d => d.ProctorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 22. student_exam_tickets
        modelBuilder.Entity<StudentExamTicket>(entity =>
        {
            entity.ToTable("student_exam_tickets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ExamSetId).HasColumnName("exam_set_id");
            entity.Property(e => e.SeatNumber).HasColumnName("seat_number").IsRequired();
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("SCHEDULED");
            entity.Property(e => e.IsLocked).HasColumnName("is_locked").HasDefaultValue(false);
            entity.Property(e => e.CheckedInAt).HasColumnName("checked_in_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.HasIndex(e => e.ShiftId).HasDatabaseName("ix_student_exam_tickets_shift");
            entity.HasIndex(e => e.StudentId).HasDatabaseName("ix_student_exam_tickets_student");
            entity.HasIndex(e => e.ExamSetId).HasDatabaseName("ix_student_exam_tickets_set");
            entity.HasIndex(e => new { e.ShiftId, e.SeatNumber }).IsUnique().HasDatabaseName("uq_tickets_seat");
            entity.HasIndex(e => new { e.ShiftId, e.StudentId }).IsUnique().HasDatabaseName("uq_tickets_student");

            entity.HasOne(d => d.Shift)
                .WithMany(p => p.StudentExamTickets)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany(p => p.ExamTickets)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamSet)
                .WithMany(p => p.StudentExamTickets)
                .HasForeignKey(d => d.ExamSetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 23. exam_question_submissions
        modelBuilder.Entity<ExamQuestionSubmission>(entity =>
        {
            entity.ToTable("exam_question_submissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id").IsRequired();
            entity.Property(e => e.ExamQuestionId).HasColumnName("exam_question_id").IsRequired();
            entity.Property(e => e.AudioR2Url).HasColumnName("audio_r2_url");
            entity.Property(e => e.AudioHashSha256).HasColumnName("audio_hash_sha256").HasMaxLength(64);
            entity.Property(e => e.TranscriptWhisper).HasColumnName("transcript_whisper");
            entity.Property(e => e.TimestampsWhisper).HasColumnName("timestamps_whisper").HasColumnType("jsonb");
            entity.Property(e => e.TimeSpentSeconds).HasColumnName("time_spent_seconds").HasDefaultValue(0);
            entity.Property(e => e.AiScore).HasColumnName("ai_score").HasPrecision(4, 2);
            entity.Property(e => e.FinalScore).HasColumnName("final_score").HasPrecision(4, 2);
            entity.Property(e => e.GradingStatus).HasColumnName("grading_status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.TicketId).HasDatabaseName("ix_exam_question_submissions_ticket");
            entity.HasIndex(e => e.ExamQuestionId).HasDatabaseName("ix_exam_question_submissions_question");
            entity.HasIndex(e => new { e.TicketId, e.ExamQuestionId }).IsUnique().HasDatabaseName("uq_ticket_question");

            entity.HasOne(d => d.Ticket)
                .WithMany(p => p.Submissions)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamQuestion)
                .WithMany(p => p.Submissions)
                .HasForeignKey(d => d.ExamQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 24. lecturer_audits
        modelBuilder.Entity<LecturerAudit>(entity =>
        {
            entity.ToTable("lecturer_audits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id").IsRequired();
            entity.Property(e => e.LecturerId).HasColumnName("lecturer_id").IsRequired();
            entity.Property(e => e.OriginalAiScore).HasColumnName("original_ai_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.AuditedScore).HasColumnName("audited_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.OverrideReason).HasColumnName("override_reason").IsRequired();
            entity.Property(e => e.IsLocked).HasColumnName("is_locked").HasDefaultValue(false);
            entity.Property(e => e.AuditedAt).HasColumnName("audited_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.TicketId).IsUnique().HasDatabaseName("ix_lecturer_audits_ticket");
            entity.HasIndex(e => e.LecturerId).HasDatabaseName("ix_lecturer_audits_lecturer");

            entity.HasOne(d => d.Ticket)
                .WithOne(p => p.LecturerAudit)
                .HasForeignKey<LecturerAudit>(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Lecturer)
                .WithMany(p => p.LecturerAudits)
                .HasForeignKey(d => d.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 25. lecturer_audit_details
        modelBuilder.Entity<LecturerAuditDetail>(entity =>
        {
            entity.ToTable("lecturer_audit_details");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AuditId).HasColumnName("audit_id").IsRequired();
            entity.Property(e => e.CriterionId).HasColumnName("criterion_id").IsRequired();
            entity.Property(e => e.AiScore).HasColumnName("ai_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.AuditedScore).HasColumnName("audited_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.LecturerComment).HasColumnName("lecturer_comment");
            entity.HasIndex(e => e.AuditId).HasDatabaseName("ix_lecturer_audit_details_audit");
            entity.HasIndex(e => e.CriterionId).HasDatabaseName("ix_lecturer_audit_details_criterion");

            entity.HasOne(d => d.Audit)
                .WithMany(p => p.Details)
                .HasForeignKey(d => d.AuditId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Criterion)
                .WithMany(p => p.LecturerAuditDetails)
                .HasForeignKey(d => d.CriterionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 26. dead_letter_queues
        modelBuilder.Entity<DeadLetterQueue>(entity =>
        {
            entity.ToTable("dead_letter_queues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TaskType).HasColumnName("task_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").IsRequired();
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_dead_letter_queues_status");
        });

        // 27. audit_logs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityName).HasColumnName("entity_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_audit_logs_user");
            entity.HasIndex(e => new { e.EntityName, e.EntityId }).HasDatabaseName("ix_audit_logs_entity");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_audit_logs_created");

            entity.HasOne(d => d.User)
                .WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 28. appeal_requests
        modelBuilder.Entity<AppealRequest>(entity =>
        {
            entity.ToTable("appeal_requests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.Reason).HasColumnName("reason").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("PENDING").IsRequired();
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to").IsRequired();
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Decision).HasColumnName("decision").HasMaxLength(50);
            entity.Property(e => e.OriginalScore).HasColumnName("original_score").HasPrecision(4, 2);
            entity.Property(e => e.ProposedScore).HasColumnName("proposed_score").HasPrecision(4, 2);
            entity.Property(e => e.ReviewNotes).HasColumnName("review_notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");

            entity.HasIndex(e => e.TicketId).HasDatabaseName("ix_appeal_requests_ticket");
            entity.HasIndex(e => e.StudentId).HasDatabaseName("ix_appeal_requests_student");
            entity.HasIndex(e => e.SessionId).HasDatabaseName("ix_appeal_requests_session");
            entity.HasIndex(e => new { e.AssignedTo, e.Status }).HasDatabaseName("ix_appeal_requests_assigned");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_appeal_requests_status");

            entity.HasOne(d => d.Ticket)
                .WithMany()
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Session)
                .WithMany()
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Submission)
                .WithMany()
                .HasForeignKey(d => d.SubmissionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.AssignedToUser)
                .WithMany()
                .HasForeignKey(d => d.AssignedTo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReviewedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
