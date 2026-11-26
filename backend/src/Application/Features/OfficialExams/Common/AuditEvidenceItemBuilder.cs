using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal static class AuditEvidenceItemBuilder
{
    public static async Task<AuditEvidenceItemDto> BuildAsync(
        StudentExamTicket ticket,
        IReadOnlyDictionary<Guid, ExamQuestion> questionMap,
        IStorageService? storageService,
        CancellationToken cancellationToken)
    {
        var orderedSubmissions = ticket.Submissions
            .OrderBy(s => s.SubmittedAt)
            .ThenBy(s => s.FollowUpIndex)
            .ToList();

        var mainSubmissions = orderedSubmissions.Where(s => !s.IsFollowUp).ToList();
        var followUpSubmissions = orderedSubmissions.Where(s => s.IsFollowUp).ToList();
        var latestSubmission = mainSubmissions.LastOrDefault() ?? orderedSubmissions.LastOrDefault();

        var latestEvalPerSubmission = orderedSubmissions
            .Select(s => s.AiEvaluations
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault())
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        var latestEval = latestEvalPerSubmission
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstOrDefault();

        static bool IsDisallowedUriScheme(string scheme) =>
            string.Equals(scheme, "javascript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "data", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "vbscript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "blob", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "ftp", StringComparison.OrdinalIgnoreCase);

        async Task<string?> ResolveAudioUrlAsync(string? rawKeyOrUrl)
        {
            if (string.IsNullOrWhiteSpace(rawKeyOrUrl))
            {
                return null;
            }

            var candidate = rawKeyOrUrl.Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsedUri))
            {
                if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (IsDisallowedUriScheme(parsedUri.Scheme))
                {
                    return null;
                }
            }

            if (storageService is not null)
            {
                try
                {
                    var presigned = await storageService.CreatePresignedDownloadUrlAsync(
                        candidate,
                        TimeSpan.FromMinutes(15),
                        cancellationToken);
                    return presigned.DownloadUrl;
                }
                catch (InvalidOperationException)
                {
                    // Khi R2 chưa được cấu hình ở môi trường dev/test cục bộ, degrade an toàn thay vì làm sập HTTP 500 toàn Evidence Panel.
                    return null;
                }
            }

            return candidate;
        }

        List<AuditCriterionEvidenceItemDto>? BuildCriterionDetails(ExamQuestionSubmission? sub, AiEvaluation? eval)
        {
            if (sub is null)
            {
                return null;
            }

            var auditDetails = ticket.LecturerAudit?.Details?
                .Where(d => d.SubmissionId == sub.Id || (d.SubmissionId is null && mainSubmissions.Count <= 1))
                .GroupBy(d => d.CriterionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.SubmissionId.HasValue).ThenByDescending(d => d.Id).First())
                ?? new Dictionary<Guid, LecturerAuditDetail>();

            var evalDetails = eval?.Details?.ToList() ?? [];
            if (evalDetails.Count == 0 && auditDetails.Count == 0)
            {
                return null;
            }

            var result = new List<AuditCriterionEvidenceItemDto>();
            var seenCriteria = new HashSet<Guid>();

            foreach (var d in evalDetails)
            {
                seenCriteria.Add(d.CriterionId);
                auditDetails.TryGetValue(d.CriterionId, out var overrideDetail);
                result.Add(new AuditCriterionEvidenceItemDto
                {
                    CriterionId = d.CriterionId,
                    CriterionName = d.Criterion?.CriterionName ?? "Tiêu chí đánh giá",
                    MaxScore = d.Criterion?.MaxScore ?? 10m,
                    AiScore = d.Score,
                    AuditedScore = overrideDetail?.AuditedScore,
                    Comment = overrideDetail?.LecturerComment ?? d.Comment
                });
            }

            foreach (var extra in auditDetails.Values.Where(a => !seenCriteria.Contains(a.CriterionId)))
            {
                result.Add(new AuditCriterionEvidenceItemDto
                {
                    CriterionId = extra.CriterionId,
                    CriterionName = "Tiêu chí đánh giá",
                    MaxScore = 10m,
                    AiScore = extra.AiScore,
                    AuditedScore = extra.AuditedScore,
                    Comment = extra.LecturerComment
                });
            }

            return result.Count > 0 ? result : null;
        }

        var rawAudioKey = latestSubmission?.AudioObjectKey
            ?? latestSubmission?.AudioR2Url
            ?? orderedSubmissions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AudioObjectKey ?? s.AudioR2Url))?.AudioObjectKey
            ?? orderedSubmissions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AudioR2Url))?.AudioR2Url
            ?? ticket.AudioObjectKey
            ?? ticket.AudioR2Url;

        var audioR2Url = await ResolveAudioUrlAsync(rawAudioKey);

        var audioHashSha256 = latestSubmission?.AudioHashSha256
            ?? orderedSubmissions.FirstOrDefault(s => !string.IsNullOrEmpty(s.AudioHashSha256))?.AudioHashSha256
            ?? ticket.AudioHashSha256;

        var transcripts = orderedSubmissions
            .Where(s => !string.IsNullOrWhiteSpace(s.TranscriptWhisper))
            .Select((s, index) =>
            {
                if (orderedSubmissions.Count <= 1)
                {
                    return s.TranscriptWhisper!;
                }

                questionMap.TryGetValue(s.ExamQuestionId, out var q);
                var label = s.IsFollowUp
                    ? $"[Câu phụ {s.FollowUpIndex}: {s.FollowUpPrompt ?? q?.Title ?? "Follow-up"}]"
                    : (!string.IsNullOrWhiteSpace(q?.Title)
                        ? $"[Câu {index + 1}: {q.Title}]"
                        : $"[Câu {index + 1}]");
                return $"{label}\n{s.TranscriptWhisper!}";
            })
            .ToList();

        var transcriptWhisper = transcripts.Count > 1
            ? string.Join("\n\n---\n\n", transcripts)
            : transcripts.FirstOrDefault();

        decimal? aiScore = ticket.LecturerAudit?.OriginalAiScore
            ?? OfficialExamScoreAggregator.ComputeTicketAiScore(orderedSubmissions)
            ?? latestSubmission?.AiScore
            ?? latestEval?.TotalScore;

        decimal? finalScore = ticket.LecturerAudit?.AuditedScore
            ?? OfficialExamScoreAggregator.ComputeTicketFinalScore(orderedSubmissions)
            ?? latestSubmission?.FinalScore
            ?? aiScore;

        var mainSubmissionItems = new List<AuditMainSubmissionEvidenceItemDto>(mainSubmissions.Count);
        for (var idx = 0; idx < mainSubmissions.Count; idx++)
        {
            var main = mainSubmissions[idx];
            var mainEval = main.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
            var mainAudio = await ResolveAudioUrlAsync(main.AudioObjectKey ?? main.AudioR2Url ?? (mainSubmissions.Count == 1 ? ticket.AudioObjectKey ?? ticket.AudioR2Url : null));
            questionMap.TryGetValue(main.ExamQuestionId, out var mainQuestion);

            mainSubmissionItems.Add(new AuditMainSubmissionEvidenceItemDto
            {
                SubmissionId = main.Id,
                ExamQuestionId = main.ExamQuestionId,
                OrderIndex = idx + 1,
                QuestionTitle = mainQuestion?.Title,
                QuestionContent = mainQuestion?.Content,
                QuestionImageUrl = mainQuestion?.ImageUrl,
                QuestionImageDescription = mainQuestion?.ImageDescription,
                AudioR2Url = mainAudio,
                AudioHashSha256 = main.AudioHashSha256 ?? (mainSubmissions.Count == 1 ? ticket.AudioHashSha256 : null),
                TranscriptWhisper = main.TranscriptWhisper,
                TimestampsWhisper = main.TimestampsWhisper,
                AiScore = main.AiScore ?? mainEval?.TotalScore,
                ConfidenceScore = mainEval?.ConfidenceScore,
                IsSuspicious = main.IsSuspicious,
                SuspiciousReason = main.SuspiciousReason,
                FinalScore = main.FinalScore ?? main.AiScore ?? mainEval?.TotalScore,
                CotTrace = mainEval?.CotTrace,
                CriterionDetails = BuildCriterionDetails(main, mainEval)
            });
        }

        var followUpItems = new List<AuditFollowUpEvidenceItemDto>(followUpSubmissions.Count);
        foreach (var fu in followUpSubmissions)
        {
            var fuEval = fu.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
            var fuAudio = await ResolveAudioUrlAsync(fu.AudioObjectKey ?? fu.AudioR2Url);
            followUpItems.Add(new AuditFollowUpEvidenceItemDto
            {
                SubmissionId = fu.Id,
                ParentSubmissionId = fu.ParentSubmissionId,
                ExamQuestionId = fu.ExamQuestionId,
                FollowUpIndex = fu.FollowUpIndex,
                FollowUpPrompt = fu.FollowUpPrompt,
                TranscriptWhisper = fu.TranscriptWhisper,
                AudioR2Url = fuAudio,
                AudioHashSha256 = fu.AudioHashSha256,
                AiScore = fu.AiScore ?? fuEval?.TotalScore,
                FinalScore = fu.FinalScore ?? fu.AiScore ?? fuEval?.TotalScore,
                CotTrace = fuEval?.CotTrace
            });
        }

        var confidences = latestEvalPerSubmission
            .Where(e => e.ConfidenceScore.HasValue)
            .Select(e => e.ConfidenceScore!.Value)
            .ToList();

        decimal? confidenceScore = confidences.Any() ? confidences.Min() : null;

        var cotTraces = latestEvalPerSubmission
            .Where(e => !string.IsNullOrWhiteSpace(e.CotTrace))
            .Select(e => e.CotTrace!)
            .ToList();

        var cotTrace = cotTraces.Count > 1
            ? string.Join("\n\n---\n\n", cotTraces)
            : cotTraces.FirstOrDefault();

        bool isSuspicious = false;
        string? suspiciousReason = null;

        if (confidenceScore.HasValue && confidenceScore.Value < 0.70m)
        {
            isSuspicious = true;
            suspiciousReason = $"Độ tin cậy AI thấp ({(int)Math.Round(confidenceScore.Value * 100, MidpointRounding.AwayFromZero)}% < 70%). Cần đối soát lại phát âm hoặc chất lượng ghi âm.";
        }
        else if (orderedSubmissions.Any(s => s.IsSuspicious))
        {
            isSuspicious = true;
            suspiciousReason = orderedSubmissions
                .Select(s => s.SuspiciousReason)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))
                ?? "Bài nộp bị đánh dấu nghi ngờ trong quá trình chấm.";
        }
        else if (ticket.LecturerAudit != null && Math.Abs(ticket.LecturerAudit.AuditedScore - ticket.LecturerAudit.OriginalAiScore) >= 2.0m)
        {
            isSuspicious = true;
            suspiciousReason = $"Điểm giảng viên chốt ({ticket.LecturerAudit.AuditedScore:F2}) lệch lớn so với AI ban đầu ({ticket.LecturerAudit.OriginalAiScore:F2}).";
        }
        else if ((finalScore ?? aiScore) is < 5.0m)
        {
            isSuspicious = true;
            suspiciousReason = $"Điểm bài thi dưới mức đạt ({(finalScore ?? aiScore)!.Value:F2} < 5.00). Cần giảng viên hậu kiểm trước khi công bố.";
        }

        ExamQuestion? latestQuestion = null;
        if (latestSubmission != null)
        {
            questionMap.TryGetValue(latestSubmission.ExamQuestionId, out latestQuestion);
        }

        var latestSubmissionEval = latestSubmission?.AiEvaluations
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstOrDefault();

        return new AuditEvidenceItemDto
        {
            TicketId = ticket.Id,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            FullName = ticket.Student?.FullName ?? string.Empty,
            SeatNumber = ticket.SeatNumber,
            Status = ticket.Status,
            AudioR2Url = audioR2Url,
            AudioHashSha256 = audioHashSha256,
            TranscriptWhisper = transcriptWhisper,
            TimestampsWhisper = latestSubmission?.TimestampsWhisper,
            AiScore = aiScore,
            ConfidenceScore = confidenceScore,
            IsSuspicious = isSuspicious,
            SuspiciousReason = suspiciousReason,
            AiChainOfThought = cotTrace,
            FinalScore = finalScore,
            IsLocked = ticket.IsLocked,
            SubmissionId = latestSubmission?.Id,
            OverrideReason = ticket.LecturerAudit?.OverrideReason,
            QuestionId = latestSubmission?.ExamQuestionId,
            QuestionTitle = latestQuestion?.Title,
            QuestionContent = latestQuestion?.Content,
            QuestionImageUrl = latestQuestion?.ImageUrl,
            QuestionImageDescription = latestQuestion?.ImageDescription,
            CriterionDetails = BuildCriterionDetails(latestSubmission, latestSubmissionEval),
            MainSubmissions = mainSubmissionItems.Count > 0 ? mainSubmissionItems : null,
            FollowUpItems = followUpItems.Count > 0 ? followUpItems : null
        };
    }
}
