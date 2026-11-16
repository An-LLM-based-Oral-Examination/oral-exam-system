using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Controllers.v1;
using FluentAssertions;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OralExamination.API.Configuration;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

// Real HTTP/cookies/controllers/MediatR/nonce/validator policy; only Google's signature network boundary is stubbed.
public sealed class GoogleOAuthRouteTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private const string UserEmail = "oauth.qa@fpt.edu.vn";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Authentication:JwtSigningKey"] = "qa-oauth-only-not-a-production-secret-123456789012345",
            ["Authentication:GoogleClientId"] = "qa-client"
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var database = Guid.NewGuid().ToString();
        builder.Services.AddApplication();
        builder.Services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.Replace(ServiceDescriptor.Singleton<IGoogleTokenSignatureVerifier, SignatureStub>());
        builder.Services.AddApiErrorContracts();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        _app = builder.Build();
        _app.UseApiErrorContracts();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        var url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(url) };
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        db.Users.Add(new User { Email = UserEmail, FullName = "Provisioned QA", Role = "student", IsActive = true, MustChangePassword = true });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }

    [Fact]
    public async Task SignedCredential_WithoutBrowserChallenge_CannotLogin()
    {
        await AssertError(await Login(new string('n', 43)), 401);
        using var scope = _app.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<OralExamDbContext>().RefreshTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Challenge_HasHttpOnlyStrictHostOnlyCookieAndNoStore()
    {
        var response = await _client.PostAsync("/api/v1/auth/google-challenge", null);
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        cookie.Should().Contain("httponly").And.Contain("samesite=strict").And.Contain("path=/api/v1/auth");
        cookie.Should().NotContain("domain=");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("nonce").GetString().Should().HaveLength(43);
        body.GetProperty("expiresIn").GetInt32().Should().Be(300);
    }

    [Fact]
    public async Task ValidGoogleExchange_PreservesDatabaseFlag_AndReplayIsRejected()
    {
        var nonce = await Challenge();
        var login = await Login(nonce);
        login.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("user").GetProperty("fullName").GetString().Should().Be("Provisioned QA");
        await AssertError(await Login(nonce), 401);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        (await db.RefreshTokens.CountAsync()).Should().Be(1);
        (await db.Users.SingleAsync()).MustChangePassword.Should().BeTrue();
        (await db.AuditLogs.AnyAsync(row => row.Action == "GOOGLE_LOGIN_FAILED")).Should().BeTrue();
        json.TryGetProperty("refreshToken", out _).Should().BeFalse("refresh token chỉ được lưu trong HttpOnly cookie");
        var rotated = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { });
        rotated.EnsureSuccessStatusCode();
        var after = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync()).RootElement;
        after.TryGetProperty("refreshToken", out _).Should().BeFalse();
        db.ChangeTracker.Clear();
        (await db.RefreshTokens.CountAsync(row => row.RevokedAt != null)).Should().Be(1);
    }

    [Fact]
    public async Task LoginCookie_RestoresAnotherTab_AndSignOutClearsBrowserSession()
    {
        var login = await Login(await Challenge());
        login.EnsureSuccessStatusCode();

        var loginCookie = login.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("oes_refresh_session=", StringComparison.OrdinalIgnoreCase))
            .ToLowerInvariant();
        loginCookie.Should().Contain("httponly")
            .And.Contain("samesite=strict")
            .And.Contain("path=/api/v1/auth");
        loginCookie.Should().NotContain("domain=");

        var restored = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { });
        restored.EnsureSuccessStatusCode();
        var restoredBody = JsonDocument.Parse(await restored.Content.ReadAsStringAsync()).RootElement;
        restoredBody.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        restored.Headers.GetValues("Set-Cookie")
            .Should().Contain(value => value.StartsWith("oes_refresh_session=", StringComparison.OrdinalIgnoreCase));

        var signOut = await _client.PostAsJsonAsync("/api/v1/auth/sign-out", new { });
        signOut.EnsureSuccessStatusCode();
        signOut.Headers.GetValues("Set-Cookie")
            .Should().Contain(value =>
                value.StartsWith("oes_refresh_session=", StringComparison.OrdinalIgnoreCase)
                && (value.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("expires=", StringComparison.OrdinalIgnoreCase)));

        var afterSignOut = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new { });
        afterSignOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MismatchedSignedNonce_AndOldNonceWithNewCookie_AreRejected()
    {
        var old = await Challenge();
        await Challenge();
        await AssertError(await Login(old), 401);
    }

    [Theory]
    [InlineData("locked", 403)] [InlineData("invalid-signature", 401)]
    public async Task RejectedLogin_IsAuditedWithoutCredentialsOrNewTokens(string kind, int status)
    {
        if (kind != "invalid-signature")
        {
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var user = await db.Users.SingleAsync();
            if (kind == "unknown") db.Users.Remove(user); else user.IsActive = false;
            await db.SaveChangesAsync();
        }
        var nonce = await Challenge();
        var token = kind == "invalid-signature" ? "invalid-signature" : nonce;
        await AssertError(await Login(token), status);
        using var verify = _app.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<OralExamDbContext>();
        (await context.RefreshTokens.CountAsync()).Should().Be(0);
        (await context.Users.CountAsync()).Should().Be(kind == "unknown" ? 0 : 1);
        var audit = await context.AuditLogs.SingleAsync();
        audit.Action.Should().Be("GOOGLE_LOGIN_FAILED");
        audit.NewValues.Should().NotContain(nonce).And.NotContain("invalid-signature").And.NotContain(UserEmail);
    }

    [Theory]
    [InlineData(UserEmail)]
    [InlineData("new.qa@gmail.com")]
    public async Task NewGoogleStudent_IsCreatedAsStudent_IgnoresRequestedAdminRole_AndDoesNotEnroll(string email)
    {
        using (var setup = _app.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.RemoveRange(db.Users);
            await db.SaveChangesAsync();
        }
        ((SignatureStub)_app.Services.GetRequiredService<IGoogleTokenSignatureVerifier>()).Email = email;
        var response = await Login(await Challenge());
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("student");
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var student = await context.Users.SingleAsync();
        student.Email.Should().Be(email);
        student.Role.Should().Be("student");
        student.MustChangePassword.Should().BeFalse();
        student.PasswordHash.Should().BeNull();
        (await context.RefreshTokens.CountAsync()).Should().Be(1);
        (await context.ClassEnrollments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentExchange_ConsumesChallengeExactlyOnce()
    {
        var nonce = await Challenge();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Login(nonce)));
        results.Count(result => result.IsSuccessStatusCode).Should().Be(1);
        results.Count(result => result.StatusCode == HttpStatusCode.Unauthorized).Should().Be(3);
    }

    [Theory]
    [InlineData("/api/v1/auth/login")][InlineData("/api/v1/auth/register")][InlineData("/api/v1/auth/google-callback")]
    public async Task LegacyAndPublicRegistrationRoutes_AreNotAvailable(string path)
        => await AssertError(await _client.PostAsJsonAsync(path, new { code = "not-supported", role = "admin" }), 404);

    private Task<HttpResponseMessage> Login(string token) => _client.PostAsJsonAsync("/api/v1/auth/google-login", new { idToken = token, role = "admin" });
    private async Task<string> Challenge()
    {
        var response = await _client.PostAsync("/api/v1/auth/google-challenge", null);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("nonce").GetString()!;
    }
    private static async Task AssertError(HttpResponseMessage response, int status)
    {
        ((int)response.StatusCode).Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("status").GetInt32().Should().Be(status);
        json.GetProperty("errorCode").GetString().Should().NotBeNullOrWhiteSpace();
    }
    private sealed class SignatureStub : IGoogleTokenSignatureVerifier
    {
        public string Email { get; set; } = UserEmail;
        public Task<GoogleJsonWebSignature.Payload> VerifyAsync(string token, string audience, CancellationToken cancellationToken)
        {
            if (token == "invalid-signature") throw new InvalidJwtException("Rejected by test signature boundary");
            return Task.FromResult(new GoogleJsonWebSignature.Payload { Audience = audience, Issuer = "https://accounts.google.com",
                Email = Email, EmailVerified = true, HostedDomain = Email.EndsWith("@gmail.com") ? null : "fpt.edu.vn", Name = "Untrusted display", Subject = "qa-subject",
                Nonce = token, ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() });
        }
    }
}
