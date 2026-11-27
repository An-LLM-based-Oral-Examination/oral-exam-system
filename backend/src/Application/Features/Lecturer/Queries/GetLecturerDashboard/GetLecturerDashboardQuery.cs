using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank;
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

        if (!user.IsActive || user.Role is not (UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<LecturerDashboardResponseDto>.Failure("FORBIDDEN: Không có quyền truy cập bảng điều khiển giảng viên.");
        }

        var isDeptHead = user.Role == UserRole.DepartmentHead;
        var allowedCourseIds = await QuestionCourseAccess
            .AllowedCourseIds(_context, user.Role, user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (request.CourseId.HasValue &&
            request.CourseId.Value != Guid.Empty &&
            !allowedCourseIds.Contains(request.CourseId.Value))
        {
            return Result<LecturerDashboardResponseDto>.Failure(
                "FORBIDDEN: Bạn không phụ trách hoặc chưa được phân công môn học này.");
        }

        var examQuestionsQuery = _context.ExamQuestions
            .AsNoTracking()
            .Where(q => !q.IsDeleted && !q.IsPaperCopy && q.Course != null && q.Course.IsActive && allowedCourseIds.Contains(q.CourseId));

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            examQuestionsQuery = examQuestionsQuery.Where(q => q.CourseId == request.CourseId.Value);
        }

        var practiceQuestionsQuery = _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => !q.IsDeleted && q.Course != null && q.Course.IsActive && allowedCourseIds.Contains(q.CourseId));

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            practiceQuestionsQuery = practiceQuestionsQuery.Where(q => q.CourseId == request.CourseId.Value);
        }

        if (!isDeptHead)
        {
            var sampleQuestionIds = allowedCourseIds.Count == 0
                ? new List<Guid>()
                : await _context.ExamSetQuestions
                    .AsNoTracking()
                    .Where(esq =>
                        allowedCourseIds.Contains(esq.ExamSet.CourseId) &&
                        esq.ExamSet.IsSampleSet &&
                        esq.ExamSet.IsActive &&
                        esq.ExamSet.Status != ExamSetStatus.Rejected)
                    .Select(esq => esq.ExamQuestionId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            // Giảng viên chỉ thấy câu hỏi do chính mình soạn hoặc câu hỏi đã duyệt vào ngân hàng (không thuộc Đề Mẫu active)
            examQuestionsQuery = examQuestionsQuery.Where(q =>
                q.SubmittedBy == user.Id ||
                (q.ApprovalStatus == ApprovalStatus.Approved &&
                 q.IsActive &&
                 (q.BankCurationStatus == BankCurationStatus.BankReady ||
                  q.BankCurationStatus == BankCurationStatus.CuratedIntoBank) &&
                 !sampleQuestionIds.Contains(q.Id)));

            practiceQuestionsQuery = practiceQuestionsQuery.Where(q =>
                q.SubmittedBy == user.Id ||
                (q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive));
        }

        var statusCounts = await examQuestionsQuery
            .GroupBy(q => q.ApprovalStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusCountMap = statusCounts
            .Where(x => x.Status != null)
            .ToDictionary(x => x.Status, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var totalExamQuestionsCount = statusCounts.Sum(x => x.Count);
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

        var revisionQuery = isDeptHead
            ? examQuestionsQuery.Where(q =>
                q.ApprovalStatus == ApprovalStatus.SubmittedForReview ||
                q.ApprovalStatus == ApprovalStatus.NeedsRevision)
            : examQuestionsQuery.Where(q =>
                q.SubmittedBy == user.Id &&
                q.ApprovalStatus == ApprovalStatus.NeedsRevision);

        var examRevisions = await revisionQuery
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

        var practiceRevisionFilter = isDeptHead
            ? practiceQuestionsQuery.Where(q =>
                q.ApprovalStatus == ApprovalStatus.SubmittedForReview ||
                q.ApprovalStatus == ApprovalStatus.NeedsRevision)
            : practiceQuestionsQuery.Where(q =>
                q.SubmittedBy == user.Id &&
                q.ApprovalStatus == ApprovalStatus.NeedsRevision);

        var practiceRevisions = await practiceRevisionFilter
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

        var examRevisionIds = examRevisions.Select(r => r.QuestionId).ToHashSet();
        var questionsNeedingRevision = examRevisions
            .Concat(practiceRevisions.Where(r => !examRevisionIds.Contains(r.QuestionId)))
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.QuestionId)
            .Take(20)
            .ToList();

        // Ca thi cần hậu kiểm — khóa phạm vi theo ExaminerUserId (GV) hoặc allowedCourseIds (TBM)
        var shiftsBaseQuery = _context.RealExamSessionShifts
            .AsNoTracking()
            .Where(s => s.Session != null && s.Session.Course != null && s.Session.Course.IsActive);

        shiftsBaseQuery = isDeptHead
            ? shiftsBaseQuery.Where(s => allowedCourseIds.Contains(s.Session.CourseId))
            : shiftsBaseQuery.Where(s => s.ExaminerUserId == user.Id);

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

        // Đơn phúc khảo — khóa phạm vi theo AssignedTo (GV) hoặc allowedCourseIds (TBM)
        var appealsBaseQuery = _context.AppealRequests
            .AsNoTracking()
            .Where(a => isDeptHead
                ? ((a.Session != null && allowedCourseIds.Contains(a.Session.CourseId)) ||
                   (a.Ticket != null && a.Ticket.Shift != null && a.Ticket.Shift.Session != null &&
                    allowedCourseIds.Contains(a.Ticket.Shift.Session.CourseId)))
                : a.AssignedTo == user.Id);

        if (request.CourseId.HasValue && request.CourseId.Value != Guid.Empty)
        {
            var filterCourseId = request.CourseId.Value;
            appealsBaseQuery = appealsBaseQuery.Where(a =>
                (a.Session != null && a.Session.CourseId == filterCourseId) ||
                (a.Ticket != null && a.Ticket.Shift != null && a.Ticket.Shift.Session != null &&
                 a.Ticket.Shift.Session.CourseId == filterCourseId));
        }

        var appeals = await appealsBaseQuery
            .Include(a => a.Student)
            .Include(a => a.Session)
            .Where(a => isDeptHead
                ? (a.Status == AppealStatus.SubmittedToExamOffice ||
                   a.Status == AppealStatus.AssignedToLecturer ||
                   a.Status == AppealStatus.RegradedByLecturer)
                : a.Status == AppealStatus.AssignedToLecturer)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var assignedPendingCount = await appealsBaseQuery
            .CountAsync(a => a.AssignedTo == user.Id && a.Status == AppealStatus.AssignedToLecturer, cancellationToken);

        var departmentPendingCount = isDeptHead
            ? await appealsBaseQuery.CountAsync(a => a.Status == AppealStatus.SubmittedToExamOffice, cancellationToken)
            : 0;

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
