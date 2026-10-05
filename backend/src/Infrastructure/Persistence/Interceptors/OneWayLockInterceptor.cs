using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OralExamination.Domain.Common;
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
            // Nếu Entity đang có cờ IsLocked trong CSDL (Original values) thì KHÔNG CHO PHÉP Update hoặc Delete
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var isLockedProperty = entry.Property(x => x.IsLocked);
                bool wasLocked = (bool)isLockedProperty.OriginalValue;

                if (wasLocked)
                {
                    // Chặn cập nhật/xóa khi đã khóa
                    // Trong thực tế, exception này sẽ được Middleware (hoặc ExceptionFilter) chụp lại và trả về HTTP 403.
                    throw new InvalidOperationException($"Cannot modify or delete entity of type {entry.Entity.GetType().Name} because it is permanently locked (One-Way Lock).");
                }
            }
        }
    }
}
