using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public async Task<T> ExecuteShiftMutationAsync<T>(Guid shiftId, bool isOfficial,
        Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational()) return await mutation(cancellationToken);

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await LockShiftRowAsync(shiftId, isOfficial, cancellationToken);
            var result = await mutation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task LockShiftRowAsync(Guid shiftId, bool isOfficial, CancellationToken cancellationToken)
    {
        // FOR UPDATE also conflicts with the FK key-share lock of a concurrent child insert.
        // Read children only AFTER this lock: an insert that committed first must block deletion.
        if (isOfficial)
        {
            await RealExamSessionShifts.FromSqlInterpolated(
                $"SELECT * FROM real_exam_session_shifts WHERE id = {shiftId} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            // Existing tickets may be checked in or receive submissions without changing the shift.
            // Lock them too, before the guard reads their status or evidence. Child FK inserts
            // (submissions/audits/logs) then serialize with the same management operation.
            await StudentExamTickets.FromSqlInterpolated(
                $"SELECT * FROM student_exam_tickets WHERE shift_id = {shiftId} ORDER BY id FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
        }
        else
            await InClassExamShifts.FromSqlInterpolated(
                $"SELECT * FROM in_class_exam_shifts WHERE id = {shiftId} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
    }
}
