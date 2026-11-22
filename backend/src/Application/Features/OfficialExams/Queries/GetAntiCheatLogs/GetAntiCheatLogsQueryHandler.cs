using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAntiCheatLogs;

public sealed class GetAntiCheatLogsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAntiCheatLogsQuery, Result<List<ExamAntiCheatLogDto>>>
{
    public async Task<Result<List<ExamAntiCheatLogDto>>> Handle(GetAntiCheatLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<List<ExamAntiCheatLogDto>>.Failure(
                "FORBIDDEN: Không xác định được danh tính người theo dõi vi phạm an ninh phòng thi.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.ExaminationOfficer or UserRole.Proctor or UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<List<ExamAntiCheatLogDto>>.Failure(
                "FORBIDDEN: Bạn không có quyền xem nhật ký vi phạm an ninh phòng thi.");
        }

        var shift = await context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<List<ExamAntiCheatLogDto>>.Failure("NOT_FOUND: Ca thi không tồn tại trong hệ thống.");
        }

        if (!await OfficialExamAuthorization.CanMonitorShiftAntiCheatAsync(context, actor.Id, shift, cancellationToken))
        {
            return Result<List<ExamAntiCheatLogDto>>.Failure(
                "FORBIDDEN: Bạn không có quyền theo dõi nhật ký vi phạm của ca thi này.");
        }

        var query = context.ExamAntiCheatLogs
            .AsNoTracking()
            .Where(l => l.ShiftId == request.ShiftId);

        if (request.TicketId.HasValue)
        {
            query = query.Where(l => l.TicketId == request.TicketId.Value);
        }

        var rawLogs = await query
            .OrderByDescending(l => l.OccurredAt)
            .Select(l => new
            {
                l.Id,
                l.ClientEventId,
                l.TicketId,
                l.ShiftId,
                l.StudentId,
                StudentCode = l.Student.StudentCode ?? string.Empty,
                StudentName = l.Student.FullName,
                l.EventType,
                l.Severity,
                l.Details,
                l.ViolationCount,
                l.OccurredAt,
                TicketStatus = l.Ticket.Status,
                TicketIsLocked = l.Ticket.IsLocked
            })
            .ToListAsync(cancellationToken);

        var logs = rawLogs
            .Select(l => new ExamAntiCheatLogDto
            {
                Id = l.Id,
                ClientEventId = l.ClientEventId ?? l.Id.ToString("D"),
                TicketId = l.TicketId,
                ShiftId = l.ShiftId,
                StudentId = l.StudentId,
                StudentCode = l.StudentCode,
                StudentName = l.StudentName,
                EventType = l.EventType,
                Severity = l.Severity,
                Details = l.Details,
                ViolationCount = l.ViolationCount,
                IsLocked = l.TicketIsLocked || string.Equals(l.TicketStatus, ExamTicketStatus.Locked, StringComparison.Ordinal),
                MaxViolations = OfficialExamRules.MaxAllowedWindowBlurs,
                LockThreshold = OfficialExamRules.WindowBlurLockThreshold,
                RemainingStrikes = Math.Max(0, OfficialExamRules.WindowBlurLockThreshold - l.ViolationCount),
                OccurredAt = l.OccurredAt
            })
            .ToList();

        return Result<List<ExamAntiCheatLogDto>>.Success(logs);
    }
}
