using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;

public sealed class GetMyWorkstationTicketQueryHandler : IRequestHandler<GetMyWorkstationTicketQuery, Result<StudentWorkstationTicketResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<GetMyWorkstationTicketQuery> _validator;

    public GetMyWorkstationTicketQueryHandler(
        IApplicationDbContext context,
        IValidator<GetMyWorkstationTicketQuery> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<StudentWorkstationTicketResponse>> Handle(
        GetMyWorkstationTicketQuery request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.Student)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId)
            && !OfficialExamDeviceGuard.TryNormalizeDeviceId(request.DeviceId, out _))
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.DeviceIdInvalid);
        }

        // BYOD: ip_address is audit-only, never filter out tickets on IP change (Wi-Fi/NAT).
        var tickets = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(item => item.Shift)
                .ThenInclude(shift => shift.Session)
            .Where(item => item.StudentId == actor.Id)
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.WorkstationTicketNotFound);
        }

        var openStatus = tickets
            .Where(item => ExamShiftWindow.IsOpenStatus(item.Shift.Status))
            .ToList();
        if (openStatus.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.ShiftClosed);
        }

        var inWindow = openStatus
            .Where(item => ExamShiftWindow.Contains(item.Shift, request.AtUtc))
            .ToList();
        if (inWindow.Count == 0)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.ShiftNotOpen);
        }

        if (inWindow.Count > 1)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.OverlappingShifts);
        }

        var ticket = inWindow[0];

        // Enforce BYOD DeviceId & optional active session token verification.
        // Read-only discovery allows unlocked tickets awaiting replacement check-in so Kiosk can prompt rebind,
        // while still suppressing Questions & FollowUps via isUnlockedAwaitingRebind below.
        var deviceError = OfficialExamDeviceGuard.ValidateBoundDeviceAndSession(
            ticket,
            request.DeviceId,
            request.KioskSessionToken,
            requireSessionToken: false,
            allowUnlockedPendingRebind: true);
        if (deviceError is not null)
        {
            return Result<StudentWorkstationTicketResponse>.Failure(deviceError);
        }

        // Fail-closed BYOD: require both bound DeviceId and verified active session token before exposing questions/follow-ups.
        // This closes both post-Proctor-attendance unbound tickets (DeviceId == null, DeviceUnlockCount == 0)
        // and EO device-unlocked tickets awaiting replacement check-in (DeviceId == null, DeviceUnlockCount > 0).
        var hasBoundDevice = !string.IsNullOrWhiteSpace(ticket.DeviceId)
            && !string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash);
        var sessionTokenSatisfied = hasBoundDevice
            && OfficialExamDeviceGuard.VerifySessionToken(request.KioskSessionToken, ticket.DeviceSessionTokenHash);

        var canExposeQuestions =
            sessionTokenSatisfied
            && string.Equals(ticket.Shift.Status, ExamShiftWindow.InProgress, StringComparison.OrdinalIgnoreCase)
            && ticket.Shift.IsExamPublished
            && string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.Ordinal);

        string? examSetCode = null;
        IReadOnlyList<WorkstationExamQuestionDto>? workstationQuestions = null;
        if (canExposeQuestions && ticket.ExamSetId.HasValue)
        {
            examSetCode = await _context.ExamSets
                .AsNoTracking()
                .Where(es => es.Id == ticket.ExamSetId.Value)
                .Select(es => es.SetCode)
                .FirstOrDefaultAsync(cancellationToken);

            workstationQuestions = await _context.ExamSetQuestions
                .AsNoTracking()
                .Include(esq => esq.ExamQuestion)
                .Where(esq => esq.ExamSetId == ticket.ExamSetId.Value && esq.ExamQuestion != null)
                .OrderBy(esq => esq.OrderIndex)
                .Select(esq => new WorkstationExamQuestionDto(
                    esq.ExamQuestionId,
                    esq.OrderIndex,
                    esq.ExamQuestion.Title,
                    esq.ExamQuestion.Content,
                    esq.ExamQuestion.BloomLevel,
                    esq.ExamQuestion.Difficulty,
                    esq.ExamQuestion.ImageUrl,
                    esq.ExamQuestion.ImageDescription))
                .ToListAsync(cancellationToken);
        }

        var followUps = canExposeQuestions
            ? await _context.ExamQuestionSubmissions
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
            : new List<WorkstationFollowUpItemDto>();

        var blurCount = await _context.ExamAntiCheatLogs
            .AsNoTracking()
            .CountAsync(
                l => l.TicketId == ticket.Id && l.EventType == AntiCheatEventType.WindowBlur,
                cancellationToken);

        var isKioskLocked = ticket.IsLocked || ticket.Status is
            ExamTicketStatus.Submitted or
            ExamTicketStatus.AiGraded or
            ExamTicketStatus.Audited or
            ExamTicketStatus.Published or
            ExamTicketStatus.Locked;

        var remainingStrikes = Math.Max(0, OfficialExamRules.WindowBlurLockThreshold - blurCount);

        return Result<StudentWorkstationTicketResponse>.Success(new StudentWorkstationTicketResponse(
            ticket.Id,
            ticket.ShiftId,
            ticket.Shift.ShiftName,
            ticket.Shift.RoomLab,
            ticket.Shift.StartTime,
            ticket.Shift.EndTime,
            ticket.Shift.Status,
            ticket.Shift.Session?.Title ?? string.Empty,
            ticket.StudentId,
            ticket.SeatNumber,
            ticket.Status,
            ticket.ExamSetId,
            examSetCode,
            ticket.Shift.Session?.ExamInputMode ?? "VoiceWithTranscriptEdit",
            ticket.Shift.Session?.TranscriptBufferSeconds ?? 60,
            ticket.Shift.Session?.HasFollowUp ?? false,
            ticket.Shift.Session?.MaxFollowUpQuestions ?? 2,
            workstationQuestions,
            ticket.Shift.IsExamPublished,
            isKioskLocked,
            ticket.DeviceId,
            ticket.DeviceLockedAt,
            ticket.DeviceUnlockCount,
            blurCount,
            OfficialExamRules.MaxAllowedWindowBlurs,
            OfficialExamRules.WindowBlurLockThreshold,
            remainingStrikes,
            DateTime.UtcNow,
            ticket.Shift.Session?.ExamAttemptType ?? OfficialExamAttemptType.FinalExam,
            followUps));
    }
}
