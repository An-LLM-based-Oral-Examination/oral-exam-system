using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Common;

namespace OralExamination.Application.Features.MockExams.Commands;

public sealed record PauseMockExamSessionCommand(Guid SessionId, Guid StudentId) : IRequest<Result<int>>;

public sealed class PauseMockExamSessionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PauseMockExamSessionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PauseMockExamSessionCommand request, CancellationToken cancellationToken)
    {
        return await context.ExecuteMockExamGradingAsync(request.SessionId, async lockCt =>
        {
            var now = DateTime.UtcNow;
            var session = await context.MockExamSessions
                .Include(s => s.Course)
                .Include(s => s.ExamSet)
                    .ThenInclude(es => es!.Structure)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, lockCt);
            if (session is null)
            {
                return Result<int>.Failure("NOT_FOUND: Không tìm thấy ca thi thử.");
            }

            if (session.StudentId != request.StudentId)
            {
                return Result<int>.Failure("FORBIDDEN: Bạn không có quyền tạm dừng ca thi thử của sinh viên khác.");
            }

            if (!string.Equals(session.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return Result<int>.Failure("CONFLICT: Ca thi thử đã kết thúc hoặc không ở trạng thái đang làm bài.");
            }

            var baseDurationMinutes = await MockExamClockHelper.ResolveBaseDurationMinutesAsync(context, session, lockCt);
            var maxAllowedExpiresAt = MockExamClockHelper.ResolveMaxAllowedExpiresAt(session, baseDurationMinutes);
            if (now >= maxAllowedExpiresAt)
            {
                return Result<int>.Failure("SESSION_EXPIRED: Ca thi thử đã hết thời gian làm bài.");
            }

            var effectiveExpiresAt = session.ExpiresAt > maxAllowedExpiresAt
                ? maxAllowedExpiresAt
                : session.ExpiresAt;

            if (effectiveExpiresAt <= (session.PausedAt ?? now))
            {
                return Result<int>.Failure("SESSION_EXPIRED: Ca thi thử đã hết thời gian làm bài.");
            }

            if (session.PausedAt is null)
            {
                if (effectiveExpiresAt >= maxAllowedExpiresAt)
                {
                    // Đã dùng hết trần cộng bù tạm dừng (MaxPauseExtensionMinutes = 15 phút) -> không cho tạm dừng tiếp
                    return Result<int>.Failure("CONFLICT: Ca thi đã dùng hết thời gian cộng bù tạm dừng tối đa.");
                }

                session.PausedAt = now;
                try
                {
                    await context.SaveChangesAsync(lockCt);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Result<int>.Failure("CONFLICT: Xung đột cập nhật đồng thời khi tạm dừng ca thi thử.");
                }
            }

            var remaining = effectiveExpiresAt - session.PausedAt.Value;
            return remaining > TimeSpan.Zero
                ? Result<int>.Success((int)Math.Ceiling(remaining.TotalSeconds))
                : Result<int>.Failure("SESSION_EXPIRED: Ca thi thử đã hết thời gian làm bài.");
        }, cancellationToken);
    }
}
