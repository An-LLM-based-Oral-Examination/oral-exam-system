using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;

/// <summary>
/// Handler xử lý nộp câu trả lời Voice-First cho một câu hỏi trong ca thi thử (MF-02).
/// Sử dụng Cloudflare Whisper AI bóc băng trong RAM, cam kết Zero R2 storage (không lưu audio).
/// Khi ca thi bật HasFollowUp, sau câu chính/phụ có thể mở câu hỏi phụ (không theo band điểm 4–8 của practice).
/// </summary>
public sealed class SubmitMockExamAnswerCommandHandler(
    IApplicationDbContext context,
    ISpeechToTextService speechToTextService
) : IRequestHandler<SubmitMockExamAnswerCommand, Result<SubmitMockExamAnswerResponse>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly ISpeechToTextService _speechToTextService = speechToTextService;

    private const string DefaultFollowUpPrompt =
        "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.";

    public async Task<Result<SubmitMockExamAnswerResponse>> Handle(
        SubmitMockExamAnswerCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _context.MockExamSessions
            .Include(s => s.Course)
            .Include(s => s.Answers)
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

        var transcriptResult = await ResolveTranscriptAsync(request, cancellationToken);
        if (!transcriptResult.IsSuccess)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure(transcriptResult.Error ?? "Không nhận diện được giọng nói.");
        }

        // Chỉnh sửa transcript đã nộp trước đó → không sinh thêm câu phụ mới.
        var wasAlreadyAnswered =
            string.Equals(answer.Status, "answered", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(answer.AnswerText);

        answer.AnswerText = transcriptResult.Value;
        answer.TimeTakenSeconds = request.TimeTakenSeconds;
        answer.Status = "answered";
        answer.AnsweredAt = DateTime.UtcNow;

        // Chỉ sinh / trả follow-up khi nộp lần đầu; chỉnh sửa transcript không kích hoạt lại.
        var followUp = wasAlreadyAnswered
            ? null
            : await TryOpenNextFollowUpAsync(session, answer, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SubmitMockExamAnswerResponse>.Success(new SubmitMockExamAnswerResponse(
            AnswerId: answer.Id,
            QuestionId: answer.QuestionId,
            Transcript: answer.AnswerText ?? string.Empty,
            Status: answer.Status,
            AnsweredAt: answer.AnsweredAt,
            IsFollowUp: answer.IsFollowUp,
            ParentAnswerId: answer.ParentAnswerId,
            NeedsFollowUp: followUp is not null,
            FollowUpPrompt: followUp?.PromptText,
            FollowUpAnswerId: followUp?.Id,
            FollowUpIndex: followUp is null ? 0 : CountFollowUps(session, RootParentId(answer)),
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
        CancellationToken cancellationToken)
    {
        if (request.AudioStream != null && request.AudioStream.Length > 0)
        {
            var fileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "audio.webm";
            Exception? sttException = null;
            var transcript = string.Empty;

            try
            {
                SpeechTranscriptionResult? detailed = await _speechToTextService.TranscribeDetailedAsync(
                    request.AudioStream,
                    fileName,
                    vocabularyHint: null,
                    cancellationToken);
                transcript = detailed?.Text ?? string.Empty;

                if (detailed is null && string.IsNullOrWhiteSpace(transcript))
                {
                    if (request.AudioStream.CanSeek)
                    {
                        request.AudioStream.Position = 0;
                    }
                    transcript = await _speechToTextService.TranscribeAudioAsync(
                        request.AudioStream,
                        fileName,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                sttException = ex;
            }

            var normalizedServer = !string.IsNullOrWhiteSpace(transcript)
                ? TranscriptHelper.NormalizeOrFallback(transcript, _speechToTextService.NormalizeTranscript(transcript))
                : string.Empty;
            var normalizedClient = !string.IsNullOrWhiteSpace(request.DirectTranscript)
                ? TranscriptHelper.NormalizeOrFallback(
                    request.DirectTranscript,
                    _speechToTextService.NormalizeTranscript(request.DirectTranscript))
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                return Result<string>.Success(normalizedServer);
            }

            if (!string.IsNullOrWhiteSpace(normalizedClient))
            {
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

            return Result<string>.Success(normalized);
        }

        return Result<string>.Failure("File âm thanh không hợp lệ hoặc không có dữ liệu.");
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
            .OrderBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        // Tái sử dụng slot phụ đang pending (chưa có transcript)
        var pending = existingFollowUps.FirstOrDefault(a =>
            a.Status == "pending" || string.IsNullOrWhiteSpace(a.AnswerText));
        if (pending is not null && pending.Id != justAnswered.Id)
        {
            return pending;
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

        // Chỉ mở follow-up mới ngay sau khi vừa nộp (câu chính hoặc câu phụ trước đó)
        var question = await _context.PracticeQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == justAnswered.QuestionId, cancellationToken);

        var prompt = ResolveFollowUpPrompt(question, totalFollowUps + 1);
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

    private static Guid RootParentId(MockExamAnswer answer)
        => answer.IsFollowUp && answer.ParentAnswerId is Guid parent && parent != Guid.Empty
            ? parent
            : answer.Id;

    private static int CountFollowUps(MockExamSession session, Guid parentId)
        => session.Answers.Count(a => a.IsFollowUp && a.ParentAnswerId == parentId);

    private static int GetMaxFollowUps(MockExamSession session)
    {
        var configured = session.Course?.MaxFollowUpQuestions ?? 2;
        return Math.Clamp(configured, 1, 5);
    }

    private static string ResolveFollowUpPrompt(PracticeQuestion? question, int followUpOrdinal)
    {
        if (question is not null && !string.IsNullOrWhiteSpace(question.FollowUpPrompt))
        {
            if (followUpOrdinal <= 1)
            {
                return question.FollowUpPrompt.Trim();
            }

            return $"{question.FollowUpPrompt.Trim()} (Hãy bổ sung thêm khía cạnh hoặc ví dụ khác so với câu trả lời trước.)";
        }

        return followUpOrdinal <= 1
            ? DefaultFollowUpPrompt
            : "Hãy bổ sung thêm ví dụ kỹ thuật hoặc tình huống áp dụng liên quan đến câu trả lời trước của bạn.";
    }
}
