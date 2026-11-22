using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    partial void OnModelCreatingOfficialExamWave2(ModelBuilder modelBuilder)
    {
        // M1: official_exam_sessions — ExamAttemptType (FE/RE), default max_follow_up_questions = 2, partial unique & ESR index
        modelBuilder.Entity<OfficialExamSession>(entity =>
        {
            entity.ToTable("official_exam_sessions", t =>
            {
                t.HasCheckConstraint("ck_official_sessions_attempt_type", "exam_attempt_type IN ('FE', 'RE')");
            });

            entity.Property(e => e.ExamAttemptType)
                .HasColumnName("exam_attempt_type")
                .HasMaxLength(10)
                .HasDefaultValue(OfficialExamAttemptType.FinalExam)
                .IsRequired();

            entity.Property(e => e.MaxFollowUpQuestions)
                .HasColumnName("max_follow_up_questions")
                .HasDefaultValue(2);

            entity.HasIndex(e => new { e.SemesterId, e.CourseId, e.ExamAttemptType })
                .IsUnique()
                .HasDatabaseName("uq_official_exam_sessions_semester_course_attempt");

            entity.HasIndex(e => new { e.SemesterId, e.ExamAttemptType, e.ExamDate })
                .IsDescending(false, false, true)
                .HasDatabaseName("ix_official_exam_sessions_semester_attempt_date");
        });

        // M1: official_exam_candidates (3NF imported candidate roster per session)
        modelBuilder.Entity<OfficialExamCandidate>(entity =>
        {
            entity.ToTable("official_exam_candidates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(e => e.StudentId).HasColumnName("student_id").IsRequired();
            entity.Property(e => e.ImportedBy).HasColumnName("imported_by");
            entity.Property(e => e.ImportedAt).HasColumnName("imported_at").HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

            entity.HasIndex(e => new { e.SessionId, e.StudentId })
                .IsUnique()
                .HasDatabaseName("uq_official_exam_candidates_session_student");
            entity.HasIndex(e => e.StudentId)
                .HasDatabaseName("ix_official_exam_candidates_student");
            entity.HasIndex(e => e.ImportedBy)
                .HasDatabaseName("ix_official_exam_candidates_imported_by");
            entity.HasIndex(e => new { e.SessionId, e.ImportedAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_official_exam_candidates_session_imported_at");

            entity.HasOne(d => d.Session)
                .WithMany(p => p.Candidates)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Student)
                .WithMany()
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ImportedByUser)
                .WithMany()
                .HasForeignKey(d => d.ImportedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // M1 + M2: real_exam_session_shifts — distinct proctor/examiner CHECK, ESR indexes, PAdES certificate hash/thumbprint/signed_at
        modelBuilder.Entity<RealExamSessionShift>(entity =>
        {
            entity.ToTable("real_exam_session_shifts", t =>
            {
                t.HasCheckConstraint(
                    "ck_shifts_distinct_proctor_examiner",
                    "proctor_user_id IS NULL OR examiner_user_id IS NULL OR proctor_user_id <> examiner_user_id");
                t.HasCheckConstraint(
                    "ck_shifts_certificate_hash_len",
                    "certificate_hash IS NULL OR length(certificate_hash) = 64");
            });

            entity.Property(e => e.CertificateHash).HasColumnName("certificate_hash").HasMaxLength(64);
            entity.Property(e => e.CertificateThumbprint).HasColumnName("certificate_thumbprint").HasMaxLength(64);
            entity.Property(e => e.CertificateSignedAt).HasColumnName("certificate_signed_at");

            entity.HasIndex(e => new { e.SessionId, e.Status, e.StartTime, e.EndTime })
                .HasDatabaseName("ix_real_exam_shifts_session_status_time");
            entity.HasIndex(e => new { e.Status, e.StartTime, e.EndTime })
                .HasDatabaseName("ix_real_exam_shifts_status_time");
            entity.HasIndex(e => e.CertificateHash)
                .HasDatabaseName("ix_real_exam_shifts_certificate_hash");
        });

        // M2: student_exam_tickets — BYOD device binding, single-session SHA-256 hash & unlock audit
        modelBuilder.Entity<StudentExamTicket>(entity =>
        {
            entity.ToTable("student_exam_tickets", t =>
            {
                t.HasCheckConstraint("ck_tickets_device_unlock_count", "device_unlock_count >= 0");
                t.HasCheckConstraint(
                    "ck_tickets_device_binding_consistency",
                    "(device_id IS NULL AND device_session_token_hash IS NULL) OR (device_id IS NOT NULL)");
            });

            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(128);
            entity.Property(e => e.DeviceLockedAt).HasColumnName("device_locked_at");
            entity.Property(e => e.DeviceSessionTokenHash).HasColumnName("device_session_token_hash").HasMaxLength(128);
            entity.Property(e => e.DeviceUnlockCount).HasColumnName("device_unlock_count").HasDefaultValue(0).IsRequired();
            entity.Property(e => e.LastDeviceUnlockedBy).HasColumnName("last_device_unlocked_by");
            entity.Property(e => e.LastDeviceUnlockedAt).HasColumnName("last_device_unlocked_at");

            entity.HasIndex(e => new { e.ShiftId, e.DeviceId })
                .IsUnique()
                .HasFilter("device_id IS NOT NULL")
                .HasDatabaseName("uq_tickets_shift_device");
            entity.HasIndex(e => e.DeviceId)
                .HasFilter("device_id IS NOT NULL")
                .HasDatabaseName("ix_student_exam_tickets_device_id");
            entity.HasIndex(e => e.LastDeviceUnlockedBy)
                .HasDatabaseName("ix_student_exam_tickets_last_unlocked_by");

            entity.HasOne(d => d.LastDeviceUnlockedByUser)
                .WithMany()
                .HasForeignKey(d => d.LastDeviceUnlockedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // M2: exam_anti_cheat_logs — client_event_id offline idempotency & ESR indexes
        modelBuilder.Entity<ExamAntiCheatLog>(entity =>
        {
            entity.Property(e => e.ClientEventId).HasColumnName("client_event_id").HasMaxLength(128);

            entity.HasIndex(e => new { e.TicketId, e.ClientEventId })
                .IsUnique()
                .HasFilter("client_event_id IS NOT NULL")
                .HasDatabaseName("uq_exam_anti_cheat_logs_ticket_client_event");
            entity.HasIndex(e => new { e.TicketId, e.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_exam_anti_cheat_logs_ticket_occurred");
            entity.HasIndex(e => new { e.ShiftId, e.OccurredAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_exam_anti_cheat_logs_shift_occurred");
        });

        // M2: exam_question_submissions — MF-04 Follow-up 3NF self-reference, R2 per-turn audio & edit-ratio cap
        modelBuilder.Entity<ExamQuestionSubmission>(entity =>
        {
            entity.ToTable("exam_question_submissions", t =>
            {
                t.HasCheckConstraint(
                    "ck_exam_question_submissions_follow_up_index",
                    "follow_up_index >= 0 AND follow_up_index <= 5");
                t.HasCheckConstraint(
                    "ck_exam_question_submissions_follow_up_consistency",
                    "(is_follow_up = false AND parent_submission_id IS NULL AND follow_up_index = 0 AND follow_up_prompt IS NULL) OR (is_follow_up = true AND parent_submission_id IS NOT NULL AND follow_up_index >= 1 AND follow_up_index <= 5 AND follow_up_prompt IS NOT NULL AND btrim(follow_up_prompt) <> '')");
                t.HasCheckConstraint(
                    "ck_exam_question_submissions_transcript_edit_ratio",
                    "transcript_edit_ratio IS NULL OR (transcript_edit_ratio >= 0 AND transcript_edit_ratio <= 1)");
            });

            entity.Property(e => e.IsFollowUp).HasColumnName("is_follow_up").HasDefaultValue(false).IsRequired();
            entity.Property(e => e.ParentSubmissionId).HasColumnName("parent_submission_id");
            entity.Property(e => e.FollowUpIndex).HasColumnName("follow_up_index").HasDefaultValue(0).IsRequired();
            entity.Property(e => e.FollowUpPrompt).HasColumnName("follow_up_prompt");
            entity.Property(e => e.GeneratedFollowUpPrompt).HasColumnName("generated_follow_up_prompt");
            entity.Property(e => e.InitialVoiceTranscript).HasColumnName("initial_voice_transcript");
            entity.Property(e => e.TranscriptEditRatio).HasColumnName("transcript_edit_ratio").HasPrecision(5, 4);
            entity.Property(e => e.AudioObjectKey).HasColumnName("audio_object_key").HasMaxLength(512);
            entity.Property(e => e.AudioVerifiedAt).HasColumnName("audio_verified_at");

            entity.HasIndex(e => new { e.TicketId, e.ExamQuestionId, e.FollowUpIndex })
                .IsUnique()
                .HasDatabaseName("uq_exam_submissions_ticket_question_turn");
            entity.HasIndex(e => new { e.TicketId, e.ParentSubmissionId })
                .IsUnique()
                .HasFilter("is_follow_up = true AND parent_submission_id IS NOT NULL")
                .HasDatabaseName("uq_exam_submissions_ticket_parent");
            entity.HasIndex(e => e.ParentSubmissionId)
                .HasDatabaseName("ix_exam_question_submissions_parent");

            entity.HasOne(d => d.ParentSubmission)
                .WithMany(p => p.FollowUpSubmissions)
                .HasForeignKey(d => d.ParentSubmissionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_exam_question_submissions_parent_submission_id");
        });
    }
}
