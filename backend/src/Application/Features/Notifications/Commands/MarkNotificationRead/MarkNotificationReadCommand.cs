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

        var alreadyMarked = await _context.AuditLogs
            .AnyAsync(
                a => a.UserId == request.UserId &&
                     a.Action == "NOTIFICATION_READ" &&
                     a.EntityId == request.NotificationId,
                cancellationToken);

        if (!alreadyMarked)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Action = "NOTIFICATION_READ",
                EntityName = "notifications",
                EntityId = request.NotificationId,
                NewValues = "is_read=true",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
