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
    string? Note = null,
    int? SeatNumber = null) : IRequest<Result<TicketProctorActionResponseDto>>;

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

        if (request.SeatNumber.HasValue)
        {
            if (request.SeatNumber.Value is < 1 or > 40)
            {
                return Result<TicketProctorActionResponseDto>.Failure(
                    "VALIDATION: Số ghế phải nằm trong khoảng 1 đến 40.");
            }

            var isSeatTaken = await _context.StudentExamTickets.AsNoTracking().AnyAsync(t => t.ShiftId == ticket.ShiftId && t.Id != ticket.Id && t.SeatNumber == request.SeatNumber.Value, cancellationToken);
            if (isSeatTaken)
            {
                return Result<TicketProctorActionResponseDto>.Failure("CONFLICT: Số ghế này đã được gán cho một sinh viên khác trong cùng ca thi.");
            }
            ticket.SeatNumber = request.SeatNumber.Value;
        }

        var oldStatus = ticket.Status;
        var now = DateTime.UtcNow;

        ticket.Status = request.IsPresent ? ExamTicketStatus.InProgress : ExamTicketStatus.Scheduled;
        if (request.IsPresent)
        {
            ticket.CheckedInAt = now;
        }

        // Cam kết Bất biến & Điều Cấm Kỵ 14: Giám thị (Proctor) KHÔNG ĐƯỢC PHÉP sửa IP máy trạm và KHÔNG CÓ QUYỀN mở ca thi

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId == Guid.Empty ? null : request.ActorUserId,
            Action = "PROCTOR_ATTENDANCE_UPDATED",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            OldValues = $"status={oldStatus}",
            NewValues = $"status={ticket.Status};seat_number={ticket.SeatNumber}" + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" | {request.Note.Trim()}"),
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
