using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.CreateAppeal;

public sealed class CreateAppealCommandHandler : IRequestHandler<CreateAppealCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateAppealCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AppealResponseDto>> Handle(CreateAppealCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.StudentExamTickets
            .Include(t => t.Shift)
                .ThenInclude(s => s.Session)
            .Include(t => t.Student)
            .Include(t => t.Submissions)
            .Include(t => t.LecturerAudit)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result<AppealResponseDto>.Failure("Vé thi không tồn tại trong hệ thống.");
        }

        if (ticket.StudentId != request.StudentId)
        {
            return Result<AppealResponseDto>.Failure("Sinh viên không sở hữu vé thi này.");
        }

        // Bắt buộc vé thi phải ở trạng thái PUBLISHED hoặc LOCKED mới cho phép nộp đơn phúc khảo
        if (!string.Equals(ticket.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AppealResponseDto>.Failure("Chỉ được phép nộp đơn phúc khảo sau khi điểm thi đã được công bố chính thức (PUBLISHED hoặc LOCKED).");
        }

        if (request.SubmissionId.HasValue)
        {
            var submissionExists = ticket.Submissions.Any(s => s.Id == request.SubmissionId.Value);
            if (!submissionExists)
            {
                return Result<AppealResponseDto>.Failure("Bản nộp câu hỏi không tồn tại hoặc không thuộc vé thi này.");
            }
        }

        // Kiểm tra xem đã có đơn phúc khảo cho vé / câu này đang chờ hoặc đang xử lý chưa
        var hasPendingAppeal = await _context.AppealRequests.AnyAsync(a =>
            a.TicketId == request.TicketId &&
            a.SubmissionId == request.SubmissionId &&
            (a.Status == AppealStatus.Pending || a.Status == AppealStatus.InReview),
            cancellationToken);

        if (hasPendingAppeal)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo cho bài thi này đã tồn tại và đang chờ xử lý.");
        }

        // Tự động gán Trưởng Bộ Môn (department_head). Nếu chưa có, gán user có role admin
        var assignedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Role == UserRole.DepartmentHead && u.IsActive, cancellationToken)
            ?? await _context.Users
            .FirstOrDefaultAsync(u => u.Role == UserRole.Admin && u.IsActive, cancellationToken)
            ?? await _context.Users
            .FirstOrDefaultAsync(u => u.IsActive, cancellationToken);

        if (assignedUser == null)
        {
            return Result<AppealResponseDto>.Failure("Không tìm thấy người phụ trách thẩm định đơn phúc khảo trong hệ thống.");
        }

        // Xác định OriginalScore
        decimal? originalScore = null;
        if (request.SubmissionId.HasValue)
        {
            var sub = ticket.Submissions.First(s => s.Id == request.SubmissionId.Value);
            originalScore = sub.FinalScore ?? sub.AiScore;
        }
        else
        {
            originalScore = ticket.LecturerAudit?.AuditedScore;
            if (!originalScore.HasValue && ticket.Submissions.Any())
            {
                var scoredSubmissions = ticket.Submissions
                    .Where(s => s.FinalScore.HasValue || s.AiScore.HasValue)
                    .ToList();
                if (scoredSubmissions.Any())
                {
                    originalScore = scoredSubmissions.Average(s => s.FinalScore ?? s.AiScore ?? 0m);
                }
            }
        }

        var sessionId = ticket.Shift?.SessionId ?? Guid.Empty;
        if (sessionId == Guid.Empty)
        {
            var defaultSession = await _context.OfficialExamSessions
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            sessionId = defaultSession;
        }

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = request.StudentId,
            SessionId = sessionId,
            SubmissionId = request.SubmissionId,
            Reason = request.Reason.Trim(),
            Status = AppealStatus.Pending,
            AssignedTo = assignedUser.Id,
            OriginalScore = originalScore,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AppealRequests.Add(appeal);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new AppealResponseDto
        {
            Id = appeal.Id,
            TicketId = appeal.TicketId,
            StudentId = appeal.StudentId,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            StudentName = ticket.Student?.FullName ?? string.Empty,
            SessionId = appeal.SessionId,
            SessionTitle = ticket.Shift?.Session?.Title ?? string.Empty,
            SubmissionId = appeal.SubmissionId,
            Reason = appeal.Reason,
            Status = appeal.Status,
            AssignedTo = appeal.AssignedTo,
            AssignedToName = assignedUser.FullName,
            OriginalScore = appeal.OriginalScore,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt
        };

        return Result<AppealResponseDto>.Success(dto);
    }
}
