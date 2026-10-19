using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OralExamination.Domain.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Infrastructure.Persistence.Interceptors;

public class OneWayLockInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnforceLock(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        EnforceLock(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceLock(DbContext? context)
    {
        if (context == null) return;

        EnforceLockedAggregateChildren(context);

        var entries = context.ChangeTracker.Entries<ILockableEntity>();

        foreach (var entry in entries)
        {
            // Nếu Entity đang có cờ IsLocked trong CSDL (Original values) thì KHÔNG CHO PHÉP Update hoặc Delete trái phép
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var isLockedProperty = entry.Property(x => x.IsLocked);
                bool wasLocked = (bool)isLockedProperty.OriginalValue;

                if (wasLocked)
                {
                    if (entry.State == EntityState.Modified && IsAuthorizedAppealOrAcknowledgementUpdate(context, entry))
                    {
                        continue;
                    }

                    // Chặn cập nhật/xóa khi đã khóa
                    // Trong thực tế, exception này sẽ được Middleware (hoặc ExceptionFilter) chụp lại và trả về HTTP 403.
                    throw CreateLockException(entry.Entity.GetType().Name);
                }
            }
        }
    }

    private static bool IsAuthorizedAppealOrAcknowledgementUpdate(DbContext context, EntityEntry<ILockableEntity> entry)
    {
        // Không bao giờ cho phép gỡ khóa IsLocked về false
        if (!entry.Entity.IsLocked)
        {
            return false;
        }

        // Trường hợp 1: Phê duyệt đơn phúc khảo (AppealRequest APPROVED) mở đường cập nhật LecturerAudit.AuditedScore
        if (entry.Entity is LecturerAudit lockedAudit)
        {
            bool hasApprovedAppealInTransaction = context.ChangeTracker
                .Entries<AppealRequest>()
                .Any(a => a.State == EntityState.Modified
                          && a.Entity.TicketId == lockedAudit.TicketId
                          && a.Property(nameof(AppealRequest.Status)).OriginalValue is AppealStatus.Pending or AppealStatus.InReview
                          && string.Equals(a.Entity.Decision, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(a.Entity.Status, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
                          && a.Entity.ProposedScore.HasValue
                          && a.Entity.ProposedScore.Value == lockedAudit.AuditedScore);

            if (hasApprovedAppealInTransaction)
            {
                var modifiedProps = entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => p.Metadata.Name)
                    .ToList();

                return modifiedProps.All(name => name == nameof(LecturerAudit.AuditedScore) || name == nameof(LecturerAudit.OverrideReason));
            }
        }

        // Trường hợp 2: Sinh viên xác nhận đã xem điểm trên Student Portal (StudentAcknowledgementStatus = ACKNOWLEDGED)
        if (entry.Entity is StudentExamTicket ticket)
        {
            var modifiedProps = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToList();

            var originalAcknowledgement = entry.Property(nameof(StudentExamTicket.StudentAcknowledgementStatus)).OriginalValue as string;
            if (modifiedProps.Count == 1
                && modifiedProps[0] == nameof(StudentExamTicket.StudentAcknowledgementStatus)
                && string.Equals(originalAcknowledgement, "PENDING", StringComparison.Ordinal)
                && string.Equals(ticket.StudentAcknowledgementStatus, "ACKNOWLEDGED", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnforceLockedAggregateChildren(DbContext context)
    {
        var ticketIds = context.ChangeTracker.Entries<ExamQuestionSubmission>()
            .Where(entry => IsModifiedOrDeleted(entry) && !IsAuthorizedAppealSubmissionUpdate(context, entry))
            .Select(entry => entry.Entity.TicketId)
            .Concat(
                from entry in context.ChangeTracker.Entries<LecturerAuditDetail>()
                where IsModifiedOrDeleted(entry)
                join audit in context.Set<LecturerAudit>().AsNoTracking()
                    on entry.Entity.AuditId equals audit.Id
                select audit.TicketId)
            .Concat(
                from entry in context.ChangeTracker.Entries<AiEvaluation>()
                where IsModifiedOrDeleted(entry) && entry.Entity.ExamSubmissionId.HasValue
                join submission in context.Set<ExamQuestionSubmission>().AsNoTracking()
                    on entry.Entity.ExamSubmissionId!.Value equals submission.Id
                select submission.TicketId)
            .Concat(
                from entry in context.ChangeTracker.Entries<AiEvaluationDetail>()
                where IsModifiedOrDeleted(entry)
                join evaluation in context.Set<AiEvaluation>().AsNoTracking()
                    on entry.Entity.EvaluationId equals evaluation.Id
                where evaluation.ExamSubmissionId.HasValue
                join submission in context.Set<ExamQuestionSubmission>().AsNoTracking()
                    on evaluation.ExamSubmissionId!.Value equals submission.Id
                select submission.TicketId)
            .Distinct()
            .ToList();

        if (ticketIds.Count == 0)
        {
            return;
        }

        var hasLockedTicket = context.Set<StudentExamTicket>()
            .AsNoTracking()
            .Any(ticket => ticketIds.Contains(ticket.Id) && ticket.IsLocked);

        if (hasLockedTicket)
        {
            throw CreateLockException("official exam grade aggregate");
        }
    }

    private static bool IsModifiedOrDeleted<TEntity>(EntityEntry<TEntity> entry)
        where TEntity : class =>
        entry.State is EntityState.Modified or EntityState.Deleted;

    private static bool IsAuthorizedAppealSubmissionUpdate(
        DbContext context,
        EntityEntry<ExamQuestionSubmission> submissionEntry)
    {
        if (submissionEntry.State != EntityState.Modified)
        {
            return false;
        }

        var modifiedProperties = submissionEntry.Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .ToList();
        if (modifiedProperties.Count != 1
            || modifiedProperties[0] != nameof(ExamQuestionSubmission.FinalScore)
            || !submissionEntry.Entity.FinalScore.HasValue)
        {
            return false;
        }

        return context.ChangeTracker.Entries<AppealRequest>().Any(appealEntry =>
            appealEntry.State == EntityState.Modified
            && appealEntry.Entity.SubmissionId == submissionEntry.Entity.Id
            && appealEntry.Property(nameof(AppealRequest.Status)).OriginalValue is AppealStatus.Pending or AppealStatus.InReview
            && string.Equals(appealEntry.Entity.Status, AppealStatus.Approved, StringComparison.Ordinal)
            && string.Equals(appealEntry.Entity.Decision, AppealStatus.Approved, StringComparison.Ordinal)
            && appealEntry.Entity.ProposedScore == submissionEntry.Entity.FinalScore);
    }

    private static OneWayLockException CreateLockException(string entityName) =>
        new($"Không thể sửa hoặc xóa {entityName} vì điểm thi đã được khóa một chiều.");
}
