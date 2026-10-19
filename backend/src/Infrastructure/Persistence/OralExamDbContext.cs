using System;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext : DbContext, IApplicationDbContext
{
    public OralExamDbContext(DbContextOptions<OralExamDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
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
    public virtual DbSet<GradingJob> GradingJobs { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<SystemConfig> SystemConfigs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_role", "role IN ('admin', 'department_head', 'lecturer', 'proctor', 'student')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("ix_users_email");
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(e => e.StudentCode).HasColumnName("student_code").HasMaxLength(20);
            entity.HasIndex(e => e.StudentCode).IsUnique().HasDatabaseName("ix_users_student_code");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(32);
            entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("ix_users_username");
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20).HasDefaultValue("student").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasData(
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Email = "admin@fpt.edu.vn",
                    FullName = "Nguyen Quang Thanh",
                    Username = "admin",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Admin!", "admin"),
                    MustChangePassword = false,
                    Role = "admin",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Email = "headse@fe.edu.vn",
                    FullName = "Tran Van Truong",
                    Username = "headse",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Head!", "headse"),
                    MustChangePassword = false,
                    Role = "department_head",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Email = "totnt@fe.edu.vn",
                    FullName = "Nguyen Trong Tot",
                    Username = "totnt",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Lecturer!", "totnt"),
                    MustChangePassword = false,
                    Role = "lecturer",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                    Email = "haind@fe.edu.vn",
                    FullName = "Nguyen Dang Hai",
                    Username = "haind",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-Proctor!", "haind"),
                    MustChangePassword = false,
                    Role = "proctor",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    Email = "hoanglvse170001@fpt.edu.vn",
                    FullName = "Le Vu Hoang",
                    StudentCode = "SE170001",
                    Username = "hoalvse170001",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170001!", "SE170001"),
                    MustChangePassword = true,
                    Role = "student",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                    Email = "maittse170002@fpt.edu.vn",
                    FullName = "Tran Thi Mai",
                    StudentCode = "SE170002",
                    Username = "maittse170002",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE170002!", "SE170002"),
                    MustChangePassword = true,
                    Role = "student",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-00000000000b"),
                    Email = "thanhnqse184236@fpt.edu.vn",
                    FullName = "Nguyen Quang Thanh",
                    StudentCode = "SE184236",
                    Username = "thanhnqse184236",
                    PasswordHash = Pbkdf2PasswordHasher.HashForSeed("ChangeMe-SE184236!", "SE184236"),
                    MustChangePassword = false,
                    Role = "student",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 1b. refresh_tokens
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("ix_refresh_tokens_token_hash");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(128);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_refresh_tokens_user");

            entity.HasOne(d => d.User)
                .WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 2. semesters
        modelBuilder.Entity<Semester>(entity =>
        {
            entity.ToTable("semesters", t =>
            {
                t.HasCheckConstraint("ck_semesters_dates", "end_date >= start_date");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ix_semesters_code");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasData(new Semester
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                Code = "FA26",
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31),
                IsActive = true,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 3. courses
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses", t =>
            {
                t.HasCheckConstraint("ck_courses_credits", "credits > 0");
                t.HasCheckConstraint("ck_courses_transcript_buffer", "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");
                t.HasCheckConstraint("ck_courses_max_follow_up", "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");
                t.HasCheckConstraint("ck_courses_mock_quota", "max_mock_exams_per_day >= 1");
                t.HasCheckConstraint("ck_courses_input_mode", "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");
            });
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
            entity.Property(e => e.MaxMockExamsPerDay).HasColumnName("max_mock_exams_per_day").HasDefaultValue(3);
            entity.Property(e => e.ExamInputMode)
                .HasColumnName("exam_input_mode")
                .HasMaxLength(30)
                .HasDefaultValue("VoiceWithTranscriptEdit")
                .IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Semester)
                .WithMany(p => p.Courses)
                .HasForeignKey(d => d.SemesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new Course
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    Code = "PRN231",
                    Name = "Building Cross-Platform Back-End Applications with .NET",
                    Credits = 3,
                    SemesterId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                    HasFollowUp = true,
                    TranscriptBufferSeconds = 60,
                    MaxFollowUpQuestions = 2,
                    MaxMockExamsPerDay = 3,
                    ExamInputMode = "VoiceWithTranscriptEdit",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Course
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000009"),
                    Code = "SWD392",
                    Name = "Software Architecture and Design",
                    Credits = 3,
                    SemesterId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                    HasFollowUp = true,
                    TranscriptBufferSeconds = 60,
                    MaxFollowUpQuestions = 2,
                    MaxMockExamsPerDay = 3,
                    ExamInputMode = "VoiceWithTranscriptEdit",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 4. classes
        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes", t =>
            {
                t.HasCheckConstraint("ck_classes_max_students", "max_students > 0");
            });
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

            entity.HasData(new Class
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Code = "SE1801-NET",
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                SemesterId = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                LecturerId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                MaxStudents = 35,
                IsActive = true,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 5. class_enrollments
        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.ToTable("class_enrollments", t =>
            {
                t.HasCheckConstraint("ck_class_enrollment_status", "status IN ('enrolled', 'dropped', 'completed')");
            });
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

            entity.HasData(
                new ClassEnrollment
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                    ClassId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                    StudentId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    Status = "enrolled",
                    EnrolledAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ClassEnrollment
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                    ClassId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                    StudentId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                    Status = "enrolled",
                    EnrolledAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 6. rubrics
        modelBuilder.Entity<Rubric>(entity =>
        {
            entity.ToTable("rubrics", t =>
            {
                t.HasCheckConstraint("ck_rubrics_total_score", "total_max_score = 10.00");
            });
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

            entity.HasData(new Rubric
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                Name = "Barem Đánh Giá Kiến Trúc .NET 8",
                Description = "Đánh giá Clean Architecture",
                TotalMaxScore = 10.00m,
                IsActive = true,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 7. rubric_criteria
        modelBuilder.Entity<RubricCriterion>(entity =>
        {
            entity.ToTable("rubric_criteria", t =>
            {
                t.HasCheckConstraint("ck_rubric_criteria_score", "max_score > 0 AND max_score <= 10.00");
                t.HasCheckConstraint("ck_rubric_criteria_weight", "weight > 0 AND weight <= 1.00");
            });
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

            entity.HasData(
                new RubricCriterion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000014"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    CriterionName = "Khái niệm & Ranh giới 4 tầng Clean Architecture",
                    Description = "Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.",
                    MaxScore = 3.00m,
                    Weight = 0.30m,
                    BloomLevel = "Understand",
                    OrderIndex = 1,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new RubricCriterion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    CriterionName = "Nguyên lý Dependency Inversion & CQRS MediatR",
                    Description = "Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.",
                    MaxScore = 4.00m,
                    Weight = 0.40m,
                    BloomLevel = "Apply",
                    OrderIndex = 2,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new RubricCriterion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    CriterionName = "Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)",
                    Description = "Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.",
                    MaxScore = 3.00m,
                    Weight = 0.30m,
                    BloomLevel = "Analyze",
                    OrderIndex = 3,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 8. practice_questions
        modelBuilder.Entity<PracticeQuestion>(entity =>
        {
            entity.ToTable("practice_questions", t =>
            {
                t.HasCheckConstraint("ck_practice_questions_diff", "difficulty IN ('easy', 'medium', 'hard')");
                t.HasCheckConstraint("ck_practice_questions_bloom", "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");
            });
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

            entity.HasData(
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Nguyên lý Dependency Inversion",
                    Content = "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.",
                    SampleAnswer = "Module cấp cao phụ thuộc abstraction. Application khai báo interface, Infrastructure hiện thực.",
                    KeyPoints = "[\"Module cấp cao không phụ thuộc module cấp thấp\",\"Phụ thuộc vào Abstraction/Interface\"]",
                    Difficulty = "medium",
                    BloomLevel = "Apply",
                    HasFollowUp = true,
                    FollowUpPrompt = "Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000101"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Vai trò tầng Domain trong Clean Architecture",
                    Content = "Tầng Domain trong Clean Architecture chứa những thành phần nào và tại sao không được tham chiếu tới các tầng khác?",
                    SampleAnswer = "Tầng Domain chứa Entities, Value Objects, Domain Events và Enums cốt lõi. Không phụ thuộc vào framework hay database để bảo toàn quy tắc nghiệp vụ.",
                    KeyPoints = "[\"Chứa Entities và quy tắc cốt lõi\",\"Không phụ thuộc Infrastructure hay UI\"]",
                    Difficulty = "easy",
                    BloomLevel = "Understand",
                    HasFollowUp = true,
                    FollowUpPrompt = "Làm thế nào để tầng Domain giao tiếp với các dịch vụ bên ngoài mà không phá vỡ tính độc lập?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Phân biệt Command và Query trong mẫu CQRS",
                    Content = "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.",
                    SampleAnswer = "Command dùng để thay đổi trạng thái dữ liệu (Create, Update, Delete), trong khi Query chỉ dùng để truy vấn đọc dữ liệu và không làm biến đổi trạng thái.",
                    KeyPoints = "[\"Command thay đổi trạng thái\",\"Query chỉ đọc dữ liệu\",\"Tách biệt mô hình đọc và ghi\"]",
                    Difficulty = "easy",
                    BloomLevel = "Understand",
                    HasFollowUp = true,
                    FollowUpPrompt = "Khi nào nên sử dụng AsNoTracking() trong các Query Handler?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000103"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Cơ chế Pipeline Behavior với FluentValidation",
                    Content = "Cách thức hoạt động của IPipelineBehavior trong MediatR kết hợp với FluentValidation để kiểm tra dữ liệu đầu vào trước khi vào Handler?",
                    SampleAnswer = "ValidationBehavior chặn request trước khi tới Handler, chạy toàn bộ IValidator<TRequest> đã đăng ký và ném ValidationException nếu có lỗi.",
                    KeyPoints = "[\"Chặn trước khi vào Handler\",\"Chạy tất cả IValidator\",\"Trả lỗi chuẩn RFC 7807\"]",
                    Difficulty = "medium",
                    BloomLevel = "Apply",
                    HasFollowUp = true,
                    FollowUpPrompt = "Ưu điểm của việc đặt validation ở tầng Application thay vì chỉ đặt ở Controller là gì?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000104"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Thiết kế hàng đợi BoundedChannel và xử lý Dead-Letter Queue",
                    Content = "Phân tích cách sử dụng BoundedChannel kết hợp BackgroundService và Polly Retry để xử lý chấm điểm bất đồng bộ chịu tải cao.",
                    SampleAnswer = "BoundedChannel giới hạn số lượng slot trong RAM để áp dụng backpressure (FullMode.Wait). BackgroundService đọc tuần tự và dùng Polly retry, nếu vẫn lỗi thì lưu vào bảng dead_letter_queues.",
                    KeyPoints = "[\"BoundedChannel chống tràn RAM\",\"BackgroundService xử lý nền\",\"Polly Retry và DLQ bảo toàn dữ liệu\"]",
                    Difficulty = "hard",
                    BloomLevel = "Analyze",
                    HasFollowUp = true,
                    FollowUpPrompt = "Tại sao phải tạo IServiceScope mới bên trong BackgroundService mỗi khi xử lý một item?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new PracticeQuestion
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000105"),
                    CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                    RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                    Title = "Tối ưu hóa truy vấn EF Core 8 và phòng chống lỗi N+1 Query",
                    Content = "Phân tích nguyên nhân gây ra lỗi N+1 Query trong Entity Framework Core và các giải pháp khắc phục hiệu quả.",
                    SampleAnswer = "Lỗi N+1 xảy ra khi truy vấn danh sách thực thể cha rồi lặp qua từng phần tử để truy vấn thực thể con. Khắc phục bằng Eager Loading (Include/ThenInclude) hoặc Projection (Select).",
                    KeyPoints = "[\"Nguyên nhân do truy vấn trong vòng lặp\",\"Dùng Include/ThenInclude\",\"Dùng Select Projection và AsNoTracking\"]",
                    Difficulty = "hard",
                    BloomLevel = "Analyze",
                    HasFollowUp = true,
                    FollowUpPrompt = "Khi nào nên dùng AsSplitQuery() khi sử dụng nhiều lệnh Include?",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 9. exam_questions
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.ToTable("exam_questions", t =>
            {
                t.HasCheckConstraint("ck_exam_questions_diff", "difficulty IN ('easy', 'medium', 'hard')");
                t.HasCheckConstraint("ck_exam_questions_bloom", "bloom_level IN ('Remember', 'Understand', 'Apply', 'Analyze', 'Evaluate', 'Create')");
            });
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

            entity.HasData(new ExamQuestion
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                RubricId = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                Title = "Phân biệt BackgroundService và IHostedService trong .NET 8",
                Content = "Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?",
                SampleAnswer = "IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.",
                KeyPoints = "[\"IHostedService là interface cốt lõi\",\"BackgroundService là abstract class kế thừa IHostedService\",\"ExecuteAsync chạy Task bất đồng bộ\"]",
                Difficulty = "hard",
                BloomLevel = "Analyze",
                ApprovedBy = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Source = "manual",
                ApprovalStatus = "APPROVED",
                IsActive = true,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 10. practice_sessions
        modelBuilder.Entity<PracticeSession>(entity =>
        {
            entity.ToTable("practice_sessions", t =>
            {
                t.HasCheckConstraint("ck_practice_sessions_mode", "practice_mode IN ('per_question', 'full_session')");
                t.HasCheckConstraint("ck_practice_sessions_status", "status IN ('in_progress', 'completed', 'abandoned', 'timed_out')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.PracticeMode).HasColumnName("practice_mode").HasMaxLength(20).HasDefaultValue("per_question");
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("in_progress");
            entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.SelectedDifficulties).HasColumnName("selected_difficulties").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.HasIndex(e => new { e.StudentId, e.StartedAt }).HasDatabaseName("ix_practice_sessions_student");
            entity.HasIndex(e => e.CourseId).HasDatabaseName("ix_practice_sessions_course");
            entity.HasIndex(e => new { e.Status, e.LastActivityAt }).HasDatabaseName("ix_practice_sessions_last_activity");

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
            entity.ToTable("practice_answers", t =>
            {
                t.HasCheckConstraint("ck_practice_answers_status", "status IN ('pending', 'grading', 'graded', 'failed')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.AnswerText).HasColumnName("answer_text").IsRequired();
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
            entity.ToTable("ai_evaluations", t =>
            {
                t.HasCheckConstraint("ck_ai_evaluations_type", "answer_type IN ('practice', 'exam')");
                t.HasCheckConstraint("ck_ai_evaluations_score", "total_score >= 0 AND total_score <= 10.00");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AnswerType).HasColumnName("answer_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.PracticeAnswerId).HasColumnName("practice_answer_id");
            entity.Property(e => e.ExamSubmissionId).HasColumnName("exam_submission_id");
            entity.Property(e => e.MockExamAnswerId).HasColumnName("mock_exam_answer_id");
            entity.Property(e => e.TotalScore).HasColumnName("total_score").HasPrecision(4, 2).IsRequired();
            entity.Property(e => e.Feedback).HasColumnName("feedback");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score").HasPrecision(3, 2);
            entity.Property(e => e.CotTrace).HasColumnName("cot_trace");
            entity.Property(e => e.EvaluatedAt).HasColumnName("evaluated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.PracticeAnswerId).HasDatabaseName("ix_ai_evaluations_practice_answer");
            entity.HasIndex(e => e.ExamSubmissionId)
                .IsUnique()
                .HasFilter("exam_submission_id IS NOT NULL")
                .HasDatabaseName("uq_ai_evaluations_exam_submission");
            entity.HasIndex(e => e.MockExamAnswerId).HasDatabaseName("ix_ai_evaluations_mock_exam_answer");

            entity.HasOne(d => d.PracticeAnswer)
                .WithMany(p => p.AiEvaluations)
                .HasForeignKey(d => d.PracticeAnswerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ExamSubmission)
                .WithMany(p => p.AiEvaluations)
                .HasForeignKey(d => d.ExamSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.MockExamAnswer)
                .WithMany(p => p.AiEvaluations)
                .HasForeignKey(d => d.MockExamAnswerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. ai_evaluation_details
        modelBuilder.Entity<AiEvaluationDetail>(entity =>
        {
            entity.ToTable("ai_evaluation_details", t =>
            {
                t.HasCheckConstraint("ck_ai_eval_detail_score", "score >= 0");
            });
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
            entity.ToTable("exam_structures", t =>
            {
                t.HasCheckConstraint("ck_exam_struct_duration", "duration_minutes > 0");
                t.HasCheckConstraint("ck_exam_struct_questions", "total_questions > 0");
            });
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

            entity.HasData(new ExamStructure
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                CreatedBy = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)",
                TotalQuestions = 5,
                DurationMinutes = 30,
                BloomDistribution = "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}",
                IsActive = true,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
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

            entity.HasData(new ExamSet
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                StructureId = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                SetCode = "PRN231-SET01",
                GeneratedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
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

            entity.HasData(new ExamSetQuestion
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000021"),
                ExamSetId = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                ExamQuestionId = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                OrderIndex = 1
            });
        });

        // 17. mock_exam_quotas
        modelBuilder.Entity<MockExamQuota>(entity =>
        {
            entity.ToTable("mock_exam_quotas", t =>
            {
                t.HasCheckConstraint("ck_mock_exam_quotas_count", "used_count >= 0");
            });
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
            entity.ToTable("mock_exam_sessions", t =>
            {
                t.HasCheckConstraint("ck_mock_sessions_status", "status IN ('in_progress', 'submitted', 'graded', 'abandoned')");
            });
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
            entity.ToTable("mock_exam_answers", t =>
            {
                t.HasCheckConstraint("ck_mock_answers_status", "status IN ('pending', 'answered', 'graded', 'failed')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(e => e.AnswerText).HasColumnName("answer_text");
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
            entity.ToTable("official_exam_sessions", t =>
            {
                t.HasCheckConstraint("ck_official_sessions_status", "status IN ('scheduled', 'in_progress', 'grading', 'auditing', 'concluded')");
                t.HasCheckConstraint("ck_official_sessions_max_follow_up", "max_follow_up_questions >= 1 AND max_follow_up_questions <= 5");
                t.HasCheckConstraint("ck_official_sessions_input_mode", "exam_input_mode IN ('VoiceOnly', 'VoiceWithTranscriptEdit')");
                t.HasCheckConstraint("ck_official_sessions_transcript_buffer", "transcript_buffer_seconds >= 10 AND transcript_buffer_seconds <= 300");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CourseId).HasColumnName("course_id").IsRequired();
            entity.Property(e => e.ExamStructureId).HasColumnName("exam_structure_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ExamDate).HasColumnName("exam_date").IsRequired();
            entity.Property(e => e.HasFollowUp).HasColumnName("has_follow_up").HasDefaultValue(false);
            entity.Property(e => e.MaxFollowUpQuestions).HasColumnName("max_follow_up_questions").HasDefaultValue(1);
            entity.Property(e => e.ExamInputMode).HasColumnName("exam_input_mode").HasMaxLength(30).HasDefaultValue("VoiceWithTranscriptEdit").IsRequired();
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

            entity.HasData(new OfficialExamSession
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                CourseId = Guid.Parse("00000000-0000-0000-0000-000000000008"),
                ExamStructureId = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                Title = "Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng",
                ExamDate = new DateOnly(2026, 10, 15),
                HasFollowUp = true,
                MaxFollowUpQuestions = 2,
                ExamInputMode = "VoiceWithTranscriptEdit",
                TranscriptBufferSeconds = 60,
                Status = "scheduled",
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 21. real_exam_session_shifts
        modelBuilder.Entity<RealExamSessionShift>(entity =>
        {
            entity.ToTable("real_exam_session_shifts", t =>
            {
                t.HasCheckConstraint("ck_shifts_time", "end_time > start_time");
                t.HasCheckConstraint("ck_shifts_status", "status IN ('scheduled', 'in_progress', 'completed', 'cancelled')");
            });
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

            entity.HasData(new RealExamSessionShift
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                SessionId = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                ShiftName = "Kíp 1 (08h00 - 09h30)",
                RoomLab = "Lab 301",
                StartTime = new DateTime(2026, 10, 15, 1, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 10, 15, 2, 30, 0, DateTimeKind.Utc),
                ProctorUserId = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Status = "scheduled",
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 22. student_exam_tickets
        modelBuilder.Entity<StudentExamTicket>(entity =>
        {
            entity.ToTable("student_exam_tickets", t =>
            {
                t.HasCheckConstraint("ck_tickets_seat", "seat_number >= 1 AND seat_number <= 40");
                t.HasCheckConstraint("ck_tickets_status", "status IN ('SCHEDULED', 'IN_PROGRESS', 'SUBMITTED', 'AI_GRADED', 'AUDITED', 'PUBLISHED', 'LOCKED')");
                t.HasCheckConstraint("ck_tickets_ack_status", "student_acknowledgement_status IN ('PENDING', 'ACKNOWLEDGED', 'APPEALED')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ExamSetId).HasColumnName("exam_set_id");
            entity.Property(e => e.SeatNumber).HasColumnName("seat_number").IsRequired();
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.AudioObjectKey).HasColumnName("audio_object_key").HasMaxLength(512);
            entity.Property(e => e.AudioR2Url).HasColumnName("audio_r2_url").HasMaxLength(1024);
            entity.Property(e => e.AudioHashSha256).HasColumnName("audio_hash_sha256").HasMaxLength(64);
            entity.Property(e => e.AudioVerifiedAt).HasColumnName("audio_verified_at");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("SCHEDULED");
            entity.Property(e => e.StudentAcknowledgementStatus).HasColumnName("student_acknowledgement_status").HasMaxLength(20).HasDefaultValue("PENDING").IsRequired();
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

            entity.HasData(
                new StudentExamTicket
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000024"),
                    ShiftId = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                    StudentId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    ExamSetId = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                    SeatNumber = 1,
                    IpAddress = "192.168.1.101",
                    Status = "SCHEDULED",
                    StudentAcknowledgementStatus = "PENDING",
                    IsLocked = false,
                    CheckedInAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new StudentExamTicket
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000025"),
                    ShiftId = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                    StudentId = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                    ExamSetId = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                    SeatNumber = 2,
                    IpAddress = "192.168.1.102",
                    Status = "SCHEDULED",
                    StudentAcknowledgementStatus = "PENDING",
                    IsLocked = false,
                    CheckedInAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        // 23. exam_question_submissions
        modelBuilder.Entity<ExamQuestionSubmission>(entity =>
        {
            entity.ToTable("exam_question_submissions", t =>
            {
                t.HasCheckConstraint("ck_submissions_grading_status", "grading_status IN ('pending', 'transcribing', 'transcribed', 'ai_graded', 'audited', 'failed')");
            });
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
            entity.Property(e => e.IsSuspicious).HasColumnName("is_suspicious").HasDefaultValue(false);
            entity.Property(e => e.SuspiciousReason).HasColumnName("suspicious_reason");
            entity.Property(e => e.GradingStartedAt).HasColumnName("grading_started_at");
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

        modelBuilder.Entity<GradingJob>(entity =>
        {
            entity.ToTable("grading_jobs", t =>
            {
                t.HasCheckConstraint("ck_grading_jobs_status", "status IN ('pending', 'processing', 'completed', 'failed')");
                t.HasCheckConstraint("ck_grading_jobs_attempt_count", "attempt_count >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
            entity.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
            entity.Property(e => e.LastError).HasColumnName("last_error");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.HasIndex(e => e.SubmissionId).IsUnique().HasDatabaseName("uq_grading_jobs_submission");
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt, e.LockedUntil }).HasDatabaseName("ix_grading_jobs_dispatch");
            entity.HasOne(e => e.Submission)
                .WithOne(e => e.GradingJob)
                .HasForeignKey<GradingJob>(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 24. lecturer_audits
        modelBuilder.Entity<LecturerAudit>(entity =>
        {
            entity.ToTable("lecturer_audits", t =>
            {
                t.HasCheckConstraint("ck_lecturer_audits_scores", "original_ai_score >= 0 AND audited_score >= 0");
            });
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
            entity.ToTable("lecturer_audit_details", t =>
            {
                t.HasCheckConstraint("ck_audit_details_scores", "ai_score >= 0 AND audited_score >= 0");
            });
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
            entity.ToTable("dead_letter_queues", t =>
            {
                t.HasCheckConstraint("ck_dlq_status", "status IN ('pending', 'resolved', 'abandoned')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TaskType).HasColumnName("task_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").IsRequired();
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.NextRetryAt).HasColumnName("next_retry_at");
            entity.Property(e => e.LastAttemptAt).HasColumnName("last_attempt_at");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_dead_letter_queues_status");
            entity.HasIndex(e => new { e.Status, e.NextRetryAt }).HasDatabaseName("ix_dead_letter_queues_retry");
            entity.HasIndex(e => new { e.TaskType, e.CorrelationId, e.Status }).HasDatabaseName("ix_dead_letter_queues_correlation");
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
            entity.ToTable("appeal_requests", t =>
            {
                t.HasCheckConstraint("ck_appeal_requests_status", "status IN ('PENDING', 'IN_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED')");
                t.HasCheckConstraint("ck_appeal_requests_scores", "(original_score IS NULL OR (original_score >= 0 AND original_score <= 10.00)) AND (proposed_score IS NULL OR (proposed_score >= 0 AND proposed_score <= 10.00))");
            });
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

            entity.HasData(new AppealRequest
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000026"),
                TicketId = Guid.Parse("00000000-0000-0000-0000-000000000024"),
                StudentId = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                SessionId = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                Reason = "Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic",
                Status = "PENDING",
                AssignedTo = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                OriginalScore = 7.50m,
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        // 29. system_configs
        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("system_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique().HasDatabaseName("ix_system_configs_key");
            entity.Property(e => e.Value).HasColumnName("value").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasData(
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                    Key = "MaxPracticeQuestionsPerSession",
                    Value = "10",
                    Description = "Số lượng câu hỏi luyện tập tối đa trong một phiên do Admin cấu hình",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                    Key = "MinMixedPracticeQuestions",
                    Value = "3",
                    Description = "Số lượng câu hỏi luyện tập tối thiểu cho chế độ Dễ đến Khó do Admin cấu hình",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                    Key = "MaxMixedPracticeQuestions",
                    Value = "10",
                    Description = "Số lượng câu hỏi luyện tập tối đa cho chế độ Dễ đến Khó do Admin cấu hình",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                    Key = "TranscriptBufferSeconds",
                    Value = "60",
                    Description = "Thời gian đệm hiệu đính transcript mặc định (giây) do Admin cấu hình",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000031"),
                    Key = "MaxPracticeFollowUpQuestions",
                    Value = "2",
                    Description = "Số lượng câu hỏi follow-up luyện tập tối đa do Admin cấu hình (1-5 câu)",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new SystemConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000032"),
                    Key = "SessionInactivityTimeoutMinutes",
                    Value = "5",
                    Description = "Thời gian timeout không tương tác của phiên luyện tập (phút)",
                    UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });
    }
}
