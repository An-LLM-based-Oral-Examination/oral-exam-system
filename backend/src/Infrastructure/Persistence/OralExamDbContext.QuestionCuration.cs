using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    partial void OnModelCreatingQuestionCuration(ModelBuilder modelBuilder)
    {
        // M3: MF-03 ExamQuestion Bank Curation + pg_trgm similarity handoff (#7 §2.8, #10 DB-3)
        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.ToTable("exam_questions", t =>
            {
                t.HasCheckConstraint(
                    "ck_exam_questions_bank_curation_status",
                    "bank_curation_status IN ('BANK_READY', 'PENDING_CURATION', 'CURATED_INTO_BANK', 'EXCLUDED_FROM_BANK')");
            });

            entity.Property(e => e.BankCurationStatus)
                .HasColumnName("bank_curation_status")
                .HasMaxLength(30)
                .HasDefaultValue(BankCurationStatus.BankReady)
                .IsRequired();

            entity.Property(e => e.CuratedBy).HasColumnName("curated_by");
            entity.Property(e => e.CuratedAt).HasColumnName("curated_at");

            entity.HasIndex(e => new { e.CourseId, e.IsDeleted, e.IsActive, e.ApprovalStatus, e.BankCurationStatus })
                .HasDatabaseName("ix_exam_questions_course_curation_pool");

            entity.HasIndex(e => e.CuratedBy)
                .HasDatabaseName("ix_exam_questions_curated_by");

            entity.HasOne(d => d.CuratedByUser)
                .WithMany()
                .HasForeignKey(d => d.CuratedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_exam_questions_users_curated_by");
        });

        // M3: ESR Index on exam_sets for yellow-tag <= 70% overlap check & post-approval hiding
        modelBuilder.Entity<ExamSet>(entity =>
        {
            entity.HasIndex(e => new { e.StructureId, e.IsActive, e.Status, e.IsSampleSet })
                .HasDatabaseName("ix_exam_sets_structure_status_active");
        });
    }
}
