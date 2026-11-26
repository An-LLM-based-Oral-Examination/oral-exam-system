using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;

public sealed class SubmitPracticeAnswerCommandHandler : IRequestHandler<SubmitPracticeAnswerCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGradingQueueChannel _queueChannel;

    public SubmitPracticeAnswerCommandHandler(IApplicationDbContext context, IGradingQueueChannel queueChannel)
    {
        _context = context;
        _queueChannel = queueChannel;
    }

    public async Task<Result<Guid>> Handle(SubmitPracticeAnswerCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Session & Ownership
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<Guid>.Failure("NOT_FOUND: Phiên luyện tập không tồn tại.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<Guid>.Failure("FORBIDDEN: Phiên luyện tập không thuộc về sinh viên này.");
        }

        // Lazy Inactivity Timeout Check (đọc từ system_configs, mặc định 5 phút)
        int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(_context, cancellationToken);
        if (await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(_context, session, timeoutMinutes, cancellationToken))
        {
            return Result<Guid>.Failure(
                $"SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn {timeoutMinutes} phút.");
        }

        if (session.Status != "in_progress")
        {
            return Result<Guid>.Failure("CONFLICT: Phiên luyện tập đã kết thúc.");
        }

        if (string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase) && request.IsFollowUp)
        {
            return Result<Guid>.Failure("VALIDATION: Chế độ Full-Session Progressive không cho phép nộp câu trả lời phụ (Follow-up).");
        }

        if (session.SelectedQuestionIds.Count > 0 && !session.SelectedQuestionIds.Contains(request.QuestionId))
        {
            return Result<Guid>.Failure("VALIDATION: Câu hỏi nộp không nằm trong danh sách câu hỏi của phiên luyện tập này.");
        }

        var targetQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (targetQuestion == null || targetQuestion.CourseId != session.CourseId || !targetQuestion.IsActive || targetQuestion.IsDeleted)
        {
            return Result<Guid>.Failure("VALIDATION: Câu hỏi không tồn tại hoặc không thuộc môn học của phiên luyện tập này.");
        }

        if (targetQuestion.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Result<Guid>.Failure("VALIDATION: Câu hỏi chưa được phê duyệt để luyện tập.");
        }

        // 2. Validate Duplicate / Follow-Up Boundary
        string? resolvedPromptText = null;
        if (!request.IsFollowUp)
        {
            if (request.ParentAnswerId.HasValue && request.ParentAnswerId.Value != Guid.Empty)
            {
                return Result<Guid>.Failure("VALIDATION: Câu trả lời chính (isFollowUp = false) không được chứa parentAnswerId.");
            }

            var existingMainAnswer = await _context.PracticeAnswers
                .AnyAsync(a => a.SessionId == request.SessionId && a.QuestionId == request.QuestionId && !a.IsFollowUp, cancellationToken);
            if (existingMainAnswer)
            {
                return Result<Guid>.Failure("CONFLICT: Câu trả lời chính cho câu hỏi này đã được nộp trước đó.");
            }
        }
        else
        {
            if (!request.ParentAnswerId.HasValue || request.ParentAnswerId.Value == Guid.Empty)
            {
                return Result<Guid>.Failure("VALIDATION: Câu trả lời phụ (isFollowUp = true) bắt buộc phải có parentAnswerId hợp lệ.");
            }

            var parentAnswer = await _context.PracticeAnswers
                .Include(a => a.AiEvaluations)
                .FirstOrDefaultAsync(a => a.Id == request.ParentAnswerId.Value && a.SessionId == request.SessionId && a.QuestionId == request.QuestionId, cancellationToken);

            if (parentAnswer == null)
            {
                return Result<Guid>.Failure("NOT_FOUND: Không tìm thấy câu trả lời gốc tương ứng trong phiên luyện tập này.");
            }

            var latestParentEval = parentAnswer.AiEvaluations
                .OrderByDescending(e => e.EvaluatedAt)
                .FirstOrDefault();

            if (latestParentEval == null)
            {
                return Result<Guid>.Failure("VALIDATION: Câu trả lời gốc chưa có kết quả đánh giá để tạo câu hỏi phụ.");
            }

            if (latestParentEval.TotalScore < 4.0m || latestParentEval.TotalScore > 8.0m)
            {
                return Result<Guid>.Failure($"VALIDATION: Điểm câu trả lời gốc ({latestParentEval.TotalScore:F1}) không nằm trong khoảng [4.0, 8.0] cho phép hỏi tiếp.");
            }

            var duplicateFollowUp = await _context.PracticeAnswers
                .AnyAsync(a => a.SessionId == request.SessionId && a.ParentAnswerId == request.ParentAnswerId.Value, cancellationToken);
            if (duplicateFollowUp)
            {
                return Result<Guid>.Failure("CONFLICT: Câu trả lời phụ cho câu trả lời gốc này đã được nộp trước đó.");
            }

            var maxFollowUpConfig = await _context.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
            int maxAllowedFollowUps = 2;
            if (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMax))
            {
                maxAllowedFollowUps = Math.Clamp(parsedMax, 1, 5);
            }

            var currentFollowUpCount = await _context.PracticeAnswers
                .CountAsync(a => a.SessionId == request.SessionId && a.QuestionId == request.QuestionId && a.IsFollowUp, cancellationToken);
            if (currentFollowUpCount >= maxAllowedFollowUps)
            {
                return Result<Guid>.Failure($"VALIDATION: Câu hỏi này đã đạt giới hạn tối đa {maxAllowedFollowUps} câu hỏi phụ.");
            }

            resolvedPromptText = FollowUpPromptBuilder.ResolveEvaluationFollowUpPrompt(latestParentEval);
        }

        int orderIndex = 1;
        var idxInSelected = session.SelectedQuestionIds.IndexOf(request.QuestionId);
        if (idxInSelected >= 0)
        {
            orderIndex = idxInSelected + 1;
        }
        else
        {
            var existingOrderForQuestion = await _context.PracticeAnswers
                .AsNoTracking()
                .Where(a => a.SessionId == request.SessionId && a.QuestionId == request.QuestionId)
                .Select(a => (int?)a.OrderIndex)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingOrderForQuestion is > 0)
            {
                orderIndex = existingOrderForQuestion.Value;
            }
            else
            {
                var existingDistinctQuestions = await _context.PracticeAnswers
                    .AsNoTracking()
                    .Where(a => a.SessionId == request.SessionId)
                    .Select(a => a.QuestionId)
                    .Distinct()
                    .CountAsync(cancellationToken);
                orderIndex = existingDistinctQuestions + 1;
            }
        }

        var answer = new PracticeAnswer
        {
            SessionId = request.SessionId,
            QuestionId = request.QuestionId,
            StudentId = request.StudentId,
            OrderIndex = orderIndex,
            AnswerText = request.AnswerText,
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.IsFollowUp ? request.ParentAnswerId : null,
            PromptText = resolvedPromptText,
            Status = "pending", // Waiting for AI Evaluation
            SubmittedAt = DateTime.UtcNow
        };

        try
        {
            _context.PracticeAnswers.Add(answer);
            session.LastActivityAt = DateTime.UtcNow;
            session.TotalScore = null;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (!request.IsFollowUp)
            {
                return Result<Guid>.Failure("CONFLICT: Câu trả lời chính cho câu hỏi này đã được nộp trước đó.");
            }
            return Result<Guid>.Failure("CONFLICT: Câu trả lời phụ cho câu trả lời gốc này đã được nộp trước đó.");
        }

        var gradingTask = new GradingTask
        {
            AnswerId = answer.Id,
            SessionId = session.Id,
            QuestionId = request.QuestionId,
            StudentId = request.StudentId,
            AnswerText = request.AnswerText,
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.IsFollowUp ? request.ParentAnswerId : null,
            IsFullSession = session.PracticeMode == "full_session",
            ConnectionId = string.Empty
        };

        await _queueChannel.EnqueueAsync(gradingTask, cancellationToken);
        return Result<Guid>.Success(answer.Id);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current.GetType().Name == "PostgresException")
            {
                var sqlStateProp = current.GetType().GetProperty("SqlState");
                var sqlState = sqlStateProp?.GetValue(current)?.ToString();
                if (sqlState == "23505")
                {
                    return true;
                }
            }

            var msg = current.Message;
            if (msg.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("uq_practice_answers", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
