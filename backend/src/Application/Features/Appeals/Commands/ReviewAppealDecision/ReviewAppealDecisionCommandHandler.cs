using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;

public sealed class ReviewAppealDecisionCommandHandler : IRequestHandler<ReviewAppealDecisionCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfDigitalSigner? _pdfSigner;

    public ReviewAppealDecisionCommandHandler(
        IApplicationDbContext context,
        IPdfDigitalSigner? pdfSigner = null)
    {
        _context = context;
        _pdfSigner = pdfSigner;
    }

    public async Task<Result<AppealResponseDto>> Handle(ReviewAppealDecisionCommand request, CancellationToken cancellationToken)
    {
        var ticketId = await _context.AppealRequests.AsNoTracking()
            .Where(appeal => appeal.Id == request.AppealId)
            .Select(appeal => (Guid?)appeal.TicketId)
            .FirstOrDefaultAsync(cancellationToken);

        // Preserve the handler's standard not-found result without manufacturing a cross-ticket lock.
        if (!ticketId.HasValue)
        {
            return await _context.ExecuteAppealTransitionAsync(request.AppealId,
                ct => HandleSerialized(request, ct), cancellationToken);
        }

        // Acquire the shift-level row lock (real_exam_session_shifts -> student_exam_tickets ORDER BY id)
        // in consistent order so concurrent appeal approvals in the same shift never race on shift.CertificateHash
        // or LecturerAudit/Submission grades, without opening nested transactions on PostgreSQL.
        var shiftId = await _context.StudentExamTickets.AsNoTracking()
            .Where(ticket => ticket.Id == ticketId.Value)
            .Select(ticket => (Guid?)ticket.ShiftId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shiftId.HasValue)
        {
            return await _context.ExecuteShiftMutationAsync(shiftId.Value, isOfficial: true,
                ct => HandleSerialized(request, ct), cancellationToken);
        }

        return await _context.ExecuteAppealDecisionAsync(request.AppealId, ticketId.Value,
            ct => HandleSerialized(request, ct), cancellationToken);
    }

    private async Task<Result<AppealResponseDto>> HandleSerialized(ReviewAppealDecisionCommand request, CancellationToken cancellationToken)
    {
        var reviewer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ReviewerId && u.IsActive, cancellationToken);
        if (reviewer is null || reviewer.Role != UserRole.ExaminationOfficer)
        {
            return Result<AppealResponseDto>.Failure(AppealAuthorization.Forbidden);
        }

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

        if (appeal.Status != AppealStatus.RegradedByLecturer)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo này đã có quyết định xử lý trước đó.");
        }

        if (request.Decision == AppealStatus.Approved && !appeal.ProposedScore.HasValue)
        {
            return Result<AppealResponseDto>.Failure("CONFLICT: Đơn phúc khảo chưa có điểm chấm lại hợp lệ.");
        }
        if (request.ProposedScore.HasValue && request.ProposedScore != appeal.ProposedScore)
        {
            return Result<AppealResponseDto>.Failure("CONFLICT: Điểm quyết định phải đúng với điểm do giảng viên độc lập đề xuất.");
        }

        var effectiveProposedScore = appeal.ProposedScore;
        var previousStatus = appeal.Status;

        appeal.ReviewedBy = request.ReviewerId;
        appeal.Decision = request.Decision;
        appeal.ReviewNotes = request.ReviewNotes.Trim();
        appeal.ProposedScore = effectiveProposedScore;
        appeal.Status = request.Decision;
        appeal.ResolvedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;

        if (request.Decision == AppealStatus.Approved && effectiveProposedScore.HasValue)
        {
            var ticketSubmissions = await _context.ExamQuestionSubmissions
                .Where(s => s.TicketId == appeal.TicketId)
                .ToListAsync(cancellationToken);

            if (appeal.SubmissionId.HasValue)
            {
                var submission = ticketSubmissions.FirstOrDefault(s => s.Id == appeal.SubmissionId.Value);
                if (submission != null)
                {
                    submission.FinalScore = effectiveProposedScore.Value;
                    foreach (var sibling in ticketSubmissions.Where(s => s.Id != submission.Id))
                    {
                        sibling.FinalScore ??= sibling.AiScore;
                    }
                }
            }
            else
            {
                foreach (var sub in ticketSubmissions)
                {
                    sub.FinalScore = effectiveProposedScore.Value;
                }
            }

            var audit = await _context.LecturerAudits
                .FirstOrDefaultAsync(a => a.TicketId == appeal.TicketId, cancellationToken);
            if (audit != null)
            {
                audit.OverrideReason = request.ReviewNotes.Trim();
                if (appeal.SubmissionId.HasValue && ticketSubmissions.Count > 1)
                {
                    audit.AuditedScore = OfficialExamScoreAggregator.ComputeTicketFinalScore(ticketSubmissions)
                        ?? effectiveProposedScore.Value;
                }
                else
                {
                    audit.AuditedScore = effectiveProposedScore.Value;
                }
            }

            // Recompute shift CertificateHash atomically inside the same transaction when an approved appeal changes grades
            if (appeal.Ticket?.Shift is not null)
            {
                var shift = appeal.Ticket.Shift;
                var shiftTickets = await _context.StudentExamTickets
                    .AsNoTracking()
                    .Include(t => t.Student)
                    .Include(t => t.LecturerAudit)
                    .Include(t => t.Submissions)
                        .ThenInclude(s => s.AiEvaluations)
                    .Where(t => t.ShiftId == shift.Id)
                    .ToListAsync(cancellationToken);

                // Overlay the newly modified submissions & audit of the appealed ticket onto the snapshot for accurate re-hashing
                foreach (var st in shiftTickets)
                {
                    if (st.Id == appeal.TicketId)
                    {
                        st.Submissions = ticketSubmissions;
                        if (audit != null)
                        {
                            st.LecturerAudit = audit;
                        }
                    }
                }

                var tempShift = new Domain.Entities.RealExamSessionShift
                {
                    Id = shift.Id,
                    SessionId = shift.SessionId,
                    ShiftName = shift.ShiftName,
                    RoomLab = shift.RoomLab,
                    StudentExamTickets = shiftTickets
                };
                var updatedRows = ExamShiftCertificateHashGenerator.BuildGradeRows(tempShift);
                shift.CertificateHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(tempShift, updatedRows);
                shift.CertificateThumbprint = _pdfSigner?.GetActiveCertificateThumbprint() ?? shift.CertificateThumbprint;
                shift.CertificateSignedAt = DateTime.UtcNow;
            }
        }

        AppealWorkflowEffects.AddTransitionAudit(_context, reviewer.Id, appeal, previousStatus, appeal.Status,
            request.ReviewNotes.Trim());
        await AppealWorkflowEffects.AddNotificationIfMissingAsync(_context, appeal.StudentId, appeal,
            "APPEAL_DECIDED", "Đơn phúc khảo đã có quyết định",
            $"Đơn phúc khảo của bạn đã được quyết định: {appeal.Status}.", cancellationToken);

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
