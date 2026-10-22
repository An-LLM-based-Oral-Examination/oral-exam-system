using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;

public sealed class SubmitPracticeBatchCommandHandler : IRequestHandler<SubmitPracticeBatchCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGradingQueueChannel _queueChannel;

    public SubmitPracticeBatchCommandHandler(IApplicationDbContext context, IGradingQueueChannel queueChannel)
    {
        _context = context;
        _queueChannel = queueChannel;
    }

    public async Task<Result<Unit>> Handle(SubmitPracticeBatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Session & Ownership
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            
        if (session == null || session.StudentId != request.StudentId)
        {
            return Result<Unit>.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        // Lazy Inactivity Timeout Check (5 phút)
        var timeoutConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "SessionInactivityTimeoutMinutes", cancellationToken);
        int timeoutMinutes = (timeoutConfig != null && int.TryParse(timeoutConfig.Value, out var parsedTimeout))
            ? parsedTimeout
            : 5;

        var elapsed = DateTime.UtcNow - session.LastActivityAt;
        if (elapsed > TimeSpan.FromMinutes(timeoutMinutes))
        {
            if (session.Status == "in_progress")
            {
                session.Status = "completed";
                session.CompletedAt = session.LastActivityAt.AddMinutes(timeoutMinutes);
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (session.Status == "completed")
            {
                return Result<Unit>.Failure("SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút. Điểm các câu đã nộp trước đó được bảo toàn.");
            }
        }

        if (session.Status != "in_progress")
        {
            return Result<Unit>.Failure("Phiên luyện tập đã kết thúc.");
        }

        if (session.PracticeMode != "full_session")
        {
            return Result<Unit>.Failure("Tính năng nộp trọn gói chỉ hỗ trợ cho phiên [Full-Session].");
        }

        if (request.Answers == null || request.Answers.Count == 0)
        {
            return Result<Unit>.Failure("VALIDATION: Danh sách câu trả lời nộp hàng loạt không được để trống.");
        }

        if (string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase) && request.Answers.Any(a => a.IsFollowUp))
        {
            return Result<Unit>.Failure("VALIDATION: Chế độ Full-Session Progressive không cho phép nộp câu trả lời phụ (Follow-up).");
        }

        var courseHasQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .AnyAsync(q => q.CourseId == session.CourseId && q.IsActive, cancellationToken);

        foreach (var answerDto in request.Answers)
        {
            if (answerDto.QuestionId == Guid.Empty)
            {
                return Result<Unit>.Failure("VALIDATION: Mã câu hỏi (QuestionId) trong danh sách nộp hàng loạt không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(answerDto.AnswerText))
            {
                return Result<Unit>.Failure("VALIDATION: Nội dung câu trả lời trong danh sách nộp hàng loạt không được để trống.");
            }

            if (session.SelectedQuestionIds.Count > 0 && !session.SelectedQuestionIds.Contains(answerDto.QuestionId))
            {
                return Result<Unit>.Failure("VALIDATION: Câu hỏi nộp không nằm trong danh sách câu hỏi của phiên luyện tập này.");
            }

            if (courseHasQuestions)
            {
                var isValidCourseQuestion = await _context.PracticeQuestions
                    .AsNoTracking()
                    .AnyAsync(q => q.Id == answerDto.QuestionId && q.CourseId == session.CourseId && q.IsActive, cancellationToken);
                if (!isValidCourseQuestion)
                {
                    return Result<Unit>.Failure("VALIDATION: Câu hỏi không tồn tại hoặc không thuộc môn học của phiên luyện tập này.");
                }
            }
        }

        // Khử trùng lặp theo QuestionId (lấy câu trả lời cuối cùng cho mỗi QuestionId trong batch)
        var deduplicatedAnswers = request.Answers
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        // 2. Prepare Entities for Bulk Insert
        var answers = new List<PracticeAnswer>();
        var gradingTasks = new List<GradingTask>();

        foreach (var answerDto in deduplicatedAnswers)
        {
            var answer = new PracticeAnswer
            {
                SessionId = request.SessionId,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
                AnswerText = answerDto.AnswerText,
                IsFollowUp = answerDto.IsFollowUp,
                ParentAnswerId = answerDto.ParentAnswerId,
                Status = "pending",
                SubmittedAt = DateTime.UtcNow
            };

            answers.Add(answer);
            
            // Queue Tasks
            gradingTasks.Add(new GradingTask
            {
                AnswerId = answer.Id,
                SessionId = session.Id,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
                AnswerText = answerDto.AnswerText,
                IsFollowUp = answerDto.IsFollowUp,
                ParentAnswerId = answerDto.ParentAnswerId,
                IsFullSession = true,
                ConnectionId = string.Empty
            });
        }

        // 3. Update Session Status
        session.Status = "completed";
        session.CompletedAt = DateTime.UtcNow;
        session.LastActivityAt = DateTime.UtcNow;

        // 4. Bulk Insert & Save
        _context.PracticeAnswers.AddRange(answers);
        await _context.SaveChangesAsync(cancellationToken); // 1 Transaction cho tất cả

        // 5. Đẩy vào Hàng đợi BoundedChannel
        foreach (var (task, answer) in gradingTasks.Zip(answers))
        {
            task.AnswerId = answer.Id; // Ensure Id is set after save
            await _queueChannel.EnqueueAsync(task, cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
