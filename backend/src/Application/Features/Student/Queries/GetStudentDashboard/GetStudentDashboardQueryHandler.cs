using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Student.Queries.GetStudentDashboard;

/// <summary>
/// Handler tổng hợp dữ liệu Student Dashboard (FE-10 / BE-V03):
/// - Số phiên Luyện tập (MF-01) & Điểm trung bình
/// - Số bài Thi thử (MF-02) & Điểm trung bình
/// - Hạn ngạch thi thử còn lại trong ngày theo từng môn học
/// - Mức độ nắm vững tiêu chí Rubric / Bloom Level
/// - Danh sách ca thi thật phòng Lab & kết quả điểm đã công bố (MF-04)
/// </summary>
public sealed class GetStudentDashboardQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetStudentDashboardQuery, Result<StudentDashboardDto>>
{
    public async Task<Result<StudentDashboardDto>> Handle(
        GetStudentDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (request.StudentId == Guid.Empty)
        {
            return Result<StudentDashboardDto>.Failure("Không xác định được định danh sinh viên.");
        }

        var student = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.StudentId, cancellationToken);

        if (student is null)
        {
            return Result<StudentDashboardDto>.Failure("Sinh viên không tồn tại trong hệ thống.");
        }

        // 1. Thống kê Luyện tập (MF-01) — Tự động đóng các phiên quá hạn không tương tác
        var timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(context, cancellationToken);
        var activePracticeSessions = await context.PracticeSessions
            .Include(ps => ps.Answers)
            .Where(ps => ps.StudentId == request.StudentId && ps.Status == "in_progress")
            .ToListAsync(cancellationToken);

        foreach (var activeSession in activePracticeSessions)
        {
            await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(
                context,
                activeSession,
                timeoutMinutes,
                cancellationToken);
        }

        var practiceSessions = await context.PracticeSessions
            .AsNoTracking()
            .Include(ps => ps.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .AsSplitQuery()
            .Where(ps => ps.StudentId == request.StudentId)
            .ToListAsync(cancellationToken);

        int totalPracticeSessions = practiceSessions.Count;
        int completedPracticeSessions = practiceSessions.Count(ps =>
            string.Equals(ps.Status, "completed", StringComparison.OrdinalIgnoreCase));

        var practiceSessionScores = new List<decimal>();
        foreach (var session in practiceSessions)
        {
            var answers = session.Answers ?? new List<Domain.Entities.PracticeAnswer>();
            var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);
            var isCompleted = string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase);

            var distinctMainCount = answers
                .Where(a => !a.IsFollowUp)
                .Select(a => a.QuestionId)
                .Distinct()
                .Count();
            var plannedCount = Math.Max(session.SelectedQuestionIds.Count, distinctMainCount);

            var mainScores = answers
                .Where(a => a.AiEvaluations != null && a.AiEvaluations.Any())
                .GroupBy(a => a.QuestionId)
                .Select(g =>
                {
                    var mainAnswerEval = g
                        .Where(a => !a.IsFollowUp)
                        .SelectMany(a => a.AiEvaluations)
                        .OrderByDescending(e => e.EvaluatedAt)
                        .FirstOrDefault();
                    var followUpScores = g
                        .Where(a => a.IsFollowUp)
                        .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                        .ToList();

                    if (followUpScores.Count == 0)
                    {
                        return mainAnswerEval?.TotalScore ?? 0.0m;
                    }

                    var mainScore = mainAnswerEval?.TotalScore ?? 0.0m;
                    var avgFollowUp = followUpScores.Average();
                    if (avgFollowUp >= mainScore)
                    {
                        return g.SelectMany(a => a.AiEvaluations).Max(e => e.TotalScore);
                    }

                    var allScores = new List<decimal> { mainScore };
                    allScores.AddRange(followUpScores);
                    return Math.Round(allScores.Average(), 2, MidpointRounding.AwayFromZero);
                })
                .ToList();

