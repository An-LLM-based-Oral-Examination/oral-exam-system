using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Common;

namespace OralExamination.Application.Features.MockExams.Commands;

public sealed record PauseMockExamSessionCommand(Guid SessionId, Guid StudentId) : IRequest<int?>;

public sealed class PauseMockExamSessionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PauseMockExamSessionCommand, int?>
{
    public async Task<int?> Handle(PauseMockExamSessionCommand request, CancellationToken cancellationToken)
    {
        return await context.ExecuteMockExamGradingAsync(request.SessionId, async lockCt =>
        {
            var now = DateTime.UtcNow;
            var session = await context.MockExamSessions
                .Include(s => s.Course)
                .Include(s => s.ExamSet)
                    .ThenInclude(es => es!.Structure)
                .FirstOrDefaultAsync(
                    s => s.Id == request.SessionId && s.StudentId == request.StudentId && s.Status == "in_progress",
                    lockCt);
            if (session is null)
            {
                return (int?)null;
            }

            var baseDurationMinutes = await MockExamClockHelper.ResolveBaseDurationMinutesAsync(context, session, lockCt);
            var maxAllowedExpiresAt = MockExamClockHelper.ResolveMaxAllowedExpiresAt(session, baseDurationMinutes);
            if (now >= maxAllowedExpiresAt)
            {
                return (int?)null;
            }

            var effectiveExpiresAt = session.ExpiresAt > maxAllowedExpiresAt
                ? maxAllowedExpiresAt
                : session.ExpiresAt;

            if (effectiveExpiresAt <= (session.PausedAt ?? now))
            {
                return (int?)null;
            }

            if (session.PausedAt is null)
            {
                if (effectiveExpiresAt >= maxAllowedExpiresAt)
                {
                    // Đã dùng hết trần cộng bù tạm dừng (MaxPauseExtensionMinutes = 15 phút) -> không cho tạm dừng tiếp
                    return (int?)null;
                }

                session.PausedAt = now;
                try
                {
                    await context.SaveChangesAsync(lockCt);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return (int?)null;
                }
            }

            var remaining = effectiveExpiresAt - session.PausedAt.Value;
            return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : (int?)null;
        }, cancellationToken);
    }
}
