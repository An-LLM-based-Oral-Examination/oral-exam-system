using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;

public sealed class ReviewAppealDecisionCommandHandler : IRequestHandler<ReviewAppealDecisionCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public ReviewAppealDecisionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AppealResponseDto>> Handle(ReviewAppealDecisionCommand request, CancellationToken cancellationToken)
    {
        var appeal = await _context.AppealRequests
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.Session)
            .Include(a => a.Student)
            .Include(a => a.AssignedToUser)
            .Include(a => a.ReviewedByUser)
            .FirstOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);

        if (appeal == null)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo không tồn tại trong hệ thống.");
        }

        if (appeal.Status != AppealStatus.Pending && appeal.Status != AppealStatus.InReview)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo này đã có quyết định xử lý trước đó.");
        }

        var reviewer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.ReviewerId, cancellationToken);

        if (reviewer == null || (reviewer.Role != UserRole.DepartmentHead && reviewer.Role != UserRole.Admin))
        {
            return Result<AppealResponseDto>.Failure("Người dùng không có quyền thẩm định đơn phúc khảo (yêu cầu Trưởng Bộ Môn hoặc Quản trị viên).");
        }

        appeal.ReviewedBy = request.ReviewerId;
        appeal.Decision = request.Decision;
        appeal.ReviewNotes = request.ReviewNotes.Trim();
        appeal.ProposedScore = request.ProposedScore;
        appeal.Status = request.Decision;
        appeal.ResolvedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;

        if (request.Decision == AppealStatus.Approved && request.ProposedScore.HasValue)
        {
            if (appeal.SubmissionId.HasValue)
            {
                var submission = await _context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(s => s.Id == appeal.SubmissionId.Value, cancellationToken);
                if (submission != null)
                {
                    submission.FinalScore = request.ProposedScore.Value;
                }
            }

            var audit = await _context.LecturerAudits
                .FirstOrDefaultAsync(a => a.TicketId == appeal.TicketId, cancellationToken);
            if (audit != null)
            {
                audit.AuditedScore = request.ProposedScore.Value;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new AppealResponseDto
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
            AssignedTo = appeal.AssignedTo,
            AssignedToName = appeal.AssignedToUser?.FullName ?? string.Empty,
            ReviewedBy = appeal.ReviewedBy,
            ReviewedByName = reviewer.FullName,
            Decision = appeal.Decision,
            OriginalScore = appeal.OriginalScore,
            ProposedScore = appeal.ProposedScore,
            ReviewNotes = appeal.ReviewNotes,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt,
            ResolvedAt = appeal.ResolvedAt
        };

        return Result<AppealResponseDto>.Success(dto);
    }
}
