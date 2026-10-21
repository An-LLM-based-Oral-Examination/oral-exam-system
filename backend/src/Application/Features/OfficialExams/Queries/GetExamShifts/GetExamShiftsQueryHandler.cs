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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = query.Where(sh => sh.Status.ToLower() == normalizedStatus);
        }

        var shifts = await query
            .OrderByDescending(sh => sh.StartTime)
            .Select(sh => new ExamShiftResponse(
                sh.Id,
                sh.SessionId,
                sh.ShiftName,
                sh.RoomLab,
                sh.StartTime,
                sh.EndTime,
                sh.ProctorUserId,
                sh.Status))
            .ToListAsync(cancellationToken);

        return Result<List<ExamShiftResponse>>.Success(shifts);
    }
}
