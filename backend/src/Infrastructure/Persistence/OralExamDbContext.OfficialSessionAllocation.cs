using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Infrastructure.Persistence;

public partial class OralExamDbContext
{
    public async Task<T> ExecuteOfficialSemesterBatchAsync<T>(
        Guid semesterId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await action(cancellationToken);
        }

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var key = $"oes:official-semester-sessions:{semesterId:N}";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<T> ExecuteOfficialShiftScheduleBatchAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await action(cancellationToken);
        }

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            const string key = "oes:official-shift-schedule-batch";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<T> ExecuteOfficialSessionCandidatesAsync<T>(
        Guid sessionId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await action(cancellationToken);
        }

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var key = $"oes:official-session-candidates:{sessionId:N}";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                cancellationToken);

            await OfficialExamSessions.FromSqlInterpolated(
                    $"SELECT * FROM official_exam_sessions WHERE id = {sessionId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<T> ExecuteOfficialSessionAllocationAsync<T>(
        Guid sessionId,
        Func<CancellationToken, Task<T>> allocation,
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await allocation(cancellationToken);
        }

        var attempt = 0;
        return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                ChangeTracker.Clear();
            }

            await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // Canonical Lock Order: Session Candidates -> Session Alloc -> Shift Rows (ORDER BY id)
            var candidatesKey = $"oes:official-session-candidates:{sessionId:N}";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({candidatesKey}, 0))",
                cancellationToken);

            var allocKey = $"oes:official-session-alloc:{sessionId:N}";
            await Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({allocKey}, 0))",
                cancellationToken);

            // Khóa thêm các phòng thi thuộc session theo thứ tự id để đồng bộ với ShiftMutation / BulkTicketImport.
            await RealExamSessionShifts.FromSqlInterpolated(
                    $"SELECT * FROM real_exam_session_shifts WHERE session_id = {sessionId} ORDER BY id FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var result = await allocation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
