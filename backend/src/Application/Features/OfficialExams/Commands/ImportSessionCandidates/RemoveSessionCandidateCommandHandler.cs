using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

public sealed class RemoveSessionCandidateCommandHandler(IApplicationDbContext context)
    : IRequestHandler<RemoveSessionCandidateCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveSessionCandidateCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<bool>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<bool>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (request.StudentId == Guid.Empty)
        {
            return Result<bool>.Failure("NOT_FOUND: Không tìm thấy thí sinh trong danh sách dự thi.");
        }

        return await context.ExecuteOfficialSessionCandidatesAsync(
            request.SessionId,
            ct => RemoveLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result<bool>> RemoveLockedAsync(
        RemoveSessionCandidateCommand request,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == request.SessionId)
            .Select(s => new { s.Id, s.Status, s.ExamAttemptType })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<bool>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (string.Equals(session.Status, "concluded", StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.Failure("CONFLICT: Không thể xóa thí sinh khỏi kỳ thi đã kết thúc.");
        }

        var candidate = await context.OfficialExamCandidates
            .FirstOrDefaultAsync(
                c => c.SessionId == request.SessionId && c.StudentId == request.StudentId,
                cancellationToken);

        if (candidate is null)
        {
            return Result<bool>.Failure("NOT_FOUND: Sinh viên không nằm trong danh sách dự thi của đợt thi này.");
        }

        var sessionShiftIds = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(sh => sh.SessionId == request.SessionId)
            .Select(sh => sh.Id)
            .ToListAsync(cancellationToken);

        var hasTicket = sessionShiftIds.Count > 0 && await context.StudentExamTickets
            .AsNoTracking()
            .AnyAsync(t => sessionShiftIds.Contains(t.ShiftId) && t.StudentId == request.StudentId, cancellationToken);

        if (hasTicket)
        {
            return Result<bool>.Failure(
                "CONFLICT: Sinh viên đã được cấp vé phòng thi trong đợt thi này nên không thể xóa khỏi danh sách dự thi.");
        }

        context.OfficialExamCandidates.Remove(candidate);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "OFFICIAL_SESSION_CANDIDATE_REMOVED",
            EntityName = nameof(OfficialExamSession),
            EntityId = request.SessionId,
            OldValues = JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId,
                examAttemptType = session.ExamAttemptType,
                studentId = request.StudentId
            }),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
