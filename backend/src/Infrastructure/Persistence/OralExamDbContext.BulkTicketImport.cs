using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public async Task<T> ExecuteBulkExamTicketImportAsync<T>(Guid shiftId,
        Func<CancellationToken, Task<T>> import, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational()) return await import(cancellationToken);

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var key = $"oes:bulk-ticket-import:{shiftId:N}";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            var result = await import(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