            bool hasPendingAnswers = answers.Any(a =>
                string.Equals(a.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Status, "grading", StringComparison.OrdinalIgnoreCase));

            if (isFullSession && isCompleted && plannedCount > 0)
            {
                if (!hasPendingAnswers)
                {
                    if (mainScores.Count > 0)
                    {
                        practiceSessionScores.Add(mainScores.Sum() / plannedCount);
                    }
                    else if (session.SelectedQuestionIds.Count > 0)
                    {
                        practiceSessionScores.Add(0.0m);
                    }
                }
            }
            else if (!hasPendingAnswers && mainScores.Count > 0)
            {
                practiceSessionScores.Add(mainScores.Average());
            }
        }

        decimal? averagePracticeScore = practiceSessionScores.Count > 0
            ? Math.Round(practiceSessionScores.Average(), 2, MidpointRounding.AwayFromZero)
            : null;

        // 2. Thống kê Thi thử (MF-02) — Tự động đóng các phiên thi thử đã hết giờ
        var nowUtc = DateTime.UtcNow;
        var expiredMockSessions = await context.MockExamSessions
            .Include(ms => ms.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .Where(ms => ms.StudentId == request.StudentId &&
                         ms.Status == "in_progress" &&
                         nowUtc > ms.ExpiresAt.AddSeconds(10))
            .ToListAsync(cancellationToken);

        if (expiredMockSessions.Count > 0)
        {
            foreach (var expired in expiredMockSessions)
            {
                expired.SubmittedAt ??= expired.ExpiresAt;
                var mainAnswers = expired.Answers.Where(a => !a.IsFollowUp).ToList();
                var answeredMains = mainAnswers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var allAnswered = expired.Answers.Where(a => !string.IsNullOrWhiteSpace(a.AnswerText)).ToList();
                var hasAnsweredFollowUps = allAnswered.Any(a => a.IsFollowUp);

                if (allAnswered.Count == 0)
                {
                    expired.Status = "graded";
                    expired.TotalScore ??= 0.0m;
                }
                else if (answeredMains.Count > 0 && allAnswered.All(a =>
                             a.AiEvaluations != null &&
                             a.AiEvaluations.Any() &&
                             (!hasAnsweredFollowUps || string.Equals(a.Status, "graded", StringComparison.OrdinalIgnoreCase))))
                {
                    int totalMainCount = Math.Max(1, mainAnswers.Count);
                    decimal sumMainScores = answeredMains.Sum(a =>
                        a.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).First().TotalScore);
                    expired.TotalScore = Math.Clamp(
                        Math.Round(sumMainScores / totalMainCount, 2, MidpointRounding.AwayFromZero),
                        0.0m,
                        10.0m);
                    foreach (var ans in allAnswered)
                    {
                        ans.Status = "graded";
                    }
                    expired.Status = "graded";
                }
                else
                {
                    expired.Status = "submitted";
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var mockSessions = await context.MockExamSessions
            .AsNoTracking()
            .Where(ms => ms.StudentId == request.StudentId)
            .ToListAsync(cancellationToken);

        int totalMockExams = mockSessions.Count;
        var scoredMockSessions = mockSessions
            .Where(ms => ms.TotalScore.HasValue)
            .ToList();

        int completedMockExams = mockSessions.Count(ms =>
            string.Equals(ms.Status, "graded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ms.Status, "submitted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ms.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
            ms.TotalScore.HasValue);

        decimal? averageMockExamScore = scoredMockSessions.Count > 0
            ? Math.Round(scoredMockSessions.Average(ms => ms.TotalScore!.Value), 2, MidpointRounding.AwayFromZero)
            : null;

        // 3. Hạn ngạch thi thử trong ngày (Daily Quota per Course)
        var enrolledCourseIds = await context.ClassEnrollments
            .AsNoTracking()
            .Include(ce => ce.Class)
            .Where(ce => ce.StudentId == request.StudentId &&
                         ce.Class != null &&
                         (ce.Status == null || ce.Status == "enrolled" || ce.Status == "active"))
            .Select(ce => ce.Class.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeCoursesQuery = context.Courses.AsNoTracking().Where(c => c.IsActive);
        var courses = enrolledCourseIds.Count > 0
            ? await activeCoursesQuery.Where(c => enrolledCourseIds.Contains(c.Id)).OrderBy(c => c.Code).ToListAsync(cancellationToken)
            : await activeCoursesQuery.OrderBy(c => c.Code).ToListAsync(cancellationToken);

        var sysQuotaStr = await context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "max_mock_exams_per_day" || c.Key == "MaxMockExamsPerDay")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var fallbackQuota = int.TryParse(sysQuotaStr, out var parsedSysQuota) && parsedSysQuota > 0 ? parsedSysQuota : 3;

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayQuotas = await context.MockExamQuotas
            .AsNoTracking()
            .Where(q => q.StudentId == request.StudentId && (q.QuotaDate == todayVn || q.QuotaDate == todayUtc))
            .ToListAsync(cancellationToken);

        var courseQuotaDtos = courses.Select(course =>
        {
            var quotaRecord = todayQuotas.FirstOrDefault(q => q.CourseId == course.Id && q.QuotaDate == todayVn)
                ?? todayQuotas.FirstOrDefault(q => q.CourseId == course.Id && q.QuotaDate == todayUtc && DateOnly.FromDateTime(q.UpdatedAt.AddHours(7)) == todayVn);
            var usedToday = quotaRecord?.UsedCount ?? 0;
            var maxPerDay = course.MaxMockExamsPerDay > 0 ? course.MaxMockExamsPerDay : fallbackQuota;
            var remaining = Math.Max(0, maxPerDay - usedToday);

            return new StudentCourseQuotaDto
            {
                CourseId = course.Id,
                CourseCode = course.Code,
                CourseName = course.Name,
                MaxMockExamsPerDay = maxPerDay,
                UsedMockExamsToday = usedToday,
                RemainingMockExamsToday = remaining
            };
        }).ToList();

        // 4. Phân tích độ thông thạo theo Tiêu chí Rubric & Bloom Level
        var evalDetails = await context.AiEvaluationDetails
            .AsNoTracking()
            .Include(d => d.Criterion)
            .Include(d => d.Evaluation)
                .ThenInclude(e => e.PracticeAnswer)
            .Include(d => d.Evaluation)
                .ThenInclude(e => e.MockExamAnswer)
                    .ThenInclude(ma => ma!.Session)
            .AsSplitQuery()
            .Where(d =>
                (d.Evaluation.PracticeAnswer != null && d.Evaluation.PracticeAnswer.StudentId == request.StudentId) ||
                (d.Evaluation.MockExamAnswer != null && d.Evaluation.MockExamAnswer.Session.StudentId == request.StudentId))
            .ToListAsync(cancellationToken);

        var criterionMastery = evalDetails
            .Where(d => d.Criterion != null && d.Criterion.MaxScore > 0)
            .GroupBy(d => new
            {
                d.Criterion.CriterionName,
                BloomLevel = string.IsNullOrWhiteSpace(d.Criterion.BloomLevel) ? "Understand" : d.Criterion.BloomLevel!
            })
            .Select(g =>
            {
                var avgRatio = g.Average(x => Math.Clamp(x.Score / x.Criterion.MaxScore, 0m, 1m));
                return new StudentCriterionMasteryDto
                {
                    CriterionName = g.Key.CriterionName,
                    BloomLevel = g.Key.BloomLevel,
                    EvaluatedCount = g.Count(),
                    AverageScorePercentage = Math.Round(avgRatio * 100m, 2, MidpointRounding.AwayFromZero)
                };
            })
            .OrderByDescending(m => m.EvaluatedCount)
            .ThenBy(m => m.CriterionName)
            .ToList();

        // 5. Danh sách ca thi thật phòng Lab & Điểm công bố (MF-04)
        var examTickets = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Shift)
                .ThenInclude(s => s.Session)
                    .ThenInclude(ss => ss.Course)
            .Include(t => t.LecturerAudit)
            .Include(t => t.Submissions)
            .Where(t => t.StudentId == request.StudentId)
            .OrderByDescending(t => t.Shift.StartTime)
            .ToListAsync(cancellationToken);

        var officialExamDtos = examTickets.Select(t =>
        {
            bool isGradeVisible = t.IsLocked
                || string.Equals(t.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

            decimal? visibleScore = null;
            if (isGradeVisible)
            {
                visibleScore = t.LecturerAudit?.AuditedScore
                    ?? (t.Submissions.Any(s => s.FinalScore.HasValue)
                        ? (decimal?)Math.Round(
                            t.Submissions
                                .Select(s => s.FinalScore ?? s.AiScore)
                                .Where(v => v.HasValue)
                                .Average(v => v!.Value),
                            2,
                            MidpointRounding.AwayFromZero)
                        : (t.Submissions.Any(s => s.AiScore.HasValue)
                            ? (decimal?)Math.Round(t.Submissions.Where(s => s.AiScore.HasValue).Average(s => s.AiScore!.Value), 2, MidpointRounding.AwayFromZero)
                            : null));
            }

            return new StudentOfficialExamSummaryDto
            {
                TicketId = t.Id,
                ShiftId = t.ShiftId,
                CourseCode = t.Shift?.Session?.Course?.Code ?? string.Empty,
                CourseName = t.Shift?.Session?.Course?.Name ?? string.Empty,
                SessionTitle = t.Shift?.Session?.Title ?? string.Empty,
                ShiftName = t.Shift?.ShiftName ?? string.Empty,
                RoomLab = t.Shift?.RoomLab ?? string.Empty,
                ExamDate = t.Shift?.Session?.ExamDate ?? (t.Shift != null ? DateOnly.FromDateTime(t.Shift.StartTime) : todayUtc),
                StartTime = t.Shift?.StartTime ?? t.CheckedInAt,
                EndTime = t.Shift?.EndTime ?? t.CheckedInAt,
                SeatNumber = t.SeatNumber,
                Status = t.Status,
                FinalScore = visibleScore,
                IsLocked = t.IsLocked,
                StudentAcknowledgementStatus = t.StudentAcknowledgementStatus ?? "PENDING"
            };
        }).ToList();

        return Result<StudentDashboardDto>.Success(new StudentDashboardDto
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode ?? string.Empty,
            FullName = student.FullName,
            Email = student.Email,
            TotalPracticeSessions = totalPracticeSessions,
            CompletedPracticeSessions = completedPracticeSessions,
            AveragePracticeScore = averagePracticeScore,
            TotalMockExams = totalMockExams,
            CompletedMockExams = completedMockExams,
            AverageMockExamScore = averageMockExamScore,
            CourseQuotas = courseQuotaDtos,
            CriterionMastery = criterionMastery,
            OfficialExamTickets = officialExamDtos
        });
    }
}

internal static class QueryableSplitExtensions
{
    public static IQueryable<TEntity> AsSplitQuery<TEntity>(this IQueryable<TEntity> source) where TEntity : class
    {
        try
        {
            var relationalExtType = Type.GetType("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions, Microsoft.EntityFrameworkCore.Relational");
            if (relationalExtType != null)
            {
                var method = relationalExtType.GetMethod("AsSplitQuery", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    var genericMethod = method.MakeGenericMethod(typeof(TEntity));
                    var result = genericMethod.Invoke(null, [source]);
                    if (result is IQueryable<TEntity> queryable)
                    {
                        return queryable;
                    }
                }
            }
        }
        catch
        {
            // Fallback gracefully
        }

        return source;
    }
}
