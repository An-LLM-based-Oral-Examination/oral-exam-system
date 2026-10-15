using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Identity;

namespace OralExamination.Application.Features.Auth.Commands.PasswordLogin;

public sealed class PasswordLoginCommandHandler : IRequestHandler<PasswordLoginCommand, Result<PasswordLoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IValidator<PasswordLoginCommand> _validator;

    public PasswordLoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAccessTokenIssuer accessTokenIssuer,
        IValidator<PasswordLoginCommand> validator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _accessTokenIssuer = accessTokenIssuer;
        _validator = validator;
    }

    public async Task<Result<PasswordLoginResponse>> Handle(PasswordLoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<PasswordLoginResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var username = StudentUsername.Normalize(request.Username);
        var user = await _context.Users
            .FirstOrDefaultAsync(item => item.Username == username, cancellationToken);

        if (user is null)
        {
            return Result<PasswordLoginResponse>.Failure(AuthErrors.AccountNotFound);
        }

        if (!user.IsActive)
        {
            return Result<PasswordLoginResponse>.Failure(AuthErrors.AccountLocked);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Result<PasswordLoginResponse>.Failure(AuthErrors.PasswordNotSet);
        }

        if (!_passwordHasher.Verify(request.Password!, user.PasswordHash))
        {
            return Result<PasswordLoginResponse>.Failure(AuthErrors.WrongPassword);
        }

        var accessToken = _accessTokenIssuer.Issue(
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.MustChangePassword);

        var rawRefreshToken = GenerateRawToken();
        _context.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(AuthTokenDefaults.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        return Result<PasswordLoginResponse>.Success(new PasswordLoginResponse(
            accessToken.Token,
            rawRefreshToken,
            "Bearer",
            accessToken.ExpiresInSeconds,
            user.MustChangePassword,
            new GoogleLoginUserDto(user.Id, user.Email, user.FullName, user.StudentCode, user.Role)));
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
