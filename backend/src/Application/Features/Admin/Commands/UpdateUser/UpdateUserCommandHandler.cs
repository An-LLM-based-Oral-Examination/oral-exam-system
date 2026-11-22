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

namespace OralExamination.Application.Features.Admin.Commands.UpdateUser;

public sealed partial class UpdateUserCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateUserCommand, Result<AdminUserItemDto>>
{
    [GeneratedRegex("^[a-z0-9._-]+$")]
    private static partial Regex LowerUsernameRegex();

    public async Task<Result<AdminUserItemDto>> Handle(
        UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TargetUserId == Guid.Empty)
        {
            return Result<AdminUserItemDto>.Failure("VALIDATION: Mã người dùng không hợp lệ.");
        }

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

        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserItemDto>.Failure("NOT_FOUND: Không tìm thấy người dùng trong hệ thống.");
        }

        string? nextStudentCode;
        string? nextUsername;

        if (string.Equals(user.Role, UserRole.Student, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.StudentCode))
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Mã sinh viên là bắt buộc đối với vai trò student.");
            }

            nextStudentCode = request.StudentCode.Trim().ToUpperInvariant();
            if (nextStudentCode.Length > 20)
            {
                return Result<AdminUserItemDto>.Failure("VALIDATION: Mã sinh viên không được vượt quá 20 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                nextUsername = user.Username;
            }
            else if (!string.Equals(request.Username.Trim(), user.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Không thể chỉnh sửa thủ công tên đăng nhập của sinh viên.");
            }
            else
            {
                nextUsername = user.Username;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.StudentCode))
            {
                return Result<AdminUserItemDto>.Failure(
                    "VALIDATION: Chỉ tài khoản sinh viên mới được phép có Mã sinh viên.");
            }

            nextStudentCode = null;

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                nextUsername = null;
            }
            else
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

                nextUsername = candidate;
            }
        }

        var emailExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id != user.Id && u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Result<AdminUserItemDto>.Failure("CONFLICT: Email đã tồn tại trong hệ thống.");
        }

        if (nextStudentCode is not null)
        {
            var studentCodeExists = await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != user.Id && u.StudentCode != null && u.StudentCode.ToUpper() == nextStudentCode, cancellationToken);
            if (studentCodeExists)
            {
                return Result<AdminUserItemDto>.Failure("CONFLICT: Mã sinh viên đã tồn tại trong hệ thống.");
            }
        }

        if (nextUsername is not null)
        {
            var usernameExists = await context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id != user.Id && u.Username != null && u.Username.ToLower() == nextUsername.ToLower(), cancellationToken);
            if (usernameExists)
            {
                return Result<AdminUserItemDto>.Failure("CONFLICT: Tên đăng nhập đã tồn tại trong hệ thống.");
            }
        }

        var emailChanged = !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        var usernameChanged = !string.Equals(user.Username, nextUsername, StringComparison.OrdinalIgnoreCase);

        var oldValues = $"email={user.Email};fullName={user.FullName};studentCode={user.StudentCode};username={user.Username};auth_version={user.AuthVersion}";
        var now = DateTime.UtcNow;

        user.Email = normalizedEmail;
        user.FullName = trimmedFullName;
        user.StudentCode = nextStudentCode;
        user.Username = nextUsername;
        user.UpdatedAt = now;

        if (emailChanged || usernameChanged)
        {
            user.BumpAuthVersion();
            foreach (var token in user.RefreshTokens.Where(t => !t.RevokedAt.HasValue))
            {
                token.RevokedAt = now;
            }
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorAdminId == Guid.Empty ? null : request.ActorAdminId,
            Action = "USER_UPDATED",
            EntityName = "users",
            EntityId = user.Id,
            OldValues = oldValues,
            NewValues = $"email={user.Email};fullName={user.FullName};studentCode={user.StudentCode};username={user.Username};auth_version={user.AuthVersion}",
            CreatedAt = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Result<AdminUserItemDto>.Failure(
                "CONFLICT: Email, Mã sinh viên hoặc Tên đăng nhập đã tồn tại trong hệ thống.");
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
