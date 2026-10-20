using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Lecturer.Queries.GetLecturerDashboard;

public sealed record LecturerQuestionBankStatsDto
{
    public int TotalExamQuestions { get; init; }
    public int TotalPracticeQuestions { get; init; }
    public int DraftCount { get; init; }
    public int SubmittedForReviewCount { get; init; }
    public int ApprovedCount { get; init; }
    public int NeedsRevisionCount { get; init; }
    public int RejectedCount { get; init; }
    public decimal ApprovalRatePercent { get; init; }
}

public sealed record QuestionRevisionItemDto
{
    public Guid QuestionId { get; init; }
    public Guid CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string BloomLevel { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? ReviewNotes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record ShiftAuditSummaryDto
{
    public Guid ShiftId { get; init; }
    public Guid SessionId { get; init; }
    public string SessionTitle { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string ShiftName { get; init; } = string.Empty;
    public string RoomLab { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int TotalTickets { get; init; }
    public int ScoredTickets { get; init; }
    public int SuspiciousCount { get; init; }
    public bool IsPublished { get; init; }
}

public sealed record LecturerAppealQueueItemDto
{
    public Guid AppealId { get; init; }
    public Guid TicketId { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string SessionTitle { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal? OriginalScore { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record LecturerDashboardResponseDto
{
    public Guid LecturerId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public LecturerQuestionBankStatsDto QuestionStats { get; init; } = new();
    public IReadOnlyList<QuestionRevisionItemDto> QuestionsNeedingRevision { get; init; } = Array.Empty<QuestionRevisionItemDto>();
    public IReadOnlyList<ShiftAuditSummaryDto> ShiftsPendingAudit { get; init; } = Array.Empty<ShiftAuditSummaryDto>();
    public int TotalSuspiciousSubmissions { get; init; }
    public int AssignedAppealsPendingCount { get; init; }
    public int DepartmentPendingAppealsCount { get; init; }
    public IReadOnlyList<LecturerAppealQueueItemDto> ActiveAppeals { get; init; } = Array.Empty<LecturerAppealQueueItemDto>();
}

public sealed record GetLecturerDashboardQuery(
    Guid LecturerUserId,
    Guid? CourseId = null) : IRequest<Result<LecturerDashboardResponseDto>>;

public sealed class GetLecturerDashboardQueryHandler
    : IRequestHandler<GetLecturerDashboardQuery, Result<LecturerDashboardResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLecturerDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<LecturerDashboardResponseDto>> Handle(
        GetLecturerDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerUserId, cancellationToken);

        if (user is null)
        {
            return Result<LecturerDashboardResponseDto>.Failure("NOT_FOUND: Không tìm thấy thông tin giảng viên.");
        }

        var isDeptHeadOrAdmin = user.Role is UserRole.DepartmentHead or UserRole.Admin;

        var examQuestionsQuery = _context.ExamQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .AsQueryable();

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            examQuestionsQuery = examQuestionsQuery.Where(q => q.CourseId == request.CourseId.Value);
        }
        else if (!isDeptHeadOrAdmin)
        {
            // Giảng viên xem câu hỏi do mình tạo hoặc toàn bộ kho nếu SubmittedBy == null (seed data)
            examQuestionsQuery = examQuestionsQuery.Where(q => q.SubmittedBy == user.Id || q.SubmittedBy == null);
        }

        var examQuestions = await examQuestionsQuery.ToListAsync(cancellationToken);

        var practiceQuestionsQuery = _context.PracticeQuestions.AsNoTracking().AsQueryable();
        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            practiceQuestionsQuery = practiceQuestionsQuery.Where(q => q.CourseId == request.CourseId.Value);
        }

        var totalPracticeCount = await practiceQuestionsQuery.CountAsync(cancellationToken);

        var draftCount = examQuestions.Count(q => q.ApprovalStatus == ApprovalStatus.Draft);
        var submittedCount = examQuestions.Count(q => q.ApprovalStatus == ApprovalStatus.SubmittedForReview);
        var approvedCount = examQuestions.Count(q => q.ApprovalStatus == ApprovalStatus.Approved);
        var needsRevisionCount = examQuestions.Count(q => q.ApprovalStatus == ApprovalStatus.NeedsRevision);
        var rejectedCount = examQuestions.Count(q => q.ApprovalStatus == ApprovalStatus.Rejected);

        var reviewedTotal = approvedCount + needsRevisionCount + rejectedCount;
        var approvalRate = reviewedTotal > 0
            ? Math.Round(approvedCount * 100.0m / reviewedTotal, 2, MidpointRounding.AwayFromZero)
            : 0.0m;

        var questionsNeedingRevision = examQuestions
            .Where(q => isDeptHeadOrAdmin
                ? q.ApprovalStatus is ApprovalStatus.SubmittedForReview or ApprovalStatus.NeedsRevision
                : q.ApprovalStatus == ApprovalStatus.NeedsRevision)
            .OrderByDescending(q => q.CreatedAt)
            .Take(20)
            .Select(q => new QuestionRevisionItemDto
            {
                QuestionId = q.Id,
                CourseId = q.CourseId,
                CourseCode = q.Course?.Code ?? string.Empty,
                Title = q.Title,
                Difficulty = q.Difficulty,
                BloomLevel = q.BloomLevel,
                ApprovalStatus = q.ApprovalStatus,
                ReviewNotes = q.ReviewNotes,
                CreatedAt = q.CreatedAt
            })
            .ToList();

        // Ca thi cần hậu kiểm
        var shiftsQuery = _context.RealExamSessionShifts
            .AsNoTracking()
            .Include(s => s.Session)
                .ThenInclude(sess => sess.Course)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.LecturerAudit)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Submissions)
                    .ThenInclude(sub => sub.AiEvaluations)
            .AsQueryable();

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            shiftsQuery = shiftsQuery.Where(s => s.Session.CourseId == request.CourseId.Value);
        }

        var shifts = await shiftsQuery
            .OrderByDescending(s => s.StartTime)
            .Take(20)
            .ToListAsync(cancellationToken);

        var shiftSummaries = new List<ShiftAuditSummaryDto>();
        int totalSuspicious = 0;

        foreach (var shift in shifts)
        {
            var tickets = shift.StudentExamTickets.ToList();
            var isPublished = tickets.Count > 0 && tickets.All(t => t.IsLocked || t.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked);
            var scoredTickets = tickets.Count(t =>
                t.LecturerAudit != null ||
                t.Submissions.Any(sub => sub.FinalScore.HasValue || sub.AiScore.HasValue));

            var suspiciousInShift = tickets.Count(t =>
                t.Submissions.Any(sub => sub.IsSuspicious || sub.GradingStatus == "suspicious") ||
                t.Submissions.SelectMany(sub => sub.AiEvaluations).Any(e => e.ConfidenceScore.HasValue && e.ConfidenceScore.Value < 0.70m));

            totalSuspicious += suspiciousInShift;

            shiftSummaries.Add(new ShiftAuditSummaryDto
            {
                ShiftId = shift.Id,
                SessionId = shift.SessionId,
                SessionTitle = shift.Session?.Title ?? string.Empty,
                CourseCode = shift.Session?.Course?.Code ?? string.Empty,
                ShiftName = shift.ShiftName,
                RoomLab = shift.RoomLab,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                TotalTickets = tickets.Count,
                ScoredTickets = scoredTickets,
                SuspiciousCount = suspiciousInShift,
                IsPublished = isPublished
            });
        }

        // Đơn phúc khảo
        var appeals = await _context.AppealRequests
            .AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Session)
            .Where(a => isDeptHeadOrAdmin
                ? (a.Status == AppealStatus.Pending || a.Status == AppealStatus.InReview)
                : (a.AssignedTo == user.Id && a.Status == AppealStatus.InReview))
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var assignedPendingCount = await _context.AppealRequests
            .AsNoTracking()
            .CountAsync(a => a.AssignedTo == user.Id && a.Status == AppealStatus.InReview, cancellationToken);

        var departmentPendingCount = await _context.AppealRequests
            .AsNoTracking()
            .CountAsync(a => a.Status == AppealStatus.Pending, cancellationToken);

        var activeAppealItems = appeals
            .Select(a => new LecturerAppealQueueItemDto
            {
                AppealId = a.Id,
                TicketId = a.TicketId,
                StudentCode = a.Student?.StudentCode ?? string.Empty,
                StudentName = a.Student?.FullName ?? string.Empty,
                SessionTitle = a.Session?.Title ?? string.Empty,
                Status = a.Status,
                OriginalScore = a.OriginalScore,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return Result<LecturerDashboardResponseDto>.Success(new LecturerDashboardResponseDto
        {
            LecturerId = user.Id,
            FullName = user.FullName,
            Role = user.Role,
            QuestionStats = new LecturerQuestionBankStatsDto
            {
                TotalExamQuestions = examQuestions.Count,
                TotalPracticeQuestions = totalPracticeCount,
                DraftCount = draftCount,
                SubmittedForReviewCount = submittedCount,
                ApprovedCount = approvedCount,
                NeedsRevisionCount = needsRevisionCount,
                RejectedCount = rejectedCount,
                ApprovalRatePercent = approvalRate
            },
            QuestionsNeedingRevision = questionsNeedingRevision,
            ShiftsPendingAudit = shiftSummaries,
            TotalSuspiciousSubmissions = totalSuspicious,
            AssignedAppealsPendingCount = assignedPendingCount,
            DepartmentPendingAppealsCount = departmentPendingCount,
            ActiveAppeals = activeAppealItems
        });
    }
}
