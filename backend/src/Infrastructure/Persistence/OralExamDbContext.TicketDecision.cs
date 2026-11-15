using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public async Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
            return await decision(cancellationToken);

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Failed transactions must not retain inserts/notifications on a retry.
            if (attempt++ > 0) ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            // A common row lock serializes acknowledgement and appeal, across API instances.
            await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM student_exam_tickets WHERE id = {ticketId} FOR UPDATE", cancellationToken);
            // A context may have tracked the ticket before acquiring the lock. Re-read its state.
            foreach (var entry in ChangeTracker.Entries<Domain.Entities.StudentExamTicket>().Where(e => e.Entity.Id == ticketId).ToList())
                await entry.ReloadAsync(cancellationToken);
            var result = await decision(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
