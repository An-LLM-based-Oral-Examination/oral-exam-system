using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Admin.Commands.DeleteAdminNotification;

public sealed record DeleteAdminNotificationCommand(
    Guid ActorAdminId,
    Guid NotificationId) : IRequest<Result<bool>>;

public sealed class DeleteAdminNotificationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteAdminNotificationCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteAdminNotificationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.NotificationId == Guid.Empty)
        {
            return Result<bool>.Failure("VALIDATION: Mã thông báo không hợp lệ.");
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        if (notification is null)
        {
            return Result<bool>.Failure("NOT_FOUND: Không tìm thấy thông báo trong hệ thống.");
        }

        var oldSummary = $"userId={notification.UserId};type={notification.Type};title={notification.Title}";
        context.Notifications.Remove(notification);

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "NOTIFICATION_DELETED",
            EntityName = "notifications",
            EntityId = notification.Id,
            OldValues = oldSummary,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
