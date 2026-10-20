using System;
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

namespace OralExamination.Application.Features.Admin.Commands.UpdateUserStatus;

public sealed record UpdateUserStatusCommand(
    Guid ActorAdminId,
    Guid TargetUserId,
    bool IsActive,
    string? Reason = null) : IRequest<Result<AdminUserItemDto>>;

public sealed class UpdateUserStatusCommandHandler
    : IRequestHandler<UpdateUserStatusCommand, Result<AdminUserItemDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminUserItemDto>> Handle(
        UpdateUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorAdminId == request.TargetUserId && !request.IsActive)
        {
            return Result<AdminUserItemDto>.Failure(
                "CONFLICT: Quản trị viên không được tự khóa tài khoản của chính mình.");
        }

        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserItemDto>.Failure(
                "NOT_FOUND: Không tìm thấy người dùng trong hệ thống.");
        }

        var oldActive = user.IsActive;
        var now = DateTime.UtcNow;

        user.IsActive = request.IsActive;
        user.UpdatedAt = now;

        if (!request.IsActive)
        {
            foreach (var token in user.RefreshTokens.Where(t => !t.RevokedAt.HasValue))
            {
                token.RevokedAt = now;
            }
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_STATUS_CHANGED",
            EntityName = "users",
            EntityId = user.Id,
            OldValues = $"is_active={oldActive}",
            NewValues = $"is_active={request.IsActive}" + (string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : $" | {request.Reason.Trim()}"),
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AdminUserItemDto>.Success(new AdminUserItemDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            StudentCode = user.StudentCode,
            Username = user.Username,
            Role = user.Role,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }
}
