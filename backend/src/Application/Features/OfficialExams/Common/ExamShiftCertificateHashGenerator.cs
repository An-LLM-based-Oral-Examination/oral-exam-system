using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

/// <summary>
/// Bộ sinh chữ ký số SHA-256 tất định (deterministic) cho bảng điểm ca thi thật (MF-04)
/// và trích xuất dữ liệu dòng điểm chuẩn hóa từ RealExamSessionShift.
/// </summary>
public static class ExamShiftCertificateHashGenerator
{
    public static List<ShiftGradeRowDto> BuildGradeRows(RealExamSessionShift shift)
    {
        var orderedTickets = (shift.StudentExamTickets ?? Array.Empty<StudentExamTicket>())
            .OrderBy(t => t.SeatNumber)
            .ThenBy(t => t.Id)
            .ToList();

        var rows = new List<ShiftGradeRowDto>(orderedTickets.Count);
        for (int i = 0; i < orderedTickets.Count; i++)
        {
            var ticket = orderedTickets[i];
            var submissions = ticket.Submissions ?? Array.Empty<ExamQuestionSubmission>();
            var latestSubmission = submissions
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefault();

            var allEvals = submissions
                .Where(s => s.AiEvaluations != null)
                .SelectMany(s => s.AiEvaluations)
                .ToList();

            var latestEval = allEvals
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault();

            decimal? aiScore = ticket.LecturerAudit?.OriginalAiScore
                ?? latestSubmission?.AiScore
                ?? (submissions.Any(s => s.AiScore.HasValue)
                    ? (decimal?)Math.Round(submissions.Where(s => s.AiScore.HasValue).Average(s => s.AiScore!.Value), 2, MidpointRounding.AwayFromZero)
                    : null)
                ?? latestEval?.TotalScore;

            decimal? finalScore = ticket.LecturerAudit?.AuditedScore
                ?? latestSubmission?.FinalScore
                ?? (submissions.Any(s => s.FinalScore.HasValue)
                    ? (decimal?)Math.Round(submissions.Where(s => s.FinalScore.HasValue).Average(s => s.FinalScore!.Value), 2, MidpointRounding.AwayFromZero)
                    : null)
                ?? aiScore;

            var audioHash = latestSubmission?.AudioHashSha256
                ?? submissions.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.AudioHashSha256))?.AudioHashSha256;

            rows.Add(new ShiftGradeRowDto
            {
                Index = i + 1,
                SeatNumber = ticket.SeatNumber,
                StudentCode = ticket.Student?.StudentCode ?? string.Empty,
                FullName = ticket.Student?.FullName ?? string.Empty,
                Email = ticket.Student?.Email ?? string.Empty,
                AiScore = aiScore.HasValue ? Math.Round(aiScore.Value, 2, MidpointRounding.AwayFromZero) : null,
                FinalScore = finalScore.HasValue ? Math.Round(finalScore.Value, 2, MidpointRounding.AwayFromZero) : null,
                TicketStatus = ticket.Status ?? string.Empty,
                IsLocked = ticket.IsLocked,
                OverrideReason = ticket.LecturerAudit?.OverrideReason,
                AudioHashSha256 = audioHash
            });
        }

        return rows;
    }

    public static string ComputeCertificateHash(RealExamSessionShift shift, IReadOnlyList<ShiftGradeRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.Append("FPTU-ORAL-EXAM-SEAL-V1|");
        sb.Append(shift.Id.ToString("D")).Append('|');
        sb.Append(shift.SessionId.ToString("D")).Append('|');
        sb.Append(shift.ShiftName?.Trim() ?? string.Empty).Append('|');
        sb.Append(shift.RoomLab?.Trim() ?? string.Empty).Append('|');

        foreach (var row in rows.OrderBy(r => r.SeatNumber).ThenBy(r => r.StudentCode, StringComparer.Ordinal))
        {
            var scoreText = row.FinalScore.HasValue
                ? row.FinalScore.Value.ToString("0.00", CultureInfo.InvariantCulture)
                : "NA";
            sb.Append(row.SeatNumber.ToString(CultureInfo.InvariantCulture)).Append(':')
              .Append(row.StudentCode.Trim()).Append(':')
              .Append(scoreText).Append(':')
              .Append(row.IsLocked ? '1' : '0').Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
