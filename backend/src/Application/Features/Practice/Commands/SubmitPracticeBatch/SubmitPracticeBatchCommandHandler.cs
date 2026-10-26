using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
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
        int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(_context, cancellationToken);
        if (await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(_context, session, timeoutMinutes, cancellationToken))
        {
            return Result<Unit>.Failure("SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút. Điểm các câu đã nộp trước đó được bảo toàn.");
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

        if (session.SelectedQuestionIds.Count > 0)
        {
            var submittedIds = deduplicatedAnswers.Select(a => a.QuestionId).ToHashSet();
            foreach (var selectedQId in session.SelectedQuestionIds)
            {
                if (!submittedIds.Contains(selectedQId))
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

        foreach (var answerDto in deduplicatedAnswers)
        {
            var isSkipped = string.IsNullOrWhiteSpace(answerDto.AnswerText);
            var answer = new PracticeAnswer
            {
                SessionId = request.SessionId,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
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

        // 4. Bulk Insert & Save
        _context.PracticeAnswers.AddRange(answers);
        if (skippedEvaluations.Count > 0)
        {
            _context.AiEvaluations.AddRange(skippedEvaluations);
        }
        await _context.SaveChangesAsync(cancellationToken); // 1 Transaction cho tất cả

        // 5. Đẩy vào Hàng đợi BoundedChannel (chỉ đẩy các câu có nội dung trả lời)
        foreach (var (task, answer) in pendingQueuePairs)
        {
            task.AnswerId = answer.Id; // Ensure Id is set after save
            await _queueChannel.EnqueueAsync(task, cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
