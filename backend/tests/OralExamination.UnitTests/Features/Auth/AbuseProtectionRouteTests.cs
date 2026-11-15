using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.API.Configuration;
using OralExamination.Application;
using OralExamination.Infrastructure.Authentication;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

// Real HTTP middleware/JWT/CQRS; isolated host, no provider calls or production Program startup.
public sealed class AbuseProtectionRouteTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = "isolated-abuse-test-key-not-production-123456789012345",
            ["Security:Abuse:LoginPermits"] = "2", ["Security:Abuse:SpeechPermits"] = "2",
            ["Security:Abuse:UploadPermits"] = "2", ["Security:Abuse:AiPermits"] = "2",
            ["Security:Abuse:MaxUploadBytes"] = "1024", ["Security:Abuse:ExpensiveRequestSeconds"] = "1"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddApplication();
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddApiErrorContracts();
        builder.Services.AddApiAbuseProtection(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AbuseDiagnosticsController).Assembly);
        _app = builder.Build();
        // Model an Internet peer, not the framework's trusted loopback proxy.
        _app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10"); return next(context); });
        _app.UseForwardedHeaders();
        _app.UseRouting();
        _app.UseApiErrorContracts();
        _app.UseAuthentication();
        _app.UseApiAbuseProtection();
        _app.UseAuthorization();
        _app.MapPost("/api/v1/auth/login-probe", () => Results.Ok());
        _app.MapPost("/api/v1/speech/probe", () => Results.Ok());
        _app.MapGet("/api/v1/generate-probe", () => Results.Ok());
        _app.MapPost("/api/v1/upload-probe", async (HttpContext context) => { await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted); return Results.Ok(); });
        _app.MapGet("/api/v1/speech/slow", async (HttpContext context) => { await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted); return Results.Ok(); }).WithRequestTimeout("expensive");
        _app.MapControllers();
        await _app.StartAsync();
        var url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(url) };
    }

    public async Task DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }

    [Fact]
    public async Task UntrustedForwardedIpRotation_CannotBypassLogin429()
    {
        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login-probe");
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{i + 1}");
            var response = await _client.SendAsync(request);
            if (i < 2) response.StatusCode.Should().Be(HttpStatusCode.OK);
            else
            {
                await AssertProblem(response, 429, "RATE_LIMITED");
                response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
            }
        }
    }

    [Fact]
    public async Task AuthenticatedUserBudgets_AreIndependentOnSameIp()
    {
        var alice = Guid.NewGuid(); var bob = Guid.NewGuid();
        Authenticate(alice);
        (await _client.GetAsync("/api/v1/generate-probe")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/v1/generate-probe")).StatusCode.Should().Be(HttpStatusCode.OK);
        Authenticate(bob);
        (await _client.GetAsync("/api/v1/generate-probe")).StatusCode.Should().Be(HttpStatusCode.OK);
        Authenticate(alice);
        await AssertProblem(await _client.GetAsync("/api/v1/generate-probe"), 429, "RATE_LIMITED");
    }

    [Fact]
    public async Task RouteCategories_DoNotShareSpeechAndAiBudget()
    {
        for (var i = 0; i < 2; i++) (await _client.PostAsync("/api/v1/speech/probe", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertProblem(await _client.PostAsync("/api/v1/speech/probe", null), 429, "RATE_LIMITED");
        (await _client.GetAsync("/api/v1/generate-probe")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OversizedUpload_IsRejectedBeforeAction()
        => await AssertProblem(await _client.PostAsync("/api/v1/upload-probe", new ByteArrayContent(new byte[1025])), 413, "PAYLOAD_TOO_LARGE");

    [Fact]
    public async Task ExpensiveRequest_UsesConfiguredTimeoutAnd408Problem()
        => await AssertProblem(await _client.GetAsync("/api/v1/speech/slow"), 408, "REQUEST_TIMEOUT");

    [Fact]
    public async Task Metrics_AreAdminOnlyAndExposeActualRejectionsWithoutIdentity()
    {
        await AssertProblem(await _client.GetAsync("/api/v1/diagnostics/abuse"), 401, "UNAUTHORIZED");
        Authenticate(Guid.NewGuid());
        await AssertProblem(await _client.GetAsync("/api/v1/diagnostics/abuse"), 403, "FORBIDDEN");
        for (var i = 0; i < 3; i++) await _client.GetAsync("/api/v1/generate-probe");
        Authenticate(Guid.NewGuid(), "admin");
        var response = await _client.GetAsync("/api/v1/diagnostics/abuse");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("rejected").GetInt64().Should().Be(1);
        json.GetProperty("scope").GetString().Should().Be("api-process");
        json.EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo("requests", "rejected", "loginLockouts", "scope");
    }

    private void Authenticate(Guid user, string role = "student")
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtAccessTokenIssuer(_app.Configuration).Issue(user, "test@example.test", "Test", role).Token);

    private static async Task AssertProblem(HttpResponseMessage response, int status, string code)
    {
        ((int)response.StatusCode).Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("status").GetInt32().Should().Be(status);
        json.GetProperty("errorCode").GetString().Should().Be(code);
        json.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
