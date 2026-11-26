using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.DeleteOfficialExamSession;

public sealed class DeleteOfficialExamSessionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteOfficialExamSessionCommand, Result>
{
    public async Task<Result> Handle(DeleteOfficialExamSessionCommand request, CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context,
                request.ActorUserId,
                request.ActorRole,
                UserRole.ExaminationOfficer,
                cancellationToken))
        {
            return Result.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        return await context.ExecuteOfficialSessionCandidatesAsync(
            request.SessionId,
            ct => HandleLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result> HandleLockedAsync(
        DeleteOfficialExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            return Result.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (!string.Equals(session.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("CONFLICT: Chỉ có thể xóa kỳ thi đang ở trạng thái scheduled.");
        }

        if (await context.RealExamSessionShifts.AsNoTracking().AnyAsync(sh => sh.SessionId == session.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Kỳ thi đã có ca thi phòng Lab, không thể xóa.");
        }

        if (await context.OfficialExamCandidates.AsNoTracking().AnyAsync(c => c.SessionId == session.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Kỳ thi đã có danh sách thí sinh dự thi, không thể xóa.");
        }

        if (await context.StudentExamTickets.AsNoTracking().AnyAsync(t => t.Shift.SessionId == session.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Kỳ thi đã có vé thi được cấp, không thể xóa.");
        }

        if (await context.AppealRequests.AsNoTracking().AnyAsync(a => a.SessionId == session.Id, cancellationToken))
        {
            return Result.Failure("CONFLICT: Kỳ thi đã có đơn phúc khảo liên quan, không thể xóa.");
        }

        if (await context.ExamSets.AsNoTracking().AnyAsync(
                es => es.SessionId == session.Id
                    && es.IsActive
                    && (es.Status == ExamSetStatus.Official || es.Status == ExamSetStatus.HandedOver),
                cancellationToken))
        {
            return Result.Failure("CONFLICT: Kỳ thi đang gắn bộ đề chính thức hoặc đã bàn giao, không thể xóa.");
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            session.Id,
            session.CourseId,
            session.SemesterId,
            session.ExamAttemptType,
            session.Title,
            session.ExamDate,
            session.Status
        });

        context.OfficialExamSessions.Remove(session);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "OFFICIAL_EXAM_SESSION_DELETED",
            EntityName = nameof(OfficialExamSession),
            EntityId = session.Id,
            OldValues = oldValues,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
