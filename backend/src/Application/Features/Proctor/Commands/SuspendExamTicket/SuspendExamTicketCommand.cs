using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Proctor.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Proctor.Commands.SuspendExamTicket;

public sealed record SuspendExamTicketCommand(
    Guid ActorUserId,
    Guid TicketId,
    string Reason,
    int? BlurViolationCount = null) : IRequest<Result<TicketProctorActionResponseDto>>;

public sealed class SuspendExamTicketCommandHandler
    : IRequestHandler<SuspendExamTicketCommand, Result<TicketProctorActionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public SuspendExamTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TicketProctorActionResponseDto>> Handle(
        SuspendExamTicketCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "VALIDATION: Lý do đình chỉ thi Kiosk phải có tối thiểu 10 ký tự.");
        }

        var ticket = await _context.StudentExamTickets
            .Include(t => t.Student)
            .Include(t => t.LecturerAudit)
            .Include(t => t.Submissions)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy vé thi trong hệ thống.");
        }

        if (ticket.IsLocked || ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "LOCKED: Điểm thi đã được công bố và khóa một chiều, không thể đình chỉ vé thi này.");
        }

        var now = DateTime.UtcNow;
        var trimmedReason = request.Reason.Trim();
        var formattedReason = request.BlurViolationCount.HasValue
            ? $"[SUSPENDED - Blur={request.BlurViolationCount.Value}] {trimmedReason}"
            : $"[SUSPENDED] {trimmedReason}";

        var oldStatus = ticket.Status;
        ticket.Status = ExamTicketStatus.Audited;
        ticket.SubmittedAt ??= now;

        foreach (var submission in ticket.Submissions)
        {
            submission.FinalScore = 0.00m;
            submission.GradingStatus = "audited";
            submission.IsSuspicious = true;
            submission.SuspiciousReason = formattedReason;
        }

        if (ticket.LecturerAudit is null)
        {
            var audit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                LecturerId = request.ActorUserId,
                OriginalAiScore = 0.00m,
                AuditedScore = 0.00m,
                OverrideReason = formattedReason,
                IsLocked = false,
                AuditedAt = now
            };
            _context.LecturerAudits.Add(audit);
        }
        else
        {
            ticket.LecturerAudit.LecturerId = request.ActorUserId;
            ticket.LecturerAudit.AuditedScore = 0.00m;
            ticket.LecturerAudit.OverrideReason = formattedReason;
            ticket.LecturerAudit.AuditedAt = now;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "KIOSK_TICKET_SUSPENDED",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            OldValues = $"status={oldStatus}",
            NewValues = formattedReason,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<TicketProctorActionResponseDto>.Success(new TicketProctorActionResponseDto
        {
            TicketId = ticket.Id,
            ShiftId = ticket.ShiftId,
            StudentId = ticket.StudentId,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            FullName = ticket.Student?.FullName ?? string.Empty,
            SeatNumber = ticket.SeatNumber,
            IpAddress = ticket.IpAddress,
            Status = ticket.Status,
            IsSuspended = true,
            SuspensionReason = formattedReason,
            UpdatedAt = now
        });
    }
}
