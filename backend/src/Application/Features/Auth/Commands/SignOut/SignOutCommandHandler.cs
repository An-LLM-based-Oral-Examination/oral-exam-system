using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;

namespace OralExamination.Application.Features.Auth.Commands.SignOut;

public sealed class SignOutCommandHandler : IRequestHandler<SignOutCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<SignOutCommand> _validator;

    public SignOutCommandHandler(
        IApplicationDbContext context,
        IValidator<SignOutCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<bool>.Failure(validation.Errors[0].ErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            if (request.AuthenticatedUserId is Guid authenticatedUserId && authenticatedUserId != Guid.Empty)
            {
                await RevokeAuthenticatedUserSessionsAsync(authenticatedUserId, cancellationToken);
            }

            return Result<bool>.Success(true);
        }

        var tokenHash = RefreshTokenCommandHandler.HashToken(request.RefreshToken);

        return await _context.ExecuteRefreshTokenRotationAsync(
            ct => RevokeSessionAsync(tokenHash, request.AuthenticatedUserId, ct),
            cancellationToken);
    }

    private async Task RevokeAuthenticatedUserSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        user.BumpAuthVersion();

        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = nowUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<bool>> RevokeSessionAsync(
        string tokenHash,
        Guid? authenticatedUserId,
        CancellationToken cancellationToken)
    {
        await using var lockScope = await _context.BeginRefreshTokenLockScopeAsync(tokenHash, cancellationToken);
        if (!lockScope.HasLock)
        {
            return Result<bool>.Failure("CONFLICT: Phiên đăng xuất đang xung đột với yêu cầu làm mới token đồng thời. Vui lòng thử lại.");
        }

        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is not null && existingToken.IsActive)
        {
            var nowUtc = DateTime.UtcNow;
            existingToken.RevokedAt = nowUtc;
            existingToken.User?.BumpAuthVersion();

            // Thu hồi đồng bộ toàn bộ RefreshToken còn hiệu lực của user để đồng nhất với BumpAuthVersion().
            var siblingActiveTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null && rt.Id != existingToken.Id)
                .ToListAsync(cancellationToken);

            foreach (var sibling in siblingActiveTokens)
            {
                sibling.RevokedAt = nowUtc;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await lockScope.CommitAsync(cancellationToken);
        }
        else if (authenticatedUserId is Guid authUserId && authUserId != Guid.Empty)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == authUserId, cancellationToken);

            if (user is not null)
            {
                var nowUtc = DateTime.UtcNow;
                user.BumpAuthVersion();

                var activeTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var token in activeTokens)
                {
                    token.RevokedAt = nowUtc;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await lockScope.CommitAsync(cancellationToken);
            }
        }

        return Result<bool>.Success(true);
    }
}
