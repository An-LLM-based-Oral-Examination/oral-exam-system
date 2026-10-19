using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OralExamination.Domain.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
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
                    throw new InvalidOperationException($"Cannot modify or delete entity of type {entry.Entity.GetType().Name} because it is permanently locked (One-Way Lock).");
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
                          && string.Equals(a.Entity.Decision, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(a.Entity.Status, AppealStatus.Approved, StringComparison.OrdinalIgnoreCase));

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
        if (entry.Entity is StudentExamTicket)
        {
            var modifiedProps = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToList();

            if (modifiedProps.Count == 1 && modifiedProps[0] == nameof(StudentExamTicket.StudentAcknowledgementStatus))
            {
                return true;
            }
        }

        return false;
    }
}
