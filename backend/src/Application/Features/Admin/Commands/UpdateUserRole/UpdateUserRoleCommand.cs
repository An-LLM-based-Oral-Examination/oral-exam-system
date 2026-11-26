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
using OralExamination.Domain.Identity;

namespace OralExamination.Application.Features.Admin.Commands.UpdateUserRole;

public sealed record UpdateUserRoleCommand(
    Guid ActorAdminId,
    Guid TargetUserId,
    string Role,
    string? StudentCode = null) : IRequest<Result<AdminUserItemDto>>;

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
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserItemDto>.Failure(
                "NOT_FOUND: Không tìm thấy người dùng trong hệ thống.");
        }

        var oldRole = user.Role;
        var oldStudentCode = user.StudentCode;
        var oldUsername = user.Username;

        string? nextStudentCode = user.StudentCode;
        string? nextUsername = user.Username;

        if (normalizedRole == UserRole.Student)
        {
            var candidateCode = (request.StudentCode ?? user.StudentCode)?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(candidateCode))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Mã sinh viên là bắt buộc khi gán vai trò student.");
            }

            if (candidateCode.Length > 20)
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Mã sinh viên không được vượt quá 20 ký tự.");
            }

            if (!StudentUsername.TryCreate(user.FullName, candidateCode, out var generatedUsername))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Họ tên và Mã sinh viên chưa hợp lệ để tạo tên đăng nhập sinh viên (yêu cầu họ tên tối thiểu 2 từ và mã sinh viên dạng 2 chữ cái + 6 chữ số).");
            }

            var studentCodeExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != user.Id && u.StudentCode != null && u.StudentCode.ToUpper() == candidateCode, cancellationToken);
            if (studentCodeExists)
            {
                return Result<AdminUserItemDto>.Failure(
                    "CONFLICT: Mã sinh viên đã tồn tại trong hệ thống.");
            }

            var usernameExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != user.Id && u.Username != null && u.Username.ToLower() == generatedUsername.ToLower(), cancellationToken);
            if (usernameExists)
            {
                return Result<AdminUserItemDto>.Failure(
                    "CONFLICT: Tên đăng nhập sinh viên đã tồn tại trong hệ thống.");
            }

            nextStudentCode = candidateCode;
            nextUsername = generatedUsername;
        }
        else
        {
            nextStudentCode = null;
        }

        var now = DateTime.UtcNow;
        var usernameChanged = !string.Equals(user.Username, nextUsername, StringComparison.OrdinalIgnoreCase);

        user.Role = normalizedRole;
        user.StudentCode = nextStudentCode;
        user.Username = nextUsername;
        user.UpdatedAt = now;

        if (oldRole != normalizedRole || usernameChanged)
        {
            user.BumpAuthVersion();
            foreach (var token in user.RefreshTokens.Where(t => t.RevokedAt == null))
            {
                token.RevokedAt = now;
            }
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_ROLE_CHANGED",
            EntityName = "users",
            EntityId = user.Id,
            OldValues = $"role={oldRole};studentCode={oldStudentCode};username={oldUsername}",
            NewValues = $"role={normalizedRole};studentCode={user.StudentCode};username={user.Username}",
            CreatedAt = now
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Result<AdminUserItemDto>.Failure(
                "CONFLICT: Mã sinh viên hoặc Tên đăng nhập đã tồn tại trong hệ thống.");
        }

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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current.GetType().Name == "PostgresException" &&
                string.Equals(current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString(), "23505", StringComparison.Ordinal))
            {
                return true;
            }

            var message = current.Message;
            if (message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ix_users", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
