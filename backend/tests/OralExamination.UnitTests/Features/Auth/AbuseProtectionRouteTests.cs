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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.API.Configuration;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
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
            ["Security:Abuse:LoginPermits"] = "2", ["Security:Abuse:RefreshPermits"] = "3",
            ["Security:Abuse:SpeechPermits"] = "2", ["Security:Abuse:UploadPermits"] = "2",
            ["Security:Abuse:AiPermits"] = "2", ["Security:Abuse:SubmitPermits"] = "3",
            ["Security:Abuse:MaxUploadBytes"] = "1024", ["Security:Abuse:ExpensiveRequestSeconds"] = "1",
            ["Security:TrustedNetworks:0"] = "172.18.0.0/16"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddApplication();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddApiErrorContracts();
        builder.Services.AddApiAbuseProtection(builder.Configuration);
        builder.Services.AddOralExamRateLimiting(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AbuseDiagnosticsController).Assembly);
        _app = builder.Build();
        // Model an Internet peer by default, or a simulated peer IP when header is supplied in test.
        _app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Peer-Ip", out var peerIp) && IPAddress.TryParse(peerIp, out var parsedPeer))
                context.Connection.RemoteIpAddress = parsedPeer;
            else
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            return next(context);
        });
        _app.UseForwardedHeaders();
        _app.UseRouting();
        _app.UseApiErrorContracts();
        _app.UseAuthentication();
        _app.UseApiAbuseProtection();
        _app.UseAuthorization();
        _app.MapPost("/api/v1/auth/login-probe", () => Results.Ok());
        _app.MapPost("/api/v1/auth/probe/refresh-token", () => Results.Ok());
        _app.MapPost("/api/v1/auth/probe/sign-out", () => Results.Ok());
        _app.MapPost("/api/v1/speech/probe", () => Results.Ok());
        _app.MapGet("/api/v1/generate-probe", () => Results.Ok());
        _app.MapPost("/api/v1/practice/submit-probe", () => Results.Ok()).RequireAuthorization();
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

    [Fact]
    public async Task Concurrent120Students_BehindSameNginxBridgeAndCampusNat_AllSucceedOnSubmitWithout429()
    {
        var students = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid()).ToArray();
        var tokens = new Dictionary<Guid, string>(students.Length);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var issuer = new JwtAccessTokenIssuer(_app.Configuration);
            foreach (var studentId in students)
            {
                db.Users.Add(new User
                {
                    Id = studentId,
                    Email = $"{studentId:N}@fpt.edu.vn",
                    FullName = "Student Concurrency",
                    Role = "student",
                    IsActive = true,
                    AuthVersion = 1
                });
                tokens[studentId] = issuer.Issue(studentId, $"{studentId:N}@fpt.edu.vn", "Student Concurrency", "student", authVersion: 1).Token;
            }
            await db.SaveChangesAsync();
        }

        var submitTasks = students.Select(async studentId =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/practice/submit-probe");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[studentId]);
            // All 120 requests arrive through Docker Nginx container 172.18.0.4 and share 1 campus NAT IP 118.69.10.20.
            request.Headers.Add("X-Test-Peer-Ip", "172.18.0.4");
            request.Headers.Add("X-Forwarded-For", "118.69.10.20");
            return await _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(submitTasks);
        responses.Should().HaveCount(120);
        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);

        // Verify per-user submit limit (SubmitPermits = 3 in test fixture) still enforces 429 on 4th submit by same student
        var singleStudentToken = tokens[students[0]];
        for (var extra = 0; extra < 2; extra++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/practice/submit-probe");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", singleStudentToken);
            (await _client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var fourthReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/practice/submit-probe");
        fourthReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", singleStudentToken);
        await AssertProblem(await _client.SendAsync(fourthReq), 429, "RATE_LIMITED");
    }

    [Fact]
    public async Task ExhaustedLoginBucket_DoesNotBlockSignOutOrRefreshToken()
    {
        for (var i = 0; i < 2; i++)
            (await _client.PostAsync("/api/v1/auth/login-probe", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertProblem(await _client.PostAsync("/api/v1/auth/login-probe", null), 429, "RATE_LIMITED");

        (await _client.PostAsync("/api/v1/auth/probe/sign-out", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsync("/api/v1/auth/probe/refresh-token", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "M4-Item10: POST /hubs/in-class-shift/negotiate dùng mặc định 360 permits trên cùng Campus NAT và chặn #361 bằng 429 RATE_LIMITED")]
    public async Task HubNegotiate_Default360PermitsOnSameCampusNat_Allows360AndRejects361With429()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = "isolated-hub-negotiate-test-key-not-production-12345",
            ["Security:TrustedNetworks:0"] = "172.18.0.0/16"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddApiErrorContracts();
        builder.Services.AddApiAbuseProtection(builder.Configuration);
        builder.Services.AddOralExamRateLimiting(builder.Configuration);

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.4");
            return next(context);
        });
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseApiErrorContracts();
        app.UseAuthentication();
        app.UseApiAbuseProtection();
        app.UseAuthorization();
        app.MapPost("/hubs/in-class-shift/negotiate", () => Results.Ok());
        await app.StartAsync();

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };

        for (var i = 0; i < 360; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/hubs/in-class-shift/negotiate");
            req.Headers.Add("X-Forwarded-For", "118.69.10.20");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"hub negotiate permit #{i + 1} of 360 should succeed");
        }

        using var exceededReq = new HttpRequestMessage(HttpMethod.Post, "/hubs/in-class-shift/negotiate");
        exceededReq.Headers.Add("X-Forwarded-For", "118.69.10.20");
        var exceededResp = await client.SendAsync(exceededReq);
        await AssertProblem(exceededResp, 429, "RATE_LIMITED");
    }

    private void Authenticate(Guid user, string role = "student")
    {
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var existing = db.Users.Find(user);
            if (existing is null)
            {
                db.Users.Add(new User
                {
                    Id = user,
                    Email = $"{user:N}@example.test",
                    FullName = "Test User",
                    Role = role,
                    IsActive = true,
                    AuthVersion = 1
                });
                db.SaveChanges();
            }
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtAccessTokenIssuer(_app.Configuration).Issue(user, "test@example.test", "Test", role, authVersion: 1).Token);
    }

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
