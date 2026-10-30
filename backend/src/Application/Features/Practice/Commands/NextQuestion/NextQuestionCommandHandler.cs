using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
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

        // 2. Lazy Inactivity Timeout Check (đọc từ system_configs, mặc định 5 phút)
        int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(_context, cancellationToken);
        if (await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(_context, session, timeoutMinutes, cancellationToken))
        {
            return Result<NextQuestionResponse>.Failure("SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.");
        }

        if (session.Status != "in_progress")
        {
            return Result<NextQuestionResponse>.Failure("Phiên luyện tập đã kết thúc.");
        }

        if (session.PracticeMode != "per_question")
        {
            return Result<NextQuestionResponse>.Failure("Tính năng bốc câu hỏi theo yêu cầu chỉ áp dụng cho chế độ [Per-Question].");
        }

        // 3. Lấy danh sách câu trả lời chính đã làm trong session này (loại trừ follow-up) bằng Projection nhẹ
        var answeredAnswers = await _context.PracticeAnswers
            .AsNoTracking()
            .Where(a => a.SessionId == session.Id && !a.IsFollowUp)
            .OrderBy(a => a.SubmittedAt)
            .Select(a => new
            {
                a.QuestionId,
                QuestionDifficulty = a.Question != null ? a.Question.Difficulty : null
            })
            .ToListAsync(cancellationToken);

        var priorMainQuestionIds = new List<Guid>(session.SelectedQuestionIds);
        foreach (var ansQid in answeredAnswers.Select(a => a.QuestionId))
        {
            if (ansQid != Guid.Empty && !priorMainQuestionIds.Contains(ansQid))
            {
                priorMainQuestionIds.Add(ansQid);
            }
        }

        var excludedQuestionIds = priorMainQuestionIds.ToHashSet();

        // 4. Xác định danh sách độ khó đã chọn của session
        var selectedDifficulties = session.DifficultiesList
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => d is "easy" or "medium" or "hard")
            .Distinct()
            .ToList();

        if (!selectedDifficulties.Any())
        {
            selectedDifficulties = new List<string> { "easy", "medium", "hard" };
        }

        var selectedDiffSet = selectedDifficulties.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 5. Pha 1: Truy vấn danh sách (Id, Difficulty) nhẹ của môn học trên Index, không kéo toàn bộ Content/Rubric/Criteria lên RAM
        var allCourseQuestionMeta = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == session.CourseId && q.IsActive)
            .Select(q => new { q.Id, q.Difficulty })
            .ToListAsync(cancellationToken);

        var questionDifficulties = allCourseQuestionMeta
            .Where(q => excludedQuestionIds.Contains(q.Id))
            .ToDictionary(q => q.Id, q => (q.Difficulty ?? string.Empty).Trim().ToLowerInvariant());

        var availableQuestions = allCourseQuestionMeta
            .Where(q => !excludedQuestionIds.Contains(q.Id) &&
                        !string.IsNullOrWhiteSpace(q.Difficulty) &&
                        selectedDiffSet.Contains(q.Difficulty.Trim()))
            .ToList();

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
        if (selectedDifficulties.Count > 1 && priorMainQuestionIds.Count >= 2)
        {
            string? last1 = null;
            string? last2 = null;

            var qId1 = priorMainQuestionIds[^1];
            var qId2 = priorMainQuestionIds[^2];

            questionDifficulties.TryGetValue(qId1, out last1);
            questionDifficulties.TryGetValue(qId2, out last2);

            if (string.IsNullOrEmpty(last1))
            {
                last1 = answeredAnswers.LastOrDefault(a => a.QuestionId == qId1)?.QuestionDifficulty?.Trim().ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(last2))
            {
                last2 = answeredAnswers.LastOrDefault(a => a.QuestionId == qId2)?.QuestionDifficulty?.Trim().ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(last1) && last1 == last2)
            {
                excludeDifficulty = last1;
            }
        }

        var candidatePool = availableQuestions;
        if (excludeDifficulty != null)
        {
            var nonExcludedPool = availableQuestions
                .Where(q => !string.Equals(q.Difficulty.Trim(), excludeDifficulty, StringComparison.OrdinalIgnoreCase))
                .ToList();

            candidatePool = nonExcludedPool.Any()
                ? nonExcludedPool
                : availableQuestions
                    .Where(q => string.Equals(q.Difficulty.Trim(), excludeDifficulty, StringComparison.OrdinalIgnoreCase))
                    .ToList();
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
        var selectedQuestionId = diffPool[Random.Shared.Next(diffPool.Count)].Id;

        // Pha 2: Chỉ tải đầy đủ nội dung câu hỏi và Rubric Criteria cho đúng 1 câu hỏi đã được chọn
        var selectedQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric).ThenInclude(r => r.Criteria)
            .FirstAsync(q => q.Id == selectedQuestionId, cancellationToken);

        // 8. Cập nhật danh sách câu hỏi đã bốc và LastActivityAt
        var currentSelectedIds = session.SelectedQuestionIds;
        if (!currentSelectedIds.Contains(selectedQuestion.Id))
        {
            currentSelectedIds.Add(selectedQuestion.Id);
            session.SelectedQuestionIds = currentSelectedIds;
        }
        session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        int questionOrder = Math.Max(answeredAnswers.Count, priorMainQuestionIds.Count) + 1;

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
                RubricCriteria = selectedQuestion.Rubric?.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList() ?? new List<string>(),
                ImageUrl = selectedQuestion.ImageUrl,
                ImageDescription = selectedQuestion.ImageDescription
            }
        });
    }
}
