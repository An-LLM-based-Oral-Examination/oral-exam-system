using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public async Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate,
        Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
            return await start(cancellationToken); // Compatibility for unit provider, not race-safety evidence.

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // A transaction-scoped lock also covers the absence of a quota row.
            // The key is namespaced, stable across API processes and parameterized.
            var key = FormattableString.Invariant($"oes:mock-start:{studentId:N}:{courseId:N}:{quotaDate:yyyyMMdd}");
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

            // A reused context may hold a quota read before another request committed.
            foreach (var entry in ChangeTracker.Entries<Domain.Entities.MockExamQuota>()
                .Where(e => e.Entity.StudentId == studentId && e.Entity.CourseId == courseId && e.Entity.QuotaDate == quotaDate).ToList())
            {
                if (entry.State != EntityState.Unchanged)
                    throw new InvalidOperationException("Mock start requires no pending changes to its quota.");
                await entry.ReloadAsync(cancellationToken);
            }

            var result = await start(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
