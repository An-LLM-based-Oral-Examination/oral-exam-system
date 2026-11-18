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

namespace OralExamination.Application.Features.Admin.Commands.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    Guid ActorAdminId,
    Guid TargetUserId,
    string TemporaryPassword,
    string? Reason = null) : IRequest<Result<AdminUserItemDto>>;

public sealed class ResetUserPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : IRequestHandler<ResetUserPasswordCommand, Result<AdminUserItemDto>>
{
    public async Task<Result<AdminUserItemDto>> Handle(
        ResetUserPasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TargetUserId == Guid.Empty)
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Mã người dùng không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Trim().Length < 8)
        {
            return Result<AdminUserItemDto>.Failure(
                "VALIDATION: Mật khẩu tạm thời bắt buộc có tối thiểu 8 ký tự.");
        }

        if (request.TemporaryPassword.Length > 128)
        {
            return Result<AdminUserItemDto>.Failure(
                "VALIDATION: Mật khẩu tạm thời không được vượt quá 128 ký tự.");
        }

        if (!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Trim().Length > 500)
        {
            return Result<AdminUserItemDto>.Failure(
                "VALIDATION: Lý do cấp lại mật khẩu không được vượt quá 500 ký tự.");
        }

        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserItemDto>.Failure(
                "NOT_FOUND: Không tìm thấy người dùng trong hệ thống.");
        }

        var oldMustChange = user.MustChangePassword;
        var oldAuthVersion = user.AuthVersion;
        var now = DateTime.UtcNow;
        user.PasswordHash = passwordHasher.Hash(request.TemporaryPassword);
        user.MustChangePassword = true;
        user.BumpAuthVersion();
        user.UpdatedAt = now;

        foreach (var token in user.RefreshTokens.Where(t => !t.RevokedAt.HasValue))
        {
            token.RevokedAt = now;
        }

        var reasonSuffix = string.IsNullOrWhiteSpace(request.Reason)
            ? string.Empty
            : $" | reason={request.Reason.Trim()}";

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_PASSWORD_RESET",
            EntityName = "users",
            EntityId = user.Id,
            OldValues = $"must_change_password={oldMustChange.ToString().ToLowerInvariant()};auth_version={oldAuthVersion}",
            NewValues = $"must_change_password=true;auth_version={user.AuthVersion}{reasonSuffix}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

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
