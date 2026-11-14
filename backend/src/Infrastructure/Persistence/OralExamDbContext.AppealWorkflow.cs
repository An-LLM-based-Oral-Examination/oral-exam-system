using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public Task<T> ExecuteAppealTransitionAsync<T>(Guid appealId, Func<CancellationToken, Task<T>> transition,
        CancellationToken cancellationToken = default) =>
        ExecuteAppealLocksAsync([$"appeal:{appealId:N}"], transition, cancellationToken);

    public Task<T> ExecuteAppealDecisionAsync<T>(Guid appealId, Guid ticketId, Func<CancellationToken, Task<T>> decision,
        CancellationToken cancellationToken = default) =>
        ExecuteAppealLocksAsync([$"appeal:{appealId:N}", $"ticket:{ticketId:N}"], decision, cancellationToken);

    public Task<T> ExecuteAppealAssignmentAsync<T>(IReadOnlyCollection<Guid> appealIds,
        IReadOnlyCollection<Guid> lecturerIds, Func<CancellationToken, Task<T>> assignment,
        CancellationToken cancellationToken = default)
    {
        var keys = appealIds.Select(id => $"appeal:{id:N}")
            .Concat(lecturerIds.Select(id => $"lecturer:{id:N}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return ExecuteAppealLocksAsync(keys, assignment, cancellationToken);
    }

    private async Task<T> ExecuteAppealLocksAsync<T>(IReadOnlyCollection<string> keys,
        Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken)
    {
        if (!Database.IsRelational()) return await mutation(cancellationToken);

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            foreach (var key in keys)
            {
                var lockKey = $"oes:appeal:{key}";
                await Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            }

            var result = await mutation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
