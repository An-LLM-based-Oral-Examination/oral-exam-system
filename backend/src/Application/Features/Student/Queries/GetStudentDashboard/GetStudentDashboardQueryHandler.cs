using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
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

        // 1. Thống kê Luyện tập (MF-01)
        var practiceSessions = await context.PracticeSessions
            .AsNoTracking()
            .Include(ps => ps.Answers)
                .ThenInclude(a => a.AiEvaluations)
            .Where(ps => ps.StudentId == request.StudentId)
            .ToListAsync(cancellationToken);

        int totalPracticeSessions = practiceSessions.Count;
        int completedPracticeSessions = practiceSessions.Count(ps =>
            string.Equals(ps.Status, "completed", StringComparison.OrdinalIgnoreCase));

        var practiceSessionScores = new List<decimal>();
        foreach (var session in practiceSessions)
        {
            var mainScores = session.Answers
                .Where(a => !a.IsFollowUp && a.AiEvaluations.Any())
                .Select(a => a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).First().TotalScore)
                .ToList();

            if (mainScores.Count > 0)
            {
                practiceSessionScores.Add(mainScores.Average());
            }
        }

        decimal? averagePracticeScore = practiceSessionScores.Count > 0
            ? Math.Round(practiceSessionScores.Average(), 2, MidpointRounding.AwayFromZero)
            : null;

        // 2. Thống kê Thi thử (MF-02)
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
            .Where(ce => ce.StudentId == request.StudentId && ce.Class != null)
            .Select(ce => ce.Class.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeCoursesQuery = context.Courses.AsNoTracking().Where(c => c.IsActive);
        var courses = enrolledCourseIds.Count > 0
            ? await activeCoursesQuery.Where(c => enrolledCourseIds.Contains(c.Id)).OrderBy(c => c.Code).ToListAsync(cancellationToken)
            : await activeCoursesQuery.OrderBy(c => c.Code).ToListAsync(cancellationToken);

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayQuotas = await context.MockExamQuotas
            .AsNoTracking()
            .Where(q => q.StudentId == request.StudentId && (q.QuotaDate == todayVn || q.QuotaDate == todayUtc))
            .ToListAsync(cancellationToken);

        var courseQuotaDtos = courses.Select(course =>
        {
            var quotaRecord = todayQuotas.FirstOrDefault(q => q.CourseId == course.Id && q.QuotaDate == todayVn)
                ?? todayQuotas.FirstOrDefault(q => q.CourseId == course.Id && q.QuotaDate == todayUtc);
            var usedToday = quotaRecord?.UsedCount ?? 0;
            var maxPerDay = course.MaxMockExamsPerDay > 0 ? course.MaxMockExamsPerDay : 3;
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
                        ? (decimal?)Math.Round(t.Submissions.Where(s => s.FinalScore.HasValue).Average(s => s.FinalScore!.Value), 2, MidpointRounding.AwayFromZero)
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
