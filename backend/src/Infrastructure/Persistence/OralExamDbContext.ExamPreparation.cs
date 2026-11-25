using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    private static void ConfigureExamPreparation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamQuestion>().Property(x => x.IsPaperCopy).HasColumnName("is_paper_copy");
        var preparation = modelBuilder.Entity<ExamPreparation>();
        preparation.ToTable("exam_preparations", t => t.HasCheckConstraint("ck_exam_preparation_count", "required_set_count > 0"));
        preparation.HasKey(x => x.Id);
        preparation.HasIndex(x => x.SessionId).IsUnique();
        preparation.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        preparation.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        preparation.HasOne<User>().WithMany().HasForeignKey(x => x.HandedOverBy).OnDelete(DeleteBehavior.Restrict);
        preparation.Property(x => x.Notes).HasMaxLength(2000);

        var assignment = modelBuilder.Entity<ExamAuthoringAssignment>();
        assignment.ToTable("exam_authoring_assignments", t => t.HasCheckConstraint("ck_exam_assignment_count", "set_count > 0"));
        assignment.HasKey(x => x.Id);
        assignment.HasIndex(x => new { x.PreparationId, x.LecturerId, x.StructureId }).IsUnique();
        assignment.HasOne(x => x.Preparation).WithMany().HasForeignKey(x => x.PreparationId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne(x => x.Structure).WithMany().HasForeignKey(x => x.StructureId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<User>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        assignment.Property(x => x.Notes).HasMaxLength(2000);

        var scope = modelBuilder.Entity<ExamStructureClo>();
        scope.ToTable("exam_structure_clos");
        scope.HasKey(x => new { x.StructureId, x.CloId });
        scope.HasOne(x => x.Structure).WithMany().HasForeignKey(x => x.StructureId).OnDelete(DeleteBehavior.Restrict);
        scope.HasOne(x => x.Clo).WithMany().HasForeignKey(x => x.CloId).OnDelete(DeleteBehavior.Restrict);

        var revision = modelBuilder.Entity<ExamSetRevision>();
        revision.ToTable("exam_set_revisions");
        revision.HasKey(x => x.Id);
        revision.HasIndex(x => new { x.ExamSetId, x.Sequence }).IsUnique();
        revision.HasOne(x => x.ExamSet).WithMany().HasForeignKey(x => x.ExamSetId).OnDelete(DeleteBehavior.Restrict);
        revision.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        revision.Property(x => x.Snapshot).HasColumnType("jsonb");
        revision.Property(x => x.Action).HasMaxLength(40);
        revision.Property(x => x.Notes).HasMaxLength(2000);

        modelBuilder.Entity<ExamSet>().HasIndex(x => new { x.CourseId, x.SetCode }).IsUnique().HasDatabaseName("uq_exam_sets_course_code");
        modelBuilder.Entity<ExamSet>().ToTable("exam_sets", t => t.HasCheckConstraint("ck_exam_set_assignment_session", "assignment_id IS NULL OR session_id IS NOT NULL"));
        modelBuilder.Entity<ExamSet>().HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExamSetQuestion>().Property(x => x.IsBankQuestion).HasColumnName("is_bank_question");
        modelBuilder.Entity<ExamSetQuestion>().Property(x => x.SourceQuestionId).HasColumnName("source_question_id");
        modelBuilder.Entity<ExamStructure>().Property(x => x.EasyCount).HasColumnName("easy_count");
        modelBuilder.Entity<ExamStructure>().Property(x => x.MediumCount).HasColumnName("medium_count");
        modelBuilder.Entity<ExamStructure>().Property(x => x.HardCount).HasColumnName("hard_count");

        // Keep these new tables consistent with the existing PostgreSQL snake_case schema.
        foreach (var type in new[] { typeof(ExamPreparation), typeof(ExamAuthoringAssignment), typeof(ExamStructureClo), typeof(ExamSetRevision) })
        foreach (var property in modelBuilder.Entity(type).Metadata.GetProperties())
            property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
        modelBuilder.Entity<ExamSet>().Property(x => x.AssignmentId).HasColumnName("assignment_id");
    }
}
