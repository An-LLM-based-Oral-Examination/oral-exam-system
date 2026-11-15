using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using System.Collections.Generic;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class GoogleLoginCommandHandlerTests
{
    [Theory]
    [InlineData("New.Student@gmail.com", null)]
    [InlineData("TotntSE180123@fpt.edu.vn", "SE180123")]
    public async Task Handle_VerifiedNewGoogleIdentity_ProvisionsOnlyStudentWithoutLocalPassword(string email, string? studentCode)
    {
        await using var context = CreateDbContext();
        var google = new FakeGoogle
        {
            Identity = new GoogleUserIdentity(email, "Sinh Viên Mới")
        };
        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, google, issuer);

        var result = await handler.Handle(new GoogleLoginCommand("token", AuthTestNonceStore.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Role.Should().Be(UserRole.Student);
        var user = await context.Users.SingleAsync();
        user.Email.Should().Be(email.ToLowerInvariant());
        user.StudentCode.Should().Be(studentCode);
        user.IsActive.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        user.PasswordHash.Should().BeNull();
        user.Username.Should().BeNull();
        (await context.RefreshTokens.CountAsync()).Should().Be(1);
        context.ChangeTracker.HasChanges().Should().BeFalse();
        issuer.Calls.Should().Be(1);
        issuer.Role.Should().Be(UserRole.Student);
        issuer.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NewIdentityWithWrongBrowserNonce_DoesNotProvisionUserOrIssueTokens()
    {
        await using var context = CreateDbContext();
        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, new FakeGoogle { Identity = new GoogleUserIdentity("new@fpt.edu.vn", "New") }, issuer);

        var result = await handler.Handle(new GoogleLoginCommand("valid-signed-token", new string('x', 43)), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(AuthErrors.GoogleNonceInvalid);
        (await context.Users.CountAsync()).Should().Be(0);
        (await context.RefreshTokens.CountAsync()).Should().Be(0);
        issuer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ExistingActiveUser_KeepsDatabaseRole()
    {
        await using var context = CreateDbContext();
        context.Users.Add(new User
        {
            Email = "admin@fpt.edu.vn",
            FullName = "Nguyen Quang Thanh",
            Role = UserRole.Admin,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var google = new FakeGoogle
        {
            Identity = new GoogleUserIdentity("admin@fpt.edu.vn", "Tên từ Google")
        };
        var handler = CreateHandler(context, google, new FakeIssuer());

        var result = await handler.Handle(new GoogleLoginCommand("token", AuthTestNonceStore.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.User.Role.Should().Be(UserRole.Admin);
        result.Value.User.FullName.Should().Be("Nguyen Quang Thanh");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        (await context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsFailureAndDoesNotIssueToken()
    {
        await using var context = CreateDbContext();
        context.Users.Add(new User
        {
            Email = "locked@gmail.com",
            FullName = "Tài khoản khóa",
            Role = UserRole.Student,
            IsActive = false
        });
        await context.SaveChangesAsync();
        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, new FakeGoogle
        {
            Identity = new GoogleUserIdentity("locked@gmail.com", "Tài khoản khóa")
        }, issuer);

        var result = await handler.Handle(new GoogleLoginCommand("token", AuthTestNonceStore.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("khóa");
        issuer.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ExistingUser_PreservesPasswordChangeRequirementInDatabaseAndToken(bool mustChangePassword)
    {
        await using var context = CreateDbContext();
        var user = new User
        {
            Email = "provisioned@fpt.edu.vn",
            FullName = "Sinh Viên Được Cấp",
            Role = UserRole.Student,
            IsActive = true,
            MustChangePassword = mustChangePassword
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, new FakeGoogle
        {
            Identity = new GoogleUserIdentity(user.Email, user.FullName)
        }, issuer);

        var result = await handler.Handle(new GoogleLoginCommand("valid-token", AuthTestNonceStore.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        issuer.Calls.Should().Be(1);
        issuer.MustChangePassword.Should().Be(mustChangePassword);
        context.ChangeTracker.Clear();
        var persistedUser = await context.Users.SingleAsync();
        persistedUser.MustChangePassword.Should().Be(mustChangePassword);
        (await context.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_RejectedGoogleToken_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var issuer = new FakeIssuer();
        var handler = CreateHandler(context, new FakeGoogle { Reject = true }, issuer);

        var result = await handler.Handle(new GoogleLoginCommand("bad-token", AuthTestNonceStore.Value), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Google");
        issuer.Calls.Should().Be(0);
        (await context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyIdToken_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var google = new FakeGoogle();
        var handler = CreateHandler(context, google, new FakeIssuer());

        var result = await handler.Handle(new GoogleLoginCommand("  "), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        google.Calls.Should().Be(0);
    }

    [Fact]
    public void Issue_WritesSubEmailNameRoleAndFifteenMinuteLifetime()
    {
        Dictionary<string, string?> settings = new()
        {
            ["JWT_SIGNING_KEY"] = new string('k', 48),
            ["Authentication:AccessTokenMinutes"] = "15"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var userId = Guid.NewGuid();

        var issued = new JwtAccessTokenIssuer(configuration)
            .Issue(userId, "student@gmail.com", "Sinh Viên Test", UserRole.Lecturer);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Token);

        issued.ExpiresInSeconds.Should().Be(900);
        token.Subject.Should().Be(userId.ToString());
        token.Claims.Should().Contain(c => c.Type == "email" && c.Value == "student@gmail.com");
        token.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Sinh Viên Test");
        token.Claims.Should().Contain(c => c.Type == "role" && c.Value == UserRole.Lecturer);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(AuthTokenDefaults.AccessTokenMinutes), TimeSpan.FromMinutes(1));
    }

    private static GoogleLoginCommandHandler CreateHandler(
        OralExamDbContext context,
        FakeGoogle google,
        FakeIssuer issuer)
    {
        return new GoogleLoginCommandHandler(context, google, issuer, new GoogleLoginCommandValidator(), new AuthTestNonceStore());
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private sealed class FakeGoogle : IGoogleIdTokenValidator
    {
        public GoogleUserIdentity? Identity { get; init; }
        public bool Reject { get; init; }
        public int Calls { get; private set; }

        public Task<GoogleUserIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
        {
            Calls++;
            if (Reject)
            {
                throw new GoogleIdTokenRejectedException();
            }

            return Task.FromResult(Identity! with { Nonce = AuthTestNonceStore.Value });
        }
    }

    private sealed class FakeIssuer : IAccessTokenIssuer
    {
        public int Calls { get; private set; }
        public bool MustChangePassword { get; private set; }
        public string? Role { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false)
        {
            Calls++;
            MustChangePassword = mustChangePassword;
            Role = role;
            return new IssuedAccessToken("access-token", 900);
        }
    }
}
