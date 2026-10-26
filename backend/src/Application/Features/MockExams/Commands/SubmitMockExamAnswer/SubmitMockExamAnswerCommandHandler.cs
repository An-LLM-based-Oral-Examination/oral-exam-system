using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;

/// <summary>
/// Handler xử lý nộp câu trả lời Voice-First cho một câu hỏi trong ca thi thử (MF-02).
/// Sử dụng Cloudflare Whisper AI bóc băng trong RAM, cam kết Zero R2 storage (không lưu audio).
/// Khi ca thi bật HasFollowUp, sau câu chính/phụ sẽ mở câu hỏi phụ động dựa trên ngữ cảnh câu trả lời của sinh viên.
/// </summary>
public sealed class SubmitMockExamAnswerCommandHandler
    : IRequestHandler<SubmitMockExamAnswerCommand, Result<SubmitMockExamAnswerResponse>>
{
    private static readonly ConcurrentDictionary<Guid, string> VoiceTranscriptBaselines = new();

    private readonly IApplicationDbContext _context;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IAiGradingService? _aiGradingService;
    private readonly ILogger<SubmitMockExamAnswerCommandHandler> _logger;

    public SubmitMockExamAnswerCommandHandler(
        IApplicationDbContext context,
        ISpeechToTextService speechToTextService,
        IAiGradingService? aiGradingService = null,
        ILogger<SubmitMockExamAnswerCommandHandler>? logger = null)
    {
        _context = context;
        _speechToTextService = speechToTextService;
        _aiGradingService = aiGradingService;
        _logger = logger ?? NullLogger<SubmitMockExamAnswerCommandHandler>.Instance;
    }

    public async Task<Result<SubmitMockExamAnswerResponse>> Handle(
        SubmitMockExamAnswerCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _context.MockExamSessions
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi không tồn tại.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Bạn không có quyền nộp bài cho ca thi này.");
        }

        if (session.Status == "submitted" || session.Status == "graded")
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã kết thúc hoặc đã nộp bài, không thể ghi nhận thêm câu trả lời.");
        }

        var now = DateTime.UtcNow;
        if (now > session.ExpiresAt.AddSeconds(10))
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã hết thời gian làm bài.");
        }

        var answer = ResolveTargetAnswer(session, request.QuestionId, request.AnswerId);
        if (answer == null)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Câu hỏi không thuộc đề thi này.");
        }

        // Kiểm tra xem câu này đã từng được trả lời bằng giọng nói trước đó chưa
        var wasAlreadyAnswered =
            string.Equals(answer.Status, "answered", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(answer.AnswerText);

        var transcriptResult = await ResolveTranscriptAsync(request, answer, wasAlreadyAnswered, session.Course?.Code, cancellationToken);
        if (!transcriptResult.IsSuccess)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure(transcriptResult.Error ?? "Không nhận diện được giọng nói.");
        }

        if (answer.AiEvaluations is { Count: > 0 } &&
            !string.Equals(answer.AnswerText, transcriptResult.Value, StringComparison.Ordinal))
        {
            _context.AiEvaluations.RemoveRange(answer.AiEvaluations);
            answer.AiEvaluations.Clear();
        }

        answer.AnswerText = transcriptResult.Value;
        answer.TimeTakenSeconds = request.TimeTakenSeconds;
        answer.Status = "answered";
        if (!wasAlreadyAnswered || !answer.IsFollowUp)
        {
            answer.AnsweredAt = DateTime.UtcNow;
        }

        MockExamAnswer? newFollowUp = null;
        if (session.HasFollowUp)
        {
            newFollowUp = wasAlreadyAnswered
                ? await TryRefreshPendingFollowUpAsync(session, answer, cancellationToken)
                : await TryOpenNextFollowUpAsync(session, answer, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var rootParentId = RootParentId(answer);
        var followUpIdx = newFollowUp is null
            ? 0
            : GetFollowUpOrdinal(session, rootParentId, newFollowUp.Id);

        return Result<SubmitMockExamAnswerResponse>.Success(new SubmitMockExamAnswerResponse(
            AnswerId: answer.Id,
            QuestionId: answer.QuestionId,
            Transcript: answer.AnswerText ?? string.Empty,
            Status: answer.Status,
            AnsweredAt: answer.AnsweredAt,
            IsFollowUp: answer.IsFollowUp,
            ParentAnswerId: answer.ParentAnswerId,
            NeedsFollowUp: newFollowUp is not null,
            FollowUpPrompt: newFollowUp?.PromptText,
            FollowUpAnswerId: newFollowUp?.Id,
            FollowUpIndex: followUpIdx,
            MaxFollowUps: GetMaxFollowUps(session)
        ));
    }

    private static MockExamAnswer? ResolveTargetAnswer(
        MockExamSession session,
        Guid questionId,
        Guid? answerId)
    {
        if (answerId is Guid id && id != Guid.Empty)
        {
            return session.Answers.FirstOrDefault(a => a.Id == id && a.QuestionId == questionId);
        }

        // Mặc định: câu chính (không phải follow-up)
        return session.Answers.FirstOrDefault(a => a.QuestionId == questionId && !a.IsFollowUp);
    }

    private async Task<Result<string>> ResolveTranscriptAsync(
        SubmitMockExamAnswerCommand request,
        MockExamAnswer targetAnswer,
        bool wasAlreadyAnswered,
        string? courseCode,
        CancellationToken cancellationToken)
    {
        if (request.AudioStream != null && (!request.AudioStream.CanSeek || request.AudioStream.Length > 0))
        {
            var normalizedClient = !string.IsNullOrWhiteSpace(request.DirectTranscript)
                ? TranscriptHelper.NormalizeOrFallback(
                    request.DirectTranscript,
                    _speechToTextService.NormalizeTranscript(request.DirectTranscript))
                : string.Empty;

            // Tối ưu hiệu năng: Khi Frontend đã bóc băng trước qua /api/v1/speech/stt (đã chứng thực tại Server STT)
            // và gửi kèm file âm thanh thật để chứng thực Voice-First Gate, sử dụng trực tiếp bản bóc băng hợp lệ
            // thay vì gọi lặp lại Cloudflare Whisper lần thứ 2 trên cùng file âm thanh.
            if (!string.IsNullOrWhiteSpace(normalizedClient) && TranscriptHelper.IsVerifiedServerTranscript(normalizedClient))
            {
                LockVoiceBaseline(targetAnswer, normalizedClient);
                return Result<string>.Success(normalizedClient);
            }

            var fileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "audio.webm";
            Exception? sttException = null;
            var transcript = string.Empty;
            SpeechTranscriptionResult? detailed = null;
            double? serverConfidence = null;
            string? sttHint = null;
            var hasRealAudioHeader = false;
            System.IO.MemoryStream? bufferedStream = null;

            try
            {
                System.IO.Stream streamToTranscribe = request.AudioStream;
                if (request.AudioStream.CanSeek)
                {
                    request.AudioStream.Position = 0;
                    var headerBuffer = new byte[256];
                    var bytesRead = await request.AudioStream.ReadAsync(headerBuffer.AsMemory(0, 256), cancellationToken);
                    hasRealAudioHeader = TranscriptHelper.HasRealAudioContainerHeader(headerBuffer.AsSpan(0, bytesRead));
                    request.AudioStream.Position = 0;
                }
                else
                {
                    bufferedStream = new System.IO.MemoryStream();
                    await request.AudioStream.CopyToAsync(bufferedStream, cancellationToken);
                    if (bufferedStream.Length == 0)
                    {
                        return Result<string>.Failure("File âm thanh không hợp lệ hoặc không có dữ liệu.");
                    }

                    var headerLength = (int)Math.Min(256, bufferedStream.Length);
                    hasRealAudioHeader = TranscriptHelper.HasRealAudioContainerHeader(
                        bufferedStream.GetBuffer().AsSpan(0, headerLength));
                    bufferedStream.Position = 0;
                    streamToTranscribe = bufferedStream;
                }

                // Không truyền Title/Content làm vocabularyHint — tránh STT bịa đáp án khi im lặng.
                // Không ép lang:en để Whisper Large-v3-Turbo nhận diện song ngữ Việt - Anh cho mọi môn học (cả ENT và SE).
                detailed = await _speechToTextService.TranscribeDetailedAsync(
                    streamToTranscribe,
                    fileName,
                    vocabularyHint: sttHint,
                    cancellationToken);
                transcript = detailed?.Text ?? string.Empty;
                serverConfidence = detailed?.ConfidenceScore;

                if (detailed is null && string.IsNullOrWhiteSpace(transcript) && string.IsNullOrWhiteSpace(normalizedClient))
                {
                    if (streamToTranscribe.CanSeek)
                    {
                        streamToTranscribe.Position = 0;
                    }
                    transcript = await _speechToTextService.TranscribeAudioAsync(
                        streamToTranscribe,
                        fileName,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                sttException = ex;
            }
            finally
            {
                bufferedStream?.Dispose();
            }

            var normalizedServer = !string.IsNullOrWhiteSpace(transcript)
                ? TranscriptHelper.NormalizeOrFallback(transcript, _speechToTextService.NormalizeTranscript(transcript))
                : string.Empty;

            if (serverConfidence is > 0.0 and < 0.35 && !string.IsNullOrWhiteSpace(normalizedClient))
            {
                LockVoiceBaseline(targetAnswer, normalizedClient);
                return Result<string>.Success(normalizedClient);
            }

            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                TranscriptHelper.RegisterVerifiedTranscript(normalizedServer);
                LockVoiceBaseline(targetAnswer, normalizedServer);
                return Result<string>.Success(normalizedServer);
            }

            if (hasRealAudioHeader && TranscriptHelper.IsServerConfirmedSilence(detailed, sttException))
            {
                return Result<string>.Failure(
                    "Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedClient))
            {
                LockVoiceBaseline(targetAnswer, normalizedClient);
                return Result<string>.Success(normalizedClient);
            }

            if (sttException != null)
            {
                return Result<string>.Failure($"Lỗi khi bóc băng âm thanh: {sttException.Message}");
            }

            return Result<string>.Failure(
                "Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
        }

        if (!string.IsNullOrWhiteSpace(request.DirectTranscript))
        {
            var normalized = TranscriptHelper.NormalizeOrFallback(
                request.DirectTranscript,
                _speechToTextService.NormalizeTranscript(request.DirectTranscript));

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Result<string>.Failure(
                    "Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
            }

            if (!wasAlreadyAnswered)
            {
                return Result<string>.Failure(
                    "VOICE_FIRST_GATE: Câu hỏi này chưa có bản ghi âm giọng nói. Bắt buộc ghi âm qua microphone trước khi hiệu đính văn bản.");
            }

            var baselineText = ResolveImmutableVoiceBaseline(targetAnswer);
            var baselineLength = baselineText.Length;
            var maxAllowedLength = Math.Max((int)Math.Ceiling(baselineLength * 2.5), baselineLength + 120);
            if (normalized.Length > maxAllowedLength || !IsMinorTermRevision(baselineText, normalized))
            {
                return Result<string>.Failure(
                    "VOICE_FIRST_GATE: Chỉ cho phép hiệu đính thuật ngữ trên bản ghi âm đã có; vui lòng ghi âm lại nếu muốn thay đổi toàn bộ câu trả lời.");
            }

            return Result<string>.Success(normalized);
        }

        return Result<string>.Failure("File âm thanh không hợp lệ hoặc không có dữ liệu.");
    }

    private static void LockVoiceBaseline(MockExamAnswer targetAnswer, string voiceTranscript)
    {
        var cleanBaseline = voiceTranscript.Trim();
        targetAnswer.InitialVoiceTranscript = cleanBaseline;
        if (targetAnswer.Id != Guid.Empty)
        {
            VoiceTranscriptBaselines[targetAnswer.Id] = cleanBaseline;
        }
    }

    private static string ResolveImmutableVoiceBaseline(MockExamAnswer targetAnswer)
    {
        if (!string.IsNullOrWhiteSpace(targetAnswer.InitialVoiceTranscript))
        {
            var initial = targetAnswer.InitialVoiceTranscript.Trim();
            if (targetAnswer.Id != Guid.Empty)
            {
                VoiceTranscriptBaselines.TryAdd(targetAnswer.Id, initial);
            }
            return initial;
        }

        if (targetAnswer.Id != Guid.Empty &&
            VoiceTranscriptBaselines.TryGetValue(targetAnswer.Id, out var cachedBaseline) &&
            !string.IsNullOrWhiteSpace(cachedBaseline))
        {
            targetAnswer.InitialVoiceTranscript = cachedBaseline;
            return cachedBaseline;
        }

        var fallbackBaseline = targetAnswer.AnswerText!.Trim();
        targetAnswer.InitialVoiceTranscript = fallbackBaseline;
        if (targetAnswer.Id != Guid.Empty)
        {
            VoiceTranscriptBaselines.TryAdd(targetAnswer.Id, fallbackBaseline);
        }
        return fallbackBaseline;
    }

    private static bool IsMinorTermRevision(string baselineText, string revisedText)
    {
        var baselineTokens = TokenizeWords(baselineText);
        var revisedTokens = TokenizeWords(revisedText);
        if (baselineTokens.Count <= 3)
        {
            return true;
        }

        if (revisedTokens.Count == 0)
        {
            return false;
        }

        var remaining = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in revisedTokens)
        {
            remaining[token] = remaining.GetValueOrDefault(token) + 1;
        }

        var matched = 0;
        foreach (var token in baselineTokens)
        {
            if (remaining.TryGetValue(token, out var count) && count > 0)
            {
                matched++;
                remaining[token] = count - 1;
            }
        }

        var retentionRatio = (double)matched / baselineTokens.Count;
        return retentionRatio >= 0.45;
    }

    private static List<string> TokenizeWords(string value)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private async Task<MockExamAnswer?> TryOpenNextFollowUpAsync(
        MockExamSession session,
        MockExamAnswer justAnswered,
        CancellationToken cancellationToken)
    {
        if (!session.HasFollowUp)
        {
            return null;
        }

        var parentId = RootParentId(justAnswered);
        var maxFollowUps = GetMaxFollowUps(session);
        var existingFollowUps = session.Answers
            .Where(a => a.IsFollowUp && a.ParentAnswerId == parentId)
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        // Nếu đang có slot phụ pending (chưa có transcript), làm mới lại câu hỏi phụ theo câu trả lời vừa nộp
        var pending = existingFollowUps.FirstOrDefault(a =>
            a.Status == "pending" || string.IsNullOrWhiteSpace(a.AnswerText));
        if (pending is not null && pending.Id != justAnswered.Id)
        {
            var pendingOrdinal = existingFollowUps.FindIndex(a => a.Id == pending.Id) + 1;
            if (pendingOrdinal <= 0)
            {
                pendingOrdinal = 1;
            }

            var priorFollowUps = existingFollowUps
                .Take(pendingOrdinal - 1)
                .ToList();

            try
            {
                var updatedPrompt = await GenerateContextualFollowUpPromptAsync(
                    session,
                    justAnswered,
                    parentId,
                    pendingOrdinal,
                    priorFollowUps,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(updatedPrompt))
                {
                    return null;
                }

                pending.PromptText = updatedPrompt;
                return pending;
            }
            catch
            {
                return null;
            }
        }

        // Đếm mọi follow-up đã tồn tại (kể cả vừa trả lời)
        var totalFollowUps = existingFollowUps.Count;
        if (justAnswered.IsFollowUp && !existingFollowUps.Any(a => a.Id == justAnswered.Id))
        {
            totalFollowUps += 1;
        }

        if (totalFollowUps >= maxFollowUps)
        {
            return null;
        }

        var followUpOrdinal = totalFollowUps + 1;
        string? prompt;
        try
        {
            prompt = await GenerateContextualFollowUpPromptAsync(
                session,
                justAnswered,
                parentId,
                followUpOrdinal,
                existingFollowUps,
                cancellationToken);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var parentMain = session.Answers.FirstOrDefault(a => a.Id == parentId) ?? justAnswered;

        var followUp = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = justAnswered.QuestionId,
            OrderIndex = parentMain.OrderIndex,
            AnswerText = null,
            TimeTakenSeconds = 0,
            Status = "pending",
            AnsweredAt = DateTime.UtcNow,
            IsFollowUp = true,
            ParentAnswerId = parentId,
            PromptText = prompt
        };

        session.Answers.Add(followUp);
        _context.MockExamAnswers.Add(followUp);
        return followUp;
    }

    private async Task<MockExamAnswer?> TryRefreshPendingFollowUpAsync(
        MockExamSession session,
        MockExamAnswer justAnswered,
        CancellationToken cancellationToken)
    {
        if (!session.HasFollowUp)
        {
            return null;
        }

        var rootMainId = RootParentId(justAnswered);
        var existingFollowUps = session.Answers
            .Where(a => a.IsFollowUp && a.ParentAnswerId == rootMainId)
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        var justAnsweredIdx = justAnswered.IsFollowUp
            ? existingFollowUps.FindIndex(a => a.Id == justAnswered.Id)
            : -1;

        var subsequentFollowUps = justAnsweredIdx >= 0
            ? existingFollowUps.Skip(justAnsweredIdx + 1).ToList()
            : existingFollowUps;

        // Không làm mới nếu đã có câu hỏi phụ phía sau được sinh viên trả lời
        if (subsequentFollowUps.Any(a => !string.IsNullOrWhiteSpace(a.AnswerText)))
        {
            return null;
        }

        var pending = subsequentFollowUps.FirstOrDefault(a =>
            a.Id != justAnswered.Id && string.IsNullOrWhiteSpace(a.AnswerText));

        if (pending is null)
        {
            var maxFollowUps = GetMaxFollowUps(session);
            if (existingFollowUps.Count < maxFollowUps)
            {
                return await TryOpenNextFollowUpAsync(session, justAnswered, cancellationToken);
            }

            return null;
        }

        var pendingOrdinal = existingFollowUps.FindIndex(a => a.Id == pending.Id) + 1;
        if (pendingOrdinal <= 0)
        {
            pendingOrdinal = 1;
        }

        var priorFollowUps = existingFollowUps
            .Take(pendingOrdinal - 1)
            .ToList();

        try
        {
            var refreshedPrompt = await GenerateContextualFollowUpPromptAsync(
                session,
                justAnswered,
                rootMainId,
                pendingOrdinal,
                priorFollowUps,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(refreshedPrompt))
            {
                pending.PromptText = refreshedPrompt;
            }
        }
        catch
        {
            // Giữ nguyên PromptText hiện có của pending follow-up nếu quá trình làm mới gặp sự cố
        }

        return pending;
    }

    private async Task<string> GenerateContextualFollowUpPromptAsync(
        MockExamSession session,
        MockExamAnswer justAnswered,
        Guid rootMainId,
        int followUpOrdinal,
        List<MockExamAnswer> priorFollowUps,
        CancellationToken cancellationToken)
    {
        var question = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(q => q.Id == justAnswered.QuestionId, cancellationToken);

        var orderedCriteria = question?.Rubric?.Criteria?
            .OrderBy(c => c.OrderIndex)
            .ToList() ?? new List<RubricCriterion>();

        var previousFollowUpPrompts = priorFollowUps
            .Select(f => f.PromptText)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToList();

        var parentMain = session.Answers.FirstOrDefault(a => a.Id == rootMainId) ?? justAnswered;

        string? aiGeneratedFollowUpPrompt = null;
        string? weakestCriterionName = null;
        string? weakestCriterionDescription = null;

        if (_aiGradingService != null && question != null)
        {
            try
            {
                var aiRequest = new AiGradingRequest
                {
                    QuestionContent = question.Content,
                    SampleAnswer = question.SampleAnswer ?? string.Empty,
                    Transcript = justAnswered.AnswerText ?? string.Empty,
                    CourseCode = session.Course?.Code,
                    QuestionImageUrl = question.ImageUrl,
                    QuestionImageDescription = question.ImageDescription,
                    Criteria = orderedCriteria.Select(c => new AiGradingCriterion
                    {
                        Id = c.Id.ToString(),
                        Name = c.CriterionName,
                        Description = c.Description ?? string.Empty,
                        MaxScore = c.MaxScore,
                        Weight = c.Weight
                    }).ToList(),
                    IsFollowUp = justAnswered.IsFollowUp,
                    FollowUpPrompt = justAnswered.PromptText,
                    OriginalAnswerTranscript = parentMain.AnswerText,
                    FollowUpIndex = justAnswered.IsFollowUp
                        ? priorFollowUps.TakeWhile(f => f.Id != justAnswered.Id).Count() + 1
                        : 0,
                    PreviousFollowUpPrompts = previousFollowUpPrompts,
                    ThrowOnTransientFailure = true
                };

                var aiResult = await _aiGradingService.GradeAnswerAsync(aiRequest, cancellationToken);
                if (aiResult != null)
                {
                    var rawDetails = aiResult.Details ?? new List<GradingDetailDto>();
                    var evalId = Guid.NewGuid();
                    var evalDetails = new List<AiEvaluationDetail>();

                    for (int i = 0; i < orderedCriteria.Count; i++)
                    {
                        var crit = orderedCriteria[i];
                        var matchedDetail = rawDetails.FirstOrDefault(d =>
                                string.Equals(d.CriterionId?.Trim(), crit.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                            ?? rawDetails.FirstOrDefault(d =>
                                !string.IsNullOrWhiteSpace(crit.CriterionName) &&
                                string.Equals(d.CriterionId?.Trim(), crit.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                            ?? (i < rawDetails.Count ? rawDetails[i] : null);

                        var detailScore = matchedDetail != null
                            ? Math.Clamp(Math.Round(matchedDetail.Score, 2, MidpointRounding.AwayFromZero), 0.0m, crit.MaxScore)
                            : 0.0m;
                        var comment = !string.IsNullOrWhiteSpace(matchedDetail?.Comment)
                            ? matchedDetail!.Comment
                            : detailScore > 0.0m
                                ? $"Đạt {detailScore:0.0}/{crit.MaxScore:0.0}đ cho tiêu chí '{crit.CriterionName}'."
                                : $"Chưa đáp ứng tiêu chí '{crit.CriterionName}' (0.0/{crit.MaxScore:0.0}đ).";

                        evalDetails.Add(new AiEvaluationDetail
                        {
                            Id = Guid.NewGuid(),
                            EvaluationId = evalId,
                            CriterionId = crit.Id,
                            Score = detailScore,
                            Comment = comment
                        });
                    }

                    var finalCachedScore = rawDetails.Count > 0 && evalDetails.Count > 0
                        ? Math.Clamp(Math.Round(evalDetails.Sum(d => d.Score), 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                        : Math.Clamp(Math.Round(aiResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);

                    if (justAnswered.AiEvaluations is { Count: > 0 })
                    {
                        _context.AiEvaluations.RemoveRange(justAnswered.AiEvaluations);
                        justAnswered.AiEvaluations.Clear();
                    }

                    var cachedEval = new AiEvaluation
                    {
                        Id = evalId,
                        AnswerType = "practice",
                        PracticeAnswerId = null,
                        ExamSubmissionId = null,
                        MockExamAnswerId = justAnswered.Id,
                        TotalScore = finalCachedScore,
                        Feedback = aiResult.Feedback,
                        ConfidenceScore = aiResult.ConfidenceScore,
                        CotTrace = aiResult.CotTrace,
                        EvaluatedAt = DateTime.UtcNow,
                        Details = evalDetails
                    };
                    justAnswered.AiEvaluations ??= new List<AiEvaluation>();
                    justAnswered.AiEvaluations.Add(cachedEval);
                    _context.AiEvaluations.Add(cachedEval);

                    if (aiResult.Score <= 0.0m ||
                        aiResult.IsSuspicious ||
                        (!string.IsNullOrWhiteSpace(aiResult.CotTrace) &&
                         aiResult.CotTrace.Contains("[NON_ANSWER_GUARD]", StringComparison.OrdinalIgnoreCase)))
                    {
                        return string.Empty;
                    }

                    aiGeneratedFollowUpPrompt = !string.IsNullOrWhiteSpace(aiResult.FollowUpPrompt)
                        ? aiResult.FollowUpPrompt
                        : FollowUpPromptBuilder.TryExtractFollowUpFromCot(aiResult.CotTrace);

                    if (orderedCriteria.Count > 0 && rawDetails.Count > 0)
                    {
                        RubricCriterion? weakestCriterion = null;
                        decimal lowestRatio = decimal.MaxValue;

                        for (int i = 0; i < orderedCriteria.Count; i++)
                        {
                            var crit = orderedCriteria[i];
                            var matchedDetail = rawDetails.FirstOrDefault(d =>
                                    string.Equals(d.CriterionId?.Trim(), crit.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                                ?? rawDetails.FirstOrDefault(d =>
                                    !string.IsNullOrWhiteSpace(crit.CriterionName) &&
                                    string.Equals(d.CriterionId?.Trim(), crit.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                                ?? (i < rawDetails.Count ? rawDetails[i] : null);

                            var detailScore = matchedDetail != null
                                ? Math.Clamp(matchedDetail.Score, 0.0m, crit.MaxScore)
                                : 0.0m;
                            var ratio = crit.MaxScore > 0m ? detailScore / crit.MaxScore : 0m;
                            if (ratio < lowestRatio)
                            {
                                lowestRatio = ratio;
                                weakestCriterion = crit;
                            }
                        }

                        if (weakestCriterion != null)
                        {
                            weakestCriterionName = weakestCriterion.CriterionName;
                            weakestCriterionDescription = weakestCriterion.Description;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gọi AI grading để sinh follow-up câu thi thử {AnswerId}. Chuyển sang fallback FollowUpPromptBuilder.", justAnswered.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(weakestCriterionName) && orderedCriteria.Count > 0)
        {
            var fallbackCriterion = orderedCriteria[Math.Max(0, followUpOrdinal - 1) % orderedCriteria.Count];
            weakestCriterionName = fallbackCriterion.CriterionName;
            weakestCriterionDescription = fallbackCriterion.Description;
        }

        return FollowUpPromptBuilder.ResolveFollowUpPrompt(
            courseCode: session.Course?.Code,
            questionFollowUpPrompt: null,
            aiGeneratedFollowUpPrompt: aiGeneratedFollowUpPrompt,
            followUpSequence: followUpOrdinal,
            weakestCriterionName: weakestCriterionName,
            weakestCriterionDescription: weakestCriterionDescription,
            criterionNames: orderedCriteria.Select(c => c.CriterionName),
            questionContent: question?.Content,
            sampleAnswer: null,
            studentTranscript: justAnswered.AnswerText,
            originalAnswerTranscript: parentMain.AnswerText,
            previousFollowUpPrompts: previousFollowUpPrompts);
    }

    private static Guid RootParentId(MockExamAnswer answer)
        => answer.IsFollowUp && answer.ParentAnswerId is Guid parent && parent != Guid.Empty
            ? parent
            : answer.Id;

    private static int GetFollowUpOrdinal(MockExamSession session, Guid parentId, Guid followUpAnswerId)
    {
        var orderedFollowUps = session.Answers
            .Where(a => a.IsFollowUp && a.ParentAnswerId == parentId)
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        var idx = orderedFollowUps.FindIndex(a => a.Id == followUpAnswerId) + 1;
        return idx > 0 ? idx : 1;
    }

    private static int GetMaxFollowUps(MockExamSession session)
    {
        var configured = session.Course is { MaxFollowUpQuestions: > 0 }
            ? session.Course.MaxFollowUpQuestions
            : 2;
        return Math.Clamp(configured, 1, 5);
    }
}
