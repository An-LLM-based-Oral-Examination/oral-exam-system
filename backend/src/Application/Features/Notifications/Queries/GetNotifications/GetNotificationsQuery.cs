using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Notifications.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    Guid UserId,
    bool? UnreadOnly = null,
    int Limit = 50,
    int Page = 1,
    int PageSize = 0) : IRequest<Result<NotificationsSummaryResponseDto>>;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<NotificationsSummaryResponseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<NotificationsSummaryResponseDto>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<NotificationsSummaryResponseDto>.Failure("NOT_FOUND: Không tìm thấy thông tin người dùng.");
        }

        var readLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == user.Id && a.Action == "NOTIFICATION_READ" && a.EntityId.HasValue)
            .Select(a => new { EntityId = a.EntityId!.Value, a.CreatedAt })
            .ToListAsync(cancellationToken);

        var readLookup = readLogs
            .GroupBy(x => x.EntityId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CreatedAt));

        var markAllReadLog = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == user.Id && a.Action == "NOTIFICATION_READ_ALL")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var notifications = new List<NotificationItemDto>();

        void AddNotification(string type, string title, string message, string entityName, Guid entityId, DateTime createdAt)
        {
            var deterministicId = NotificationIdHelper.CreateDeterministicId(user.Id, type, entityId);
            DateTime? readAt = null;

            if (readLookup.TryGetValue(deterministicId, out var r1))
            {
                readAt = r1;
            }
            else if (readLookup.TryGetValue(entityId, out var r2))
            {
                readAt = r2;
            }
            else if (markAllReadLog.HasValue && createdAt <= markAllReadLog.Value)
            {
                readAt = markAllReadLog.Value;
            }

            var metadata = new Dictionary<string, string?>
            {
                ["entityName"] = entityName,
                ["entityId"] = entityId.ToString()
            };

            if (string.Equals(entityName, "student_exam_tickets", StringComparison.OrdinalIgnoreCase))
            {
                metadata["ticketId"] = entityId.ToString();
            }
            else if (string.Equals(entityName, "appeal_requests", StringComparison.OrdinalIgnoreCase))
            {
                metadata["appealId"] = entityId.ToString();
            }
            else if (string.Equals(entityName, "exam_questions", StringComparison.OrdinalIgnoreCase))
            {
                metadata["questionId"] = entityId.ToString();
            }

            notifications.Add(new NotificationItemDto
            {
                Id = deterministicId,
                UserId = user.Id,
                Type = type,
                Title = title,
                Message = message,
                RelatedEntityName = entityName,
                RelatedEntityId = entityId,
                Metadata = metadata,
                IsRead = readAt.HasValue,
                ReadAt = readAt,
                CreatedAt = createdAt
            });
        }

        // 1. Vé thi của sinh viên (Lịch thi & Công bố điểm)
        var studentTickets = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Shift)
            .Where(t => t.StudentId == user.Id)
            .OrderByDescending(t => t.CheckedInAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        foreach (var ticket in studentTickets)
        {
            var shiftName = ticket.Shift?.ShiftName ?? "Ca thi chính thức";
            var roomLab = ticket.Shift?.RoomLab ?? "Phòng Lab";

            AddNotification(
                "EXAM_SCHEDULE_PUBLISHED",
                "Lịch thi vấn đáp chính thức",
                $"Bạn đã được phân ghế số {ticket.SeatNumber} tại {roomLab} ({shiftName}).",
                "student_exam_tickets",
                ticket.Id,
                ticket.Shift?.CreatedAt ?? ticket.CheckedInAt);

            if (ticket.IsLocked || ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked)
            {
                AddNotification(
                    "EXAM_GRADE_PUBLISHED",
                    "Đã công bố điểm thi vấn đáp",
                    $"Kết quả điểm thi của bạn tại {shiftName} ({roomLab}) đã được công bố chính thức.",
                    "student_exam_tickets",
                    ticket.Id,
                    ticket.SubmittedAt ?? ticket.CheckedInAt);
            }
        }

        // 2. Đơn phúc khảo của sinh viên
        var studentAppeals = await _context.AppealRequests
            .AsNoTracking()
            .Where(a => a.StudentId == user.Id)
            .OrderByDescending(a => a.UpdatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var appeal in studentAppeals)
        {
            if (appeal.Status is AppealStatus.Approved or AppealStatus.Rejected)
            {
                AddNotification(
                    "APPEAL_RESOLVED",
                    "Kết quả giải quyết đơn phúc khảo",
                    $"Đơn phúc khảo của bạn đã được xử lý với quyết định: {appeal.Status}.",
                    "appeal_requests",
                    appeal.Id,
                    appeal.ResolvedAt ?? appeal.UpdatedAt);
            }
        }

        // 3. Thông báo dành cho Giảng viên (Phân công chấm phúc khảo & Duyệt câu hỏi)
        if (user.Role is UserRole.Lecturer or UserRole.DepartmentHead or UserRole.Admin)
        {
            var assignedAppeals = await _context.AppealRequests
                .AsNoTracking()
                .Where(a => a.AssignedTo == user.Id)
                .OrderByDescending(a => a.UpdatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var appeal in assignedAppeals)
            {
                AddNotification(
                    "APPEAL_LECTURER_ASSIGNED",
                    "Phân công chấm lại đơn phúc khảo",
                    $"Bạn được phân công chấm lại đơn phúc khảo #{appeal.Id.ToString()[..8]} (Trạng thái: {appeal.Status}).",
                    "appeal_requests",
                    appeal.Id,
                    appeal.UpdatedAt);
            }

            var reviewedQuestions = await _context.ExamQuestions
                .AsNoTracking()
                .Where(q => q.SubmittedBy == user.Id &&
                            (q.ApprovalStatus == ApprovalStatus.Approved ||
                             q.ApprovalStatus == ApprovalStatus.NeedsRevision ||
                             q.ApprovalStatus == ApprovalStatus.Rejected))
                .OrderByDescending(q => q.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var question in reviewedQuestions)
            {
                AddNotification(
                    $"QUESTION_{question.ApprovalStatus}",
                    "Kết quả phê duyệt câu hỏi ngân hàng đề",
                    $"Câu hỏi '{question.Title}' đã được duyệt với trạng thái {question.ApprovalStatus}.",
                    "exam_questions",
                    question.Id,
                    question.CreatedAt);
            }
        }

        // 4. Thông báo dành cho Trưởng bộ môn / Admin (Đề chờ duyệt & Đơn phúc khảo mới)
        if (user.Role is UserRole.DepartmentHead or UserRole.Admin)
        {
            var pendingQuestions = await _context.ExamQuestions
                .AsNoTracking()
                .Where(q => q.ApprovalStatus == ApprovalStatus.SubmittedForReview)
                .OrderByDescending(q => q.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var question in pendingQuestions)
            {
                AddNotification(
                    "QUESTION_SUBMITTED_FOR_REVIEW",
                    "Câu hỏi mới chờ Trưởng bộ môn duyệt",
                    $"Câu hỏi '{question.Title}' đang chờ phê duyệt vào ngân hàng đề.",
                    "exam_questions",
                    question.Id,
                    question.CreatedAt);
            }

            var pendingAppeals = await _context.AppealRequests
                .AsNoTracking()
                .Where(a => a.Status == AppealStatus.Pending)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var appeal in pendingAppeals)
            {
                AddNotification(
                    "APPEAL_SUBMITTED",
                    "Đơn phúc khảo mới chờ phân công",
                    $"Đơn phúc khảo #{appeal.Id.ToString()[..8]} đang chờ Trưởng bộ môn phân công giảng viên chấm lại.",
                    "appeal_requests",
                    appeal.Id,
                    appeal.CreatedAt);
            }
        }

        var ordered = notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        var unreadCount = ordered.Count(n => !n.IsRead);
        var filtered = request.UnreadOnly == true
            ? ordered.Where(n => !n.IsRead).ToList()
            : ordered;

        List<NotificationItemDto> pagedItems;
        int page;
        int pageSize;

        if (request.PageSize > 0)
        {
            page = Math.Max(1, request.Page);
            pageSize = Math.Clamp(request.PageSize, 1, 50);
            pagedItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            page = 1;
            pageSize = Math.Clamp(request.Limit, 1, 100);
            pagedItems = filtered.Take(pageSize).ToList();
        }

        return Result<NotificationsSummaryResponseDto>.Success(new NotificationsSummaryResponseDto
        {
            Items = pagedItems,
            UnreadCount = unreadCount,
            TotalCount = ordered.Count,
            Page = page,
            PageSize = pageSize
        });
    }
}
