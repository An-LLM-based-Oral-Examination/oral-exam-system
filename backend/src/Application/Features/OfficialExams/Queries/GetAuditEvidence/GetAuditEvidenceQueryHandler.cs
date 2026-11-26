using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;

/// <summary>
/// Handler xử lý trích xuất danh sách bằng chứng hậu kiểm (Evidence Panel) cho ca thi phòng Lab.
/// </summary>
public sealed class GetAuditEvidenceQueryHandler : IRequestHandler<GetAuditEvidenceQuery, Result<List<AuditEvidenceItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService? _storageService;

    public GetAuditEvidenceQueryHandler(IApplicationDbContext context, IStorageService? storageService = null)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<List<AuditEvidenceItemDto>>> Handle(GetAuditEvidenceQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<List<AuditEvidenceItemDto>>.Failure(
                "FORBIDDEN: Không xác định được danh tính người truy xuất bằng chứng hậu kiểm.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.ExaminationOfficer or UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<List<AuditEvidenceItemDto>>.Failure(
                "FORBIDDEN: Bạn không có quyền truy xuất bằng chứng hậu kiểm của ca thi này.");
        }

        var shift = await _context.RealExamSessionShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<List<AuditEvidenceItemDto>>.Failure("Ca thi không tồn tại trong hệ thống.");
        }

        if (!await OfficialExamAuthorization.CanAuditShiftAsync(_context, actor.Id, shift, cancellationToken))
        {
            return Result<List<AuditEvidenceItemDto>>.Failure(
                "FORBIDDEN: Bạn không phải Giám khảo được phân công ca thi hoặc Trưởng bộ môn phụ trách môn học này.");
        }

        var tickets = await _context.StudentExamTickets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Student)
            .Include(t => t.LecturerAudit)
                .ThenInclude(a => a!.Details)
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
                    .ThenInclude(e => e.Details)
                        .ThenInclude(d => d.Criterion)
            .Where(t => t.ShiftId == request.ShiftId)
            .OrderBy(t => t.SeatNumber)
            .ToListAsync(cancellationToken);

        var questionIds = tickets
            .SelectMany(t => t.Submissions)
            .Select(s => s.ExamQuestionId)
            .Distinct()
            .ToList();

        var questionMap = questionIds.Count > 0
            ? await _context.ExamQuestions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, cancellationToken)
            : new Dictionary<Guid, ExamQuestion>();

        var items = new List<AuditEvidenceItemDto>(tickets.Count);
        foreach (var ticket in tickets)
        {
            items.Add(await AuditEvidenceItemBuilder.BuildAsync(ticket, questionMap, _storageService, cancellationToken));
        }

        return Result<List<AuditEvidenceItemDto>>.Success(items);
    }
}
