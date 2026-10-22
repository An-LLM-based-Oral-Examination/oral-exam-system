using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.ResumeMockExamSession;

/// <summary>
/// Handler khôi phục đề + đáp án đã nộp của ca thi thử in_progress.
/// </summary>
public sealed class ResumeMockExamSessionQueryHandler(
    IApplicationDbContext context
) : IRequestHandler<ResumeMockExamSessionQuery, Result<ResumeMockExamResponse>>
{
    public async Task<Result<ResumeMockExamResponse>> Handle(
        ResumeMockExamSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await context.MockExamSessions
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question!)
                    .ThenInclude(q => q.Rubric!)
                        .ThenInclude(r => r.Criteria)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            return Result<ResumeMockExamResponse>.Failure("Không tìm thấy thông tin ca thi thử.");
        }

        if (session.StudentId != request.StudentId)
        {
            return Result<ResumeMockExamResponse>.Failure("Bạn không có quyền truy cập ca thi này.");
        }

        if (!string.Equals(session.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ResumeMockExamResponse>.Failure("Ca thi này đã kết thúc, không thể tiếp tục làm bài.");
        }

        var orderedAnswers = session.Answers
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.IsFollowUp)
            .ThenBy(a => a.AnsweredAt)
            .ThenBy(a => a.Id)
            .ToList();

        var mainAnswers = orderedAnswers.Where(a => !a.IsFollowUp).ToList();

        var questionDtos = new List<MockExamQuestionDto>();
        for (var i = 0; i < mainAnswers.Count; i++)
        {
            var answer = mainAnswers[i];
            var question = answer.Question;
            if (question is null) continue;

            var orderIndex = answer.OrderIndex > 0 ? answer.OrderIndex : i + 1;
            var criteriaDtos = question.Rubric?.Criteria != null
                ? question.Rubric.Criteria
                    .OrderBy(c => c.OrderIndex)
                    .Select(c => new MockExamRubricCriterionDto(c.CriterionName, c.MaxScore))
                    .ToList()
                : new List<MockExamRubricCriterionDto>();

            questionDtos.Add(new MockExamQuestionDto(
                Id: question.Id,
                OrderIndex: orderIndex,
                Title: question.Title,
                Content: question.Content,
                BloomLevel: question.BloomLevel,
                Difficulty: question.Difficulty,
                RubricCriteria: criteriaDtos,
                ImageUrl: question.ImageUrl,
                ImageDescription: question.ImageDescription
            ));
        }

        var answeredDtos = orderedAnswers
            .Where(a =>
                !string.IsNullOrWhiteSpace(a.AnswerText)
                && (a.Status is "answered" or "graded"))
            .Select(a => new MockExamStoredAnswerDto(
                AnswerId: a.Id,
                QuestionId: a.QuestionId,
                Transcript: a.AnswerText ?? string.Empty,
                TimeTakenSeconds: a.TimeTakenSeconds,
                Status: a.Status,
                AnsweredAt: a.AnsweredAt,
                IsFollowUp: a.IsFollowUp,
                ParentAnswerId: a.ParentAnswerId,
                PromptText: a.PromptText
            ))
            .ToList();

        var maxFollowUps = Math.Clamp(session.Course?.MaxFollowUpQuestions ?? 2, 1, 5);
        MockExamActiveFollowUpDto? activeFollowUp = null;
        if (session.HasFollowUp)
        {
            var pendingFollowUp = orderedAnswers.FirstOrDefault(a =>
                a.IsFollowUp
                && (a.Status == "pending" || string.IsNullOrWhiteSpace(a.AnswerText)));

            if (pendingFollowUp is not null && pendingFollowUp.ParentAnswerId is Guid parentId)
            {
                var followUpIndex = orderedAnswers.Count(a =>
                    a.IsFollowUp
                    && a.ParentAnswerId == parentId
                    && (a.Id == pendingFollowUp.Id
                        || (!string.IsNullOrWhiteSpace(a.AnswerText) && a.Status is "answered" or "graded")
                        || a.AnsweredAt <= pendingFollowUp.AnsweredAt));

                activeFollowUp = new MockExamActiveFollowUpDto(
                    AnswerId: pendingFollowUp.Id,
                    QuestionId: pendingFollowUp.QuestionId,
                    ParentAnswerId: parentId,
                    Prompt: pendingFollowUp.PromptText?.Trim().Length > 0
                        ? pendingFollowUp.PromptText!.Trim()
                        : "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.",
                    FollowUpIndex: Math.Max(1, followUpIndex),
                    MaxFollowUps: maxFollowUps
                );
            }
        }

        var course = session.Course;
        var maxDailyQuota = course is not null && course.MaxMockExamsPerDay > 0
            ? course.MaxMockExamsPerDay
            : 3;
        var quotaDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var quota = await context.MockExamQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                q => q.StudentId == request.StudentId
                     && q.CourseId == session.CourseId
                     && q.QuotaDate == quotaDate,
                cancellationToken);

        var usedQuota = quota?.UsedCount ?? 0;
        var remainingQuota = Math.Max(0, maxDailyQuota - usedQuota);
        var durationMinutes = Math.Max(
            1,
            (int)Math.Round((session.ExpiresAt - session.StartedAt).TotalMinutes));

        var response = new ResumeMockExamResponse(
            SessionId: session.Id,
            CourseId: session.CourseId,
            CourseCode: course?.Code ?? string.Empty,
            CourseName: course?.Name ?? string.Empty,
            HasFollowUp: session.HasFollowUp,
            DurationMinutes: durationMinutes,
            StartedAt: session.StartedAt,
            ExpiresAt: session.ExpiresAt,
            UsedQuota: usedQuota,
            RemainingQuota: remainingQuota,
            MaxDailyQuota: maxDailyQuota,
            Questions: questionDtos,
            Answers: answeredDtos,
            ActiveFollowUp: activeFollowUp
        );

        return Result<ResumeMockExamResponse>.Success(response);
    }
}
