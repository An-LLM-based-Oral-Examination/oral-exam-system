using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Admin.Commands.CreateAdminNotification;

public sealed record CreateAdminNotificationCommand(
    Guid ActorAdminId,
    string TargetScope,
    string Title,
    string Message,
    string Type = "SYSTEM_ANNOUNCEMENT",
    string? TargetRole = null,
    Guid? TargetUserId = null) : IRequest<Result<DispatchAdminNotificationResultDto>>;

public sealed class CreateAdminNotificationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateAdminNotificationCommand, Result<DispatchAdminNotificationResultDto>>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSTEM_ANNOUNCEMENT",
        "ACADEMIC_NOTICE",
        "MAINTENANCE_ALERT",
        "SECURITY_ALERT"
    };

    public async Task<Result<DispatchAdminNotificationResultDto>> Handle(
        CreateAdminNotificationCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255)
        {
            return Result<DispatchAdminNotificationResultDto>.Failure(
                "VALIDATION: Tiêu đề thông báo bắt buộc từ 1 đến 255 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 2000)
        {
            return Result<DispatchAdminNotificationResultDto>.Failure(
                "VALIDATION: Nội dung thông báo bắt buộc từ 1 đến 2000 ký tự.");
        }

        var normalizedType = string.IsNullOrWhiteSpace(request.Type)
            ? "SYSTEM_ANNOUNCEMENT"
            : request.Type.Trim().ToUpperInvariant();

        if (!AllowedTypes.Contains(normalizedType))
        {
            return Result<DispatchAdminNotificationResultDto>.Failure(
                $"VALIDATION: Loại thông báo '{request.Type}' không hợp lệ.");
        }

        var scope = (request.TargetScope ?? string.Empty).Trim().ToUpperInvariant();
        string? normalizedTargetRole = null;
        List<Guid> recipientIds;

        if (scope == "USER")
        {
            if (!request.TargetUserId.HasValue || request.TargetUserId.Value == Guid.Empty)
            {
                return Result<DispatchAdminNotificationResultDto>.Failure(
                    "VALIDATION: Vui lòng chọn người dùng nhận thông báo.");
            }

            var targetUser = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == request.TargetUserId.Value)
                .Select(u => new { u.Id, u.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (targetUser is null)
            {
                return Result<DispatchAdminNotificationResultDto>.Failure(
                    "NOT_FOUND: Không tìm thấy tài khoản người nhận.");
            }

            if (!targetUser.IsActive)
            {
                return Result<DispatchAdminNotificationResultDto>.Failure(
                    "VALIDATION: Không thể gửi thông báo cho tài khoản đang bị khóa.");
            }

            recipientIds = new List<Guid> { targetUser.Id };
        }
        else if (scope == "ROLE")
        {
            normalizedTargetRole = (request.TargetRole ?? string.Empty).Trim().ToLowerInvariant();
            if (!UserRole.All.Contains(normalizedTargetRole, StringComparer.Ordinal))
            {
                return Result<DispatchAdminNotificationResultDto>.Failure(
                    $"VALIDATION: Vai trò đích '{request.TargetRole}' không hợp lệ.");
            }

            recipientIds = await context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role == normalizedTargetRole)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }
        else if (scope == "ALL_ACTIVE")
        {
            recipientIds = await context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            return Result<DispatchAdminNotificationResultDto>.Failure(
                "VALIDATION: Phạm vi gửi thông báo chỉ chấp nhận USER, ROLE hoặc ALL_ACTIVE.");
        }

        if (recipientIds.Count == 0)
        {
            return Result<DispatchAdminNotificationResultDto>.Failure(
                "VALIDATION: Không có tài khoản đang hoạt động nào thuộc nhóm nhận này để gửi thông báo.");
        }

        var now = DateTime.UtcNow;
        var title = request.Title.Trim();
        var message = request.Message.Trim();
        var createdNotifications = new List<Notification>(recipientIds.Count);

        foreach (var recipientId in recipientIds)
        {
            createdNotifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipientId,
                Title = title,
                Message = message,
                Type = normalizedType,
                IsRead = false,
                CreatedAt = now
            });
        }

        context.Notifications.AddRange(createdNotifications);

        var targetUserSegment = scope == "USER" && request.TargetUserId.HasValue
            ? request.TargetUserId.Value.ToString()
            : "-";

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "NOTIFICATION_DISPATCHED",
            EntityName = "notifications",
            EntityId = createdNotifications.Count == 1 ? createdNotifications[0].Id : null,
            NewValues = $"scope={scope};role={normalizedTargetRole ?? "-"};targetUser={targetUserSegment};type={normalizedType};recipients={recipientIds.Count};title={title}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<DispatchAdminNotificationResultDto>.Success(new DispatchAdminNotificationResultDto
        {
            CreatedCount = recipientIds.Count,
            TargetScope = scope,
            TargetRole = normalizedTargetRole,
            TargetUserId = scope == "USER" ? request.TargetUserId : null,
            Type = normalizedType,
            Title = title,
            CreatedAt = now
        });
    }
}
