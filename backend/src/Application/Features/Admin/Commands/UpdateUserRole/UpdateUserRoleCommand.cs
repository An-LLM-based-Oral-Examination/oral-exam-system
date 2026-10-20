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

namespace OralExamination.Application.Features.Admin.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(
    Guid ActorAdminId,
    Guid TargetUserId,
    string Role) : IRequest<Result<AdminUserItemDto>>;

public sealed class UpdateUserRoleCommandHandler
    : IRequestHandler<UpdateUserRoleCommand, Result<AdminUserItemDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminUserItemDto>> Handle(
        UpdateUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return Result<AdminUserItemDto>.Failure(
                "VALIDATION: Vai trò (Role) không được để trống.");
        }

        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        if (!UserRole.All.Contains(normalizedRole, StringComparer.Ordinal))
        {
            return Result<AdminUserItemDto>.Failure(
                $"VALIDATION: Vai trò '{request.Role}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", UserRole.All)}.");
        }

        if (request.ActorAdminId == request.TargetUserId && normalizedRole != UserRole.Admin)
        {
            return Result<AdminUserItemDto>.Failure(
                "CONFLICT: Quản trị viên không được tự hạ quyền của chính mình.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserItemDto>.Failure(
                "NOT_FOUND: Không tìm thấy người dùng trong hệ thống.");
        }

        var oldRole = user.Role;
        var now = DateTime.UtcNow;

        user.Role = normalizedRole;
        user.UpdatedAt = now;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_ROLE_CHANGED",
            EntityName = "users",
            EntityId = user.Id,
            OldValues = $"role={oldRole}",
            NewValues = $"role={normalizedRole}",
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
