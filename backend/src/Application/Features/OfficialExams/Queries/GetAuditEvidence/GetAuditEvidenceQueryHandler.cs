using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;

/// <summary>
/// Handler xử lý trích xuất danh sách bằng chứng hậu kiểm (Evidence Panel) cho ca thi phòng Lab.
/// </summary>
public sealed class GetAuditEvidenceQueryHandler : IRequestHandler<GetAuditEvidenceQuery, Result<List<AuditEvidenceItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditEvidenceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AuditEvidenceItemDto>>> Handle(GetAuditEvidenceQuery request, CancellationToken cancellationToken)
    {
        var shiftExists = await _context.RealExamSessionShifts
            .AnyAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (!shiftExists)
        {
            return Result<List<AuditEvidenceItemDto>>.Failure("Ca thi không tồn tại trong hệ thống.");
        }

        var tickets = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.LecturerAudit)
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
            .Where(t => t.ShiftId == request.ShiftId)
            .OrderBy(t => t.SeatNumber)
            .ToListAsync(cancellationToken);

        var items = new List<AuditEvidenceItemDto>();

        foreach (var ticket in tickets)
        {
            var latestSubmission = ticket.Submissions
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefault();

            var allEvals = ticket.Submissions
                .SelectMany(s => s.AiEvaluations)
                .ToList();

            var latestEval = allEvals
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault();

            // Audio & Transcript
            var audioR2Url = latestSubmission?.AudioR2Url
                ?? ticket.Submissions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AudioR2Url))?.AudioR2Url;

            var audioHashSha256 = latestSubmission?.AudioHashSha256
                ?? ticket.Submissions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AudioHashSha256))?.AudioHashSha256;

            var transcripts = ticket.Submissions
                .Where(s => !string.IsNullOrWhiteSpace(s.TranscriptWhisper))
                .Select(s => s.TranscriptWhisper!)
                .ToList();

            var transcriptWhisper = transcripts.Count > 1
                ? string.Join("\n\n---\n\n", transcripts)
                : transcripts.FirstOrDefault();

            // Scores
            decimal? aiScore = ticket.LecturerAudit?.OriginalAiScore
                ?? latestSubmission?.AiScore
                ?? (ticket.Submissions.Any(s => s.AiScore.HasValue) ? (decimal?)ticket.Submissions.Average(s => s.AiScore!.Value) : null)
                ?? latestEval?.TotalScore;

            decimal? finalScore = ticket.LecturerAudit?.AuditedScore
                ?? latestSubmission?.FinalScore
                ?? (ticket.Submissions.Any(s => s.FinalScore.HasValue) ? (decimal?)ticket.Submissions.Average(s => s.FinalScore!.Value) : null)
                ?? aiScore;


            // Confidence & CoT
            var confidences = allEvals
                .Where(e => e.ConfidenceScore.HasValue)
                .Select(e => e.ConfidenceScore!.Value)
                .ToList();

            decimal? confidenceScore = confidences.Any() ? confidences.Min() : null;

            var cotTraces = allEvals
                .Where(e => !string.IsNullOrWhiteSpace(e.CotTrace))
                .Select(e => e.CotTrace!)
                .ToList();

            var cotTrace = cotTraces.Count > 1
                ? string.Join("\n\n---\n\n", cotTraces)
                : cotTraces.FirstOrDefault();

            // Suspicious Check (AI Doubt Guard)
            bool isSuspicious = false;
            string? suspiciousReason = null;

            if (confidenceScore.HasValue && confidenceScore.Value < 0.70m)
            {
                isSuspicious = true;
                suspiciousReason = $"Độ tin cậy AI thấp ({(int)Math.Round(confidenceScore.Value * 100, MidpointRounding.AwayFromZero)}% < 70%). Cần đối soát lại phát âm hoặc chất lượng ghi âm.";
            }
            else if (ticket.Submissions.Any(s => s.GradingStatus == "suspicious"))
            {
                isSuspicious = true;
                suspiciousReason = "Bài nộp bị đánh dấu nghi ngờ trong quá trình chấm.";
            }
            else if (ticket.LecturerAudit != null && Math.Abs(ticket.LecturerAudit.AuditedScore - ticket.LecturerAudit.OriginalAiScore) >= 2.0m)
            {
                isSuspicious = true;
                suspiciousReason = $"Điểm giảng viên chốt ({ticket.LecturerAudit.AuditedScore:F2}) lệch lớn so với AI ban đầu ({ticket.LecturerAudit.OriginalAiScore:F2}).";
            }

            items.Add(new AuditEvidenceItemDto
            {
                TicketId = ticket.Id,
                StudentCode = ticket.Student?.StudentCode ?? string.Empty,
                FullName = ticket.Student?.FullName ?? string.Empty,
                SeatNumber = ticket.SeatNumber,
                Status = ticket.Status,
                AudioR2Url = audioR2Url,
                AudioHashSha256 = audioHashSha256,
                TranscriptWhisper = transcriptWhisper,
                AiScore = aiScore,
                ConfidenceScore = confidenceScore,
                IsSuspicious = isSuspicious,
                SuspiciousReason = suspiciousReason,
                CotTrace = cotTrace,
                FinalScore = finalScore,
                IsLocked = ticket.IsLocked
            });
        }

        return Result<List<AuditEvidenceItemDto>>.Success(items);
    }
}
