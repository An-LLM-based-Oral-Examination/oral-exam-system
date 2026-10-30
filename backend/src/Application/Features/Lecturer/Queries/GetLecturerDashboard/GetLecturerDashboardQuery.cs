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

        var statusCounts = await examQuestionsQuery
            .GroupBy(q => q.ApprovalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusCountMap = statusCounts
            .Where(x => x.Status != null)
            .ToDictionary(x => x.Status, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var totalExamQuestionsCount = statusCounts.Sum(x => x.Count);

        var practiceQuestionsQuery = _context.PracticeQuestions.AsNoTracking().AsQueryable();
        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            practiceQuestionsQuery = practiceQuestionsQuery.Where(q => q.CourseId == request.CourseId.Value);
        }

        var totalPracticeCount = await practiceQuestionsQuery.CountAsync(cancellationToken);

        var draftCount = statusCountMap.GetValueOrDefault(ApprovalStatus.Draft);
        var submittedCount = statusCountMap.GetValueOrDefault(ApprovalStatus.SubmittedForReview);
        var approvedCount = statusCountMap.GetValueOrDefault(ApprovalStatus.Approved);
        var needsRevisionCount = statusCountMap.GetValueOrDefault(ApprovalStatus.NeedsRevision);
        var rejectedCount = statusCountMap.GetValueOrDefault(ApprovalStatus.Rejected);

        var reviewedTotal = approvedCount + needsRevisionCount + rejectedCount;
        var approvalRate = reviewedTotal > 0
            ? Math.Round(approvedCount * 100.0m / reviewedTotal, 2, MidpointRounding.AwayFromZero)
            : 0.0m;

        var revisionQuery = isDeptHeadOrAdmin
            ? examQuestionsQuery.Where(q =>
                q.ApprovalStatus == ApprovalStatus.SubmittedForReview ||
                q.ApprovalStatus == ApprovalStatus.NeedsRevision)
            : examQuestionsQuery.Where(q => q.ApprovalStatus == ApprovalStatus.NeedsRevision);

        var questionsNeedingRevision = await revisionQuery
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id)
            .Take(20)
            .Select(q => new QuestionRevisionItemDto
            {
                QuestionId = q.Id,
                CourseId = q.CourseId,
                CourseCode = q.Course != null ? q.Course.Code : string.Empty,
                Title = q.Title,
                Difficulty = q.Difficulty,
                BloomLevel = q.BloomLevel,
                ApprovalStatus = q.ApprovalStatus,
                ReviewNotes = q.ReviewNotes,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Ca thi cần hậu kiểm — dùng Projection nhẹ thay vì Include 4 tầng kéo TranscriptWhisper/CotTrace/TimestampsWhisper
        var shiftsBaseQuery = _context.RealExamSessionShifts
            .AsNoTracking()
            .AsQueryable();

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            shiftsBaseQuery = shiftsBaseQuery.Where(s => s.Session.CourseId == request.CourseId.Value);
        }

        var shifts = await shiftsBaseQuery
            .OrderByDescending(s => s.StartTime)
            .ThenByDescending(s => s.Id)
            .Take(20)
            .Select(s => new
            {
                s.Id,
                s.SessionId,
                SessionTitle = s.Session != null ? s.Session.Title : string.Empty,
                CourseCode = s.Session != null && s.Session.Course != null ? s.Session.Course.Code : string.Empty,
                s.ShiftName,
                s.RoomLab,
                s.StartTime,
                s.EndTime,
                Tickets = s.StudentExamTickets.Select(t => new
                {
                    t.IsLocked,
                    t.Status,
                    HasAudit = t.LecturerAudit != null,
                    HasScoredSubmission = t.Submissions.Any(sub => sub.FinalScore.HasValue || sub.AiScore.HasValue),
                    IsSuspiciousTicket = t.Submissions.Any(sub => sub.IsSuspicious) ||
                                         t.Submissions.SelectMany(sub => sub.AiEvaluations).Any(e => e.ConfidenceScore.HasValue && e.ConfidenceScore.Value < 0.70m)
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var shiftSummaries = new List<ShiftAuditSummaryDto>(shifts.Count);
        int totalSuspicious = 0;

        foreach (var shift in shifts)
        {
            var tickets = shift.Tickets;
            var isPublished = tickets.Count > 0 && tickets.All(t => t.IsLocked || t.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked);
            var scoredTickets = tickets.Count(t => t.HasAudit || t.HasScoredSubmission);
            var suspiciousInShift = tickets.Count(t => t.IsSuspiciousTicket);

            totalSuspicious += suspiciousInShift;

            shiftSummaries.Add(new ShiftAuditSummaryDto
            {
                ShiftId = shift.Id,
                SessionId = shift.SessionId,
                SessionTitle = shift.SessionTitle ?? string.Empty,
                CourseCode = shift.CourseCode ?? string.Empty,
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
            .ThenByDescending(a => a.Id)
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
                TotalExamQuestions = totalExamQuestionsCount,
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
