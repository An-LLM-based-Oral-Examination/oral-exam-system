using System;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OralExamination.Application.Common.Models;
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
    public async Task Handle_EmptyToken_ReturnsSuccessIdempotently()
    {
        await using var context = CreateDbContext();
        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand(""), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyToken_WithAuthenticatedUserId_BumpsAuthVersionAndRevokesTokens()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var initialVersion = user.AuthVersion;
        var token1 = SeedRefreshToken(context, user.Id, "active-token-1", DateTime.UtcNow.AddDays(7));
        var token2 = SeedRefreshToken(context, user.Id, "active-token-2", DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand(null, user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.AuthVersion.Should().Be(initialVersion + 1);
        (await context.RefreshTokens.AllAsync(t => t.RevokedAt != null)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentToken_WithAuthenticatedUserId_BumpsAuthVersionAndRevokesActiveTokens()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var initialVersion = user.AuthVersion;
        SeedRefreshToken(context, user.Id, "active-token-remaining", DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);

        var result = await handler.Handle(new SignOutCommand("stale-or-missing-token", user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.AuthVersion.Should().Be(initialVersion + 1);
        (await context.RefreshTokens.AllAsync(t => t.RevokedAt != null)).Should().BeTrue();
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

    [Fact]
    public async Task AuthController_SignOut_OnConflict_Returns409_WithoutDeletingRefreshCookie()
    {
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SignOutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("CONFLICT: Phiên đăng xuất đang xung đột."));

        var services = new ServiceCollection();
        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(e => e.EnvironmentName).Returns("Testing");
        services.AddSingleton(envMock.Object);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var controller = new AuthController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var actionResult = await controller.SignOut(null, CancellationToken.None);

        var objectResult = actionResult.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        httpContext.Response.Headers.SetCookie.ToString().Should().NotContain("oes_refresh_session");
    }
}
