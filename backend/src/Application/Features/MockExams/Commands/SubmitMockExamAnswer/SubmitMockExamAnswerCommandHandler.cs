using System;
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

    private sealed record FollowUpComputation(
        Guid? PendingFollowUpIdToUpdate,
        string? NewFollowUpPrompt,
        AiEvaluation? GeneratedCachedEvaluation);

    public async Task<Result<SubmitMockExamAnswerResponse>> Handle(
        SubmitMockExamAnswerCommand request,
        CancellationToken cancellationToken)
    {
        // Pha 1 (Ngoài khóa DB): Nạp bản chụp AsNoTracking để xác thực ban đầu và thực thi STT/AI (tránh giữ lock PostgreSQL khi gọi mạng).
        var sessionSnapshot = await _context.MockExamSessions
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (sessionSnapshot == null)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi không tồn tại.");
        }

        if (sessionSnapshot.StudentId != request.StudentId)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Bạn không có quyền nộp bài cho ca thi này.");
        }

        if (!string.Equals(sessionSnapshot.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            return Result<SubmitMockExamAnswerResponse>.Failure(
                "CONFLICT: Ca thi đã kết thúc hoặc đã nộp bài, không thể ghi nhận thêm câu trả lời.");
        }

        if (sessionSnapshot.PausedAt.HasValue)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure(
                "CONFLICT: Ca thi đang tạm dừng, vui lòng tiếp tục (resume) trước khi nộp câu trả lời.");
        }

        var now = DateTime.UtcNow;
        if (now > sessionSnapshot.ExpiresAt.AddSeconds(10))
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã hết thời gian làm bài.");
        }

        var answerSnapshot = ResolveTargetAnswer(sessionSnapshot, request.QuestionId, request.AnswerId);
        if (answerSnapshot == null)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Câu hỏi không thuộc đề thi này.");
        }

        // Kiểm tra xem câu này đã từng được trả lời bằng giọng nói trước đó chưa
        var wasAlreadyAnswered =
            string.Equals(answerSnapshot.Status, "answered", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(answerSnapshot.AnswerText);

        var transcriptResult = await ResolveTranscriptAsync(
            request,
            answerSnapshot,
            wasAlreadyAnswered,
            sessionSnapshot.Course?.Code,
            cancellationToken);
        if (!transcriptResult.IsSuccess)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure(transcriptResult.Error ?? "Không nhận diện được giọng nói.");
        }

        var resolvedTranscript = transcriptResult.Value;
        var resolvedInitialVoiceTranscript = answerSnapshot.InitialVoiceTranscript;
        var answeredAtUtc = DateTime.UtcNow;

        // Cập nhật trên bản chụp AsNoTracking để nhánh sinh câu hỏi phụ đọc đúng ngữ cảnh câu trả lời mới
        answerSnapshot.AnswerText = resolvedTranscript;
        answerSnapshot.TimeTakenSeconds = request.TimeTakenSeconds;
        answerSnapshot.Status = "answered";
        if (!wasAlreadyAnswered || !answerSnapshot.IsFollowUp)
        {
            answerSnapshot.AnsweredAt = answeredAtUtc;
        }

        FollowUpComputation? followUpPlan = null;
        if (sessionSnapshot.HasFollowUp)
        {
            followUpPlan = wasAlreadyAnswered
                ? await TryRefreshPendingFollowUpAsync(sessionSnapshot, answerSnapshot, cancellationToken)
                : await TryOpenNextFollowUpAsync(sessionSnapshot, answerSnapshot, cancellationToken);
        }

        // Pha 2 (Trong khóa tuần tự PostgreSQL): Nạp lại tươi trạng thái phiên & ghi kết quả nguyên tử,
        // đảm bảo câu trả lời chạy STT/AI chậm tuyệt đối không ghi đè vào ca thi đã submitted/graded/paused.
        return await _context.ExecuteMockExamGradingAsync(request.SessionId, async lockCt =>
        {
            var freshSession = await _context.MockExamSessions
                .Include(s => s.Course)
                .Include(s => s.Answers)
                    .ThenInclude(a => a.AiEvaluations)
                        .ThenInclude(e => e.Details)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, lockCt);

            if (freshSession == null)
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi không tồn tại.");
            }

            if (freshSession.StudentId != request.StudentId)
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Bạn không có quyền nộp bài cho ca thi này.");
            }

            if (!string.Equals(freshSession.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return Result<SubmitMockExamAnswerResponse>.Failure(
                    "CONFLICT: Ca thi đã kết thúc hoặc đã nộp bài, không thể ghi nhận thêm câu trả lời.");
            }

            if (freshSession.PausedAt.HasValue)
            {
                return Result<SubmitMockExamAnswerResponse>.Failure(
                    "CONFLICT: Ca thi đang tạm dừng, vui lòng tiếp tục (resume) trước khi nộp câu trả lời.");
            }

            if (DateTime.UtcNow > freshSession.ExpiresAt.AddSeconds(10))
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã hết thời gian làm bài.");
            }

            var freshAnswer = ResolveTargetAnswer(freshSession, request.QuestionId, request.AnswerId);
            if (freshAnswer == null)
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Câu hỏi không thuộc đề thi này.");
            }

            var freshWasAlreadyAnswered =
                string.Equals(freshAnswer.Status, "answered", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(freshAnswer.AnswerText);

            if (followUpPlan?.GeneratedCachedEvaluation is { } cachedEval)
            {
                if (freshAnswer.AiEvaluations is { Count: > 0 })
                {
                    _context.AiEvaluations.RemoveRange(freshAnswer.AiEvaluations);
                    freshAnswer.AiEvaluations.Clear();
                }

                cachedEval.MockExamAnswerId = freshAnswer.Id;
                freshAnswer.AiEvaluations ??= new List<AiEvaluation>();
                freshAnswer.AiEvaluations.Add(cachedEval);
                _context.AiEvaluations.Add(cachedEval);
            }
            else if (freshAnswer.AiEvaluations is { Count: > 0 } &&
                     !string.Equals(freshAnswer.AnswerText, resolvedTranscript, StringComparison.Ordinal))
            {
                _context.AiEvaluations.RemoveRange(freshAnswer.AiEvaluations);
                freshAnswer.AiEvaluations.Clear();
            }

            if (!string.IsNullOrWhiteSpace(resolvedInitialVoiceTranscript))
            {
                freshAnswer.InitialVoiceTranscript = resolvedInitialVoiceTranscript;
            }

            freshAnswer.AnswerText = resolvedTranscript;
            freshAnswer.TimeTakenSeconds = request.TimeTakenSeconds;
            freshAnswer.Status = "answered";
            if (!freshWasAlreadyAnswered || !freshAnswer.IsFollowUp)
            {
                freshAnswer.AnsweredAt = answeredAtUtc;
            }

            MockExamAnswer? persistedFollowUp = null;
            if (freshSession.HasFollowUp && followUpPlan is not null)
            {
                var rootMainId = RootParentId(freshAnswer);
                if (followUpPlan.PendingFollowUpIdToUpdate is Guid pendingId)
                {
                    var existingPending = freshSession.Answers.FirstOrDefault(a =>
                        a.Id == pendingId &&
                        a.IsFollowUp &&
                        (a.Status == "pending" || string.IsNullOrWhiteSpace(a.AnswerText)));
                    if (existingPending is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(followUpPlan.NewFollowUpPrompt))
                        {
                            existingPending.PromptText = followUpPlan.NewFollowUpPrompt;
                        }

                        persistedFollowUp = existingPending;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(followUpPlan.NewFollowUpPrompt))
                {
                    var existingFollowUps = freshSession.Answers
                        .Where(a => a.IsFollowUp && a.ParentAnswerId == rootMainId)
                        .OrderBy(a => a.OrderIndex)
                        .ThenBy(a => a.AnsweredAt)
                        .ThenBy(a => a.Id)
                        .ToList();

                    var existingPending = existingFollowUps.FirstOrDefault(a =>
                        a.Id != freshAnswer.Id &&
                        (a.Status == "pending" || string.IsNullOrWhiteSpace(a.AnswerText)));

                    if (existingPending is not null)
                    {
                        existingPending.PromptText = followUpPlan.NewFollowUpPrompt;
                        persistedFollowUp = existingPending;
                    }
                    else if (existingFollowUps.Count < GetMaxFollowUps(freshSession))
                    {
                        var parentMain = freshSession.Answers.FirstOrDefault(a => a.Id == rootMainId) ?? freshAnswer;
                        var newFollowUpEntity = new MockExamAnswer
                        {
                            Id = Guid.NewGuid(),
                            SessionId = freshSession.Id,
                            QuestionId = freshAnswer.QuestionId,
                            OrderIndex = parentMain.OrderIndex,
                            AnswerText = null,
                            TimeTakenSeconds = 0,
                            Status = "pending",
                            AnsweredAt = DateTime.UtcNow,
                            IsFollowUp = true,
                            ParentAnswerId = rootMainId,
                            PromptText = followUpPlan.NewFollowUpPrompt
                        };

                        freshSession.Answers.Add(newFollowUpEntity);
                        _context.MockExamAnswers.Add(newFollowUpEntity);
                        persistedFollowUp = newFollowUpEntity;
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync(lockCt);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Xung đột đồng thời khi lưu câu trả lời thi thử cho session {SessionId}, question {QuestionId}", request.SessionId, request.QuestionId);
                return Result<SubmitMockExamAnswerResponse>.Failure("CONFLICT: Câu trả lời đã được ghi nhận hoặc có xung đột dữ liệu.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật CSDL khi lưu câu trả lời thi thử cho session {SessionId}, question {QuestionId}", request.SessionId, request.QuestionId);
                return Result<SubmitMockExamAnswerResponse>.Failure("Không thể lưu kết quả nộp bài do lỗi cập nhật cơ sở dữ liệu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi lưu câu trả lời thi thử cho session {SessionId}, question {QuestionId}", request.SessionId, request.QuestionId);
                return Result<SubmitMockExamAnswerResponse>.Failure("Đã xảy ra lỗi khi lưu kết quả bài thi.");
            }

            var rootParentId = RootParentId(freshAnswer);
            var followUpIdx = persistedFollowUp is null
                ? 0
                : GetFollowUpOrdinal(freshSession, rootParentId, persistedFollowUp.Id);

            return Result<SubmitMockExamAnswerResponse>.Success(new SubmitMockExamAnswerResponse(
                AnswerId: freshAnswer.Id,
                QuestionId: freshAnswer.QuestionId,
                Transcript: freshAnswer.AnswerText ?? string.Empty,
                Status: freshAnswer.Status,
                AnsweredAt: freshAnswer.AnsweredAt,
                IsFollowUp: freshAnswer.IsFollowUp,
                ParentAnswerId: freshAnswer.ParentAnswerId,
                NeedsFollowUp: persistedFollowUp is not null,
                FollowUpPrompt: persistedFollowUp?.PromptText,
                FollowUpAnswerId: persistedFollowUp?.Id,
                FollowUpIndex: followUpIdx,
                MaxFollowUps: GetMaxFollowUps(freshSession)
            ));
        }, cancellationToken);
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

            var fileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "audio.webm";
            Exception? sttException = null;
            var transcript = string.Empty;
            SpeechTranscriptionResult? detailed = null;
            double? serverConfidence = null;
            // Không truyền Title/Content làm vocabularyHint — tránh STT bịa đáp án khi im lặng.
            // Riêng môn Tiếng Anh (ENTxxx), chỉ truyền mã môn 6 ký tự (không kèm đề bài) để Cloudflare Whisper chuyển đúng language="en" và không chèn prompt SE tiếng Việt.
            string? sttHint = !string.IsNullOrWhiteSpace(courseCode) &&
                System.Text.RegularExpressions.Regex.IsMatch(
                    courseCode.Trim(),
                    @"^ENT\d{3}$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                ? courseCode.Trim().ToUpperInvariant()
                : null;
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
                    hasRealAudioHeader = bytesRead >= 256 &&
                        TranscriptHelper.HasRealAudioContainerHeader(headerBuffer.AsSpan(0, bytesRead));
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
                    hasRealAudioHeader = bufferedStream.Length >= 256 &&
                        TranscriptHelper.HasRealAudioContainerHeader(
                            bufferedStream.GetBuffer().AsSpan(0, headerLength));
                    bufferedStream.Position = 0;
                    streamToTranscribe = bufferedStream;
                }

                // Tối ưu hiệu năng: Chỉ sử dụng lại bản bóc băng đã chứng thực tại Server STT cho đúng thí sinh này
                // khi payload kèm theo thực sự có header container âm thanh hợp lệ (chống bypass bằng 1 byte rác hoặc dùng ké SV khác).
                if (hasRealAudioHeader &&
                    !string.IsNullOrWhiteSpace(normalizedClient) &&
                    TranscriptHelper.IsVerifiedServerTranscript(normalizedClient, request.StudentId))
                {
                    LockVoiceBaseline(targetAnswer, normalizedClient);
                    return Result<string>.Success(normalizedClient);
                }

                // Chỉ truyền mã môn 6 ký tự (ENTxxx) cho môn Tiếng Anh hoặc null cho môn SE — không bao giờ truyền Title/Content đề bài.
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

            // P0-10: Khi STT nhận diện được tín hiệu nhưng độ tin cậy thấp (0 < confidence < 0.35),
            // tuyệt đối KHÔNG chấp nhận mù quáng DirectTranscript chưa xác thực server làm InitialVoiceTranscript.
            if (serverConfidence is > 0.0 and < 0.35)
            {
                if (hasRealAudioHeader &&
                    !string.IsNullOrWhiteSpace(normalizedClient) &&
                    TranscriptHelper.IsVerifiedServerTranscript(normalizedClient, request.StudentId))
                {
                    LockVoiceBaseline(targetAnswer, normalizedClient);
                    return Result<string>.Success(normalizedClient);
                }

                if (!string.IsNullOrWhiteSpace(normalizedServer))
                {
                    TranscriptHelper.RegisterVerifiedTranscript(normalizedServer, request.StudentId);
                    LockVoiceBaseline(targetAnswer, normalizedServer);
                    return Result<string>.Success(normalizedServer);
                }

                return Result<string>.Failure(
                    "VALIDATION: Tín hiệu giọng nói quá yếu hoặc nhiều nhiễu (độ tin cậy nhận diện thấp). Vui lòng nói gần microphone rõ ràng hơn và ghi âm lại.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                TranscriptHelper.RegisterVerifiedTranscript(normalizedServer, request.StudentId);
                LockVoiceBaseline(targetAnswer, normalizedServer);
                return Result<string>.Success(normalizedServer);
            }

            if (TranscriptHelper.IsServerConfirmedSilence(detailed, sttException))
            {
                return Result<string>.Failure(
                    "Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedClient))
            {
                if (!hasRealAudioHeader)
                {
                    return Result<string>.Failure(
                        "VOICE_FIRST_GATE: File âm thanh không hợp lệ hoặc không chứa dữ liệu ghi âm thực tế. Vui lòng ghi âm lại qua microphone.");
                }

                if (sttException is null || IsTransientSttOutage(sttException, cancellationToken))
                {
                    LockVoiceBaseline(targetAnswer, normalizedClient);
                    return Result<string>.Success(normalizedClient);
                }
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
        targetAnswer.InitialVoiceTranscript = voiceTranscript.Trim();
    }

    private static string ResolveImmutableVoiceBaseline(MockExamAnswer targetAnswer)
    {
        if (!string.IsNullOrWhiteSpace(targetAnswer.InitialVoiceTranscript))
        {
            return targetAnswer.InitialVoiceTranscript.Trim();
        }

        var fallbackBaseline = targetAnswer.AnswerText!.Trim();
        targetAnswer.InitialVoiceTranscript = fallbackBaseline;
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

    private async Task<FollowUpComputation?> TryOpenNextFollowUpAsync(
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
                var (updatedPrompt, generatedEval) = await GenerateContextualFollowUpPromptAsync(
                    session,
                    justAnswered,
                    parentId,
                    pendingOrdinal,
                    priorFollowUps,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(updatedPrompt))
                {
                    return generatedEval is not null
                        ? new FollowUpComputation(null, null, generatedEval)
                        : null;
                }

                return new FollowUpComputation(pending.Id, updatedPrompt, generatedEval);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể làm mới câu hỏi phụ đang chờ cho câu trả lời {AnswerId}.", justAnswered.Id);
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
        AiEvaluation? cachedEval;
        try
        {
            (prompt, cachedEval) = await GenerateContextualFollowUpPromptAsync(
                session,
                justAnswered,
                parentId,
                followUpOrdinal,
                existingFollowUps,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể sinh câu hỏi phụ kế tiếp cho câu trả lời {AnswerId}.", justAnswered.Id);
            return null;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return cachedEval is not null
                ? new FollowUpComputation(null, null, cachedEval)
                : null;
        }

        return new FollowUpComputation(null, prompt, cachedEval);
    }

    private async Task<FollowUpComputation?> TryRefreshPendingFollowUpAsync(
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

        string? refreshedPrompt = null;
        AiEvaluation? cachedEval = null;
        try
        {
            var (generatedPrompt, generatedEval) = await GenerateContextualFollowUpPromptAsync(
                session,
                justAnswered,
                rootMainId,
                pendingOrdinal,
                priorFollowUps,
                cancellationToken);

            cachedEval = generatedEval;
            if (!string.IsNullOrWhiteSpace(generatedPrompt))
            {
                refreshedPrompt = generatedPrompt;
            }
        }
        catch (Exception ex)
        {
            // Giữ nguyên PromptText hiện có của pending follow-up nếu quá trình làm mới gặp sự cố
            _logger.LogWarning(ex, "Giữ nguyên PromptText của pending follow-up {PendingId} sau lỗi làm mới.", pending.Id);
        }

        return new FollowUpComputation(pending.Id, refreshedPrompt, cachedEval);
    }

    private async Task<(string Prompt, AiEvaluation? GeneratedEval)> GenerateContextualFollowUpPromptAsync(
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
        AiEvaluation? generatedCachedEval = null;

        if (_aiGradingService != null && question != null)
        {
            try
            {
                var allOrderedCriteria = orderedCriteria;
                var isEnglishCourse = FollowUpPromptBuilder.IsEnglishCourse(
                    session.Course?.Code,
                    allOrderedCriteria.Select(c => c.CriterionName),
                    question.Content);

                if (justAnswered.IsFollowUp && !isEnglishCourse && allOrderedCriteria.Count > 1)
                {
                    var rootEvalDetails = parentMain.AiEvaluations?
                        .OrderByDescending(e => e.EvaluatedAt)
                        .FirstOrDefault()?.Details;

                    var alreadyTargetedFollowUpCriteria = priorFollowUps
                        .Where(f => f.Id != justAnswered.Id)
                        .SelectMany(f => (f.AiEvaluations ?? System.Linq.Enumerable.Empty<AiEvaluation>()).OrderByDescending(e => e.EvaluatedAt).Take(1))
                        .Where(e => e.Details != null && e.Details.Count == 1)
                        .Select(e => e.Details.First().CriterionId)
                        .ToHashSet();

                    RubricCriterion? targetCriterion = null;
                    decimal lowestRatio = decimal.MaxValue;

                    if (rootEvalDetails != null && rootEvalDetails.Count > 0)
                    {
                        var candidates = allOrderedCriteria
                            .Where(c => !alreadyTargetedFollowUpCriteria.Contains(c.Id))
                            .ToList();
                        if (candidates.Count == 0)
                        {
                            candidates = allOrderedCriteria;
                        }

                        foreach (var crit in candidates)
                        {
                            var detail = rootEvalDetails.FirstOrDefault(d => d.CriterionId == crit.Id);
                            var score = detail?.Score ?? 0m;
                            var ratio = crit.MaxScore > 0m ? score / crit.MaxScore : 0m;
                            if (ratio < lowestRatio)
                            {
                                lowestRatio = ratio;
                                targetCriterion = crit;
                            }
                        }
                    }

                    targetCriterion ??= allOrderedCriteria[0];
                    orderedCriteria = new List<RubricCriterion> { targetCriterion };
                }

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
                    var matchedByCriterionIndex = new GradingDetailDto?[orderedCriteria.Count];
                    var usedRawIndices = new HashSet<int>();

                    for (int i = 0; i < orderedCriteria.Count; i++)
                    {
                        var crit = orderedCriteria[i];
                        int matchedIdx = -1;

                        for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                        {
                            if (usedRawIndices.Contains(rIdx))
                            {
                                continue;
                            }

                            var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                            if (string.Equals(candidateId, crit.Id.ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                matchedIdx = rIdx;
                                break;
                            }
                        }

                        if (matchedIdx < 0 && !string.IsNullOrWhiteSpace(crit.CriterionName))
                        {
                            for (int rIdx = 0; rIdx < rawDetails.Count; rIdx++)
                            {
                                if (usedRawIndices.Contains(rIdx))
                                {
                                    continue;
                                }

                                var candidateId = rawDetails[rIdx].CriterionId?.Trim();
                                if (string.Equals(candidateId, crit.CriterionName.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedIdx = rIdx;
                                    break;
                                }
                            }
                        }

                        if (matchedIdx >= 0)
                        {
                            usedRawIndices.Add(matchedIdx);
                            matchedByCriterionIndex[i] = rawDetails[matchedIdx];
                        }
                    }

                    var totalCriteriaMax = orderedCriteria.Sum(c => c.MaxScore);
                    var clampedAiScore = Math.Clamp(Math.Round(aiResult.Score, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m);
                    decimal runningAllocatedScore = 0.0m;

                    for (int i = 0; i < orderedCriteria.Count; i++)
                    {
                        var crit = orderedCriteria[i];
                        var matchedDetail = matchedByCriterionIndex[i];
                        if (matchedDetail == null && i < rawDetails.Count && !usedRawIndices.Contains(i))
                        {
                            usedRawIndices.Add(i);
                            matchedDetail = rawDetails[i];
                        }

                        decimal detailScore;
                        if (rawDetails.Count > 0)
                        {
                            if (matchedDetail != null)
                            {
                                var rawScore = matchedDetail.Score;
                                if (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m && orderedCriteria.Count == 1 &&
                                    rawScore > crit.MaxScore && rawScore <= 10.0m)
                                {
                                    rawScore = rawScore * (crit.MaxScore / 10.0m);
                                }

                                detailScore = Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0.0m, crit.MaxScore);
                            }
                            else
                            {
                                detailScore = 0.0m;
                            }
                        }
                        else if (totalCriteriaMax > 0.0m && clampedAiScore > 0.0m)
                        {
                            var targetScaleSum = (totalCriteriaMax < 10.0m && clampedAiScore > totalCriteriaMax)
                                ? clampedAiScore * (totalCriteriaMax / 10.0m)
                                : clampedAiScore;

                            if (i == orderedCriteria.Count - 1)
                            {
                                detailScore = Math.Clamp(
                                    Math.Round(targetScaleSum - runningAllocatedScore, 2, MidpointRounding.AwayFromZero),
                                    0.0m,
                                    crit.MaxScore);
                            }
                            else
                            {
                                detailScore = Math.Clamp(
                                    Math.Round(targetScaleSum * (crit.MaxScore / totalCriteriaMax), 2, MidpointRounding.AwayFromZero),
                                    0.0m,
                                    crit.MaxScore);
                                runningAllocatedScore += detailScore;
                            }
                        }
                        else
                        {
                            detailScore = 0.0m;
                        }

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

                    var sumEvalDetails = evalDetails.Sum(d => d.Score);
                    var finalCachedScore = evalDetails.Count > 0
                        ? (totalCriteriaMax > 0.0m && totalCriteriaMax < 10.0m
                            ? Math.Clamp(Math.Round((sumEvalDetails / totalCriteriaMax) * 10.0m, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m)
                            : Math.Clamp(Math.Round(sumEvalDetails, 2, MidpointRounding.AwayFromZero), 0.0m, 10.0m))
                        : clampedAiScore;

                    var resolvedAiFollowUp = !string.IsNullOrWhiteSpace(aiResult.FollowUpPrompt)
                        ? aiResult.FollowUpPrompt.Trim()
                        : FollowUpPromptBuilder.TryExtractFollowUpFromCot(aiResult.CotTrace);

                    var cachedEval = new AiEvaluation
                    {
                        Id = evalId,
                        AnswerType = "mock_exam",
                        StudentId = session.StudentId,
                        PracticeAnswerId = null,
                        ExamSubmissionId = null,
                        InClassSubmissionId = null,
                        MockExamAnswerId = justAnswered.Id,
                        TotalScore = finalCachedScore,
                        Feedback = aiResult.Feedback,
                        ConfidenceScore = aiResult.ConfidenceScore,
                        CotTrace = aiResult.CotTrace,
                        FollowUpPrompt = resolvedAiFollowUp,
                        EvaluatedAt = DateTime.UtcNow,
                        Details = evalDetails
                    };
                    generatedCachedEval = cachedEval;

                    if (aiResult.Score <= 0.0m ||
                        aiResult.IsSuspicious ||
                        (!string.IsNullOrWhiteSpace(aiResult.CotTrace) &&
                         aiResult.CotTrace.Contains("[NON_ANSWER_GUARD]", StringComparison.OrdinalIgnoreCase)))
                    {
                        return (string.Empty, generatedCachedEval);
                    }

                    aiGeneratedFollowUpPrompt = resolvedAiFollowUp;

                    if (justAnswered.IsFollowUp && allOrderedCriteria.Count > 1)
                    {
                        var rootEvalDetails = parentMain.AiEvaluations?
                            .OrderByDescending(e => e.EvaluatedAt)
                            .FirstOrDefault()?.Details;
                        var usedFollowUpCriteria = priorFollowUps
                            .SelectMany(f => (f.AiEvaluations ?? System.Linq.Enumerable.Empty<AiEvaluation>()).OrderByDescending(e => e.EvaluatedAt).Take(1))
                            .Where(e => e.Details != null && e.Details.Count == 1)
                            .Select(e => e.Details.First().CriterionId)
                            .Concat(evalDetails.Select(d => d.CriterionId))
                            .ToHashSet();

                        var nextCandidates = allOrderedCriteria
                            .Where(c => !usedFollowUpCriteria.Contains(c.Id))
                            .ToList();
                        if (nextCandidates.Count == 0)
                        {
                            nextCandidates = allOrderedCriteria;
                        }

                        RubricCriterion? weakestCriterion = null;
                        decimal lowestRatio = decimal.MaxValue;
                        foreach (var crit in nextCandidates)
                        {
                            var detail = rootEvalDetails?.FirstOrDefault(d => d.CriterionId == crit.Id);
                            var detailScore = detail?.Score ?? 0m;
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
                    else if (orderedCriteria.Count > 0 && evalDetails.Count > 0)
                    {
                        RubricCriterion? weakestCriterion = null;
                        decimal lowestRatio = decimal.MaxValue;

                        for (int i = 0; i < orderedCriteria.Count; i++)
                        {
                            var crit = orderedCriteria[i];
                            var matchedDetail = evalDetails.FirstOrDefault(d => d.CriterionId == crit.Id)
                                ?? (i < evalDetails.Count ? evalDetails[i] : null);

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
            var fallbackPool = question?.Rubric?.Criteria?.OrderBy(c => c.OrderIndex).ToList() ?? orderedCriteria;
            var fallbackCriterion = fallbackPool[Math.Max(0, followUpOrdinal - 1) % fallbackPool.Count];
            weakestCriterionName = fallbackCriterion.CriterionName;
            weakestCriterionDescription = fallbackCriterion.Description;
        }

        var resolvedPrompt = FollowUpPromptBuilder.ResolveFollowUpPrompt(
            courseCode: session.Course?.Code,
            questionFollowUpPrompt: null,
            aiGeneratedFollowUpPrompt: aiGeneratedFollowUpPrompt,
            followUpSequence: followUpOrdinal,
            weakestCriterionName: weakestCriterionName,
            weakestCriterionDescription: weakestCriterionDescription,
            criterionNames: (question?.Rubric?.Criteria?.OrderBy(c => c.OrderIndex).ToList() ?? orderedCriteria).Select(c => c.CriterionName),
            questionContent: question?.Content,
            sampleAnswer: null,
            studentTranscript: justAnswered.AnswerText,
            originalAnswerTranscript: parentMain.AnswerText,
            previousFollowUpPrompts: previousFollowUpPrompts);

        return (resolvedPrompt, generatedCachedEval);
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

    private static bool IsTransientSttOutage(Exception exception, CancellationToken callerToken)
    {
        if (exception is OperationCanceledException && callerToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            TimeoutException => true,
            TaskCanceledException => true,
            System.Net.Http.HttpRequestException httpEx => httpEx.StatusCode is null
                or System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests
                or >= System.Net.HttpStatusCode.InternalServerError,
            _ => false
        };
    }
}
