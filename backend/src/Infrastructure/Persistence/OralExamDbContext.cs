using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext : DbContext
{
    public OralExamDbContext(DbContextOptions<OralExamDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassEnrollment> ClassEnrollments { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }

    public virtual DbSet<ExamStructure> ExamStructures { get; set; }

    public virtual DbSet<MockExamAnswer> MockExamAnswers { get; set; }

    public virtual DbSet<MockExamFollowupQuestion> MockExamFollowupQuestions { get; set; }

    public virtual DbSet<MockExamQuota> MockExamQuotas { get; set; }

    public virtual DbSet<MockExamSession> MockExamSessions { get; set; }

    public virtual DbSet<PracticeAnswer> PracticeAnswers { get; set; }

    public virtual DbSet<PracticeFollowupQuestion> PracticeFollowupQuestions { get; set; }

    public virtual DbSet<PracticeQuestion> PracticeQuestions { get; set; }

    public virtual DbSet<PracticeSession> PracticeSessions { get; set; }

    public virtual DbSet<RealExamAnswer> RealExamAnswers { get; set; }

    public virtual DbSet<RealExamAssignment> RealExamAssignments { get; set; }

    public virtual DbSet<RealExamFollowupQuestion> RealExamFollowupQuestions { get; set; }

    public virtual DbSet<RealExamResult> RealExamResults { get; set; }

    public virtual DbSet<RealExamSession> RealExamSessions { get; set; }

    public virtual DbSet<RealExamSessionShift> RealExamSessionShifts { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RubricItem> RubricItems { get; set; }

    public virtual DbSet<Semester> Semesters { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("audit_logs_pkey");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => e.CreatedAt, "ix_audit_logs_created").IsDescending();

            entity.HasIndex(e => new { e.TargetTable, e.TargetId }, "ix_audit_logs_target");

            entity.HasIndex(e => e.UserId, "ix_audit_logs_user");

            entity.Property(e => e.LogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("log_id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(64)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValues)
                .HasColumnType("jsonb")
                .HasColumnName("new_values");
            entity.Property(e => e.OldValues)
                .HasColumnType("jsonb")
                .HasColumnName("old_values");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetTable)
                .HasMaxLength(100)
                .HasColumnName("target_table");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("audit_logs_user_id_fkey");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId).HasName("classes_pkey");

            entity.ToTable("classes");

            entity.HasIndex(e => new { e.SemesterId, e.ClassName }, "uq_classes_semester_name").IsUnique();

            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.ClassName)
                .HasMaxLength(100)
                .HasColumnName("class_name");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.InstructorId).HasColumnName("instructor_id");
            entity.Property(e => e.SemesterId).HasColumnName("semester_id");

            entity.HasOne(d => d.Course).WithMany(p => p.Classes)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classes_course_id_fkey");

            entity.HasOne(d => d.Instructor).WithMany(p => p.Classes)
                .HasForeignKey(d => d.InstructorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classes_instructor_id_fkey");

            entity.HasOne(d => d.Semester).WithMany(p => p.Classes)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("classes_semester_id_fkey");
        });

        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId).HasName("class_enrollments_pkey");

            entity.ToTable("class_enrollments");

            entity.HasIndex(e => e.StudentId, "ix_class_enrollments_student");

            entity.HasIndex(e => new { e.ClassId, e.StudentId }, "uq_class_enrollments").IsUnique();

            entity.Property(e => e.EnrollmentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("enrollment_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("enrolled_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.StudentCode)
                .HasMaxLength(50)
                .HasColumnName("student_code");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassEnrollments)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("class_enrollments_class_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassEnrollments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("class_enrollments_student_id_fkey");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("courses_pkey");

            entity.ToTable("courses");

            entity.HasIndex(e => e.CourseCode, "courses_course_code_key").IsUnique();

            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CourseCode)
                .HasMaxLength(50)
                .HasColumnName("course_code");
            entity.Property(e => e.CourseName)
                .HasMaxLength(255)
                .HasColumnName("course_name");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("exam_questions_pkey");

            entity.ToTable("exam_questions");

            entity.Property(e => e.QuestionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("question_id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Difficulty)
                .HasMaxLength(20)
                .HasColumnName("difficulty");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ModelAnswer).HasColumnName("model_answer");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("exam_questions_approved_by_fkey");

            entity.HasOne(d => d.Course).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("exam_questions_course_id_fkey");
        });

        modelBuilder.Entity<ExamStructure>(entity =>
        {
            entity.HasKey(e => e.StructureId).HasName("exam_structures_pkey");

            entity.ToTable("exam_structures");

            entity.Property(e => e.StructureId).HasColumnName("structure_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.EasyCount)
                .HasDefaultValue(0)
                .HasColumnName("easy_count");
            entity.Property(e => e.ExamMode)
                .HasMaxLength(20)
                .HasDefaultValueSql("'FIXED'::character varying")
                .HasColumnName("exam_mode");
            entity.Property(e => e.FollowupTimeoutSec).HasColumnName("followup_timeout_sec");
            entity.Property(e => e.HardCount)
                .HasDefaultValue(0)
                .HasColumnName("hard_count");
            entity.Property(e => e.MaxFollowups).HasColumnName("max_followups");
            entity.Property(e => e.MediumCount)
                .HasDefaultValue(0)
                .HasColumnName("medium_count");
            entity.Property(e => e.StructureName)
                .HasMaxLength(255)
                .HasColumnName("structure_name");

            entity.HasOne(d => d.Course).WithMany(p => p.ExamStructures)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("exam_structures_course_id_fkey");
        });

        modelBuilder.Entity<MockExamAnswer>(entity =>
        {
            entity.HasKey(e => e.MockAnswerId).HasName("mock_exam_answers_pkey");

            entity.ToTable("mock_exam_answers");

            entity.HasIndex(e => e.MockSessionId, "ix_mock_answers_session");

            entity.Property(e => e.MockAnswerId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("mock_answer_id");
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AiScore)
                .HasPrecision(5, 2)
                .HasColumnName("ai_score");
            entity.Property(e => e.FollowupQuestionId).HasColumnName("followup_question_id");
            entity.Property(e => e.MockSessionId).HasColumnName("mock_session_id");
            entity.Property(e => e.ParentAnswerId).HasColumnName("parent_answer_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.RoundIndex)
                .HasDefaultValue(0)
                .HasColumnName("round_index");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserTranscript).HasColumnName("user_transcript");

            entity.HasOne(d => d.FollowupQuestion).WithMany(p => p.MockExamAnswers)
                .HasForeignKey(d => d.FollowupQuestionId)
                .HasConstraintName("fk_mock_answers_followup");

            entity.HasOne(d => d.MockSession).WithMany(p => p.MockExamAnswers)
                .HasForeignKey(d => d.MockSessionId)
                .HasConstraintName("mock_exam_answers_mock_session_id_fkey");

            entity.HasOne(d => d.ParentAnswer).WithMany(p => p.InverseParentAnswer)
                .HasForeignKey(d => d.ParentAnswerId)
                .HasConstraintName("mock_exam_answers_parent_answer_id_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.MockExamAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mock_exam_answers_question_id_fkey");
        });

        modelBuilder.Entity<MockExamFollowupQuestion>(entity =>
        {
            entity.HasKey(e => e.FollowupId).HasName("mock_exam_followup_questions_pkey");

            entity.ToTable("mock_exam_followup_questions");

            entity.Property(e => e.FollowupId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("followup_id");
            entity.Property(e => e.ContextTranscript).HasColumnName("context_transcript");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.MockAnswerId).HasColumnName("mock_answer_id");
            entity.Property(e => e.MockSessionId).HasColumnName("mock_session_id");
            entity.Property(e => e.ModelVersion)
                .HasMaxLength(100)
                .HasColumnName("model_version");
            entity.Property(e => e.ParentPracticeQuestionId).HasColumnName("parent_practice_question_id");
            entity.Property(e => e.PromptVersion)
                .HasMaxLength(100)
                .HasColumnName("prompt_version");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.RoundIndex)
                .HasDefaultValue(1)
                .HasColumnName("round_index");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'GENERATED'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TimeoutSec).HasColumnName("timeout_sec");

            entity.HasOne(d => d.MockAnswer).WithMany(p => p.MockExamFollowupQuestions)
                .HasForeignKey(d => d.MockAnswerId)
                .HasConstraintName("mock_exam_followup_questions_mock_answer_id_fkey");

            entity.HasOne(d => d.MockSession).WithMany(p => p.MockExamFollowupQuestions)
                .HasForeignKey(d => d.MockSessionId)
                .HasConstraintName("mock_exam_followup_questions_mock_session_id_fkey");

            entity.HasOne(d => d.ParentPracticeQuestion).WithMany(p => p.MockExamFollowupQuestions)
                .HasForeignKey(d => d.ParentPracticeQuestionId)
                .HasConstraintName("mock_exam_followup_questions_parent_practice_question_id_fkey");
        });

        modelBuilder.Entity<MockExamQuota>(entity =>
        {
            entity.HasKey(e => e.QuotaId).HasName("mock_exam_quotas_pkey");

            entity.ToTable("mock_exam_quotas");

            entity.HasIndex(e => new { e.StudentId, e.CourseId, e.QuotaDate }, "uq_mock_exam_quotas").IsUnique();

            entity.Property(e => e.QuotaId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("quota_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.MaxCount)
                .HasDefaultValue(3)
                .HasColumnName("max_count");
            entity.Property(e => e.QuotaDate)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("quota_date");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");

            entity.HasOne(d => d.Course).WithMany(p => p.MockExamQuota)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mock_exam_quotas_course_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.MockExamQuota)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mock_exam_quotas_student_id_fkey");
        });

        modelBuilder.Entity<MockExamSession>(entity =>
        {
            entity.HasKey(e => e.MockSessionId).HasName("mock_exam_sessions_pkey");

            entity.ToTable("mock_exam_sessions");

            entity.HasIndex(e => e.StudentId, "ix_mock_sessions_student");

            entity.Property(e => e.MockSessionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("mock_session_id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ExamMode)
                .HasMaxLength(20)
                .HasDefaultValueSql("'FIXED'::character varying")
                .HasColumnName("exam_mode");
            entity.Property(e => e.OverallFeedback).HasColumnName("overall_feedback");
            entity.Property(e => e.OverallScore)
                .HasPrecision(5, 2)
                .HasColumnName("overall_score");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'IN_PROGRESS'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.StructureId).HasColumnName("structure_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Structure).WithMany(p => p.MockExamSessions)
                .HasForeignKey(d => d.StructureId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mock_exam_sessions_structure_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.MockExamSessions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("mock_exam_sessions_student_id_fkey");
        });

        modelBuilder.Entity<PracticeAnswer>(entity =>
        {
            entity.HasKey(e => e.AnswerId).HasName("practice_answers_pkey");

            entity.ToTable("practice_answers");

            entity.HasIndex(e => e.SessionId, "ix_practice_answers_session");

            entity.Property(e => e.AnswerId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("answer_id");
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AiScore)
                .HasPrecision(5, 2)
                .HasColumnName("ai_score");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FollowupQuestionId).HasColumnName("followup_question_id");
            entity.Property(e => e.ParentAnswerId).HasColumnName("parent_answer_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserTranscript).HasColumnName("user_transcript");

            entity.HasOne(d => d.FollowupQuestion).WithMany(p => p.PracticeAnswers)
                .HasForeignKey(d => d.FollowupQuestionId)
                .HasConstraintName("fk_practice_answers_followup");

            entity.HasOne(d => d.ParentAnswer).WithMany(p => p.InverseParentAnswer)
                .HasForeignKey(d => d.ParentAnswerId)
                .HasConstraintName("practice_answers_parent_answer_id_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.PracticeAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("practice_answers_question_id_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.PracticeAnswers)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("practice_answers_session_id_fkey");
        });

        modelBuilder.Entity<PracticeFollowupQuestion>(entity =>
        {
            entity.HasKey(e => e.FollowupId).HasName("practice_followup_questions_pkey");

            entity.ToTable("practice_followup_questions");

            entity.Property(e => e.FollowupId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("followup_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.GapSummary)
                .HasColumnType("jsonb")
                .HasColumnName("gap_summary");
            entity.Property(e => e.ModelVersion)
                .HasMaxLength(100)
                .HasColumnName("model_version");
            entity.Property(e => e.PracticeAnswerId).HasColumnName("practice_answer_id");
            entity.Property(e => e.PracticeSessionId).HasColumnName("practice_session_id");
            entity.Property(e => e.PromptVersion)
                .HasMaxLength(100)
                .HasColumnName("prompt_version");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.RoundIndex)
                .HasDefaultValue(1)
                .HasColumnName("round_index");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'GENERATED'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TriggerScore)
                .HasPrecision(5, 2)
                .HasColumnName("trigger_score");

            entity.HasOne(d => d.PracticeAnswer).WithMany(p => p.PracticeFollowupQuestions)
                .HasForeignKey(d => d.PracticeAnswerId)
                .HasConstraintName("practice_followup_questions_practice_answer_id_fkey");

            entity.HasOne(d => d.PracticeSession).WithMany(p => p.PracticeFollowupQuestions)
                .HasForeignKey(d => d.PracticeSessionId)
                .HasConstraintName("practice_followup_questions_practice_session_id_fkey");
        });

        modelBuilder.Entity<PracticeQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("practice_questions_pkey");

            entity.ToTable("practice_questions");

            entity.Property(e => e.QuestionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("question_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Difficulty)
                .HasMaxLength(20)
                .HasColumnName("difficulty");
            entity.Property(e => e.ModelAnswer).HasColumnName("model_answer");

            entity.HasOne(d => d.Course).WithMany(p => p.PracticeQuestions)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("practice_questions_course_id_fkey");
        });

        modelBuilder.Entity<PracticeSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("practice_sessions_pkey");

            entity.ToTable("practice_sessions");

            entity.HasIndex(e => e.StudentId, "ix_practice_sessions_student");

            entity.Property(e => e.SessionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("session_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Student).WithMany(p => p.PracticeSessions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("practice_sessions_student_id_fkey");
        });

        modelBuilder.Entity<RealExamAnswer>(entity =>
        {
            entity.HasKey(e => e.RealAnswerId).HasName("real_exam_answers_pkey");

            entity.ToTable("real_exam_answers");

            entity.HasIndex(e => e.ResultId, "ix_real_answers_result");

            entity.Property(e => e.RealAnswerId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("real_answer_id");
            entity.Property(e => e.AiEvidence).HasColumnName("ai_evidence");
            entity.Property(e => e.AiScore)
                .HasPrecision(5, 2)
                .HasColumnName("ai_score");
            entity.Property(e => e.AudioUrl).HasColumnName("audio_url");
            entity.Property(e => e.FinalScore)
                .HasPrecision(5, 2)
                .HasColumnName("final_score");
            entity.Property(e => e.FollowupQuestionId).HasColumnName("followup_question_id");
            entity.Property(e => e.ParentAnswerId).HasColumnName("parent_answer_id");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.RoundIndex)
                .HasDefaultValue(0)
                .HasColumnName("round_index");
            entity.Property(e => e.RubricSnapshot)
                .HasColumnType("jsonb")
                .HasColumnName("rubric_snapshot");
            entity.Property(e => e.TimestampsJson)
                .HasColumnType("jsonb")
                .HasColumnName("timestamps_json");
            entity.Property(e => e.Transcript).HasColumnName("transcript");

            entity.HasOne(d => d.FollowupQuestion).WithMany(p => p.RealExamAnswers)
                .HasForeignKey(d => d.FollowupQuestionId)
                .HasConstraintName("fk_real_answers_followup");

            entity.HasOne(d => d.ParentAnswer).WithMany(p => p.InverseParentAnswer)
                .HasForeignKey(d => d.ParentAnswerId)
                .HasConstraintName("real_exam_answers_parent_answer_id_fkey");

            entity.HasOne(d => d.Question).WithMany(p => p.RealExamAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("real_exam_answers_question_id_fkey");

            entity.HasOne(d => d.Result).WithMany(p => p.RealExamAnswers)
                .HasForeignKey(d => d.ResultId)
                .HasConstraintName("real_exam_answers_result_id_fkey");
        });

        modelBuilder.Entity<RealExamAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("real_exam_assignments_pkey");

            entity.ToTable("real_exam_assignments");

            entity.HasIndex(e => new { e.ShiftId, e.StudentId }, "uq_real_exam_assignments").IsUnique();

            entity.Property(e => e.AssignmentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("assignment_id");
            entity.Property(e => e.AttendanceStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NOT_STARTED'::character varying")
                .HasColumnName("attendance_status");
            entity.Property(e => e.BoothNumber).HasColumnName("booth_number");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");

            entity.HasOne(d => d.Shift).WithMany(p => p.RealExamAssignments)
                .HasForeignKey(d => d.ShiftId)
                .HasConstraintName("real_exam_assignments_shift_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.RealExamAssignments)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("real_exam_assignments_student_id_fkey");
        });

        modelBuilder.Entity<RealExamFollowupQuestion>(entity =>
        {
            entity.HasKey(e => e.FollowupId).HasName("real_exam_followup_questions_pkey");

            entity.ToTable("real_exam_followup_questions");

            entity.Property(e => e.FollowupId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("followup_id");
            entity.Property(e => e.ContextTranscript).HasColumnName("context_transcript");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ModelVersion)
                .HasMaxLength(100)
                .HasColumnName("model_version");
            entity.Property(e => e.PromptSnapshot).HasColumnName("prompt_snapshot");
            entity.Property(e => e.PromptVersion)
                .HasMaxLength(100)
                .HasColumnName("prompt_version");
            entity.Property(e => e.QuestionText).HasColumnName("question_text");
            entity.Property(e => e.RealAnswerId).HasColumnName("real_answer_id");
            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.RoundIndex)
                .HasDefaultValue(1)
                .HasColumnName("round_index");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'GENERATED'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TimeoutSec).HasColumnName("timeout_sec");

            entity.HasOne(d => d.RealAnswer).WithMany(p => p.RealExamFollowupQuestions)
                .HasForeignKey(d => d.RealAnswerId)
                .HasConstraintName("real_exam_followup_questions_real_answer_id_fkey");

            entity.HasOne(d => d.Result).WithMany(p => p.RealExamFollowupQuestions)
                .HasForeignKey(d => d.ResultId)
                .HasConstraintName("real_exam_followup_questions_result_id_fkey");
        });

        modelBuilder.Entity<RealExamResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("real_exam_results_pkey");

            entity.ToTable("real_exam_results");

            entity.HasIndex(e => e.AssignmentId, "real_exam_results_assignment_id_key").IsUnique();

            entity.Property(e => e.ResultId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("result_id");
            entity.Property(e => e.AiScore)
                .HasPrecision(5, 2)
                .HasColumnName("ai_score");
            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.FinalScore)
                .HasPrecision(5, 2)
                .HasColumnName("final_score");
            entity.Property(e => e.IsFlagged)
                .HasDefaultValue(false)
                .HasColumnName("is_flagged");
            entity.Property(e => e.IsLocked)
                .HasDefaultValue(false)
                .HasColumnName("is_locked");
            entity.Property(e => e.IsOverridden)
                .HasDefaultValue(false)
                .HasColumnName("is_overridden");
            entity.Property(e => e.LockedAt).HasColumnName("locked_at");
            entity.Property(e => e.MicCheckPassed)
                .HasDefaultValue(false)
                .HasColumnName("mic_check_passed");
            entity.Property(e => e.OverrideReason).HasColumnName("override_reason");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Sha256Hash)
                .HasMaxLength(128)
                .HasColumnName("sha256_hash");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'NOT_STARTED'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.ViolationCount)
                .HasDefaultValue(0)
                .HasColumnName("violation_count");

            entity.HasOne(d => d.Assignment).WithOne(p => p.RealExamResult)
                .HasForeignKey<RealExamResult>(d => d.AssignmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("real_exam_results_assignment_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.RealExamResults)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("real_exam_results_reviewed_by_fkey");
        });

        modelBuilder.Entity<RealExamSession>(entity =>
        {
            entity.HasKey(e => e.ExamSessionId).HasName("real_exam_sessions_pkey");

            entity.ToTable("real_exam_sessions");

            entity.Property(e => e.ExamSessionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("exam_session_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExamDate).HasColumnName("exam_date");
            entity.Property(e => e.ExamMode)
                .HasMaxLength(20)
                .HasDefaultValueSql("'FIXED'::character varying")
                .HasColumnName("exam_mode");
            entity.Property(e => e.StructureId).HasColumnName("structure_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");

            entity.HasOne(d => d.Class).WithMany(p => p.RealExamSessions)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("real_exam_sessions_class_id_fkey");

            entity.HasOne(d => d.Structure).WithMany(p => p.RealExamSessions)
                .HasForeignKey(d => d.StructureId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("real_exam_sessions_structure_id_fkey");
        });

        modelBuilder.Entity<RealExamSessionShift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("real_exam_session_shifts_pkey");

            entity.ToTable("real_exam_session_shifts");

            entity.Property(e => e.ShiftId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("shift_id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.ExamSessionId).HasColumnName("exam_session_id");
            entity.Property(e => e.LabRoom)
                .HasMaxLength(100)
                .HasColumnName("lab_room");
            entity.Property(e => e.ShiftName)
                .HasMaxLength(100)
                .HasColumnName("shift_name");
            entity.Property(e => e.StartTime).HasColumnName("start_time");

            entity.HasOne(d => d.ExamSession).WithMany(p => p.RealExamSessionShifts)
                .HasForeignKey(d => d.ExamSessionId)
                .HasConstraintName("real_exam_session_shifts_exam_session_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<RubricItem>(entity =>
        {
            entity.HasKey(e => e.RubricId).HasName("rubric_items_pkey");

            entity.ToTable("rubric_items");

            entity.HasIndex(e => e.ExamQuestionId, "ix_rubric_exam");

            entity.HasIndex(e => e.PracticeQuestionId, "ix_rubric_practice");

            entity.Property(e => e.RubricId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("rubric_id");
            entity.Property(e => e.CriteriaDescription).HasColumnName("criteria_description");
            entity.Property(e => e.ExamQuestionId).HasColumnName("exam_question_id");
            entity.Property(e => e.MaxScore)
                .HasPrecision(4, 2)
                .HasColumnName("max_score");
            entity.Property(e => e.PracticeQuestionId).HasColumnName("practice_question_id");

            entity.HasOne(d => d.ExamQuestion).WithMany(p => p.RubricItems)
                .HasForeignKey(d => d.ExamQuestionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("rubric_items_exam_question_id_fkey");

            entity.HasOne(d => d.PracticeQuestion).WithMany(p => p.RubricItems)
                .HasForeignKey(d => d.PracticeQuestionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("rubric_items_practice_question_id_fkey");
        });

        modelBuilder.Entity<Semester>(entity =>
        {
            entity.HasKey(e => e.SemesterId).HasName("semesters_pkey");

            entity.ToTable("semesters");

            entity.HasIndex(e => e.SemesterCode, "semesters_semester_code_key").IsUnique();

            entity.Property(e => e.SemesterId).HasColumnName("semester_id");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.SemesterCode)
                .HasMaxLength(50)
                .HasColumnName("semester_code");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasColumnName("username");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("users_role_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
