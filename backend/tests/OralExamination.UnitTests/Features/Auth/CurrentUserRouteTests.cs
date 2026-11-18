using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OralExamination.API.Middlewares;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

// Real HTTP routing/controller/JWT/MediatR pipeline with an isolated InMemory DB.
// No production Program startup, workers, migration reconciler or external service.
public sealed class CurrentUserRouteTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private const string SigningKey = "route-test-only-key-not-a-production-credential-123456";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = SigningKey
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddApplication();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IApplicationDbContext>(services => services.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseMiddleware<MustChangePasswordMiddleware>();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [InlineData("student")]
    [InlineData("lecturer")]
    [InlineData("department_head")]
    [InlineData("examination_officer")]
    [InlineData("proctor")]
    [InlineData("admin")]
    public async Task Me_ReturnsOnlyAuthenticatedUsersCurrentDatabaseProfile(string role)
    {
        var user = await SeedUser(role, mustChangePassword: true);
        // Deliberately forged client userId query parameter must not select another profile.
        Authenticate(user.Id, role: role, mustChangePassword: true);
        var response = await _client.GetAsync($"/api/v1/auth/me?userId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        profile.Should().Be(new CurrentUserDto(user.Id, user.Email, user.Username,
            user.FullName, user.StudentCode, role, true, true));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo("id", "email", "username", "fullName", "studentCode", "role", "isActive", "mustChangePassword");
    }

    [Fact]
    public async Task Me_Anonymous_Returns401ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        await AssertProblem(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        response.Headers.WwwAuthenticate.Should().Contain(header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("bad-token")]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    public async Task Me_InvalidOrExpiredToken_Returns401ProblemDetails(string scenario)
    {
        var token = scenario == "bad-token" ? scenario : SignToken(Guid.NewGuid(),
            scenario == "expired" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
            scenario == "wrong-key" ? new string('x', 64) : SigningKey);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AssertProblem(await _client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Me_MissingDatabaseUser_Returns401ProblemDetails()
    {
        Authenticate(Guid.NewGuid());
        await AssertProblem(await _client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Me_AccountLockedAfterTokenIssued_Returns401ProblemDetails()
    {
        var user = await SeedUser("student", isActive: false);
        Authenticate(user.Id);
        await AssertProblem(await _client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Me_EmptySubject_Returns401ProblemDetails()
    {
        Authenticate(Guid.Empty);
        await AssertProblem(await _client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    private async Task<User> SeedUser(string role, bool isActive = true, bool mustChangePassword = false)
    {
        using var scope = _app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@example.test", Username = "route-user",
            FullName = "Route test user", Role = role, IsActive = isActive,
            MustChangePassword = mustChangePassword, PasswordHash = "not-for-response"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private void Authenticate(Guid userId, string role = "student", bool mustChangePassword = false)
    {
        var issuer = new JwtAccessTokenIssuer(_app.Configuration);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.Issue(userId, "stale@example.test", "Stale name", role, mustChangePassword).Token);
    }

    private string SignToken(Guid userId, DateTime expires, string signingKey)
    {
        var token = new JwtSecurityToken(
            issuer: AuthConfiguration.JwtIssuer(_app.Configuration),
            audience: AuthConfiguration.JwtAudience(_app.Configuration),
            claims: new[] { new Claim("sub", userId.ToString()) }, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string errorCode)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be(errorCode);
        json.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
