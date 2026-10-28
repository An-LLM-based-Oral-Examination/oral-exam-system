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

namespace OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore;

/// <summary>
/// Handler xử lý giảng viên hậu kiểm và ghi đè điểm AI trên Evidence Panel (MF-04 / BE-4.4b).
/// </summary>
public sealed class OverrideExamScoreCommandHandler
    : IRequestHandler<OverrideExamScoreCommand, Result<OverrideExamScoreResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public OverrideExamScoreCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OverrideExamScoreResponseDto>> Handle(
        OverrideExamScoreCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuditedScore < 0.0m || request.AuditedScore > 10.0m)
        {
            return Result<OverrideExamScoreResponseDto>.Failure(
                "VALIDATION: Điểm hậu kiểm phải nằm trong thang điểm từ 0.0 đến 10.0.");
        }

        if (string.IsNullOrWhiteSpace(request.OverrideReason) || request.OverrideReason.Trim().Length < 10)
        {
            return Result<OverrideExamScoreResponseDto>.Failure(
                "VALIDATION: Bắt buộc nhập lý do ghi đè điểm (override_reason) tối thiểu 10 ký tự.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive ||
            actor.Role is not (UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin))
        {
            return Result<OverrideExamScoreResponseDto>.Failure(
                "FORBIDDEN: Chỉ giảng viên hoặc Trưởng bộ môn đang hoạt động mới có quyền hậu kiểm và chỉnh sửa điểm.");
        }

        var ticket = await _context.StudentExamTickets
            .Include(t => t.LecturerAudit)
                .ThenInclude(a => a!.Details)
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OverrideExamScoreResponseDto>.Failure(
                "NOT_FOUND: Vé thi không tồn tại trong hệ thống.");
        }

        if (ticket.IsLocked ||
            (ticket.LecturerAudit?.IsLocked ?? false) ||
            ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked)
        {
            return Result<OverrideExamScoreResponseDto>.Failure(
                "LOCKED: Điểm thi đã được công bố và khóa một chiều (One-Way Lock). Mọi chỉnh sửa điểm sau công bố phải thực hiện qua luồng phúc khảo.");
        }

        var trimmedReason = request.OverrideReason.Trim();
        var roundedAuditedScore = Math.Round(request.AuditedScore, 2, MidpointRounding.AwayFromZero);

        decimal originalAiScore;
        if (ticket.LecturerAudit is not null)
        {
            originalAiScore = ticket.LecturerAudit.OriginalAiScore;
        }
        else if (ticket.Submissions.Any(s => s.AiScore.HasValue))
        {
            originalAiScore = Math.Round(
                ticket.Submissions.Where(s => s.AiScore.HasValue).Average(s => s.AiScore!.Value),
                2,
                MidpointRounding.AwayFromZero);
        }
        else
        {
            var evalScores = ticket.Submissions
                .SelectMany(s => s.AiEvaluations)
                .Select(e => e.TotalScore)
                .ToList();

            originalAiScore = evalScores.Count > 0
                ? Math.Round(evalScores.Average(), 2, MidpointRounding.AwayFromZero)
                : roundedAuditedScore;
        }

        if (request.SubmissionId.HasValue && request.SubmissionId.Value != Guid.Empty)
        {
            var targetSubmission = ticket.Submissions.FirstOrDefault(s => s.Id == request.SubmissionId.Value);
            if (targetSubmission is null)
            {
                return Result<OverrideExamScoreResponseDto>.Failure(
                    "NOT_FOUND: Bài nộp câu hỏi (SubmissionId) không tồn tại hoặc không thuộc vé thi này.");
            }

            targetSubmission.FinalScore = roundedAuditedScore;
            targetSubmission.GradingStatus = "audited";

            foreach (var sibling in ticket.Submissions.Where(s => s.Id != targetSubmission.Id))
            {
                sibling.FinalScore ??= sibling.AiScore;
            }
        }
        else
        {
            foreach (var submission in ticket.Submissions)
            {
                submission.FinalScore = roundedAuditedScore;
                submission.GradingStatus = "audited";
            }
        }

        var effectiveTicketAuditedScore = roundedAuditedScore;
        if (request.SubmissionId.HasValue && request.SubmissionId.Value != Guid.Empty && ticket.Submissions.Count > 1)
        {
            var scoredValues = ticket.Submissions
                .Select(s => s.FinalScore ?? s.AiScore)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (scoredValues.Count > 0)
            {
                effectiveTicketAuditedScore = Math.Round(scoredValues.Average(), 2, MidpointRounding.AwayFromZero);
            }
        }

        var now = DateTime.UtcNow;
        var audit = ticket.LecturerAudit;

        if (audit is null)
        {
            audit = new LecturerAudit
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                LecturerId = actor.Id,
                OriginalAiScore = originalAiScore,
                AuditedScore = effectiveTicketAuditedScore,
                OverrideReason = trimmedReason,
                IsLocked = false,
                AuditedAt = now
            };
            _context.LecturerAudits.Add(audit);
        }
        else
        {
            audit.LecturerId = actor.Id;
            audit.AuditedScore = effectiveTicketAuditedScore;
            audit.OverrideReason = trimmedReason;
            audit.AuditedAt = now;
        }

        if (request.CriterionDetails is { Count: > 0 })
        {
            if (audit.Details.Count > 0)
            {
                _context.LecturerAuditDetails.RemoveRange(audit.Details);
                audit.Details.Clear();
            }

            foreach (var detail in request.CriterionDetails)
            {
                var resolvedComment = !string.IsNullOrWhiteSpace(detail.LecturerComment)
                    ? detail.LecturerComment.Trim()
                    : (!string.IsNullOrWhiteSpace(detail.Comment) ? detail.Comment.Trim() : null);

                var detailEntity = new LecturerAuditDetail
                {
                    Id = Guid.NewGuid(),
                    AuditId = audit.Id,
                    CriterionId = detail.CriterionId,
                    AiScore = detail.AiScore ?? 0m,
                    AuditedScore = Math.Round(detail.AuditedScore, 2, MidpointRounding.AwayFromZero),
                    LecturerComment = resolvedComment
                };
                audit.Details.Add(detailEntity);
                _context.LecturerAuditDetails.Add(detailEntity);
            }
        }

        ticket.Status = ExamTicketStatus.Audited;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "OVERRIDE_SCORE",
            EntityName = "student_exam_tickets",
            EntityId = ticket.Id,
            OldValues = originalAiScore.ToString("F2"),
            NewValues = $"{effectiveTicketAuditedScore:F2} | {trimmedReason}",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<OverrideExamScoreResponseDto>.Success(new OverrideExamScoreResponseDto
        {
            AuditId = audit.Id,
            TicketId = ticket.Id,
            LecturerId = actor.Id,
            OriginalAiScore = audit.OriginalAiScore,
            AuditedScore = audit.AuditedScore,
            OverrideReason = audit.OverrideReason,
            TicketStatus = ticket.Status,
            IsLocked = ticket.IsLocked,
            AuditedAt = audit.AuditedAt
        });
    }
}
