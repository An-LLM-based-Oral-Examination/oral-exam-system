using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;

public sealed class GetOfficialExamTicketStateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamTicketStateQuery, Result<OfficialExamTicketStateResponse>>
{
    public async Task<Result<OfficialExamTicketStateResponse>> Handle(
        GetOfficialExamTicketStateQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<OfficialExamTicketStateResponse>.Failure(
                "FORBIDDEN: Không xác định được danh tính thí sinh.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role != UserRole.Student)
        {
            return Result<OfficialExamTicketStateResponse>.Failure(
                "FORBIDDEN: Chỉ thí sinh sở hữu vé thi mới được truy xuất trạng thái khôi phục Kiosk.");
        }

        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamTicketStateResponse>.Failure(
                $"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        if (ticket.StudentId != actor.Id)
        {
            return Result<OfficialExamTicketStateResponse>.Failure(
                "FORBIDDEN: Bạn không có quyền truy cập vé thi của thí sinh khác.");
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId) || !string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash))
        {
            var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
                ticket,
                request.DeviceId,
                request.KioskSessionToken,
                requireSessionToken: false,
                allowUnlockedPendingRebind: true);
            if (deviceError is not null)
            {
                return Result<OfficialExamTicketStateResponse>.Failure(deviceError);
            }
        }

        var isExamPublished = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(s => s.Id == ticket.ShiftId)
            .Select(s => s.IsExamPublished)
            .FirstOrDefaultAsync(cancellationToken);

        var violationCount = await context.ExamAntiCheatLogs
            .AsNoTracking()
            .CountAsync(
                l => l.TicketId == ticket.Id && l.EventType == AntiCheatEventType.WindowBlur,
                cancellationToken);

        var isUnlockedAwaitingRebind = string.IsNullOrWhiteSpace(ticket.DeviceId) && ticket.DeviceUnlockCount > 0;
        var sessionTokenSatisfied = !isUnlockedAwaitingRebind
            && (string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash)
                || OfficialExamDeviceGuard.VerifySessionToken(request.KioskSessionToken, ticket.DeviceSessionTokenHash));

        var followUps = sessionTokenSatisfied
            ? await context.ExamQuestionSubmissions
                .AsNoTracking()
                .Where(s => s.TicketId == ticket.Id && (s.IsFollowUp || s.GeneratedFollowUpPrompt != null || s.AudioVerifiedAt != null))
                .OrderBy(s => s.SubmittedAt)
                .ThenBy(s => s.FollowUpIndex)
                .Select(s => new WorkstationFollowUpItemDto(
                    s.Id,
                    s.ParentSubmissionId,
                    s.ExamQuestionId,
                    s.FollowUpIndex,
                    s.FollowUpPrompt,
                    s.GeneratedFollowUpPrompt,
                    s.TranscriptWhisper,
                    s.AudioObjectKey ?? s.AudioR2Url,
                    s.AudioHashSha256))
                .ToListAsync(cancellationToken)
            : [];

        var isKioskLocked = ticket.IsLocked || ticket.Status is
            ExamTicketStatus.Submitted or
            ExamTicketStatus.AiGraded or
            ExamTicketStatus.Audited or
            ExamTicketStatus.Published or
            ExamTicketStatus.Locked;

        var remainingStrikes = Math.Max(0, OfficialExamRules.WindowBlurLockThreshold - violationCount);

        return Result<OfficialExamTicketStateResponse>.Success(new OfficialExamTicketStateResponse(
            ticket.Id,
            ticket.ShiftId,
            ticket.Status,
            isExamPublished,
            isKioskLocked,
            violationCount,
            OfficialExamRules.MaxAllowedWindowBlurs,
            OfficialExamRules.WindowBlurLockThreshold,
            remainingStrikes,
            DateTime.UtcNow,
            ticket.DeviceId,
            ticket.DeviceLockedAt,
            ticket.DeviceUnlockCount,
            followUps));
    }
}
