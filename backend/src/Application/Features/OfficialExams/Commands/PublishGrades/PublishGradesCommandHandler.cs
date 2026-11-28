using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;

/// <summary>
/// Handler xử lý công bố điểm toàn bộ ca thi phòng Lab (MF-04).
/// BẮT BUỘC tuân thủ Hard Gate: 100% sinh viên trong ca thi phải có điểm hoàn chỉnh
/// trước khi kích hoạt khóa điểm một chiều One-Way Lock.
/// </summary>
public sealed class PublishGradesCommandHandler : IRequestHandler<PublishGradesCommand, Result<PublishGradesResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfDigitalSigner? _pdfSigner;

    public PublishGradesCommandHandler(
        IApplicationDbContext context,
        IPdfDigitalSigner? pdfSigner = null)
    {
        _context = context;
        _pdfSigner = pdfSigner;
    }

    public async Task<Result<PublishGradesResponseDto>> Handle(PublishGradesCommand request, CancellationToken cancellationToken)
    {
        // 1. Only the active examination officer may publish school-wide official grades.
        var lecturer = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);

        if (lecturer == null)
        {
            return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.ActorNotFound);
        }

        if (!lecturer.IsActive || lecturer.Role != UserRole.ExaminationOfficer)
        {
            return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.Forbidden);
        }

        // 2..6. Khóa hàng ca thi & toàn bộ vé thi trong 1 transaction nguyên tử để chống race với Giám khảo hậu kiểm / khóa vé / đình chỉ.
        return await _context.ExecuteShiftMutationAsync(
            request.ShiftId,
            isOfficial: true,
            async ct =>
            {
                // 2. Kiểm tra ca thi tồn tại
                var shift = await _context.RealExamSessionShifts
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

                if (shift == null)
                {
                    return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.ShiftNotFound);
                }

                // 3. Lấy toàn bộ vé thi của ca thi
                var tickets = await _context.StudentExamTickets
                    .Include(t => t.Student)
                    .Include(t => t.LecturerAudit)
                    .Include(t => t.Submissions)
                        .ThenInclude(submission => submission.GradingJob)
                    .Where(t => t.ShiftId == request.ShiftId)
                    .ToListAsync(ct);

                if (!tickets.Any())
                {
                    return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.NoTickets);
                }

                var isEnded = shift.Status == ExamShiftWindow.Completed
                    || (shift.Status == ExamShiftWindow.InProgress && DateTime.UtcNow >= shift.EndTime);

                if (!isEnded || shift.Status is ExamShiftWindow.Scheduled or ExamShiftWindow.Cancelled)
                {
                    return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.ShiftNotEnded);
                }

                // 4. Kiểm tra ca thi đã được công bố điểm từ trước chưa
                // (Không chặn chỉ vì tickets.All(t => t.IsLocked) do Giám khảo khóa tay qua LockExamTicket trước khi Khảo thí công bố).
                if (tickets.Any(t => t.Status == ExamTicketStatus.Published || t.GradesPublishedAt.HasValue))
                {
                    return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.AlreadyPublished);
                }

                // 5. CHỐT CHẶN 100%: Toàn bộ sinh viên trong ca thi bắt buộc phải có điểm hoàn chỉnh
                var unscoredTickets = tickets.Where(t => !HasCompleteScore(t)).ToList();
                if (unscoredTickets.Any())
                {
                    return Result<PublishGradesResponseDto>.Failure(PublishGradesErrors.IncompleteGrades);
                }

                // 6. Kích hoạt One-Way Lock & nâng cấp tất cả vé (kể cả vé đã khóa tay LOCKED chưa công bố) sang PUBLISHED
                var publishedAt = DateTime.UtcNow;

                foreach (var ticket in tickets)
                {
                    // Đồng bộ điểm FinalScore nếu còn để trống nhưng đã có AiScore
                    foreach (var sub in ticket.Submissions)
                    {
                        if (!sub.FinalScore.HasValue && sub.AiScore.HasValue)
                        {
                            sub.FinalScore = sub.AiScore.Value;
                        }
                    }

                    ticket.Status = ExamTicketStatus.Published;
                    ticket.GradesPublishedAt = publishedAt;
                    if (!ticket.IsLocked)
                    {
                        ticket.IsLocked = true;
                    }

                    if (ticket.LecturerAudit != null && !ticket.LecturerAudit.IsLocked)
                    {
                        ticket.LecturerAudit.IsLocked = true;
                    }
                }

                shift.Status = "completed";
                shift.StudentExamTickets = tickets;
                var gradeRows = ExamShiftCertificateHashGenerator.BuildGradeRows(shift);
                shift.CertificateHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(shift, gradeRows);
                shift.CertificateThumbprint = _pdfSigner?.GetActiveCertificateThumbprint() ?? shift.CertificateThumbprint;
                shift.CertificateSignedAt = publishedAt;

                await _context.SaveChangesAsync(ct);

                var response = new PublishGradesResponseDto
                {
                    ShiftId = shift.Id,
                    TotalPublished = tickets.Count,
                    PublishedAt = publishedAt,
                    Message = $"Đã công bố điểm thành công cho toàn bộ {tickets.Count}/{tickets.Count} sinh viên trong ca thi và kích hoạt khóa một chiều (One-Way Lock)."
                };

                return Result<PublishGradesResponseDto>.Success(response);
            },
            cancellationToken);
    }

    /// <summary>
    /// Kiểm tra xem vé thi của sinh viên đã có điểm hoàn chỉnh hay chưa.
    /// Có điểm khi:
    /// - Đã được giảng viên thẩm định (LecturerAudit có điểm AuditedScore), HOẶC
    /// - Đã có bản nộp câu hỏi và 100% bản nộp đều có điểm (FinalScore hoặc AiScore).
    /// </summary>
    private static bool HasCompleteScore(StudentExamTicket ticket)
    {
        if (ticket.Status is not (ExamTicketStatus.AiGraded or ExamTicketStatus.Audited or ExamTicketStatus.Locked))
        {
            return false;
        }

        if (ticket.LecturerAudit != null)
        {
            return IsValidScore(ticket.LecturerAudit.AuditedScore)
                && !string.IsNullOrWhiteSpace(ticket.LecturerAudit.OverrideReason);
        }

        if (ticket.Submissions.Any() && ticket.Submissions.All(submission =>
                submission.GradingStatus is "ai_graded" or "audited"
                && submission.GradingJob?.Status is null or "completed"
                && IsValidScore(submission.FinalScore ?? submission.AiScore)))
        {
            return true;
        }

        return false;
    }

    private static bool IsValidScore(decimal? score) => score is >= 0m and <= 10m;
}
