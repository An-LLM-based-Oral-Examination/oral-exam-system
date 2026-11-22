using System;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
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

public sealed partial class CreateUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher)
    : IRequestHandler<CreateUserCommand, Result<AdminUserItemDto>>
{
    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex LowerUsernameRegex();

    public async Task<Result<AdminUserItemDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Email không được để trống.");
        }

        var trimmedEmail = request.Email.Trim();
        if (trimmedEmail.Length > 255)
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Email không được vượt quá 255 ký tự.");
        }

        if (trimmedEmail.Any(char.IsWhiteSpace) ||
            !MailAddress.TryCreate(trimmedEmail, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Định dạng email không hợp lệ.");
        }

        var normalizedEmail = trimmedEmail.ToLowerInvariant();

        var trimmedFullName = request.FullName?.Trim() ?? string.Empty;
        if (trimmedFullName.Length is < 2 or > 150)
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Họ và tên phải có độ dài từ 2 đến 150 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Mật khẩu phải có độ dài tối thiểu 8 ký tự.");
        }

        var emailExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return Result<AdminUserItemDto>.Failure("CONFLICT: Email đã tồn tại trong hệ thống.");
        }

        var normalizedRole = request.Role?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!UserRole.All.Contains(normalizedRole))
        {
            return Result<AdminUserItemDto>.Failure($"VALIDATION: Vai trò '{request.Role}' không hợp lệ.");
        }

        string? studentCode = null;
        string? username = null;

        if (normalizedRole == UserRole.Student)
        {
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Tài khoản sinh viên tự động sinh tên đăng nhập từ họ tên và MSSV, không được nhập Username thủ công.");
            }

            if (string.IsNullOrWhiteSpace(request.StudentCode))
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Mã sinh viên là bắt buộc đối với vai trò student.");
            }

            studentCode = request.StudentCode.Trim().ToUpperInvariant();
            if (studentCode.Length > 20)
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Mã sinh viên không được vượt quá 20 ký tự.");
            }

            var studentCodeExists = await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.StudentCode != null && u.StudentCode.ToUpper() == studentCode, cancellationToken);
            if (studentCodeExists)
            {
                return Result<AdminUserItemDto>.Failure("CONFLICT: Mã sinh viên đã tồn tại trong hệ thống.");
            }

            if (StudentUsername.TryCreate(trimmedFullName, studentCode, out var generatedUsername))
            {
                username = generatedUsername;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var candidate = request.Username.Trim().ToLowerInvariant();
            if (candidate.Length is < 3 or > 32)
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Tên đăng nhập phải có độ dài từ 3 đến 32 ký tự.");
            }

            if (!LowerUsernameRegex().IsMatch(candidate))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Tên đăng nhập chỉ được chứa chữ cái, chữ số, dấu chấm (.), gạch dưới (_) và gạch ngang (-).");
            }

            username = candidate;
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
            FullName = trimmedFullName,
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
            NewValues = $"email={user.Email};role={user.Role};fullName={user.FullName};studentCode={user.StudentCode};username={user.Username};mustChangePassword={user.MustChangePassword}",
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
