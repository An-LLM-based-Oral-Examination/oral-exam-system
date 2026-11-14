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

        if (user is null)
        {
            // Explicit production-data policy: verified Google newcomers are
            // students only, never privileged roles and never auto-enrolled.
            user = new User
            {
                Email = email,
                FullName = identity.FullName,
                StudentCode = TryExtractFptStudentCode(email),
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
        var accessToken = _accessTokenIssuer.Issue(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.MustChangePassword);

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
        await _context.SaveChangesAsync(cancellationToken);

        var response = new GoogleLoginResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            accessToken.ExpiresInSeconds,
            new GoogleLoginUserDto(user.Id, user.Email, user.FullName, user.StudentCode, user.Role));

        return Result<GoogleLoginResponse>.Success(response);
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
