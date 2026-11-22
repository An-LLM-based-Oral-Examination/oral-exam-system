using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;

/// <summary>
/// Handler truy vấn danh sách ca thi phòng Lab (RealExamSessionShift).
/// </summary>
public sealed class GetExamShiftsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamShiftsQuery, Result<List<ExamShiftResponse>>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<ExamShiftResponse>>> Handle(
        GetExamShiftsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.RealExamSessionShifts.AsNoTracking().AsQueryable();

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.SessionId == request.SessionId.Value);
        }

        if (request.ProctorUserId.HasValue && request.ProctorUserId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.ProctorUserId == request.ProctorUserId.Value);
        }

        if (request.ExaminerUserId.HasValue && request.ExaminerUserId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.ExaminerUserId == request.ExaminerUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = query.Where(sh => sh.Status.ToLower() == normalizedStatus);
        }

        var rawShifts = await query
            .OrderByDescending(sh => sh.StartTime)
            .Select(sh => new
            {
                sh.Id,
                sh.SessionId,
                sh.ShiftName,
                sh.RoomLab,
                sh.StartTime,
                sh.EndTime,
                sh.ProctorUserId,
                sh.Status,
                sh.ExaminerUserId,
                ExaminerName = sh.ExaminerUser != null ? sh.ExaminerUser.FullName : null,
                ProctorName = sh.ProctorUser != null ? sh.ProctorUser.FullName : null,
                sh.MaxSeats,
                sh.IsExamPublished,
                IsGradesPublished = sh.StudentExamTickets.Any(t => t.Status == ExamTicketStatus.Published),
                GradesPublishedAt = sh.StudentExamTickets.Where(t => t.GradesPublishedAt != null).Max(t => t.GradesPublishedAt),
                sh.CertificateHash,
                AssignedTicketsCount = sh.StudentExamTickets.Count()
            })
            .ToListAsync(cancellationToken);

        var sessionIds = rawShifts.Select(s => s.SessionId).Distinct().ToList();
        var attemptTypeBySession = sessionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ExamAttemptType })
                .ToListAsync(cancellationToken))
                .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.ExamAttemptType) ? OfficialExamAttemptType.FinalExam : s.ExamAttemptType);

        var shifts = rawShifts.Select(sh =>
        {
            var attemptType = attemptTypeBySession.GetValueOrDefault(sh.SessionId, OfficialExamAttemptType.FinalExam);
            var remainingSeats = Math.Max(0, sh.MaxSeats - sh.AssignedTicketsCount);

            return new ExamShiftResponse(
                sh.Id,
                sh.SessionId,
                sh.ShiftName,
                sh.RoomLab,
                sh.StartTime,
                sh.EndTime,
                sh.ProctorUserId,
                sh.Status,
                sh.ExaminerUserId,
                sh.ExaminerName,
                sh.ProctorName,
                sh.MaxSeats,
                sh.IsExamPublished,
                sh.IsGradesPublished,
                sh.GradesPublishedAt,
                sh.CertificateHash,
                attemptType,
                sh.AssignedTicketsCount,
                remainingSeats);
        }).ToList();

        return Result<List<ExamShiftResponse>>.Success(shifts);
    }
}
