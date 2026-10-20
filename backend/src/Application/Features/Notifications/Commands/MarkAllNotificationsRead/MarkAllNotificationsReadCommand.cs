using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result<bool>>;

public sealed class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public MarkAllNotificationsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<bool>.Failure("VALIDATION: UserId không được để trống.");
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Action = "NOTIFICATION_READ_ALL",
            EntityName = "notifications",
            EntityId = request.UserId,
            NewValues = "all_read=true",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
