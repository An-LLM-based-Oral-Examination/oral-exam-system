using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppealEvidence;

public sealed class GetAppealEvidenceQueryHandler : IRequestHandler<GetAppealEvidenceQuery, Result<AuditEvidenceItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService? _storageService;

    public GetAppealEvidenceQueryHandler(IApplicationDbContext context, IStorageService? storageService = null)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<AuditEvidenceItemDto>> Handle(GetAppealEvidenceQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<AuditEvidenceItemDto>.Failure(
                "FORBIDDEN: Không xác định được danh tính người truy xuất bằng chứng phúc khảo.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.Lecturer or UserRole.ExaminationOfficer))
        {
            return Result<AuditEvidenceItemDto>.Failure(
                "FORBIDDEN: Chỉ giảng viên được phân công chấm lại hoặc Cán bộ Khảo thí mới có quyền xem bằng chứng phúc khảo.");
        }

        var appeal = await _context.AppealRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);

        if (appeal is null)
        {
            return Result<AuditEvidenceItemDto>.Failure("NOT_FOUND: Đơn phúc khảo không tồn tại.");
        }

        if (actor.Role == UserRole.Lecturer)
        {
            var isAssignedToActor = appeal.AssignedTo == actor.Id;
            var isAllowedStatus = appeal.Status is AppealStatus.AssignedToLecturer or AppealStatus.RegradedByLecturer;
            if (!isAssignedToActor || !isAllowedStatus)
            {
                return Result<AuditEvidenceItemDto>.Failure(
                    "FORBIDDEN: Bạn không phải giảng viên được phân công chấm lại đơn phúc khảo này.");
            }
        }

        var ticket = await _context.StudentExamTickets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Student)
            .Include(t => t.LecturerAudit)
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
            .FirstOrDefaultAsync(t => t.Id == appeal.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<AuditEvidenceItemDto>.Failure("NOT_FOUND: Không tìm thấy vé thi gắn với đơn phúc khảo.");
        }

        var questionIds = ticket.Submissions
            .Select(s => s.ExamQuestionId)
            .Distinct()
            .ToList();

        var questionMap = questionIds.Count > 0
            ? await _context.ExamQuestions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, cancellationToken)
            : new Dictionary<Guid, ExamQuestion>();

        var item = await AuditEvidenceItemBuilder.BuildAsync(ticket, questionMap, _storageService, cancellationToken);
        return Result<AuditEvidenceItemDto>.Success(item);
    }
}
