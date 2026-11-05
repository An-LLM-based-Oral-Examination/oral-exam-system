using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;

public sealed class BatchAssignAppealsCommandHandler(
    IApplicationDbContext context,
    IValidator<BatchAssignAppealsCommand> validator)
    : IRequestHandler<BatchAssignAppealsCommand, Result<BatchAssignAppealsResponseDto>>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IValidator<BatchAssignAppealsCommand> _validator = validator;

    public async Task<Result<BatchAssignAppealsResponseDto>> Handle(
        BatchAssignAppealsCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BatchAssignAppealsResponseDto>.Failure(validation.Errors[0].ErrorMessage);
        }

        var officer = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.OfficerId, cancellationToken);
        if (officer is null || !officer.IsActive || (officer.Role != UserRole.ExaminationOfficer && officer.Role != UserRole.Admin))
        {
            return Result<BatchAssignAppealsResponseDto>.Failure("Chỉ Cán bộ Khảo thí hoặc Quản trị viên đang hoạt động mới được phân công hàng loạt.");
        }

        var appealIds = request.Assignments.Select(a => a.AppealId).Distinct().ToList();
        var lecturerIds = request.Assignments.Select(a => a.LecturerId).Distinct().ToList();

        var appeals = await _context.AppealRequests
            .Include(a => a.Student)
            .Include(a => a.Ticket)
                .ThenInclude(t => t.Shift)
                    .ThenInclude(s => s.Session)
            .Where(a => appealIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        if (appeals.Count != appealIds.Count)
        {
            return Result<BatchAssignAppealsResponseDto>.Failure("Một số đơn phúc khảo không tồn tại trong hệ thống.");
        }

        var lecturers = await _context.Users.AsNoTracking()
            .Where(u => lecturerIds.Contains(u.Id) && u.Role == UserRole.Lecturer && u.IsActive)
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        if (lecturers.Count != lecturerIds.Count)
        {
            return Result<BatchAssignAppealsResponseDto>.Failure("Một số giảng viên được phân công không tồn tại, không hoạt động hoặc không có vai trò giảng viên.");
        }

        var assignedDtos = new List<AppealResponseDto>();

        foreach (var assignment in request.Assignments)
        {
            var appeal = appeals.First(a => a.Id == assignment.AppealId);
            var lecturer = lecturers[assignment.LecturerId];

            if (appeal.Status != AppealStatus.Pending)
            {
                return Result<BatchAssignAppealsResponseDto>.Failure($"Đơn phúc khảo {appeal.Id} không ở trạng thái PENDING.");
            }

            // Ràng buộc độc lập: không gán cho GV trùng shift.ExaminerUserId
            if (appeal.Ticket?.Shift?.ExaminerUserId.HasValue == true &&
                appeal.Ticket.Shift.ExaminerUserId.Value == lecturer.Id)
            {
                return Result<BatchAssignAppealsResponseDto>.Failure(
                    $"Giảng viên {lecturer.FullName} đã là Giám khảo chấm ban đầu của bài thi {appeal.Id}, không thể phân công chấm lại.");
            }

            appeal.AssignedTo = lecturer.Id;
            appeal.ReviewedBy = officer.Id;
            appeal.Status = AppealStatus.InReview;
            appeal.UpdatedAt = DateTime.UtcNow;

            assignedDtos.Add(new AppealResponseDto
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
                ReviewedBy = officer.Id,
                ReviewedByName = officer.FullName,
                OriginalScore = appeal.OriginalScore,
                CreatedAt = appeal.CreatedAt,
                UpdatedAt = appeal.UpdatedAt
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<BatchAssignAppealsResponseDto>.Success(new BatchAssignAppealsResponseDto
        {
            TotalAssigned = assignedDtos.Count,
            AssignedAppeals = assignedDtos
        });
    }
}
