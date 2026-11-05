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
        var lecturers = await _context.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Lecturer && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        if (lecturers.Count == 0)
        {
            return Result<AppealAllocationProposalDto>.Failure("Không tìm thấy giảng viên nào đang hoạt động để phân công.");
        }

        var appealQuery = _context.AppealRequests.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.Session)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.ExaminerUser)
            .Where(a => a.Status == AppealStatus.Pending);

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            appealQuery = appealQuery.Where(a => a.SessionId == request.SessionId.Value);
        }

        var pendingAppeals = await appealQuery
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        int totalAppeals = pendingAppeals.Count;
        int totalLecturers = lecturers.Count;
        bool exceedsStandardQuota = totalAppeals > totalLecturers * 24;

        // Định mức phân bổ cân bằng:
        // Nếu tổng bài > N_GV * 24: chia đều số bài cho các GV
        // Nếu tổng bài <= N_GV * 24: gán tối đa 24 bài/GV và chia đều cân bằng (targetCap)
        int targetCap = totalLecturers > 0 
            ? (int)Math.Ceiling((double)totalAppeals / totalLecturers) 
            : 0;

        if (!exceedsStandardQuota)
        {
            targetCap = Math.Min(24, targetCap);
        }

        var lecturerAssignments = lecturers.ToDictionary(l => l.Id, _ => new List<Guid>());

        // Thuật toán Greedy Min-Load kết hợp Ràng buộc Độc lập (Không gán bài cho GV trùng shift.ExaminerUserId)
        var appealCandidates = new List<(Domain.Entities.AppealRequest Appeal, List<Domain.Entities.User> Candidates)>();

        foreach (var appeal in pendingAppeals)
        {
            var initialExaminerId = appeal.Ticket?.Shift?.ExaminerUserId;
            var candidates = lecturers
                .Where(l => !initialExaminerId.HasValue || l.Id != initialExaminerId.Value)
                .ToList();

            appealCandidates.Add((appeal, candidates));
        }

        // Ưu tiên bài có ít ứng viên hợp lệ nhất trước
        var sortedAppeals = appealCandidates
            .OrderBy(x => x.Candidates.Count)
            .ThenBy(x => x.Appeal.CreatedAt)
            .ToList();

        var proposalItems = new List<AppealAssignmentProposalItemDto>();

        foreach (var (appeal, candidates) in sortedAppeals)
        {
            Domain.Entities.User? chosenLecturer = null;

            if (candidates.Count > 0)
            {
                var underCapCandidates = candidates
                    .Where(c => lecturerAssignments[c.Id].Count < targetCap)
                    .OrderBy(c => lecturerAssignments[c.Id].Count)
                    .ThenBy(c => c.FullName)
                    .ToList();

                if (underCapCandidates.Count > 0)
                {
                    chosenLecturer = underCapCandidates[0];
                }
                else
                {
                    chosenLecturer = candidates
                        .OrderBy(c => lecturerAssignments[c.Id].Count)
                        .ThenBy(c => c.FullName)
                        .First();
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

        var summaries = lecturers.Select(l => new LecturerAllocationSummaryDto
        {
            LecturerId = l.Id,
            LecturerName = l.FullName,
            Email = l.Email,
            AssignedCount = lecturerAssignments[l.Id].Count
        }).ToList();

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
