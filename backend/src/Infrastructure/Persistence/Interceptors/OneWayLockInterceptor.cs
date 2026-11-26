using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OralExamination.Domain.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using System;
using System.Collections.Generic;
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

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await EnforceLockAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnforceLock(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        ValidateLockableEntities(context);
        var plan = BuildAggregateLockPlan(context);
        EnforceLockedAggregateChildrenSync(context, plan);
    }

    private static async Task EnforceLockAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        ValidateLockableEntities(context);
        var plan = BuildAggregateLockPlan(context);
        await EnforceLockedAggregateChildrenAsync(context, plan, cancellationToken);
    }

    private static void ValidateLockableEntities(DbContext context)
    {
        var entries = context.ChangeTracker.Entries<ILockableEntity>();

        foreach (var entry in entries)
        {
            // Nếu Entity đang có cờ IsLocked trong CSDL (Original values) thì KHÔNG CHO PHÉP Update hoặc Delete trái phép
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var wasLocked = entry.Property(x => x.IsLocked).OriginalValue is true;

                if (wasLocked)
                {
                    if (entry.State == EntityState.Modified && IsAuthorizedLockedEntityUpdate(context, entry))
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

    private static bool IsAuthorizedLockedEntityUpdate(DbContext context, EntityEntry<ILockableEntity> entry)
    {
        // Không bao giờ cho phép gỡ khóa IsLocked về false
        if (!entry.Entity.IsLocked)
        {
            return false;
        }

        // Trường hợp 1: Phê duyệt đơn phúc khảo (AppealRequest APPROVED) mở đường cập nhật LecturerAudit.AuditedScore
        if (entry.Entity is LecturerAudit lockedAudit)
        {
            if (entry.Property(nameof(LecturerAudit.TicketId)).IsModified)
            {
                return false;
            }

            if (HasApprovedAppealForAudit(context, lockedAudit.TicketId, lockedAudit.AuditedScore))
            {
                var modifiedProps = GetModifiedPropertyNames(entry);

                return modifiedProps.All(name =>
                    name == nameof(LecturerAudit.AuditedScore) ||
                    name == nameof(LecturerAudit.OverrideReason) ||
                    name == nameof(LecturerAudit.AuditedAt) ||
                    name == nameof(LecturerAudit.LecturerId));
            }
        }

        // Trường hợp 2 & 3: Cập nhật được kiểm soát trên StudentExamTicket đã khóa
        if (entry.Entity is StudentExamTicket ticket)
        {
            var modifiedProps = GetModifiedPropertyNames(entry);

            // Trường hợp 2: Sinh viên xác nhận đã xem điểm trên Student Portal (StudentAcknowledgementStatus = ACKNOWLEDGED)
            var originalAcknowledgement = entry.Property(nameof(StudentExamTicket.StudentAcknowledgementStatus)).OriginalValue as string;
            if (modifiedProps.Count == 1
                && modifiedProps[0] == nameof(StudentExamTicket.StudentAcknowledgementStatus)
                && string.Equals(originalAcknowledgement, "PENDING", StringComparison.OrdinalIgnoreCase)
                && string.Equals(ticket.StudentAcknowledgementStatus, "ACKNOWLEDGED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Trường hợp 3: PublishGrades nâng cấp vé đã khóa tay (LOCKED, GradesPublishedAt == null) -> PUBLISHED (GradesPublishedAt != null)
            if (IsAuthorizedPublishGradeTicketUpgrade(entry, ticket, modifiedProps))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAuthorizedPublishGradeTicketUpgrade(
        EntityEntry entry,
        StudentExamTicket ticket,
        IReadOnlyList<string> modifiedProps)
    {
        if (entry.State != EntityState.Modified || !ticket.IsLocked)
        {
            return false;
        }

        if (modifiedProps.Count == 0
            || !modifiedProps.All(name => name is nameof(StudentExamTicket.Status) or nameof(StudentExamTicket.GradesPublishedAt)))
        {
            return false;
        }

        var originalStatus = entry.Property(nameof(StudentExamTicket.Status)).OriginalValue as string;
        var originalPublishedAt = entry.Property(nameof(StudentExamTicket.GradesPublishedAt)).OriginalValue;

        return string.Equals(originalStatus, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase)
            && originalPublishedAt is null
            && string.Equals(ticket.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase)
            && ticket.GradesPublishedAt.HasValue;
    }

    private static bool HasApprovedAppealForAudit(DbContext context, Guid ticketId, decimal auditedScore) =>
        context.ChangeTracker
            .Entries<AppealRequest>()
            .Any(a => IsAuthorizedApprovedAppealTransition(a)
                && a.Entity.TicketId == ticketId
                && (a.Entity.SubmissionId.HasValue || a.Entity.ProposedScore!.Value == auditedScore));

    private static bool IsAuthorizedApprovedAppealTransition(EntityEntry<AppealRequest> appealEntry) =>
        appealEntry.State == EntityState.Modified
        && !appealEntry.Property(nameof(AppealRequest.TicketId)).IsModified
        && !appealEntry.Property(nameof(AppealRequest.SubmissionId)).IsModified
        && !appealEntry.Property(nameof(AppealRequest.ProposedScore)).IsModified
        && appealEntry.Property(nameof(AppealRequest.Status)).OriginalValue is string origStatus
        && string.Equals(origStatus, AppealStatus.RegradedByLecturer, StringComparison.OrdinalIgnoreCase)
        && string.Equals(appealEntry.Entity.Decision, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
        && string.Equals(appealEntry.Entity.Status, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
        && appealEntry.Entity.ProposedScore.HasValue;

    private static AggregateLockPlan BuildAggregateLockPlan(DbContext context)
    {
        var plan = new AggregateLockPlan();

        foreach (var appealEntry in context.ChangeTracker.Entries<AppealRequest>())
        {
            if (IsAuthorizedApprovedAppealTransition(appealEntry))
            {
                plan.ApprovedAppealTicketIds.Add(appealEntry.Entity.TicketId);
            }
        }

        foreach (var ticketEntry in context.ChangeTracker.Entries<StudentExamTicket>())
        {
            if (ticketEntry.State is not (EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (ticketEntry.State == EntityState.Modified)
            {
                var modifiedProps = GetModifiedPropertyNames(ticketEntry);
                var isPublishUpgrade = ticketEntry.Property(x => x.IsLocked).OriginalValue is true
                    && IsAuthorizedPublishGradeTicketUpgrade(ticketEntry, ticketEntry.Entity, modifiedProps);
                if (isPublishUpgrade)
                {
                    plan.PublishingLockedTicketIds.Add(ticketEntry.Entity.Id);
                    continue;
                }

                var originalAcknowledgement = ticketEntry.Property(nameof(StudentExamTicket.StudentAcknowledgementStatus)).OriginalValue as string;
                var isValidAcknowledgement = ticketEntry.Entity.IsLocked
                    && modifiedProps.Count == 1
                    && modifiedProps[0] == nameof(StudentExamTicket.StudentAcknowledgementStatus)
                    && string.Equals(originalAcknowledgement, "PENDING", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ticketEntry.Entity.StudentAcknowledgementStatus, "ACKNOWLEDGED", StringComparison.OrdinalIgnoreCase);
                if (isValidAcknowledgement)
                {
                    continue;
                }
            }

            // Chặn Update()/Remove() bằng stub tách rời (detached) với IsLocked mặc định false lên vé đã khóa trong DB
            if (ticketEntry.Entity.Id != Guid.Empty)
            {
                plan.CandidateTicketIds.Add(ticketEntry.Entity.Id);
            }
        }

        foreach (var shiftEntry in context.ChangeTracker.Entries<InClassExamShift>())
        {
            if (shiftEntry.State is EntityState.Modified or EntityState.Deleted && shiftEntry.Entity.Id != Guid.Empty)
            {
                // Chặn Update()/Remove() bằng stub tách rời lên ca thi MF-05 đã khóa trong DB
                plan.CandidateInClassShiftIds.Add(shiftEntry.Entity.Id);
            }
        }

        var trackedAddedAuditTickets = new Dictionary<Guid, Guid>();
        foreach (var auditEntry in context.ChangeTracker.Entries<LecturerAudit>().Where(IsAddedModifiedOrDeleted))
        {
            if (auditEntry.State == EntityState.Added)
            {
                if (auditEntry.Entity.Id != Guid.Empty)
                {
                    trackedAddedAuditTickets[auditEntry.Entity.Id] = auditEntry.Entity.TicketId;
                }

                var isAuthorizedAppealInsert = auditEntry.Entity.IsLocked
                    && HasApprovedAppealForAudit(context, auditEntry.Entity.TicketId, auditEntry.Entity.AuditedScore);
                if (!isAuthorizedAppealInsert)
                {
                    plan.CandidateTicketIds.Add(auditEntry.Entity.TicketId);
                }

                continue;
            }

            if (auditEntry.State == EntityState.Modified)
            {
                var modifiedProps = GetModifiedPropertyNames(auditEntry);
                var isAuthorizedPublishAuditLock = plan.PublishingLockedTicketIds.Contains(auditEntry.Entity.TicketId)
                    && !auditEntry.Property(nameof(LecturerAudit.TicketId)).IsModified
                    && modifiedProps.Count == 1
                    && modifiedProps[0] == nameof(LecturerAudit.IsLocked)
                    && auditEntry.Entity.IsLocked;

                var isAuthorizedAppealAuditUpdate = auditEntry.Entity.IsLocked
                    && !auditEntry.Property(nameof(LecturerAudit.TicketId)).IsModified
                    && HasApprovedAppealForAudit(context, auditEntry.Entity.TicketId, auditEntry.Entity.AuditedScore)
                    && modifiedProps.All(name =>
                        name == nameof(LecturerAudit.AuditedScore) ||
                        name == nameof(LecturerAudit.OverrideReason) ||
                        name == nameof(LecturerAudit.AuditedAt) ||
                        name == nameof(LecturerAudit.LecturerId));

                if (isAuthorizedPublishAuditLock || isAuthorizedAppealAuditUpdate)
                {
                    continue;
                }
            }

            plan.CandidateTicketIds.Add(auditEntry.Entity.TicketId);
            if (auditEntry.Property(nameof(LecturerAudit.TicketId)).OriginalValue is Guid originalAuditTicketId)
            {
                plan.CandidateTicketIds.Add(originalAuditTicketId);
            }

            if (auditEntry.Entity.Id != Guid.Empty)
            {
                plan.MutatedAuditIds.Add(auditEntry.Entity.Id);
                plan.MutatedAuditLockCheckIds.Add(auditEntry.Entity.Id);
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<ExamQuestionSubmission>().Where(IsAddedModifiedOrDeleted))
        {
            if (entry.State == EntityState.Modified
                && (IsAuthorizedAppealSubmissionUpdate(context, entry)
                    || IsAuthorizedPublishGradeSubmissionSync(entry, plan.PublishingLockedTicketIds)))
            {
                continue;
            }

            plan.CandidateTicketIds.Add(entry.Entity.TicketId);
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Property(nameof(ExamQuestionSubmission.TicketId)).OriginalValue is Guid originalTicketId)
                {
                    plan.CandidateTicketIds.Add(originalTicketId);
                }

                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.MutatedOfficialSubmissionIds.Add(entry.Entity.Id);
                }
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<LecturerAuditDetail>().Where(IsAddedModifiedOrDeleted))
        {
            if (entry.State == EntityState.Added)
            {
                if (trackedAddedAuditTickets.TryGetValue(entry.Entity.AuditId, out var addedAuditTicketId))
                {
                    if (!plan.ApprovedAppealTicketIds.Contains(addedAuditTicketId))
                    {
                        plan.CandidateTicketIds.Add(addedAuditTicketId);
                    }
                }
                else if (plan.ApprovedAppealTicketIds.Count > 0)
                {
                    plan.AppealAwareAuditIds.Add(entry.Entity.AuditId);
                }
                else
                {
                    plan.CandidateAuditIds.Add(entry.Entity.AuditId);
                }

                continue;
            }

            var auditIdModified = entry.Property(nameof(LecturerAuditDetail.AuditId)).IsModified;
            if (entry.State == EntityState.Modified && !auditIdModified && plan.ApprovedAppealTicketIds.Count > 0)
            {
                plan.AppealAwareAuditIds.Add(entry.Entity.AuditId);
                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.AppealAwareMutatedAuditDetailIds.Add(entry.Entity.Id);
                }
            }
            else
            {
                plan.CandidateAuditIds.Add(entry.Entity.AuditId);
                if (entry.Property(nameof(LecturerAuditDetail.AuditId)).OriginalValue is Guid originalAuditId)
                {
                    plan.CandidateAuditIds.Add(originalAuditId);
                }

                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.MutatedAuditDetailIds.Add(entry.Entity.Id);
                }
            }
        }

        var trackedAddedEvaluationSubmissions = new Dictionary<Guid, (Guid? ExamSubmissionId, Guid? InClassSubmissionId)>();
        foreach (var entry in context.ChangeTracker.Entries<AiEvaluation>().Where(IsAddedModifiedOrDeleted))
        {
            if (entry.State == EntityState.Added && entry.Entity.Id != Guid.Empty)
            {
                trackedAddedEvaluationSubmissions[entry.Entity.Id] = (entry.Entity.ExamSubmissionId, entry.Entity.InClassSubmissionId);
            }

            if (entry.Entity.ExamSubmissionId.HasValue)
            {
                plan.CandidateOfficialSubmissionIds.Add(entry.Entity.ExamSubmissionId.Value);
            }

            if (entry.Entity.InClassSubmissionId.HasValue)
            {
                plan.CandidateInClassSubmissionIds.Add(entry.Entity.InClassSubmissionId.Value);
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Property(nameof(AiEvaluation.ExamSubmissionId)).OriginalValue is Guid originalSubmissionId)
                {
                    plan.CandidateOfficialSubmissionIds.Add(originalSubmissionId);
                }

                if (entry.Property(nameof(AiEvaluation.InClassSubmissionId)).OriginalValue is Guid originalInClassSubmissionId)
                {
                    plan.CandidateInClassSubmissionIds.Add(originalInClassSubmissionId);
                }

                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.MutatedAiEvaluationIds.Add(entry.Entity.Id);
                }
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<AiEvaluationDetail>().Where(IsAddedModifiedOrDeleted))
        {
            if (entry.State == EntityState.Added)
            {
                if (trackedAddedEvaluationSubmissions.TryGetValue(entry.Entity.EvaluationId, out var linkedSubmissions))
                {
                    if (linkedSubmissions.ExamSubmissionId.HasValue)
                    {
                        plan.CandidateOfficialSubmissionIds.Add(linkedSubmissions.ExamSubmissionId.Value);
                    }

                    if (linkedSubmissions.InClassSubmissionId.HasValue)
                    {
                        plan.CandidateInClassSubmissionIds.Add(linkedSubmissions.InClassSubmissionId.Value);
                    }
                }
                else
                {
                    plan.CandidateEvaluationIds.Add(entry.Entity.EvaluationId);
                }

                continue;
            }

            plan.CandidateEvaluationIds.Add(entry.Entity.EvaluationId);
            if (entry.Property(nameof(AiEvaluationDetail.EvaluationId)).OriginalValue is Guid originalEvaluationId)
            {
                plan.CandidateEvaluationIds.Add(originalEvaluationId);
            }

            if (entry.Entity.Id != Guid.Empty)
            {
                plan.MutatedAiEvaluationDetailIds.Add(entry.Entity.Id);
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<InClassExamSubmission>().Where(IsAddedModifiedOrDeleted))
        {
            plan.CandidateInClassShiftIds.Add(entry.Entity.ShiftId);
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Property(nameof(InClassExamSubmission.ShiftId)).OriginalValue is Guid originalShiftId)
                {
                    plan.CandidateInClassShiftIds.Add(originalShiftId);
                }

                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.MutatedInClassSubmissionIds.Add(entry.Entity.Id);
                }
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<InClassShiftParticipant>().Where(IsAddedModifiedOrDeleted))
        {
            plan.CandidateInClassShiftIds.Add(entry.Entity.ShiftId);
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (entry.Property(nameof(InClassShiftParticipant.ShiftId)).OriginalValue is Guid originalShiftId)
                {
                    plan.CandidateInClassShiftIds.Add(originalShiftId);
                }

                if (entry.Entity.Id != Guid.Empty)
                {
                    plan.MutatedInClassParticipantIds.Add(entry.Entity.Id);
                }
            }
        }

        return plan;
    }

    private static void EnforceLockedAggregateChildrenSync(DbContext context, AggregateLockPlan plan)
    {
        if (plan.MutatedAuditIds.Count > 0)
        {
            var ids = plan.MutatedAuditIds.ToList();
            var persistedAuditTickets = context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToList();
            plan.CandidateTicketIds.UnionWith(persistedAuditTickets);
        }

        if (plan.MutatedAuditDetailIds.Count > 0)
        {
            var ids = plan.MutatedAuditDetailIds.ToList();
            var persistedAuditIds = context.Set<LecturerAuditDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.AuditId)
                .ToList();
            plan.CandidateAuditIds.UnionWith(persistedAuditIds);
        }

        if (plan.AppealAwareMutatedAuditDetailIds.Count > 0)
        {
            var ids = plan.AppealAwareMutatedAuditDetailIds.ToList();
            var persistedAuditIds = context.Set<LecturerAuditDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.AuditId)
                .ToList();
            plan.AppealAwareAuditIds.UnionWith(persistedAuditIds);
        }

        if (plan.CandidateAuditIds.Count > 0)
        {
            var ids = plan.CandidateAuditIds.ToList();
            var auditTicketIds = context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToList();
            plan.CandidateTicketIds.UnionWith(auditTicketIds);
        }

        if (plan.AppealAwareAuditIds.Count > 0)
        {
            var ids = plan.AppealAwareAuditIds.ToList();
            var auditTicketIds = context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToList();
            foreach (var ticketId in auditTicketIds)
            {
                if (!plan.ApprovedAppealTicketIds.Contains(ticketId))
                {
                    plan.CandidateTicketIds.Add(ticketId);
                }
            }
        }

        if (plan.MutatedAiEvaluationDetailIds.Count > 0)
        {
            var ids = plan.MutatedAiEvaluationDetailIds.ToList();
            var persistedEvalIds = context.Set<AiEvaluationDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.EvaluationId)
                .ToList();
            plan.CandidateEvaluationIds.UnionWith(persistedEvalIds);
        }

        if (plan.MutatedAiEvaluationIds.Count > 0)
        {
            plan.CandidateEvaluationIds.UnionWith(plan.MutatedAiEvaluationIds);
        }

        if (plan.MutatedAuditLockCheckIds.Count > 0)
        {
            var ids = plan.MutatedAuditLockCheckIds.ToList();
            var hasLockedAudit = context.Set<LecturerAudit>()
                .AsNoTracking()
                .Any(audit => ids.Contains(audit.Id) && audit.IsLocked);
            if (hasLockedAudit)
            {
                throw CreateLockException(nameof(LecturerAudit));
            }
        }

        if (plan.PublishingLockedTicketIds.Count > 0)
        {
            var ids = plan.PublishingLockedTicketIds.ToList();
            var hasInvalidPublishUpgrade = context.Set<StudentExamTicket>()
                .AsNoTracking()
                .Any(ticket => ids.Contains(ticket.Id)
                    && ticket.IsLocked
                    && (ticket.Status != ExamTicketStatus.Locked || ticket.GradesPublishedAt != null));
            if (hasInvalidPublishUpgrade)
            {
                throw CreateLockException(nameof(StudentExamTicket));
            }
        }

        if (plan.CandidateEvaluationIds.Count > 0)
        {
            var ids = plan.CandidateEvaluationIds.ToList();
            var evalSubmissions = context.Set<AiEvaluation>()
                .AsNoTracking()
                .Where(eval => ids.Contains(eval.Id)
                    && (eval.ExamSubmissionId.HasValue || eval.InClassSubmissionId.HasValue))
                .Select(eval => new { eval.ExamSubmissionId, eval.InClassSubmissionId })
                .ToList();
            foreach (var item in evalSubmissions)
            {
                if (item.ExamSubmissionId.HasValue)
                {
                    plan.CandidateOfficialSubmissionIds.Add(item.ExamSubmissionId.Value);
                }

                if (item.InClassSubmissionId.HasValue)
                {
                    plan.CandidateInClassSubmissionIds.Add(item.InClassSubmissionId.Value);
                }
            }
        }

        if (plan.MutatedOfficialSubmissionIds.Count > 0)
        {
            plan.CandidateOfficialSubmissionIds.UnionWith(plan.MutatedOfficialSubmissionIds);
        }

        if (plan.CandidateOfficialSubmissionIds.Count > 0)
        {
            var ids = plan.CandidateOfficialSubmissionIds.ToList();
            var submissionTicketIds = context.Set<ExamQuestionSubmission>()
                .AsNoTracking()
                .Where(sub => ids.Contains(sub.Id))
                .Select(sub => sub.TicketId)
                .ToList();
            plan.CandidateTicketIds.UnionWith(submissionTicketIds);
        }

        if (plan.CandidateTicketIds.Count > 0)
        {
            var ticketIds = plan.CandidateTicketIds.ToList();
            var hasLockedTicket = context.Set<StudentExamTicket>()
                .AsNoTracking()
                .Any(ticket => ticketIds.Contains(ticket.Id) && ticket.IsLocked);

            if (hasLockedTicket)
            {
                throw CreateLockException("official exam grade aggregate");
            }
        }

        if (plan.MutatedInClassSubmissionIds.Count > 0)
        {
            plan.CandidateInClassSubmissionIds.UnionWith(plan.MutatedInClassSubmissionIds);
        }

        if (plan.CandidateInClassSubmissionIds.Count > 0)
        {
            var persistedSubIds = plan.CandidateInClassSubmissionIds.ToList();
            var persistedSubShiftIds = context.Set<InClassExamSubmission>()
                .AsNoTracking()
                .Where(sub => persistedSubIds.Contains(sub.Id))
                .Select(sub => sub.ShiftId)
                .ToList();
            plan.CandidateInClassShiftIds.UnionWith(persistedSubShiftIds);
        }

        if (plan.MutatedInClassParticipantIds.Count > 0)
        {
            var persistedPartIds = plan.MutatedInClassParticipantIds.ToList();
            var persistedPartShiftIds = context.Set<InClassShiftParticipant>()
                .AsNoTracking()
                .Where(part => persistedPartIds.Contains(part.Id))
                .Select(part => part.ShiftId)
                .ToList();
            plan.CandidateInClassShiftIds.UnionWith(persistedPartShiftIds);
        }

        if (plan.CandidateInClassShiftIds.Count > 0)
        {
            var shiftIds = plan.CandidateInClassShiftIds.ToList();
            var hasLockedInClassShift = context.Set<InClassExamShift>()
                .AsNoTracking()
                .Any(shift => shiftIds.Contains(shift.Id) && shift.IsLocked);

            if (hasLockedInClassShift)
            {
                throw CreateLockException("in-class exam grade aggregate");
            }
        }
    }

    private static async Task EnforceLockedAggregateChildrenAsync(
        DbContext context,
        AggregateLockPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.MutatedAuditIds.Count > 0)
        {
            var ids = plan.MutatedAuditIds.ToList();
            var persistedAuditTickets = await context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToListAsync(cancellationToken);
            plan.CandidateTicketIds.UnionWith(persistedAuditTickets);
        }

        if (plan.MutatedAuditDetailIds.Count > 0)
        {
            var ids = plan.MutatedAuditDetailIds.ToList();
            var persistedAuditIds = await context.Set<LecturerAuditDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.AuditId)
                .ToListAsync(cancellationToken);
            plan.CandidateAuditIds.UnionWith(persistedAuditIds);
        }

        if (plan.AppealAwareMutatedAuditDetailIds.Count > 0)
        {
            var ids = plan.AppealAwareMutatedAuditDetailIds.ToList();
            var persistedAuditIds = await context.Set<LecturerAuditDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.AuditId)
                .ToListAsync(cancellationToken);
            plan.AppealAwareAuditIds.UnionWith(persistedAuditIds);
        }

        if (plan.CandidateAuditIds.Count > 0)
        {
            var ids = plan.CandidateAuditIds.ToList();
            var auditTicketIds = await context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToListAsync(cancellationToken);
            plan.CandidateTicketIds.UnionWith(auditTicketIds);
        }

        if (plan.AppealAwareAuditIds.Count > 0)
        {
            var ids = plan.AppealAwareAuditIds.ToList();
            var auditTicketIds = await context.Set<LecturerAudit>()
                .AsNoTracking()
                .Where(audit => ids.Contains(audit.Id))
                .Select(audit => audit.TicketId)
                .ToListAsync(cancellationToken);
            foreach (var ticketId in auditTicketIds)
            {
                if (!plan.ApprovedAppealTicketIds.Contains(ticketId))
                {
                    plan.CandidateTicketIds.Add(ticketId);
                }
            }
        }

        if (plan.MutatedAiEvaluationDetailIds.Count > 0)
        {
            var ids = plan.MutatedAiEvaluationDetailIds.ToList();
            var persistedEvalIds = await context.Set<AiEvaluationDetail>()
                .AsNoTracking()
                .Where(detail => ids.Contains(detail.Id))
                .Select(detail => detail.EvaluationId)
                .ToListAsync(cancellationToken);
            plan.CandidateEvaluationIds.UnionWith(persistedEvalIds);
        }

        if (plan.MutatedAiEvaluationIds.Count > 0)
        {
            plan.CandidateEvaluationIds.UnionWith(plan.MutatedAiEvaluationIds);
        }

        if (plan.MutatedAuditLockCheckIds.Count > 0)
        {
            var ids = plan.MutatedAuditLockCheckIds.ToList();
            var hasLockedAudit = await context.Set<LecturerAudit>()
                .AsNoTracking()
                .AnyAsync(audit => ids.Contains(audit.Id) && audit.IsLocked, cancellationToken);
            if (hasLockedAudit)
            {
                throw CreateLockException(nameof(LecturerAudit));
            }
        }

        if (plan.PublishingLockedTicketIds.Count > 0)
        {
            var ids = plan.PublishingLockedTicketIds.ToList();
            var hasInvalidPublishUpgrade = await context.Set<StudentExamTicket>()
                .AsNoTracking()
                .AnyAsync(ticket => ids.Contains(ticket.Id)
                    && ticket.IsLocked
                    && (ticket.Status != ExamTicketStatus.Locked || ticket.GradesPublishedAt != null), cancellationToken);
            if (hasInvalidPublishUpgrade)
            {
                throw CreateLockException(nameof(StudentExamTicket));
            }
        }

        if (plan.CandidateEvaluationIds.Count > 0)
        {
            var ids = plan.CandidateEvaluationIds.ToList();
            var evalSubmissions = await context.Set<AiEvaluation>()
                .AsNoTracking()
                .Where(eval => ids.Contains(eval.Id)
                    && (eval.ExamSubmissionId.HasValue || eval.InClassSubmissionId.HasValue))
                .Select(eval => new { eval.ExamSubmissionId, eval.InClassSubmissionId })
                .ToListAsync(cancellationToken);
            foreach (var item in evalSubmissions)
            {
                if (item.ExamSubmissionId.HasValue)
                {
                    plan.CandidateOfficialSubmissionIds.Add(item.ExamSubmissionId.Value);
                }

                if (item.InClassSubmissionId.HasValue)
                {
                    plan.CandidateInClassSubmissionIds.Add(item.InClassSubmissionId.Value);
                }
            }
        }

        if (plan.MutatedOfficialSubmissionIds.Count > 0)
        {
            plan.CandidateOfficialSubmissionIds.UnionWith(plan.MutatedOfficialSubmissionIds);
        }

        if (plan.CandidateOfficialSubmissionIds.Count > 0)
        {
            var ids = plan.CandidateOfficialSubmissionIds.ToList();
            var submissionTicketIds = await context.Set<ExamQuestionSubmission>()
                .AsNoTracking()
                .Where(sub => ids.Contains(sub.Id))
                .Select(sub => sub.TicketId)
                .ToListAsync(cancellationToken);
            plan.CandidateTicketIds.UnionWith(submissionTicketIds);
        }

        if (plan.CandidateTicketIds.Count > 0)
        {
            var ticketIds = plan.CandidateTicketIds.ToList();
            var hasLockedTicket = await context.Set<StudentExamTicket>()
                .AsNoTracking()
                .AnyAsync(ticket => ticketIds.Contains(ticket.Id) && ticket.IsLocked, cancellationToken);

            if (hasLockedTicket)
            {
                throw CreateLockException("official exam grade aggregate");
            }
        }

        if (plan.MutatedInClassSubmissionIds.Count > 0)
        {
            plan.CandidateInClassSubmissionIds.UnionWith(plan.MutatedInClassSubmissionIds);
        }

        if (plan.CandidateInClassSubmissionIds.Count > 0)
        {
            var persistedSubIds = plan.CandidateInClassSubmissionIds.ToList();
            var persistedSubShiftIds = await context.Set<InClassExamSubmission>()
                .AsNoTracking()
                .Where(sub => persistedSubIds.Contains(sub.Id))
                .Select(sub => sub.ShiftId)
                .ToListAsync(cancellationToken);
            plan.CandidateInClassShiftIds.UnionWith(persistedSubShiftIds);
        }

        if (plan.MutatedInClassParticipantIds.Count > 0)
        {
            var persistedPartIds = plan.MutatedInClassParticipantIds.ToList();
            var persistedPartShiftIds = await context.Set<InClassShiftParticipant>()
                .AsNoTracking()
                .Where(part => persistedPartIds.Contains(part.Id))
                .Select(part => part.ShiftId)
                .ToListAsync(cancellationToken);
            plan.CandidateInClassShiftIds.UnionWith(persistedPartShiftIds);
        }

        if (plan.CandidateInClassShiftIds.Count > 0)
        {
            var shiftIds = plan.CandidateInClassShiftIds.ToList();
            var hasLockedInClassShift = await context.Set<InClassExamShift>()
                .AsNoTracking()
                .AnyAsync(shift => shiftIds.Contains(shift.Id) && shift.IsLocked, cancellationToken);

            if (hasLockedInClassShift)
            {
                throw CreateLockException("in-class exam grade aggregate");
            }
        }
    }

    private static List<string> GetModifiedPropertyNames(EntityEntry entry) =>
        entry.Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .ToList();

    private static bool IsAddedModifiedOrDeleted<TEntity>(EntityEntry<TEntity> entry)
        where TEntity : class =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static bool IsAuthorizedPublishGradeSubmissionSync(
        EntityEntry<ExamQuestionSubmission> submissionEntry,
        HashSet<Guid> publishingLockedTicketIds)
    {
        if (submissionEntry.State != EntityState.Modified
            || !publishingLockedTicketIds.Contains(submissionEntry.Entity.TicketId))
        {
            return false;
        }

        var modifiedProperties = GetModifiedPropertyNames(submissionEntry);
        return modifiedProperties.Count == 1
            && modifiedProperties[0] == nameof(ExamQuestionSubmission.FinalScore)
            && submissionEntry.Property(nameof(ExamQuestionSubmission.FinalScore)).OriginalValue is null
            && submissionEntry.Entity.AiScore.HasValue
            && submissionEntry.Entity.FinalScore == submissionEntry.Entity.AiScore;
    }

    private static bool IsAuthorizedAppealSubmissionUpdate(
        DbContext context,
        EntityEntry<ExamQuestionSubmission> submissionEntry)
    {
        if (submissionEntry.State != EntityState.Modified)
        {
            return false;
        }

        var modifiedProperties = GetModifiedPropertyNames(submissionEntry);
        if (modifiedProperties.Count != 1
            || modifiedProperties[0] != nameof(ExamQuestionSubmission.FinalScore)
            || !submissionEntry.Entity.FinalScore.HasValue)
        {
            return false;
        }

        return context.ChangeTracker.Entries<AppealRequest>().Any(appealEntry =>
            IsAuthorizedApprovedAppealTransition(appealEntry)
            && appealEntry.Entity.TicketId == submissionEntry.Entity.TicketId
            && ((!appealEntry.Entity.SubmissionId.HasValue && appealEntry.Entity.ProposedScore == submissionEntry.Entity.FinalScore)
                || (appealEntry.Entity.SubmissionId == submissionEntry.Entity.Id && appealEntry.Entity.ProposedScore == submissionEntry.Entity.FinalScore)
                || (appealEntry.Entity.SubmissionId.HasValue
                    && appealEntry.Entity.SubmissionId != submissionEntry.Entity.Id
                    && submissionEntry.Property(nameof(ExamQuestionSubmission.FinalScore)).OriginalValue == null
                    && submissionEntry.Entity.FinalScore == submissionEntry.Entity.AiScore)));
    }

    private static OneWayLockException CreateLockException(string entityName) =>
        new($"Không thể sửa hoặc xóa {entityName} vì điểm thi đã được khóa một chiều.");

    private sealed class AggregateLockPlan
    {
        public HashSet<Guid> ApprovedAppealTicketIds { get; } = [];
        public HashSet<Guid> PublishingLockedTicketIds { get; } = [];
        public HashSet<Guid> CandidateTicketIds { get; } = [];
        public HashSet<Guid> MutatedAuditIds { get; } = [];
        public HashSet<Guid> MutatedAuditLockCheckIds { get; } = [];
        public HashSet<Guid> CandidateAuditIds { get; } = [];
        public HashSet<Guid> AppealAwareAuditIds { get; } = [];
        public HashSet<Guid> MutatedAuditDetailIds { get; } = [];
        public HashSet<Guid> AppealAwareMutatedAuditDetailIds { get; } = [];
        public HashSet<Guid> CandidateOfficialSubmissionIds { get; } = [];
        public HashSet<Guid> MutatedOfficialSubmissionIds { get; } = [];
        public HashSet<Guid> CandidateEvaluationIds { get; } = [];
        public HashSet<Guid> MutatedAiEvaluationIds { get; } = [];
        public HashSet<Guid> MutatedAiEvaluationDetailIds { get; } = [];
        public HashSet<Guid> CandidateInClassSubmissionIds { get; } = [];
        public HashSet<Guid> CandidateInClassShiftIds { get; } = [];
        public HashSet<Guid> MutatedInClassSubmissionIds { get; } = [];
        public HashSet<Guid> MutatedInClassParticipantIds { get; } = [];
    }
}
