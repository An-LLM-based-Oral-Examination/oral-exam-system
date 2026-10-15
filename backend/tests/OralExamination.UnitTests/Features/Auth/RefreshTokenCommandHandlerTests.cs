using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class RefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidToken_RotatesAndReturnsNewTokens()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var rawToken = "test-refresh-token-raw-value";
        SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, issuer);

        var result = await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().NotBe(rawToken);
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.ExpiresIn.Should().Be(900);
        issuer.Calls.Should().Be(1);

        var tokens = await context.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().ContainSingle(t => t.RevokedAt.HasValue);
        tokens.Should().ContainSingle(t => !t.RevokedAt.HasValue);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var rawToken = "expired-token";
        SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(-1));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new FakeIssuer());

        var result = await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("hết hạn");
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesAllUserTokensAndReturnsFailure()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);

        // Token đã bị thu hồi (bị hacker dùng lại)
        var revokedRawToken = "revoked-token";
        var revokedToken = SeedRefreshToken(context, user.Id, revokedRawToken, DateTime.UtcNow.AddDays(7));
        revokedToken.RevokedAt = DateTime.UtcNow.AddHours(-1);

        // Token hợp lệ đang active trên thiết bị khác
        var activeRawToken = "active-on-another-device";
        SeedRefreshToken(context, user.Id, activeRawToken, DateTime.UtcNow.AddDays(7));

        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new FakeIssuer());

        var result = await handler.Handle(new RefreshTokenCommand(revokedRawToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("thu hồi");
        result.Error.Should().Contain("vô hiệu hóa");

        // RFC 6819: Toàn bộ token của user phải bị revoke
        var allTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        allTokens.Should().OnlyContain(t => t.RevokedAt.HasValue,
            "tất cả refresh token của user phải bị thu hồi khi phát hiện replay attack");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Email = "locked@gmail.com",
            FullName = "Locked",
            Role = UserRole.Student,
            IsActive = false
        };
        context.Users.Add(user);
        var rawToken = "locked-user-token";
        SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new FakeIssuer());

        var result = await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("khóa");
    }

    [Fact]
    public async Task Handle_NonExistentToken_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var handler = CreateHandler(context, new FakeIssuer());

        var result = await handler.Handle(new RefreshTokenCommand("does-not-exist"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không hợp lệ");
    }

    [Fact]
    public async Task Handle_EmptyToken_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var handler = CreateHandler(context, new FakeIssuer());

        var result = await handler.Handle(new RefreshTokenCommand(""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    private static RefreshTokenCommandHandler CreateHandler(OralExamDbContext context, FakeIssuer issuer)
    {
        return new RefreshTokenCommandHandler(context, issuer, new RefreshTokenCommandValidator());
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

    private sealed class FakeIssuer : IAccessTokenIssuer
    {
        public int Calls { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            Calls++;
            return new IssuedAccessToken("access-token", 900);
        }
    }
}
