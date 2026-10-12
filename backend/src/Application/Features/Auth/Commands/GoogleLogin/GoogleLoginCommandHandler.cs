using System;
using System.Security.Cryptography;
using System.Text;
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

public sealed class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, Result<GoogleLoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleIdTokenValidator _googleIdTokenValidator;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<GoogleLoginCommand> _validator;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleIdTokenValidator googleIdTokenValidator,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<GoogleLoginCommand> validator)
    {
        _context = context;
        _googleIdTokenValidator = googleIdTokenValidator;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
    }

    public async Task<Result<GoogleLoginResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra đầu vào
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<GoogleLoginResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        // 2. Xác minh chữ ký ID Token của Google
        GoogleUserIdentity identity;
        try
        {
            identity = await _googleIdTokenValidator.ValidateAsync(request.IdToken!, cancellationToken);
        }
        catch (GoogleIdTokenRejectedException)
        {
            return Result<GoogleLoginResponse>.Failure("ID Token của Google không hợp lệ.");
        }

        // 3. Tìm user trong bảng users theo email
        var email = identity.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                FullName = identity.FullName,
                StudentCode = null,
                Role = UserRole.Student,
                IsActive = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else if (!user.IsActive)
        {
            return Result<GoogleLoginResponse>.Failure("Tài khoản đã bị khóa.");
        }

        // 4. Cấp access token JWT (15 phút) + refresh token (7 ngày)
        var accessToken = _accessTokenIssuer.Issue(user.Id, user.Email, user.FullName, user.Role);

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
