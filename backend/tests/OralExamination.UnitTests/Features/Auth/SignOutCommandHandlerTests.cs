using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.Commands.SignOut;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class SignOutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidToken_RevokesAndReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var rawToken = "sign-out-token";
        SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var token = await context.RefreshTokens.FirstAsync();
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NonExistentToken_StillReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand("does-not-exist"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyRevokedToken_ReturnsSuccess()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var rawToken = "already-revoked";
        var token = SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(7));
        token.RevokedAt = DateTime.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyToken_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand(""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static SignOutCommandHandler CreateHandler(OralExamDbContext context)
    {
        return new SignOutCommandHandler(context, new SignOutCommandValidator());
    }

    private static User SeedActiveUser(OralExamDbContext context)
    {
        var user = new User
        {
            Email = "test@gmail.com",
            FullName = "Test User",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(user);
        return user;
    }

    private static RefreshToken SeedRefreshToken(OralExamDbContext context, Guid userId, string rawToken, DateTime expiresAt)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshTokenCommandHandler.HashToken(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
        context.RefreshTokens.Add(token);
        return token;
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }
}
