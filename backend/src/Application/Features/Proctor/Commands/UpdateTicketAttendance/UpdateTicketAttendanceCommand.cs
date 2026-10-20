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

namespace OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;

public sealed record UpdateTicketAttendanceCommand(
    Guid ActorUserId,
    Guid TicketId,
    bool IsPresent = true,
    string? IpAddress = null,
    string? Note = null) : IRequest<Result<TicketProctorActionResponseDto>>;

public sealed class UpdateTicketAttendanceCommandHandler
    : IRequestHandler<UpdateTicketAttendanceCommand, Result<TicketProctorActionResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateTicketAttendanceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TicketProctorActionResponseDto>> Handle(
        UpdateTicketAttendanceCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.StudentExamTickets
            .Include(t => t.Student)
            .Include(t => t.Shift)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy vé thi trong hệ thống.");
        }

        if (ticket.IsLocked || ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "LOCKED: Vé thi đã được công bố điểm và khóa một chiều, không thể thay đổi trạng thái điểm danh.");
        }

        if (ticket.Status is ExamTicketStatus.Submitted or ExamTicketStatus.AiGraded or ExamTicketStatus.Audited)
        {
            return Result<TicketProctorActionResponseDto>.Failure(
                "CONFLICT: Sinh viên đã nộp bài thi, không thể hoàn tác trạng thái điểm danh.");
        }

        var oldStatus = ticket.Status;
        var now = DateTime.UtcNow;

        ticket.Status = request.IsPresent ? ExamTicketStatus.InProgress : ExamTicketStatus.Scheduled;
        if (request.IsPresent)
        {
            ticket.CheckedInAt = now;
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress))
        {
            ticket.IpAddress = request.IpAddress.Trim();
        }

        if (request.IsPresent && string.Equals(ticket.Shift?.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            ticket.Shift!.Status = "in_progress";
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "PROCTOR_ATTENDANCE_UPDATED",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            OldValues = $"status={oldStatus}",
            NewValues = $"status={ticket.Status};ip_address={ticket.IpAddress}" + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" | {request.Note.Trim()}"),
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
            IsSuspended = false,
            UpdatedAt = now
        });
    }
}
