using System;
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

        var tokenHash = RefreshTokenCommandHandler.HashToken(request.RefreshToken!);

        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken is not null && existingToken.IsActive)
        {
            existingToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
