using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectExamPapersAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ProtectExamPapersAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task ProtectExamPapersAsync(CancellationToken ct)
    {
        // Inspect tracked types without DetectChanges: unrelated exam/practice writes keep their original save path.
        var detectChanges = ChangeTracker.AutoDetectChangesEnabled;
        ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            if (!ChangeTracker.Entries().Any(e => e.Entity is ExamSetRevision or ExamQuestion or Rubric or RubricCriterion or
                    ExamStructure or ExamStructureClo or ExamSet or ExamSetQuestion)) return;
            if (detectChanges) ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<ExamSetRevision>())
                if (entry.State is EntityState.Modified or EntityState.Deleted) throw Locked();
            var removedExamSetQuestionIds = ChangeTracker.Entries<ExamSetQuestion>()
                .Where(e => e.State == EntityState.Deleted)
                .Select(e => e.Entity.Id)
                .ToHashSet();
            var survivingTrackedExamQuestionIds = ChangeTracker.Entries<ExamSetQuestion>()
                .Where(e => e.State != EntityState.Deleted)
                .Select(e => e.Entity.ExamQuestionId)
                .ToHashSet();
            var trackedSubmissionExamQuestionIds = ChangeTracker.Entries<ExamQuestionSubmission>()
                .Where(e => e.State != EntityState.Deleted)
                .Select(e => e.Entity.ExamQuestionId)
                .ToHashSet();
            foreach (var entry in ChangeTracker.Entries<ExamQuestion>())
                if (entry.State is EntityState.Modified or EntityState.Deleted && entry.OriginalValues.GetValue<bool>(nameof(ExamQuestion.IsPaperCopy)))
                {
                    if (entry.State == EntityState.Deleted) throw Locked();
                    var modifiedProps = entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).ToList();
                    var onlyApprovalChanges = modifiedProps.All(name => name is nameof(ExamQuestion.ApprovalStatus) or nameof(ExamQuestion.ApprovedBy) or nameof(ExamQuestion.ApprovedAt));
                    if (onlyApprovalChanges) continue;

                    var onlySoftRetire = modifiedProps.Count > 0
                        && modifiedProps.All(name => name is nameof(ExamQuestion.IsActive) or nameof(ExamQuestion.IsDeleted))
                        && !entry.CurrentValues.GetValue<bool>(nameof(ExamQuestion.IsActive))
                        && entry.CurrentValues.GetValue<bool>(nameof(ExamQuestion.IsDeleted));
                    if (!onlySoftRetire) throw Locked();

                    var hasRemainingLink = survivingTrackedExamQuestionIds.Contains(entry.Entity.Id)
                        || trackedSubmissionExamQuestionIds.Contains(entry.Entity.Id)
                        || await ExamSetQuestions.AsNoTracking().AnyAsync(
                            esq => esq.ExamQuestionId == entry.Entity.Id && !removedExamSetQuestionIds.Contains(esq.Id), ct)
                        || await ExamQuestionSubmissions.AsNoTracking().AnyAsync(
                            s => s.ExamQuestionId == entry.Entity.Id, ct);
                    if (hasRemainingLink) throw Locked();
                }
            var rubricIds = ChangeTracker.Entries<Rubric>().Where(e => e.State is EntityState.Modified or EntityState.Deleted).Select(e => e.Entity.Id)
                .Concat(ChangeTracker.Entries<RubricCriterion>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(e => e.Entity.RubricId)).Distinct().ToList();
            if (rubricIds.Count > 0 && await ExamQuestions.AsNoTracking().AnyAsync(q => q.IsPaperCopy && rubricIds.Contains(q.RubricId), ct)) throw Locked();
            foreach (var entry in ChangeTracker.Entries<ExamStructure>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
                if (await ExamStructureClos.AsNoTracking().AnyAsync(x => x.StructureId == entry.Entity.Id, ct)) throw Locked();
            foreach (var entry in ChangeTracker.Entries<ExamStructureClo>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
                if (await ExamStructures.AsNoTracking().AnyAsync(x => x.Id == entry.Entity.StructureId, ct)) throw Locked();
            foreach (var entry in ChangeTracker.Entries<ExamSet>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
            {
                if (!entry.OriginalValues.GetValue<Guid?>(nameof(ExamSet.AssignmentId)).HasValue) continue;
                var originalStatus = entry.OriginalValues.GetValue<string>(nameof(ExamSet.Status));
                if (entry.State == EntityState.Deleted || originalStatus == ExamSetStatus.HandedOver ||
                    entry.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(ExamSet.SessionId) or nameof(ExamSet.AssignmentId) or
                        nameof(ExamSet.CourseId) or nameof(ExamSet.StructureId) or nameof(ExamSet.CreatedBy) or nameof(ExamSet.SetCode))) throw Locked();
            }
            var changedSetIds = ChangeTracker.Entries<ExamSetQuestion>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => e.Entity.ExamSetId).Distinct().ToList();
            if (changedSetIds.Count > 0 && await ExamSets.AsNoTracking().AnyAsync(s => changedSetIds.Contains(s.Id) && s.AssignmentId != null &&
                    (s.Status == ExamSetStatus.SubmittedForReview || s.Status == ExamSetStatus.Official || s.Status == ExamSetStatus.HandedOver), ct)) throw Locked();
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        }
    }

    private static OneWayLockException Locked() => new("Đề thi và lịch sử đã lưu được bảo vệ. Hãy tạo bản nháp/phiên bản mới qua luồng soạn đề.");
}
