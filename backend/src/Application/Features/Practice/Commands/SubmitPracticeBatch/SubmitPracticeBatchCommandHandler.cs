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
            
        if (session == null)
        {
            return Result<Unit>.Failure("NOT_FOUND: Phiên luyện tập không tồn tại.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<Unit>.Failure("FORBIDDEN: Phiên luyện tập không thuộc về sinh viên này.");
        }

        if (session.Status != "in_progress")
        {
            return Result<Unit>.Failure("CONFLICT: Phiên luyện tập đã kết thúc.");
        }

        // Lazy Inactivity Timeout Check (đọc từ system_configs, mặc định 5 phút)
        int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(_context, cancellationToken);
        if (await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(_context, session, timeoutMinutes, cancellationToken))
        {
            return Result<Unit>.Failure(
                $"SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn {timeoutMinutes} phút. Điểm các câu đã nộp trước đó được bảo toàn.");
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

        var requestedQuestionIds = request.Answers
            .Where(a => a.QuestionId != Guid.Empty)
            .Select(a => a.QuestionId)
            .Distinct()
            .ToList();

        var validQuestionIds = requestedQuestionIds.Count > 0
            ? (await _context.PracticeQuestions
                .AsNoTracking()
                .Where(q => q.CourseId == session.CourseId && q.IsActive && !q.IsDeleted && q.ApprovalStatus == ApprovalStatus.Approved && requestedQuestionIds.Contains(q.Id))
                .Select(q => q.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : new HashSet<Guid>();

        foreach (var answerDto in request.Answers)
        {
            if (answerDto.QuestionId == Guid.Empty)
            {
                return Result<Unit>.Failure("VALIDATION: Mã câu hỏi (QuestionId) trong danh sách nộp hàng loạt không hợp lệ.");
            }

            if (session.SelectedQuestionIds.Count > 0 && !session.SelectedQuestionIds.Contains(answerDto.QuestionId))
            {
                return Result<Unit>.Failure("VALIDATION: Câu hỏi nộp không nằm trong danh sách câu hỏi của phiên luyện tập này.");
            }

            if (!validQuestionIds.Contains(answerDto.QuestionId))
            {
                return Result<Unit>.Failure("VALIDATION: Câu hỏi không tồn tại hoặc không thuộc môn học của phiên luyện tập này.");
            }
        }

        // Khử trùng lặp theo QuestionId (lấy câu trả lời cuối cùng cho mỗi QuestionId trong batch)
        var deduplicatedAnswers = request.Answers
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        var existingMainQuestionIds = (await _context.PracticeAnswers
            .AsNoTracking()
            .Where(a => a.SessionId == request.SessionId && !a.IsFollowUp)
            .Select(a => a.QuestionId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        if (deduplicatedAnswers.Any(a => existingMainQuestionIds.Contains(a.QuestionId)))
        {
            return Result<Unit>.Failure("CONFLICT: Một hoặc nhiều câu hỏi trong danh sách nộp đã được nộp trước đó.");
        }

        if (session.SelectedQuestionIds.Count > 0)
        {
            var submittedIds = deduplicatedAnswers.Select(a => a.QuestionId).ToHashSet();
            foreach (var selectedQId in session.SelectedQuestionIds)
            {
                if (!submittedIds.Contains(selectedQId) && !existingMainQuestionIds.Contains(selectedQId))
                {
                    deduplicatedAnswers.Add(new BatchAnswerDto(selectedQId, string.Empty, false, null));
                }
            }
        }

        var skippedQuestionIds = deduplicatedAnswers
            .Where(a => string.IsNullOrWhiteSpace(a.AnswerText))
            .Select(a => a.QuestionId)
            .Distinct()
            .ToList();

        var skippedQuestionsWithRubrics = skippedQuestionIds.Count > 0
            ? await _context.PracticeQuestions
                .AsNoTracking()
                .Include(q => q.Rubric)
                    .ThenInclude(r => r.Criteria)
                .Where(q => skippedQuestionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, cancellationToken)
            : new Dictionary<Guid, PracticeQuestion>();

        // 2. Prepare Entities for Bulk Insert
        var answers = new List<PracticeAnswer>();
        var skippedEvaluations = new List<AiEvaluation>();
        var pendingQueuePairs = new List<(GradingTask Task, PracticeAnswer Answer)>();

        for (int i = 0; i < deduplicatedAnswers.Count; i++)
        {
            var answerDto = deduplicatedAnswers[i];
            var isSkipped = string.IsNullOrWhiteSpace(answerDto.AnswerText);
            var selectedIdx = session.SelectedQuestionIds.IndexOf(answerDto.QuestionId);
            var orderIndex = selectedIdx >= 0 ? selectedIdx + 1 : i + 1;

            var answer = new PracticeAnswer
            {
                SessionId = request.SessionId,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
                OrderIndex = orderIndex,
                AnswerText = isSkipped ? string.Empty : answerDto.AnswerText.Trim(),
                IsFollowUp = false,
                ParentAnswerId = null,
                Status = isSkipped ? "graded" : "pending",
                SubmittedAt = DateTime.UtcNow
            };

            answers.Add(answer);

            if (isSkipped)
            {
                var eval = new AiEvaluation
                {
                    AnswerType = "practice",
                    StudentId = request.StudentId,
                    PracticeAnswerId = answer.Id,
                    TotalScore = 0.0m,
                    ConfidenceScore = 1.0m,
                    Feedback = "Sinh viên bỏ qua câu hỏi này (0.0/10đ).",
                    CotTrace = "[SKIPPED_QUESTION] Câu hỏi bị bỏ qua trong phiên Full-Session Progressive nên được tính 0.0 điểm.",
                    EvaluatedAt = DateTime.UtcNow
                };

                if (skippedQuestionsWithRubrics.TryGetValue(answerDto.QuestionId, out var skippedQ) && skippedQ.Rubric?.Criteria != null)
                {
                    foreach (var crit in skippedQ.Rubric.Criteria.OrderBy(c => c.OrderIndex))
                    {
                        eval.Details.Add(new AiEvaluationDetail
                        {
                            EvaluationId = eval.Id,
                            CriterionId = crit.Id,
                            Score = 0.0m,
                            Comment = "Chưa trả lời tiêu chí này (0 điểm)."
                        });
                    }
                }

                answer.AiEvaluations.Add(eval);
                skippedEvaluations.Add(eval);
            }
            else
            {
                var task = new GradingTask
                {
                    AnswerId = answer.Id,
                    SessionId = session.Id,
                    QuestionId = answerDto.QuestionId,
                    StudentId = request.StudentId,
                    AnswerText = answer.AnswerText,
                    IsFollowUp = false,
                    ParentAnswerId = null,
                    IsFullSession = true,
                    ConnectionId = string.Empty
                };

                pendingQueuePairs.Add((task, answer));
            }
        }

        // 3. Update Session Status
        session.Status = "completed";
        session.CompletedAt = DateTime.UtcNow;
        session.LastActivityAt = DateTime.UtcNow;
        if (pendingQueuePairs.Count == 0)
        {
            if (existingMainQuestionIds.Count == 0)
            {
                session.TotalScore = 0.0m;
            }
            else
            {
                var existingAnswers = await _context.PracticeAnswers
                    .AsNoTracking()
                    .Include(a => a.AiEvaluations)
                    .Where(a => a.SessionId == request.SessionId)
                    .ToListAsync(cancellationToken);

                bool hasUnscoredExistingAnswer = existingAnswers.Any(a =>
                    string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Status, "grading", StringComparison.OrdinalIgnoreCase) ||
                    a.AiEvaluations == null ||
                    a.AiEvaluations.Count == 0);

                if (hasUnscoredExistingAnswer)
                {
                    session.TotalScore = null;
                }
                else
                {
                    var allAnswers = new List<PracticeAnswer>(existingAnswers.Count + answers.Count);
                    allAnswers.AddRange(existingAnswers);
                    allAnswers.AddRange(answers);
                    session.TotalScore = PracticeSessionTimeoutHelper.ComputeSessionTotalScore(session, allAnswers);
                }
            }
        }
        else
        {
            session.TotalScore = null;
        }

        // 4. Bulk Insert & Save
        try
        {
            _context.PracticeAnswers.AddRange(answers);
            if (skippedEvaluations.Count > 0)
            {
                _context.AiEvaluations.AddRange(skippedEvaluations);
            }
            await _context.SaveChangesAsync(cancellationToken); // 1 Transaction cho tất cả
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Result<Unit>.Failure("CONFLICT: Một hoặc nhiều câu trả lời trong phiên này đã được nộp trước đó.");
        }

        // 5. Đẩy vào Hàng đợi BoundedChannel (chỉ đẩy các câu có nội dung trả lời)
        foreach (var (task, answer) in pendingQueuePairs)
        {
            task.AnswerId = answer.Id; // Ensure Id is set after save
            await _queueChannel.EnqueueAsync(task, cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
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
