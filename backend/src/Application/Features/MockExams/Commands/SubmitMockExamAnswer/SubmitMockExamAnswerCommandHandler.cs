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
/// </summary>
public sealed class SubmitMockExamAnswerCommandHandler(
    IApplicationDbContext context,
    ISpeechToTextService speechToTextService
) : IRequestHandler<SubmitMockExamAnswerCommand, Result<SubmitMockExamAnswerResponse>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly ISpeechToTextService _speechToTextService = speechToTextService;

    public async Task<Result<SubmitMockExamAnswerResponse>> Handle(
        SubmitMockExamAnswerCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra ca thi thử tồn tại và thuộc về sinh viên
        var session = await _context.MockExamSessions
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

        // 2. Server Master Timer check (kèm 10 giây Network Grace Period)
        var now = DateTime.UtcNow;
        if (now > session.ExpiresAt.AddSeconds(10))
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Ca thi đã hết thời gian làm bài.");
        }

        // 3. Tìm bản ghi MockExamAnswer đã được tạo sẵn từ lúc bắt đầu ca thi
        var answer = session.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId);

        if (answer == null)
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("Câu hỏi không thuộc đề thi này.");
        }

        // 4. Trích xuất transcript từ âm thanh hoặc dùng transcript trực tiếp (hỗ trợ Hybrid Fallback)
        string transcript = string.Empty;
        if (request.AudioStream != null && request.AudioStream.Length > 0)
        {
            var fileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "audio.webm";
            Exception? sttException = null;

            try
            {
                var questionHint = await _context.PracticeQuestions
                    .AsNoTracking()
                    .Where(q => q.Id == request.QuestionId)
                    .Select(q => q.Title + ". " + q.Content)
                    .FirstOrDefaultAsync(cancellationToken);

                OralExamination.Application.Common.Models.SpeechTranscriptionResult? detailed = null;
                // Luôn gọi TranscribeDetailedAsync ngay cả khi questionHint rỗng/null
                detailed = await _speechToTextService.TranscribeDetailedAsync(
                    request.AudioStream,
                    fileName,
                    questionHint,
                    cancellationToken);
                transcript = detailed?.Text ?? string.Empty;

                // Chỉ gọi TranscribeAudioAsync khi chưa chạy TranscribeDetailedAsync (hoặc mock unit test trả về null)
                if (detailed is null && string.IsNullOrWhiteSpace(transcript))
                {
                    if (request.AudioStream.CanSeek)
                    {
                        request.AudioStream.Position = 0;
                    }
                    transcript = await _speechToTextService.TranscribeAudioAsync(request.AudioStream, fileName, cancellationToken);
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
                ? TranscriptHelper.NormalizeOrFallback(request.DirectTranscript, _speechToTextService.NormalizeTranscript(request.DirectTranscript))
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                transcript = normalizedServer;
            }
            else if (!string.IsNullOrWhiteSpace(normalizedClient))
            {
                transcript = normalizedClient;
            }
            else if (sttException != null)
            {
                return Result<SubmitMockExamAnswerResponse>.Failure($"Lỗi khi bóc băng âm thanh: {sttException.Message}");
            }
            else
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.DirectTranscript))
        {
            var normalized = TranscriptHelper.NormalizeOrFallback(
                request.DirectTranscript,
                _speechToTextService.NormalizeTranscript(request.DirectTranscript));

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Result<SubmitMockExamAnswerResponse>.Failure("Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
            }
            transcript = normalized;
        }
        else
        {
            return Result<SubmitMockExamAnswerResponse>.Failure("File âm thanh không hợp lệ hoặc không có dữ liệu.");
        }

        // 5. Cập nhật câu trả lời (Ghi đè nếu sinh viên trả lời lại trước khi hết giờ)
        answer.AnswerText = transcript;
        answer.TimeTakenSeconds = request.TimeTakenSeconds;
        answer.Status = "answered";
        answer.AnsweredAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SubmitMockExamAnswerResponse>.Success(new SubmitMockExamAnswerResponse(
            AnswerId: answer.Id,
            QuestionId: answer.QuestionId,
            Transcript: answer.AnswerText ?? string.Empty,
            Status: answer.Status,
            AnsweredAt: answer.AnsweredAt
        ));
    }
}
