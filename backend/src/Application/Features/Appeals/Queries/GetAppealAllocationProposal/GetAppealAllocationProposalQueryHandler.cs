using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;

public sealed class GetAppealAllocationProposalQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAppealAllocationProposalQuery, Result<AppealAllocationProposalDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<AppealAllocationProposalDto>> Handle(
        GetAppealAllocationProposalQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await AppealAuthorization.ActiveClaimedActorAsync(
            _context, request.ActorUserId, request.ActorRole, cancellationToken);
        if (actor is null || actor.Role != UserRole.ExaminationOfficer)
        {
            return Result<AppealAllocationProposalDto>.Failure(AppealAuthorization.Forbidden);
        }

        var appealQuery = _context.AppealRequests.AsNoTracking()
            .Include(a => a.Session)
            .Include(a => a.Student)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.Session)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.ExaminerUser)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.LecturerAudit)
            .Where(a => a.Status == AppealStatus.SubmittedToExamOffice);

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            appealQuery = appealQuery.Where(a => a.SessionId == request.SessionId.Value);
        }

        var pendingAppeals = await appealQuery
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var courseIds = pendingAppeals
            .Select(a => a.Ticket?.Shift?.Session?.CourseId ?? a.Session?.CourseId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (courseIds.Count == 0 && request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            var sessionCourseId = await _context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => s.Id == request.SessionId.Value)
                .Select(s => (Guid?)s.CourseId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sessionCourseId.HasValue && sessionCourseId.Value != Guid.Empty)
            {
                courseIds.Add(sessionCourseId.Value);
            }
        }

        var eligibleLecturerIdsByCourse = new Dictionary<Guid, HashSet<Guid>>();
        var allEligibleLecturerIds = new HashSet<Guid>();

        if (courseIds.Count > 0)
        {
            var courseLecturerPairs = await _context.CourseLecturers
                .AsNoTracking()
                .Where(cl =>
                    courseIds.Contains(cl.CourseId)
                    && cl.IsActive
                    && (cl.Course == null || cl.Course.IsActive)
                    && (cl.CourseOffering == null || cl.CourseOffering.IsEnabled))
                .Select(cl => new { cl.CourseId, LecturerId = cl.LecturerUserId })
                .ToListAsync(cancellationToken);

            var classLecturerPairs = await _context.Classes
                .AsNoTracking()
                .Where(c =>
                    courseIds.Contains(c.CourseId)
                    && c.LecturerId.HasValue
                    && c.LecturerId.Value != Guid.Empty
                    && c.IsActive
                    && (c.Course == null || c.Course.IsActive)
                    && (c.CourseOffering == null || c.CourseOffering.IsEnabled))
                .Select(c => new { c.CourseId, LecturerId = c.LecturerId!.Value })
                .ToListAsync(cancellationToken);

            foreach (var pair in courseLecturerPairs.Concat(classLecturerPairs))
            {
                if (!eligibleLecturerIdsByCourse.TryGetValue(pair.CourseId, out var set))
                {
                    set = new HashSet<Guid>();
                    eligibleLecturerIdsByCourse[pair.CourseId] = set;
                }

                set.Add(pair.LecturerId);
                allEligibleLecturerIds.Add(pair.LecturerId);
            }
        }

        var lecturers = allEligibleLecturerIds.Count == 0
            ? new List<Domain.Entities.User>()
            : await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Lecturer && u.IsActive && allEligibleLecturerIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync(cancellationToken);

        if (pendingAppeals.Count > 0 && lecturers.Count == 0)
        {
            return Result<AppealAllocationProposalDto>.Failure(
                "Không tìm thấy giảng viên nào đang hoạt động được phân công môn học của đơn phúc khảo để phân công.");
        }

        int totalAppeals = pendingAppeals.Count;
        int totalLecturers = lecturers.Count;
        var lecturerIds = lecturers.Select(lecturer => lecturer.Id).ToList();
        var activeLoads = lecturerIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _context.AppealRequests.AsNoTracking()
                .Where(a => lecturerIds.Contains(a.AssignedTo) &&
                    (a.Status == AppealStatus.AssignedToLecturer || a.Status == AppealStatus.RegradedByLecturer))
                .GroupBy(a => a.AssignedTo)
                .Select(group => new { LecturerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.LecturerId, item => item.Count, cancellationToken);
        int globalRemainingQuota = lecturers.Sum(lecturer =>
            Math.Max(0, AppealWorkflowEffects.MaxActiveAppealsPerLecturer - activeLoads.GetValueOrDefault(lecturer.Id)));

        // Định mức phân bổ cân bằng:
        // Nếu tổng bài > N_GV * 24: chia đều số bài cho các GV
        // Nếu tổng bài <= N_GV * 24: gán tối đa 24 bài/GV và chia đều cân bằng (targetCap)
        var lecturerAssignments = lecturers.ToDictionary(l => l.Id, _ => new List<Guid>());

        // Thuật toán Greedy Min-Load kết hợp Ràng buộc Độc lập (Không gán bài cho GV trùng shift.ExaminerUserId / LecturerAudit và đúng môn học)
        var appealCandidates = new List<(Domain.Entities.AppealRequest Appeal, List<Domain.Entities.User> Candidates)>();

        foreach (var appeal in pendingAppeals)
        {
            var initialExaminerId = appeal.Ticket?.Shift?.ExaminerUserId;
            var initialAuditorId = appeal.Ticket?.LecturerAudit?.LecturerId;
            var appealCourseId = appeal.Ticket?.Shift?.Session?.CourseId ?? appeal.Session?.CourseId;
            var courseEligibleLecturers = appealCourseId.HasValue && appealCourseId.Value != Guid.Empty
                ? eligibleLecturerIdsByCourse.GetValueOrDefault(appealCourseId.Value)
                : null;

            var candidates = lecturers
                .Where(l =>
                    courseEligibleLecturers != null && courseEligibleLecturers.Contains(l.Id) &&
                    (!initialExaminerId.HasValue || l.Id != initialExaminerId.Value) &&
                    (!initialAuditorId.HasValue || l.Id != initialAuditorId.Value))
                .ToList();

            appealCandidates.Add((appeal, candidates));
        }

        // Ưu tiên bài có ít ứng viên hợp lệ nhất trước
        var sortedAppeals = appealCandidates
            .OrderBy(x => x.Candidates.Count)
            .ThenBy(x => x.Appeal.CreatedAt)
            .ToList();

        var proposalItems = new List<AppealAssignmentProposalItemDto>();
        bool hasUnassignedDueToQuota = false;

        foreach (var (appeal, candidates) in sortedAppeals)
        {
            Domain.Entities.User? chosenLecturer = null;

            if (candidates.Count > 0)
            {
                var underCapCandidates = candidates
                    .Where(c => activeLoads.GetValueOrDefault(c.Id) + lecturerAssignments[c.Id].Count <
                        AppealWorkflowEffects.MaxActiveAppealsPerLecturer)
                    .OrderBy(c => activeLoads.GetValueOrDefault(c.Id) + lecturerAssignments[c.Id].Count)
                    .ThenBy(c => c.FullName)
                    .ToList();

                if (underCapCandidates.Count > 0)
                {
                    chosenLecturer = underCapCandidates[0];
                }
                else
                {
                    hasUnassignedDueToQuota = true;
                }
            }

            if (chosenLecturer != null)
            {
                lecturerAssignments[chosenLecturer.Id].Add(appeal.Id);
            }

            proposalItems.Add(new AppealAssignmentProposalItemDto
            {
                AppealId = appeal.Id,
                TicketId = appeal.TicketId,
                StudentCode = appeal.Student?.StudentCode ?? string.Empty,
                StudentName = appeal.Student?.FullName ?? string.Empty,
                SessionId = appeal.SessionId,
                SessionTitle = appeal.Ticket?.Shift?.Session?.Title ?? string.Empty,
                InitialExaminerUserId = appeal.Ticket?.Shift?.ExaminerUserId,
                InitialExaminerName = appeal.Ticket?.Shift?.ExaminerUser?.FullName,
                ProposedLecturerId = chosenLecturer?.Id,
                ProposedLecturerName = chosenLecturer?.FullName,
                OriginalScore = appeal.OriginalScore,
                Reason = appeal.Reason
            });
        }

        proposalItems = proposalItems.OrderBy(p => p.StudentCode).ThenBy(p => p.AppealId).ToList();

        var summaries = lecturers.Select(l =>
        {
            var existingActiveCount = activeLoads.GetValueOrDefault(l.Id);
            return new LecturerAllocationSummaryDto
            {
                LecturerId = l.Id,
                LecturerName = l.FullName,
                Email = l.Email,
                AssignedCount = lecturerAssignments[l.Id].Count,
                ExistingActiveCount = existingActiveCount,
                RemainingQuota = Math.Max(0, AppealWorkflowEffects.MaxActiveAppealsPerLecturer - existingActiveCount)
            };
        }).ToList();

        var exceedsStandardQuota = hasUnassignedDueToQuota || totalAppeals > globalRemainingQuota;

        var result = new AppealAllocationProposalDto
        {
            TotalAppeals = totalAppeals,
            TotalLecturers = totalLecturers,
            ExceedsStandardQuota = exceedsStandardQuota,
            LecturerSummaries = summaries,
            Items = proposalItems
        };

        return Result<AppealAllocationProposalDto>.Success(result);
    }
}
