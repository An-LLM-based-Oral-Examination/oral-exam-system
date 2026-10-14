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

        // 4. Trích xuất transcript từ âm thanh hoặc dùng transcript trực tiếp
        string transcript;
        if (!string.IsNullOrWhiteSpace(request.DirectTranscript))
        {
            transcript = request.DirectTranscript.Trim();
        }
        else if (request.AudioStream != null && request.AudioStream.Length > 0)
        {
            var fileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "audio.webm";
            transcript = await _speechToTextService.TranscribeAudioAsync(request.AudioStream, fileName, cancellationToken);
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
