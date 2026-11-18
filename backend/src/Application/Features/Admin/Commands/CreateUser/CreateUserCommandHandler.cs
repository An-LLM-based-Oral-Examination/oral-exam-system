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

namespace OralExamination.Application.Features.Admin.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : IRequestHandler<CreateUserCommand, Result<AdminUserItemDto>>
{
    public async Task<Result<AdminUserItemDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return Result<AdminUserItemDto>.Failure("CONFLICT: Email đã tồn tại trong hệ thống.");
        }

        var normalizedRole = request.Role.Trim().ToLowerInvariant();
        if (!UserRole.All.Contains(normalizedRole))
        {
            return Result<AdminUserItemDto>.Failure($"VALIDATION: Vai trò '{request.Role}' không hợp lệ.");
        }

        string? studentCode = null;
        string? username = null;

        if (normalizedRole == UserRole.Student)
        {
            if (string.IsNullOrWhiteSpace(request.StudentCode))
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Mã sinh viên là bắt buộc đối với vai trò student.");
            }

            studentCode = request.StudentCode.Trim().ToUpperInvariant();

            var studentCodeExists = await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.StudentCode != null && u.StudentCode.ToUpper() == studentCode, cancellationToken);
            if (studentCodeExists)
            {
                return Result<AdminUserItemDto>.Failure("CONFLICT: Mã sinh viên đã tồn tại trong hệ thống.");
            }

            if (StudentUsername.TryCreate(request.FullName, studentCode, out var generatedUsername))
            {
                username = generatedUsername;
            }
        }

        if (username != null)
        {
            var usernameExists = await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Username != null && u.Username.ToLower() == username.ToLower(), cancellationToken);
            if (usernameExists)
            {
                return Result<AdminUserItemDto>.Failure("CONFLICT: Tên đăng nhập đã tồn tại trong hệ thống.");
            }
        }

        var hashedPassword = passwordHasher.Hash(request.Password);
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = hashedPassword,
            Role = normalizedRole,
            StudentCode = studentCode,
            Username = username,
            MustChangePassword = true,
            IsActive = true,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Users.Add(user);

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_CREATED",
            EntityName = "users",
            EntityId = user.Id,
            NewValues = $"email={user.Email};role={user.Role};fullName={user.FullName};studentCode={user.StudentCode};mustChangePassword={user.MustChangePassword}",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Result<AdminUserItemDto>.Failure("CONFLICT: Email, Mã sinh viên hoặc Tên đăng nhập đã tồn tại trong hệ thống.");
        }

        var dto = new AdminUserItemDto
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
        };

        return Result<AdminUserItemDto>.Success(dto);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner != null)
        {
            var prop = inner.GetType().GetProperty("SqlState");
            if (prop != null)
            {
                var sqlState = prop.GetValue(inner) as string;
                if (sqlState == "23505")
                {
                    return true;
                }
            }

            var message = inner.Message;
            if (message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("ix_users", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return ex.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }
}
