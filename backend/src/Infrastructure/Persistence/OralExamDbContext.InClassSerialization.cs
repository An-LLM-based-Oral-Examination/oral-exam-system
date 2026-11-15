using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join,
        CancellationToken cancellationToken = default) =>
        ExecuteTransactionLockAsync($"oes:in-class:join:{shiftId:N}", join, cancellationToken, shiftId);

    public Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId,
        Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default) =>
        ExecuteTransactionLockAsync($"oes:in-class:submit:{shiftId:N}:{studentId:N}", submit, cancellationToken);

    private async Task<T> ExecuteTransactionLockAsync<T>(string key, Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken, Guid? joiningShiftId = null)
    {
        if (!Database.IsRelational())
        {
            return await mutation(cancellationToken);
        }

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A failed execution-strategy attempt must not leak tracked inserts or stale reads into its retry.
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            // Transaction-scoped locks cannot survive a lost connection. The execution strategy retries the
            // complete transaction and reacquires the lock before rerunning any database mutation.
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

            if (joiningShiftId.HasValue)
                await LockShiftRowAsync(joiningShiftId.Value, false, cancellationToken);

            var result = await mutation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
