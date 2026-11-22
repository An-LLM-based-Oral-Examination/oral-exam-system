using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Auth.Commands.GoogleLogin;

public sealed partial class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<GoogleLoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleIdTokenValidator _googleIdTokenValidator;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<GoogleLoginCommand> _validator;
    private readonly IGoogleLoginNonceStore _nonceStore;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleIdTokenValidator googleIdTokenValidator,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<GoogleLoginCommand> validator,
        IGoogleLoginNonceStore nonceStore)
    {
        _context = context;
        _googleIdTokenValidator = googleIdTokenValidator;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
        _nonceStore = nonceStore;
    }

    public async Task<Result<GoogleLoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra đầu vào
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<GoogleLoginResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(request.BrowserNonce) || request.BrowserNonce.Length is < 32 or > 128)
            return await Failure(AuthErrors.GoogleNonceInvalid, "NONCE_REJECTED", cancellationToken);

        // 2. Xác minh chữ ký ID Token của Google
        GoogleUserIdentity identity;
        try
        {
            identity = await _googleIdTokenValidator.ValidateAsync(request.IdToken!, cancellationToken);
        }
        catch (GoogleIdTokenRejectedException)
        {
            return await Failure(AuthErrors.GoogleTokenInvalid, "TOKEN_REJECTED", cancellationToken);
        }

        if (identity.Nonce is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(identity.Nonce), Encoding.UTF8.GetBytes(request.BrowserNonce))
            || !_nonceStore.TryConsume(request.BrowserNonce))
            return await Failure(AuthErrors.GoogleNonceInvalid, "NONCE_REJECTED", cancellationToken);

        // 3. Tìm user trong bảng users theo email
        var email = identity.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        var isNewUser = user is null;
        string? extractedStudentCode = null;
        if (user is null)
        {
            extractedStudentCode = TryExtractFptStudentCode(email);
            if (!string.IsNullOrWhiteSpace(extractedStudentCode))
            {
                var normalizedCode = extractedStudentCode.ToLowerInvariant();
                var studentCodeInUse = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        u => u.StudentCode != null && u.StudentCode.ToLower() == normalizedCode,
                        cancellationToken);
                if (studentCodeInUse)
                {
                    return await Failure(AuthErrors.GoogleStudentCodeConflict, "STUDENT_CODE_CONFLICT", cancellationToken);
                }
            }

            // Explicit production-data policy: verified Google newcomers are
            // students only, never privileged roles and never auto-enrolled.
            user = new User
            {
                Email = email,
                FullName = identity.FullName,
                StudentCode = extractedStudentCode,
                Role = UserRole.Student,
                IsActive = true,
                MustChangePassword = false
            };
            _context.Users.Add(user);
        }
        else if (!user.IsActive)
        {
            return await Failure(AuthErrors.AccountLocked, "ACCOUNT_LOCKED", cancellationToken, user.Id);
        }

        // 4. Cấp access token JWT (15 phút) + refresh token (7 ngày)
        var rawRefreshToken = GenerateRawToken();
        var refreshTokenHash = HashToken(rawRefreshToken);

        var refreshTokenEntity = new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(refreshTokenEntity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isNewUser && IsUniqueConstraintViolation(ex))
        {
            if (_context is DbContext dbContext)
            {
                dbContext.Entry(refreshTokenEntity).State = EntityState.Detached;
                dbContext.Entry(user).State = EntityState.Detached;
            }

            // Nếu 2 tab cùng đăng nhập lần đầu cho cùng email, nạp lại bản ghi vừa tạo;
            // nếu xung đột unique ở student_code với tài khoản khác, trả về 409 rõ ràng thay vì 500.
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);
            if (existingUser is null)
            {
                if (!string.IsNullOrWhiteSpace(extractedStudentCode))
                {
                    return await Failure(AuthErrors.GoogleStudentCodeConflict, "STUDENT_CODE_CONFLICT", cancellationToken);
                }

                throw;
            }

            if (!existingUser.IsActive)
            {
                return await Failure(AuthErrors.AccountLocked, "ACCOUNT_LOCKED", cancellationToken, existingUser.Id);
            }

            user = existingUser;
            var retryRefreshToken = new Domain.Entities.RefreshToken
            {
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(retryRefreshToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var accessToken = _accessTokenIssuer.Issue(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.MustChangePassword,
            user.AuthVersion);

        var response = new GoogleLoginResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new GoogleLoginUserDto(user.Id, user.Email, user.FullName, user.StudentCode, user.Role));

        return Result<GoogleLoginResponse>.Success(response);
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
                message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("uq_users_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private async Task<Result<GoogleLoginResponse>> Failure(string error, string reason, CancellationToken cancellationToken, Guid? userId = null)
    {
        _context.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            UserId = userId, EntityId = userId, EntityName = "auth_google", Action = "GOOGLE_LOGIN_FAILED",
            NewValues = JsonSerializer.Serialize(new { reason }), CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Result<GoogleLoginResponse>.Failure(error);
    }

    private static string? TryExtractFptStudentCode(string email)
    {
        var match = FptStudentEmailRegex().Match(email);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"([a-zA-Z]{2}\d{6})@fpt\.edu\.vn$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FptStudentEmailRegex();

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public static string GenerateRawToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
