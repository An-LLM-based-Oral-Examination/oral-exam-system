using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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

    [Fact(DisplayName = "T3-F008: Khi transaction commit thất bại, handler phải throw/fail và tuyệt đối không phát hành access token")]
    public async Task Handle_CommitFailure_ThrowsException_AndDoesNotIssueTokens()
    {
        await using var context = CreateDbContext();
        var user = SeedActiveUser(context);
        var rawToken = "commit-fail-token";
        SeedRefreshToken(context, user.Id, rawToken, DateTime.UtcNow.AddDays(7));
        await context.SaveChangesAsync();

        var issuer = new FakeIssuer();

        var failingLockScope = new Mock<IRefreshTokenLockScope>();
        failingLockScope.Setup(l => l.HasLock).Returns(true);
        failingLockScope.Setup(l => l.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PostgreSQL transaction commit failed due to connection break"));

        var dbMock = new Mock<IApplicationDbContext>();
        dbMock.Setup(d => d.Users).Returns(context.Users);
        dbMock.Setup(d => d.RefreshTokens).Returns(context.RefreshTokens);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        dbMock.Setup(d => d.BeginRefreshTokenLockScopeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failingLockScope.Object);

        var handler = new RefreshTokenCommandHandler(dbMock.Object, issuer, new RefreshTokenCommandValidator());

        var act = async () => await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*commit failed*");

        issuer.Calls.Should().Be(0, "không được phép phát hành access token khi commit chưa thành công");
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

    [Fact]
    public async Task Controller_RefreshToken_409Conflict_PreservesSharedRefreshSessionCookie()
    {
        var sender = new Moq.Mock<MediatR.ISender>();
        sender.Setup(x => x.Send(Moq.It.IsAny<RefreshTokenCommand>(), Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.Auth.DTOs.RefreshTokenResponse>.Failure(
                "CONFLICT: Refresh token đang được xử lý đồng thời ở một tiến trình khác."));

        var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            serviceCollection, new FakeHostEnvironment { EnvironmentName = "Testing" });
        var services = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(serviceCollection);

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Headers.Cookie = "oes_refresh_session=concurrent-old-token";

        var controller = new global::API.Controllers.v1.AuthController(sender.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext }
        };

        var actionResult = await controller.RefreshToken(null, CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict);
        httpContext.Response.Headers.SetCookie.ToString().Should().BeEmpty(
            "409 Conflict do xoay vòng đồng thời không được phát Set-Cookie xóa oes_refresh_session mới vừa cấp ở tab bên cạnh");
    }

    [Fact]
    public async Task Controller_RefreshToken_401Unauthorized_DeletesSharedRefreshSessionCookie()
    {
        var sender = new Moq.Mock<MediatR.ISender>();
        sender.Setup(x => x.Send(Moq.It.IsAny<RefreshTokenCommand>(), Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.Auth.DTOs.RefreshTokenResponse>.Failure(
                "Refresh token không tồn tại hoặc không hợp lệ."));

        var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            serviceCollection, new FakeHostEnvironment { EnvironmentName = "Testing" });
        var services = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(serviceCollection);

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Headers.Cookie = "oes_refresh_session=invalid-token";

        var controller = new global::API.Controllers.v1.AuthController(sender.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext }
        };

        var actionResult = await controller.RefreshToken(null, CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized);
        httpContext.Response.Headers.SetCookie.ToString().Should().Contain("oes_refresh_session=;");
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "OralExamination.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class FakeIssuer : IAccessTokenIssuer
    {
        public int Calls { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false, int authVersion = 1)
        {
            Calls++;
            return new IssuedAccessToken("access-token", 900);
        }
    }
}
