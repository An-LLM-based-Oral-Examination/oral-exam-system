using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.RegradeAppeal;

public sealed class RegradeAppealCommandHandler
    : IRequestHandler<RegradeAppealCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public RegradeAppealCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<AppealResponseDto>> Handle(
        RegradeAppealCommand request,
        CancellationToken cancellationToken)
    {
        var lecturer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);
        if (lecturer is null || !lecturer.IsActive || lecturer.Role != UserRole.Lecturer)
        {
            return Result<AppealResponseDto>.Failure("Chỉ giảng viên đang hoạt động mới được gửi kết quả chấm lại.");
        }

        var appeal = await _context.AppealRequests
            .Include(a => a.Ticket).ThenInclude(t => t.Shift).ThenInclude(s => s.Session)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);
        if (appeal is null)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo không tồn tại trong hệ thống.");
        }

        if (appeal.Status != AppealStatus.InReview)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo chưa được phân công hoặc đã xử lý xong.");
        }

        if (appeal.AssignedTo != request.LecturerId)
        {
            return Result<AppealResponseDto>.Failure("Giảng viên hiện tại không phải người được phân công chấm lại đơn này.");
        }

        var ticketSubmissions = await _context.ExamQuestionSubmissions
            .Where(s => s.TicketId == appeal.TicketId)
            .ToListAsync(cancellationToken);

        var submission = appeal.SubmissionId.HasValue
            ? ticketSubmissions.FirstOrDefault(s => s.Id == appeal.SubmissionId.Value)
            : null;

        var audit = await _context.LecturerAudits
            .FirstOrDefaultAsync(a => a.TicketId == appeal.TicketId, cancellationToken);

        if (submission is null && audit is null && (appeal.SubmissionId.HasValue || ticketSubmissions.Count == 0))
        {
            return Result<AppealResponseDto>.Failure("Không tìm thấy dữ liệu điểm thuộc vé thi để cập nhật phúc khảo.");
        }

        appeal.Decision = AppealStatus.Approved;
        appeal.Status = AppealStatus.Approved;
        appeal.ProposedScore = request.NewScore;
        appeal.ReviewNotes = request.ReviewNotes.Trim();
        appeal.ResolvedAt = DateTime.UtcNow;
        appeal.UpdatedAt = appeal.ResolvedAt.Value;

        if (submission is not null)
        {
            submission.FinalScore = request.NewScore;
            foreach (var sibling in ticketSubmissions.Where(s => s.Id != submission.Id))
            {
                sibling.FinalScore ??= sibling.AiScore;
            }
        }
        else if (!appeal.SubmissionId.HasValue && ticketSubmissions.Count > 0)
        {
            foreach (var sub in ticketSubmissions)
            {
                sub.FinalScore = request.NewScore;
            }
        }

        if (audit is not null)
        {
            if (appeal.SubmissionId.HasValue && ticketSubmissions.Count > 1)
            {
                var scoredValues = ticketSubmissions
                    .Select(s => s.FinalScore ?? s.AiScore)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                audit.AuditedScore = scoredValues.Count > 0
                    ? Math.Round(scoredValues.Average(), 2, MidpointRounding.AwayFromZero)
                    : request.NewScore;
            }
            else
            {
                audit.AuditedScore = request.NewScore;
            }

            audit.OverrideReason = request.ReviewNotes.Trim();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AppealResponseDto>.Success(new AppealResponseDto
        {
            Id = appeal.Id,
            TicketId = appeal.TicketId,
            StudentId = appeal.StudentId,
            StudentCode = appeal.Student?.StudentCode ?? string.Empty,
            StudentName = appeal.Student?.FullName ?? string.Empty,
            SessionId = appeal.SessionId,
            SessionTitle = appeal.Ticket?.Shift?.Session?.Title ?? string.Empty,
            SubmissionId = appeal.SubmissionId,
            Reason = appeal.Reason,
            Status = appeal.Status,
            AssignedTo = lecturer.Id,
            AssignedToName = lecturer.FullName,
            ReviewedBy = appeal.ReviewedBy,
            Decision = appeal.Decision,
            OriginalScore = appeal.OriginalScore,
            ProposedScore = appeal.ProposedScore,
            ReviewNotes = appeal.ReviewNotes,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt,
            ResolvedAt = appeal.ResolvedAt
        });
    }
}
