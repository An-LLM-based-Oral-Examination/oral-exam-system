using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket;

/// <summary>
/// Handler thực thi khóa một chiều (One-Way Lock) điểm cho một vé thi cụ thể trên Evidence Panel (MF-04 / R2.1).
/// </summary>
public sealed class LockExamTicketCommandHandler
    : IRequestHandler<LockExamTicketCommand, Result<LockExamTicketResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public LockExamTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<LockExamTicketResponseDto>> Handle(
        LockExamTicketCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<LockExamTicketResponseDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId && u.IsActive, cancellationToken);

        if (actor is null)
        {
            return Result<LockExamTicketResponseDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");
        }

        if (actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<LockExamTicketResponseDto>.Failure(
                "FORBIDDEN: Chỉ Giảng viên, Trưởng Bộ Môn hoặc Quản trị viên mới có quyền khóa điểm bài thi.");
        }

        var ticket = await _context.StudentExamTickets
            .Include(t => t.LecturerAudit)
            .Include(t => t.Submissions)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<LockExamTicketResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy vé thi.");
        }

        var now = DateTime.UtcNow;

        if (ticket.IsLocked)
        {
            return Result<LockExamTicketResponseDto>.Success(
                new LockExamTicketResponseDto(
                    ticket.Id,
                    true,
                    now,
                    ticket.Status,
                    "Điểm bài thi đã được khóa một chiều trước đó."));
        }

        var hasValidScore = ticket.LecturerAudit != null ||
                            ticket.Submissions.Any(s => s.FinalScore.HasValue || s.AiScore.HasValue);

        if (!hasValidScore)
        {
            return Result<LockExamTicketResponseDto>.Failure(
                "UNPROCESSABLE: Không thể khóa điểm khi bài thi chưa có điểm chấm.");
        }

        ticket.IsLocked = true;

        if (ticket.Status != ExamTicketStatus.Published)
        {
            ticket.Status = ExamTicketStatus.Locked;
        }

        if (ticket.LecturerAudit != null && !ticket.LecturerAudit.IsLocked)
        {
            ticket.LecturerAudit.IsLocked = true;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "LOCK_TICKET_GRADE",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            NewValues = "is_locked=true",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LockExamTicketResponseDto>.Success(
            new LockExamTicketResponseDto(
                ticket.Id,
                true,
                now,
                ticket.Status,
                "Điểm bài thi đã được khóa một chiều thành công. Hồ sơ khảo thí đã được niêm phong vĩnh viễn (chỉ mở đường cập nhật điểm khi có quyết định phân công chấm lại phúc khảo từ Trưởng Bộ Môn)."));
    }
}
