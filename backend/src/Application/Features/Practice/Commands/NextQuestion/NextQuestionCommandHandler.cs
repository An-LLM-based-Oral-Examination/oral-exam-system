using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Practice.Commands.NextQuestion;

public sealed class NextQuestionCommandHandler : IRequestHandler<NextQuestionCommand, Result<NextQuestionResponse>>
{
    private readonly IApplicationDbContext _context;

    public NextQuestionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<NextQuestionResponse>> Handle(NextQuestionCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra session tồn tại và quyền sở hữu của sinh viên
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null || session.StudentId != request.StudentId)
        {
            return Result<NextQuestionResponse>.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        // 2. Lazy Inactivity Timeout Check (đọc từ system_configs, mặc định 10 phút)
        var timeoutConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "SessionInactivityTimeoutMinutes", cancellationToken);
        int timeoutMinutes = (timeoutConfig != null && int.TryParse(timeoutConfig.Value, out var parsedTimeout))
            ? parsedTimeout
            : 10;

        var elapsed = DateTime.UtcNow - session.LastActivityAt;
        if (session.Status == "in_progress" && elapsed > TimeSpan.FromMinutes(timeoutMinutes))
        {
            session.Status = "completed";
            session.CompletedAt = session.LastActivityAt.AddMinutes(timeoutMinutes);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<NextQuestionResponse>.Failure("Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 10 phút.");
        }

        if (session.Status != "in_progress")
        {
            return Result<NextQuestionResponse>.Failure("Phiên luyện tập đã kết thúc.");
        }

        if (session.PracticeMode != "per_question")
        {
            return Result<NextQuestionResponse>.Failure("Tính năng bốc câu hỏi theo yêu cầu chỉ áp dụng cho chế độ [Per-Question].");
        }

        // 3. Lấy danh sách câu trả lời chính đã làm trong session này (loại trừ follow-up)
        var answeredAnswers = await _context.PracticeAnswers
            .AsNoTracking()
            .Include(a => a.Question)
            .Where(a => a.SessionId == session.Id && !a.IsFollowUp)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync(cancellationToken);

        var excludedQuestionIds = answeredAnswers.Select(a => a.QuestionId).Distinct().ToHashSet();

        var answeredQuestionIds = answeredAnswers.Select(a => a.QuestionId).Distinct().ToList();
        var questionDifficulties = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => answeredQuestionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.Difficulty.Trim().ToLowerInvariant(), cancellationToken);

        // 4. Xác định danh sách độ khó đã chọn của session
        List<string> selectedDifficulties;
        if (!string.IsNullOrWhiteSpace(session.SelectedDifficulties))
        {
            try
            {
                selectedDifficulties = JsonSerializer.Deserialize<List<string>>(session.SelectedDifficulties) ?? new List<string>();
            }
            catch
            {
                selectedDifficulties = session.SelectedDifficulties
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim().ToLowerInvariant())
                    .ToList();
            }
        }
        else
        {
            selectedDifficulties = new List<string> { "easy", "medium", "hard" };
        }

        selectedDifficulties = selectedDifficulties
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (!selectedDifficulties.Any())
        {
            selectedDifficulties = new List<string> { "easy", "medium", "hard" };
        }

        // 5. Truy vấn kho câu hỏi active của môn học chưa được trả lời trong session này
        var availableQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric).ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == session.CourseId &&
                        q.IsActive &&
                        !excludedQuestionIds.Contains(q.Id) &&
                        selectedDifficulties.Contains(q.Difficulty.ToLower()))
            .ToListAsync(cancellationToken);

        // Nếu kho đề cạn kiệt toàn bộ câu hỏi theo các mức đã chọn
        if (!availableQuestions.Any())
        {
            return Result<NextQuestionResponse>.Success(new NextQuestionResponse
            {
                HasMoreQuestions = false,
                Question = null,
                Message = "Đã hoàn thành toàn bộ câu hỏi khả dụng theo mức độ đã chọn."
            });
        }

        // 6. Thuật toán Anti-3-consecutive-same-level randomizer:
        // - Kiểm tra 2 câu hỏi non-follow-up liền trước trong phiên này.
        // - Nếu cả 2 đều có cùng độ khó D và selectedDifficulties có > 1 độ khó: loại trừ D khỏi candidate pool.
        string? excludeDifficulty = null;
        if (selectedDifficulties.Count > 1 && answeredAnswers.Count >= 2)
        {
            string? last1 = null;
            string? last2 = null;

            var qId1 = answeredAnswers[^1].QuestionId;
            var qId2 = answeredAnswers[^2].QuestionId;

            questionDifficulties.TryGetValue(qId1, out last1);
            questionDifficulties.TryGetValue(qId2, out last2);

            if (string.IsNullOrEmpty(last1) && answeredAnswers[^1].Question != null)
            {
                last1 = answeredAnswers[^1].Question.Difficulty?.Trim().ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(last2) && answeredAnswers[^2].Question != null)
            {
                last2 = answeredAnswers[^2].Question.Difficulty?.Trim().ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(last1) && last1 == last2)
            {
                excludeDifficulty = last1;
            }
        }

        List<PracticeQuestion> candidatePool;
        if (excludeDifficulty != null)
        {
            var nonExcludedPool = availableQuestions
                .Where(q => !string.Equals(q.Difficulty.Trim(), excludeDifficulty, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonExcludedPool.Any())
            {
                candidatePool = nonExcludedPool;
            }
            else
            {
                // Fallback to D if D has unattempted questions
                candidatePool = availableQuestions
                    .Where(q => string.Equals(q.Difficulty.Trim(), excludeDifficulty, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }
        else
        {
            candidatePool = availableQuestions;
        }

        if (!candidatePool.Any())
        {
            return Result<NextQuestionResponse>.Success(new NextQuestionResponse
            {
                HasMoreQuestions = false,
                Question = null,
                Message = "Đã hoàn thành toàn bộ câu hỏi khả dụng theo mức độ đã chọn."
            });
        }

        // 7. Bốc ngẫu nhiên công bằng giữa các độ khó khả dụng trong candidatePool
        var candidateDiffs = candidatePool.Select(q => q.Difficulty.Trim().ToLowerInvariant()).Distinct().ToList();
        var chosenDiff = candidateDiffs[Random.Shared.Next(candidateDiffs.Count)];
        var diffPool = candidatePool.Where(q => q.Difficulty.Trim().ToLowerInvariant() == chosenDiff).ToList();
        var selectedQuestion = diffPool[Random.Shared.Next(diffPool.Count)];

        // 8. Cập nhật LastActivityAt
        session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        int questionOrder = answeredAnswers.Count + 1;

        return Result<NextQuestionResponse>.Success(new NextQuestionResponse
        {
            HasMoreQuestions = true,
            Message = null,
            Question = new NextQuestionDto
            {
                Id = selectedQuestion.Id,
                Content = selectedQuestion.Content,
                Difficulty = selectedQuestion.Difficulty,
                QuestionOrder = questionOrder,
                RubricCriteria = selectedQuestion.Rubric?.Criteria.Select(c => c.Description ?? string.Empty).ToList() ?? new List<string>()
            }
        });
    }
}
