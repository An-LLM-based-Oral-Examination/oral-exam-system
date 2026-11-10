using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Domain.Entities;
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
public sealed class GetStudentDashboardQueryHandler(
    IApplicationDbContext context,
    ILogger<GetStudentDashboardQueryHandler>? logger = null)
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

        // 1. Thống kê Luyện tập (MF-01) — Tự động đóng các phiên quá hạn không tương tác (Batch SaveChanges 1 lần)
        var timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(context, cancellationToken);
        var activePracticeSessions = await context.PracticeSessions
            .Include(ps => ps.Answers)
            .Where(ps => ps.StudentId == request.StudentId && ps.Status == "in_progress")
            .ToListAsync(cancellationToken);

        bool anyTimedOutPracticeClosed = false;
        foreach (var activeSession in activePracticeSessions)
        {
            var closed = await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(
                context,
                activeSession,
                timeoutMinutes,
                cancellationToken,
                saveChanges: false);
            if (closed)
            {
                anyTimedOutPracticeClosed = true;
            }
        }

        if (anyTimedOutPracticeClosed)
        {
            await context.SaveChangesAsync(cancellationToken);
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
            var answers = session.Answers ?? new List<PracticeAnswer>();
            var resolvedScore = session.TotalScore ?? PracticeSessionTimeoutHelper.ComputeSessionTotalScore(session, answers);
            if (resolvedScore.HasValue)
            {
                practiceSessionScores.Add(resolvedScore.Value);
            }
        }

        decimal? averagePracticeScore = practiceSessionScores.Count > 0
            ? Math.Round(practiceSessionScores.Average(), 2, MidpointRounding.AwayFromZero)
            : null;

        // 2. Thống kê Thi thử (MF-02) — Tự động đóng các ca thi thử đã hết giờ
        var nowUtc = DateTime.UtcNow;
        try
        {
            var expiredMockSessions = await context.MockExamSessions
                .Include(ms => ms.Answers)
                    .ThenInclude(a => a.AiEvaluations)
                        .ThenInclude(e => e.Details)
                            .ThenInclude(d => d.Criterion)
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
                        decimal sumMainScores = 0.0m;
                        foreach (var mainAns in answeredMains)
                        {
                            var mainEval = mainAns.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                            var followUpEvals = allAnswered
                                .Where(f => f.IsFollowUp &&
                                            ((f.ParentAnswerId.HasValue && f.ParentAnswerId == mainAns.Id) ||
                                             (!f.ParentAnswerId.HasValue && f.QuestionId == mainAns.QuestionId)))
                                .Select(f => f.AiEvaluations!.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault())
                                .Where(e => e != null)
                                .Cast<AiEvaluation>()
                                .ToList();

                            var effectiveQuestionScore = FollowUpPromptBuilder.ComputeEffectiveQuestionScore(mainEval, followUpEvals);
                            if (mainEval != null && followUpEvals.Count > 0)
                            {
                                mainEval.TotalScore = effectiveQuestionScore;
                            }

                            sumMainScores += effectiveQuestionScore;
                        }

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
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Lỗi xảy ra trong quá trình tự động đóng phiên thi thử hết hạn của sinh viên {StudentId}. Tiếp tục xử lý dashboard.", request.StudentId);
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

        // 4. Phân tích độ thông thạo theo Tiêu chí Rubric & Bloom Level (Tối ưu O(1) qua ix_ai_evaluations_student + tương thích ngược)
        List<StudentCriterionMasteryDto> criterionMastery;
        try
        {
            var evalDetails = await context.AiEvaluationDetails
                .AsNoTracking()
                .Include(d => d.Criterion)
                .Include(d => d.Evaluation)
                .Where(d =>
                    d.Evaluation.StudentId == request.StudentId ||
                    (d.Evaluation.StudentId == null && (
                        (d.Evaluation.PracticeAnswer != null && d.Evaluation.PracticeAnswer.StudentId == request.StudentId) ||
                        (d.Evaluation.MockExamAnswer != null && d.Evaluation.MockExamAnswer.Session.StudentId == request.StudentId))))
                .ToListAsync(cancellationToken);

            criterionMastery = evalDetails
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
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không thể truy vấn AiEvaluationDetails cho sinh viên {StudentId} do lỗi cấu trúc CSDL hoặc dữ liệu. Tạm thời trả về danh sách tiêu chí rỗng.", request.StudentId);
            criterionMastery = new List<StudentCriterionMasteryDto>();
        }

        // 5. Danh sách ca thi thật phòng Lab & Điểm công bố (MF-04)
        List<StudentExamTicket> examTickets;
        try
        {
            examTickets = await context.StudentExamTickets
                .AsNoTracking()
                .Include(t => t.Shift)
                    .ThenInclude(s => s.Session)
                        .ThenInclude(ss => ss.Course)
                .Include(t => t.LecturerAudit)
                .Include(t => t.Submissions)
                .Where(t => t.StudentId == request.StudentId)
                .OrderByDescending(t => t.Shift != null ? t.Shift.StartTime : t.CheckedInAt)
                .ThenByDescending(t => t.Id)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Không thể truy vấn StudentExamTickets cho sinh viên {StudentId}. Tạm thời trả về danh sách vé thi rỗng.", request.StudentId);
            examTickets = new List<StudentExamTicket>();
        }

        var officialExamDtos = examTickets.Select(t =>
        {
            bool isGradeVisible = t.IsLocked
                || string.Equals(t.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

            decimal? visibleScore = null;
            if (isGradeVisible)
            {
                visibleScore = t.LecturerAudit?.AuditedScore
                    ?? (t.Submissions != null && t.Submissions.Count > 0 && t.Submissions.Any(s => s.FinalScore.HasValue || s.AiScore.HasValue)
                        ? (decimal?)Math.Round(
                            t.Submissions.Sum(s => s.FinalScore ?? s.AiScore ?? 0.0m) / t.Submissions.Count,
                            2,
                            MidpointRounding.AwayFromZero)
                        : null);
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
