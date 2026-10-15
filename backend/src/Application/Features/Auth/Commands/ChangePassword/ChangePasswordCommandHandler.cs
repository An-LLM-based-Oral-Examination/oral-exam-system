using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;

namespace OralExamination.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<ChangePasswordResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<ChangePasswordCommand> _validator;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<ChangePasswordCommand> validator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
    }

    public async Task<Result<ChangePasswordResponse>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ChangePasswordResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result<ChangePasswordResponse>.Failure(AuthErrors.AccountNotFound);
        }

        if (!user.IsActive)
        {
            return Result<ChangePasswordResponse>.Failure(AuthErrors.AccountLocked);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash)
            || !_passwordHasher.Verify(request.CurrentPassword!, user.PasswordHash))
        {
            return Result<ChangePasswordResponse>.Failure(AuthErrors.CurrentPasswordWrong);
        }

        if (_passwordHasher.Verify(request.NewPassword!, user.PasswordHash))
        {
            return Result<ChangePasswordResponse>.Failure(AuthErrors.PasswordUnchanged);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword!);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        var accessToken = _accessTokenIssuer.Issue(user.Id, user.Email, user.FullName, user.Role, mustChangePassword: false);
        var rawRefreshToken = GenerateRawToken();
        _context.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ChangePasswordResponse>.Success(new ChangePasswordResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            accessToken.ExpiresInSeconds,
            false));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateRawToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
