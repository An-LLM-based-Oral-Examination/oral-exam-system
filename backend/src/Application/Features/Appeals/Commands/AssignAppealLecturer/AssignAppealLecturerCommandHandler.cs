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
        CancellationToken cancellationToken)
    {
        var departmentHead = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadId, cancellationToken);
        if (departmentHead is null || !departmentHead.IsActive || departmentHead.Role != UserRole.DepartmentHead)
        {
            return Result<AppealResponseDto>.Failure("Chỉ Trưởng bộ môn đang hoạt động mới được phân công giảng viên chấm phúc khảo.");
        }

        var lecturer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.LecturerId, cancellationToken);
        if (lecturer is null || !lecturer.IsActive || lecturer.Role != UserRole.Lecturer)
        {
            return Result<AppealResponseDto>.Failure("Giảng viên được phân công không tồn tại, không hoạt động hoặc không đúng vai trò.");
        }

        var appeal = await _context.AppealRequests
            .Include(a => a.Ticket).ThenInclude(t => t.Shift).ThenInclude(s => s.Session)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);
        if (appeal is null)
        {
            return Result<AppealResponseDto>.Failure("Đơn phúc khảo không tồn tại trong hệ thống.");
        }

        if (appeal.Status != AppealStatus.Pending)
        {
            return Result<AppealResponseDto>.Failure("Chỉ đơn phúc khảo đang ở trạng thái PENDING mới được phân công.");
        }

        appeal.AssignedTo = lecturer.Id;
        appeal.ReviewedBy = departmentHead.Id;
        appeal.Status = AppealStatus.InReview;
        appeal.UpdatedAt = DateTime.UtcNow;
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
            ReviewedBy = departmentHead.Id,
            ReviewedByName = departmentHead.FullName,
            OriginalScore = appeal.OriginalScore,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt
        });
    }
}
