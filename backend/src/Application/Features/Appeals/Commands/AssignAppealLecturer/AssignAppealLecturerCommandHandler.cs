using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;

public sealed class AssignAppealLecturerCommandHandler
    : IRequestHandler<AssignAppealLecturerCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public AssignAppealLecturerCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<AppealResponseDto>> Handle(
        AssignAppealLecturerCommand request,
        CancellationToken cancellationToken) =>
        await _context.ExecuteAppealAssignmentAsync([request.AppealId], [request.LecturerId],
            ct => HandleSerialized(request, ct), cancellationToken);

    private async Task<Result<AppealResponseDto>> HandleSerialized(
        AssignAppealLecturerCommand request,
        CancellationToken cancellationToken)
    {
        var assigner = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.AssignerId, cancellationToken);
        if (assigner is null || !assigner.IsActive || assigner.Role != UserRole.ExaminationOfficer)
        {
            return Result<AppealResponseDto>.Failure(AppealAuthorization.Forbidden);
        }

        var lecturer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);
        if (lecturer is null || !lecturer.IsActive || lecturer.Role != UserRole.Lecturer)
        {
            return Result<AppealResponseDto>.Failure("Giảng viên được phân công không tồn tại, không hoạt động hoặc không đúng vai trò.");
        }

        var appeal = await _context.AppealRequests
            .Include(a => a.Ticket).ThenInclude(t => t.Shift).ThenInclude(s => s.Session)
            .Include(a => a.Ticket).ThenInclude(t => t.LecturerAudit)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);
        if (appeal is null)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo không tồn tại trong hệ thống.");
        }

        if (appeal.Status != AppealStatus.SubmittedToExamOffice)
        {
            return Result<AppealResponseDto>.Failure("Chỉ đơn phúc khảo đang ở trạng thái PENDING mới được phân công.");
        }

        // Ràng buộc độc lập: Giảng viên chấm lại không được trùng Giám khảo ca thi (shift.ExaminerUserId)
        // hoặc Giảng viên đã trực tiếp hậu kiểm/ghi đè điểm bài thi (ticket.LecturerAudit.LecturerId).
        if ((appeal.Ticket?.Shift?.ExaminerUserId.HasValue == true &&
             appeal.Ticket.Shift.ExaminerUserId.Value == lecturer.Id) ||
            (appeal.Ticket?.LecturerAudit != null &&
             appeal.Ticket.LecturerAudit.LecturerId == lecturer.Id))
        {
            return Result<AppealResponseDto>.Failure("Giảng viên được phân công chấm lại không được trùng với Giám khảo hoặc Giảng viên hậu kiểm ban đầu của bài thi này.");
        }

        var activeWorkload = await _context.AppealRequests.CountAsync(a =>
            a.AssignedTo == lecturer.Id &&
            (a.Status == AppealStatus.AssignedToLecturer || a.Status == AppealStatus.RegradedByLecturer),
            cancellationToken);
        if (activeWorkload >= AppealWorkflowEffects.MaxActiveAppealsPerLecturer)
        {
            return Result<AppealResponseDto>.Failure("CONFLICT: Giảng viên đã đạt định mức tối đa 24 đơn phúc khảo đang xử lý.");
        }

        var previousStatus = appeal.Status;
        appeal.AssignedTo = lecturer.Id;
        appeal.ReviewedBy = assigner.Id;
        appeal.Status = AppealStatus.AssignedToLecturer;
        appeal.UpdatedAt = DateTime.UtcNow;
        AppealWorkflowEffects.AddTransitionAudit(_context, assigner.Id, appeal, previousStatus, appeal.Status);
        await AppealWorkflowEffects.AddNotificationIfMissingAsync(_context, lecturer.Id, appeal,
            "APPEAL_ASSIGNED", "Phân công chấm phúc khảo",
            "Bạn được phân công chấm lại độc lập một đơn phúc khảo.", cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<AppealResponseDto>.Success(new AppealResponseDto
        {
            Id = appeal.Id,
            TicketId = appeal.TicketId,
            StudentId = appeal.StudentId,
            StudentCode = appeal.Student?.StudentCode ?? string.Empty,
            StudentName = appeal.Student?.FullName ?? string.Empty,
            SessionId = appeal.SessionId,
            SessionTitle = appeal.Ticket?.Shift?.Session?.Title ?? string.Empty,
            SubmissionId = appeal.SubmissionId,
            Reason = appeal.Reason,
            Status = appeal.Status,
            AssignedTo = lecturer.Id,
            AssignedToName = lecturer.FullName,
            ReviewedBy = assigner.Id,
            ReviewedByName = assigner.FullName,
            OriginalScore = appeal.OriginalScore,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt
        });
    }
}
