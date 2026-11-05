using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(
    Guid UserId,
    Guid NotificationId) : IRequest<Result<bool>>;

public sealed class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public MarkNotificationReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.NotificationId == Guid.Empty)
        {
            return Result<bool>.Failure("VALIDATION: UserId và NotificationId không được để trống.");
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        if (notification is null)
        {
            return Result<bool>.Failure("NOT_FOUND: Không tìm thấy thông báo trong hệ thống.");
        }

        if (notification.UserId != request.UserId)
        {
            return Result<bool>.Failure("FORBIDDEN: Bạn không có quyền truy cập thông báo này.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}

