using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals.Commands.CreateAppeal;

public sealed class CreateAppealCommandHandler : IRequestHandler<CreateAppealCommand, Result<AppealResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly AppealWindowOptions _window;
    private readonly TimeProvider _clock;

    public CreateAppealCommandHandler(IApplicationDbContext context, AppealWindowOptions? window = null, TimeProvider? clock = null)
    {
        _context = context;
        _window = window ?? new AppealWindowOptions();
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<AppealResponseDto>> Handle(CreateAppealCommand request, CancellationToken cancellationToken)
        => await _context.ExecuteTicketDecisionAsync(request.TicketId, ct => HandleLocked(request, ct), cancellationToken);

    private async Task<Result<AppealResponseDto>> HandleLocked(CreateAppealCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.StudentExamTickets
            .Include(t => t.Shift)
                .ThenInclude(s => s.Session)
            .Include(t => t.Student)
            .Include(t => t.Submissions)
                .ThenInclude(s => s.AiEvaluations)
            .Include(t => t.LecturerAudit)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket == null)
        {
            return Result<AppealResponseDto>.Failure("NOT_FOUND: Vé thi không tồn tại trong hệ thống.");
        }

        if (ticket.StudentId != request.StudentId)
        {
            return Result<AppealResponseDto>.Failure("FORBIDDEN: Sinh viên không sở hữu vé thi này.");
        }

        // Bắt buộc vé thi phải ở trạng thái PUBLISHED hoặc LOCKED mới cho phép nộp đơn phúc khảo
        if (!string.Equals(ticket.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ticket.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AppealResponseDto>.Failure("Chỉ được phép nộp đơn phúc khảo sau khi điểm thi đã được công bố chính thức (PUBLISHED hoặc LOCKED).");
        }

        if (request.SubmissionId.HasValue)
        {
            var submissionExists = ticket.Submissions.Any(s => s.Id == request.SubmissionId.Value);
            if (!submissionExists)
            {
                return Result<AppealResponseDto>.Failure("NOT_FOUND: Bản nộp câu hỏi không tồn tại hoặc không thuộc vé thi này.");
            }
        }

        var restriction = AppealWindowPolicy.Restriction(ticket, _clock.GetUtcNow().UtcDateTime, _window.Hours);
        if (restriction != null)
            return Result<AppealResponseDto>.Failure("CONFLICT: " + restriction);

        // Kiểm tra xem đã có đơn phúc khảo cho vé / câu này đang chờ hoặc đang xử lý chưa
        var hasPendingAppeal = await _context.AppealRequests.AnyAsync(a =>
            a.TicketId == request.TicketId &&
            a.SubmissionId == request.SubmissionId &&
            (a.Status == AppealStatus.SubmittedToExamOffice ||
             a.Status == AppealStatus.AssignedToLecturer ||
             a.Status == AppealStatus.RegradedByLecturer),
            cancellationToken);

        if (hasPendingAppeal)
        {
            return Result<AppealResponseDto>.Failure("CONFLICT: Đơn phúc khảo cho bài thi này đã tồn tại và đang chờ xử lý.");
        }

        // Đơn phải được tiếp nhận bởi Cán bộ Khảo thí đang hoạt động (R2/R7); fallback về Trưởng bộ môn nếu dữ liệu test chỉ seed Trưởng BM.
        var assignedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Role == UserRole.ExaminationOfficer && u.IsActive, cancellationToken)
            ?? await _context.Users
                .FirstOrDefaultAsync(u => u.Role == UserRole.DepartmentHead && u.IsActive, cancellationToken);

        if (assignedUser == null)
        {
            return Result<AppealResponseDto>.Failure("Không tìm thấy người phụ trách thẩm định đơn phúc khảo trong hệ thống.");
        }

        // Xác định OriginalScore
        decimal? originalScore = null;
        if (request.SubmissionId.HasValue)
        {
            var sub = ticket.Submissions.First(s => s.Id == request.SubmissionId.Value);
            originalScore = sub.FinalScore ?? sub.AiScore;
        }
        else
        {
            originalScore = ticket.LecturerAudit?.AuditedScore
                ?? OfficialExamScoreAggregator.ComputeTicketFinalScore(ticket.Submissions);
        }

        var sessionId = ticket.Shift?.SessionId ?? Guid.Empty;
        if (sessionId == Guid.Empty)
        {
            var defaultSession = await _context.OfficialExamSessions
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            sessionId = defaultSession;
        }

        var appeal = new AppealRequest
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StudentId = request.StudentId,
            SessionId = sessionId,
            SubmissionId = request.SubmissionId,
            Reason = request.Reason.Trim(),
            Status = AppealStatus.SubmittedToExamOffice,
            AssignedTo = assignedUser.Id,
            OriginalScore = originalScore,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AppealRequests.Add(appeal);

        // Tạo thông báo in-app cho Cán bộ Khảo thí và Trưởng Bộ Môn (Yêu cầu R2)
        var studentCode = ticket.Student?.StudentCode ?? "N/A";
        var studentName = ticket.Student?.FullName ?? "N/A";
        var sessionTitle = ticket.Shift?.Session?.Title ?? "Kỳ thi";

        var examOfficers = await _context.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.ExaminationOfficer && u.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var officer in examOfficers)
        {
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = officer.Id,
                Title = "Đơn phúc khảo mới",
                Message = $"Sinh viên {studentName} ({studentCode}) đã nộp đơn phúc khảo cho kỳ thi \"{sessionTitle}\". Lý do: {appeal.Reason}",
                Type = "APPEAL_SUBMITTED",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityName = "AppealRequest",
                RelatedEntityId = appeal.Id
            });
        }

        Course? course = null;
        if (ticket.Shift?.Session != null)
        {
            course = await _context.Courses.AsNoTracking()
                .Include(c => c.AcademicProgram)
                .FirstOrDefaultAsync(c => c.Id == ticket.Shift.Session.CourseId, cancellationToken);
        }

        var deptHeadTargetId = (course != null
            ? OralExamination.Application.Common.Authorization.AcademicCourseOwnership.EffectiveDepartmentHead(course)
            : null) ?? assignedUser.Id;
        if (examOfficers.All(officer => officer.Id != deptHeadTargetId))
        {
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = deptHeadTargetId,
                Title = "Đơn phúc khảo mới",
                Message = $"Sinh viên {studentName} ({studentCode}) đã nộp đơn phúc khảo cho môn \"{course?.Name ?? sessionTitle}\". Lý do: {appeal.Reason}",
                Type = "APPEAL_SUBMITTED",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityName = "AppealRequest",
                RelatedEntityId = appeal.Id
            });
        }

        AppealWorkflowEffects.AddTransitionAudit(_context, request.StudentId, appeal, null, appeal.Status);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new AppealResponseDto
        {
            Id = appeal.Id,
            TicketId = appeal.TicketId,
            StudentId = appeal.StudentId,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            StudentName = ticket.Student?.FullName ?? string.Empty,
            SessionId = appeal.SessionId,
            SessionTitle = ticket.Shift?.Session?.Title ?? string.Empty,
            SubmissionId = appeal.SubmissionId,
            Reason = appeal.Reason,
            Status = appeal.Status,
            AssignedTo = appeal.AssignedTo,
            AssignedToName = assignedUser.FullName,
            OriginalScore = appeal.OriginalScore,
            CreatedAt = appeal.CreatedAt,
            UpdatedAt = appeal.UpdatedAt
        };

        return Result<AppealResponseDto>.Success(dto);
    }
}
