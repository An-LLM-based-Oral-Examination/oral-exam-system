using System;
using System.Linq;
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

namespace OralExamination.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<RefreshTokenCommand> _validator;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<RefreshTokenCommand> validator)
    {
        _context = context;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
    }

    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<RefreshTokenResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var tokenHash = HashToken(request.RefreshToken!);

        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result<RefreshTokenResponse>.Failure("Refresh token không hợp lệ.");
        }

        if (existingToken.IsRevoked)
        {
            // RFC 6819 §5.2.2.3 — Replay Attack Detection:
            // Token đã bị thu hồi mà vẫn bị gửi lên → dấu hiệu rò rỉ token.
            // Thu hồi toàn bộ refresh token đang hoạt động của user để ép đăng nhập lại.
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            if (activeTokens.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<RefreshTokenResponse>.Failure("Refresh token đã bị thu hồi. Toàn bộ phiên đăng nhập đã bị vô hiệu hóa vì phát hiện nghi ngờ rò rỉ token.");
        }

        if (existingToken.IsExpired)
        {
            return Result<RefreshTokenResponse>.Failure("Refresh token đã hết hạn.");
        }

        if (!existingToken.User.IsActive)
        {
            return Result<RefreshTokenResponse>.Failure("Tài khoản đã bị khóa.");
        }

        var user = existingToken.User;

        // Token rotation: revoke cái cũ, tạo cái mới
        var newRawToken = GenerateRawToken();
        var newTokenHash = HashToken(newRawToken);

        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.ReplacedByTokenHash = newTokenHash;

        var newRefreshToken = new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var accessToken = _accessTokenIssuer.Issue(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.MustChangePassword);

        return Result<RefreshTokenResponse>.Success(new RefreshTokenResponse(
            accessToken.Token,
            newRawToken,
            "Bearer",
            accessToken.ExpiresInSeconds));
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
