using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Proctor.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Proctor.Queries.GetShiftAttendance;

public sealed record GetShiftAttendanceQuery(Guid ShiftId)
    : IRequest<Result<ShiftAttendanceMonitorResponseDto>>;

public sealed class GetShiftAttendanceQueryHandler
    : IRequestHandler<GetShiftAttendanceQuery, Result<ShiftAttendanceMonitorResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShiftAttendanceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ShiftAttendanceMonitorResponseDto>> Handle(
        GetShiftAttendanceQuery request,
        CancellationToken cancellationToken)
    {
        var shift = await _context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<ShiftAttendanceMonitorResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy ca thi phòng Lab trong hệ thống.");
        }

        var tickets = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.LecturerAudit)
            .Where(t => t.ShiftId == shift.Id)
            .ToListAsync(cancellationToken);

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var suspensionLogs = ticketIds.Count > 0
            ? await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Action == "KIOSK_TICKET_SUSPENDED" && a.EntityId.HasValue && ticketIds.Contains(a.EntityId.Value))
                .ToListAsync(cancellationToken)
            : [];

        var suspensionMap = suspensionLogs
            .GroupBy(a => a.EntityId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().NewValues);

        var ticketBySeat = tickets
            .GroupBy(t => t.SeatNumber)
            .ToDictionary(g => g.Key, g => g.First());

        var seats = new List<ProctorSeatItemDto>(40);
        int checkedInCount = 0;
        int inProgressCount = 0;
        int submittedCount = 0;
        int suspendedCount = 0;

        for (int seatNumber = 1; seatNumber <= 40; seatNumber++)
        {
            if (ticketBySeat.TryGetValue(seatNumber, out var ticket))
            {
                var isSuspended = suspensionMap.TryGetValue(ticket.Id, out var suspReason) ||
                                  (ticket.LecturerAudit?.OverrideReason?.StartsWith("[SUSPENDED", StringComparison.OrdinalIgnoreCase) ?? false);

                var resolvedSuspReason = suspReason ?? ticket.LecturerAudit?.OverrideReason;
                var isCheckedIn = !string.Equals(ticket.Status, ExamTicketStatus.Scheduled, StringComparison.OrdinalIgnoreCase);

                if (isCheckedIn)
                {
                    checkedInCount++;
                }

                if (string.Equals(ticket.Status, ExamTicketStatus.InProgress, StringComparison.OrdinalIgnoreCase))
                {
                    inProgressCount++;
                }

                if (ticket.Status is ExamTicketStatus.Submitted or ExamTicketStatus.AiGraded or ExamTicketStatus.Audited or ExamTicketStatus.Published or ExamTicketStatus.Locked)
                {
                    submittedCount++;
                }

                if (isSuspended)
                {
                    suspendedCount++;
                }

                seats.Add(new ProctorSeatItemDto
                {
                    SeatNumber = seatNumber,
                    IsOccupied = true,
                    TicketId = ticket.Id,
                    StudentId = ticket.StudentId,
                    StudentCode = ticket.Student?.StudentCode,
                    FullName = ticket.Student?.FullName,
                    IpAddress = ticket.IpAddress,
                    Status = ticket.Status,
                    IsCheckedIn = isCheckedIn,
                    IsSuspended = isSuspended,
                    SuspensionReason = isSuspended ? resolvedSuspReason : null,
                    CheckedInAt = isCheckedIn ? ticket.CheckedInAt : null,
                    SubmittedAt = ticket.SubmittedAt
                });
            }
            else
            {
                seats.Add(new ProctorSeatItemDto
                {
                    SeatNumber = seatNumber,
                    IsOccupied = false
                });
            }
        }

        return Result<ShiftAttendanceMonitorResponseDto>.Success(new ShiftAttendanceMonitorResponseDto
        {
            ShiftId = shift.Id,
            ShiftName = shift.ShiftName,
            RoomLab = shift.RoomLab,
            ShiftStatus = shift.Status,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            TotalSeats = 40,
            AssignedTicketsCount = tickets.Count,
            CheckedInCount = checkedInCount,
            InProgressCount = inProgressCount,
            SubmittedCount = submittedCount,
            SuspendedCount = suspendedCount,
            Seats = seats
        });
    }
}
