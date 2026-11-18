using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.API.Configuration;
using Xunit;
using MediatR;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Speech.Queries.GetTtsVoices;
using OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;
using OralExamination.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using System.Text.RegularExpressions;
using Xunit.Abstractions;
using OralExamination.Application.Features.Auth.Commands.CreateGoogleLoginChallenge;
using OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.UnitTests.Features.Controllers;

public sealed class ApiErrorContractTests(ITestOutputHelper output) : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly Mock<ISender> _sender = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:JwtSigningKey"] = "contract-tests-only-not-production-key-123456789012345" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(ErrorContractProbeController).Assembly)
            .AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorContracts();
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        builder.Services.AddSingleton(_sender.Object);
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseApiErrorContracts();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        var url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(url) };
    }

    public async Task DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }

    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)] [InlineData(404)]
    [InlineData(409)] [InlineData(410)] [InlineData(413)] [InlineData(415)]
    [InlineData(422)] [InlineData(429)]
    public async Task AnonymousControllerError_UsesProblemDetailsWireContract(int status)
    {
        var response = await _client.GetAsync($"/api/v1/contract-probe/error/{status}");
        var problem = await AssertProblem(response, status);
        problem.GetProperty("detail").GetString().Should().Be("Known business error");
    }

    [Fact]
    public async Task ExistingProblemDetails_PreservesExtensionsAndAddsTrace()
    {
        var problem = await AssertProblem(await _client.GetAsync("/api/v1/contract-probe/existing"), 409);
        problem.GetProperty("errorCode").GetString().Should().Be("CLO_IN_USE");
        problem.GetProperty("references").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UnknownRoute_Uses404ProblemDetails()
        => await AssertProblem(await _client.GetAsync("/api/v1/not-a-real-route"), 404);

    [Fact]
    public async Task WrongMethod_Uses405ProblemDetails_AndKeepsAllowHeader()
    {
        var response = await _client.PostAsync("/api/v1/contract-probe/success", null);
        await AssertProblem(response, 405);
        response.Content.Headers.Allow.Should().Contain("GET");
    }

    [Fact]
    public async Task InvalidModel_UsesValidationProblemWithErrors()
    {
        var response = await _client.GetAsync("/api/v1/contract-probe/binding?count=abc");
        var problem = await AssertProblem(response, 400);
        problem.GetProperty("errors").TryGetProperty("count", out _).Should().BeTrue();
    }

    [Fact]
    public async Task BadHttpRequest_Preserves413InsteadOfChangingTo400()
        => await AssertProblem(await _client.GetAsync("/api/v1/contract-probe/too-large"), 413);

    [Fact]
    public async Task SuccessPayload_IsNotRewritten()
    {
        var response = await _client.GetAsync("/api/v1/contract-probe/success");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("{\"message\":\"ok\"}");
    }

    [Fact]
    public async Task RealSpeechController_AnonymousLegacyErrorBecomesProblemDetails()
    {
        _sender.Setup(x => x.Send(It.IsAny<GetTtsVoicesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TtsVoiceOptionDto>>.Failure("Provider unavailable"));
        var problem = await AssertProblem(await _client.GetAsync("/api/v1/speech/voices"), 400);
        problem.GetProperty("detail").GetString().Should().Be("Provider unavailable");
    }

    [Fact]
    public async Task RealProtectedRoutes_Return401And403WithCorrectBearerHeader()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        await AssertProblem(response, 401);
        response.Headers.WwwAuthenticate.Should().Contain(x => x.Scheme == "Bearer");
        Authenticate("student");
        await AssertProblem(await _client.GetAsync("/api/v1/speech/edge-cooldown/status"), 403);
    }

    [Fact]
    public async Task RealSpeechBodyAndMediaValidation_Use400And415ProblemDetails()
    {
        Authenticate("student");
        await AssertProblem(await _client.PostAsJsonAsync("/api/v1/speech/tts", new { text = "" }), 400);
        await AssertProblem(await _client.PostAsync("/api/v1/speech/tts", new StringContent("{" , System.Text.Encoding.UTF8, "application/json")), 400);
        await AssertProblem(await _client.PostAsync("/api/v1/speech/tts", new StringContent("text")), 415);
    }

    [Fact]
    public async Task RealCloDelete_ConcurrentReferenceViolationIs409Not400()
    {
        Authenticate("department_head");
        _sender.Setup(x => x.Send(It.IsAny<DeleteCourseCloCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("race", new PostgresException("referenced", "ERROR", "ERROR", "23503",
                constraintName: "FK_practice_questions_course_clos_clo_id")));
        var problem = await AssertProblem(await _client.DeleteAsync($"/api/v1/courses/{Guid.NewGuid()}/clos/{Guid.NewGuid()}"), 409);
        problem.GetProperty("errorCode").GetString().Should().Be("CLO_IN_USE");
    }

    [Fact]
    public async Task LegacyError_PreservesTypeAndValidationErrors()
    {
        var problem = await AssertProblem(await _client.GetAsync("/api/v1/contract-probe/legacy-validation"), 422);
        problem.GetProperty("type").GetString().Should().Be("https://example.test/errors/validation");
        problem.GetProperty("errors").GetProperty("name")[0].GetString().Should().Be("Required");
    }

    [Theory]
    [InlineData("locked", 403)] [InlineData("quota", 429)]
    public async Task DomainFailures_KeepBusinessStatusAndProblemShape(string failure, int status)
        => await AssertProblem(await _client.GetAsync($"/api/v1/contract-probe/domain/{failure}"), status);

    [Theory]
    [InlineData("student", "GET", "/api/v1/official-exams/sessions")]
    [InlineData("proctor", "GET", "/api/v1/appeals")]
    [InlineData("student", "GET", "/api/v1/questions")]
    [InlineData("student", "GET", "/api/v1/admin/users")]
    public async Task CriticalControllerAuthorization_Produces403ProblemDetails(string role, string method, string path)
    {
        Authenticate(role);
        await AssertProblem(await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)), 403);
    }

    [Fact]
    public async Task EveryProtectedProductionRoute_RejectsAnonymousHttpWith401ProblemDetails()
    {
        // Discover the real MVC routes, including aliases, so new protected routes
        // enter this contract check automatically. This proves the auth boundary,
        // not all business outcomes behind each route.
        var routes = ProductionEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => new
                {
                    Method = method,
                    Path = ConcretePath(endpoint.RoutePattern.RawText!),
                    Controller = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerName
                }))
            .DistinctBy(route => (route.Method, route.Path))
            .ToArray();
        routes.Should().NotBeEmpty();
        foreach (var route in routes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(route.Method), route.Path);
            using var response = await _client.SendAsync(request);
            ((int)response.StatusCode).Should().Be(401, $"{route.Method} {route.Path} requires authentication");
            await AssertProblem(response, 401);
            response.Headers.WwwAuthenticate.Should().Contain(header => header.Scheme == "Bearer");
        }
        output.WriteLine($"AUTH CONTRACT: {routes.Length}/{routes.Length} protected method/path pairs; {routes.Select(route => route.Controller).Distinct().Count()} controllers; all 401 ProblemDetails + Bearer.");
    }

    [Fact]
    public async Task EveryRoleRestrictedProductionRoute_RejectsExcludedRolesWith403ProblemDetails()
    {
        string[] roles = ["student", "lecturer", "department_head", "proctor", "examination_officer", "admin"];
        var checkedPairs = 0;
        var checkedRoutes = 0;
        foreach (var endpoint in ProductionEndpoints().Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            var roleGroups = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Where(data => !string.IsNullOrWhiteSpace(data.Roles))
                .Select(data => data.Roles!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToArray();
            if (roleGroups.Length == 0) continue; // Ownership-only rules are tested in their feature tests.
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                checkedRoutes++;
                foreach (var role in roles.Where(role => roleGroups.Any(group => !group.Contains(role))))
                {
                    Authenticate(role);
                    using var request = new HttpRequestMessage(new HttpMethod(method), ConcretePath(endpoint.RoutePattern.RawText!));
                    using var response = await _client.SendAsync(request);
                    ((int)response.StatusCode).Should().Be(403, $"{role} cannot use {method} {request.RequestUri}");
                    await AssertProblem(response, 403);
                    checkedPairs++;
                }
            }
        }
        checkedPairs.Should().BeGreaterThan(0);
        output.WriteLine($"FORBIDDEN CONTRACT: {checkedPairs}/{checkedPairs} excluded role/route pairs across {checkedRoutes} role-restricted method/path pairs; six-role inventory; all 403 ProblemDetails.");
    }

    [Fact]
    public void AnonymousProductionActionInventory_MustMatchExplicitContractCases()
    {
        var actions = ProductionEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
                || endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!)
            .Select(action => $"{action.ControllerName}.{action.ActionName}")
            .Distinct().ToArray();
        // Adding another public action must also add a representative error-contract case.
        actions.Should().BeEquivalentTo(new[]
        {
            "Auth.GoogleChallenge", "Auth.GoogleLogin", "Auth.RefreshToken", "Auth.SignOut", "Auth.PasswordLogin",
            "Speech.GetVoices", "Verification.VerifyGradeCertificate"
        });
        output.WriteLine($"PUBLIC CONTRACT INVENTORY: {actions.Length} public actions; body-binding, speech failure, challenge quota and certificate-not-found cases cover this list.");
    }

    [Fact]
    public async Task PublicCertificateRoute_NotFoundUsesProblemDetails()
    {
        _sender.Setup(sender => sender.Send(It.IsAny<VerifyGradeCertificateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GradeCertificateVerificationDto>.Failure("Certificate not found"));
        await AssertProblem(await _client.GetAsync("/api/v1/verification/grades/unknown-certificate"), 404);
    }

    [Fact]
    public async Task PublicGoogleChallenge_QuotaFailureUsesProblemDetails()
    {
        _sender.Setup(sender => sender.Send(It.IsAny<CreateGoogleLoginChallengeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OralExamination.Domain.Exceptions.QuotaExceededException("Challenge quota exceeded"));
        await AssertProblem(await _client.PostAsync("/api/v1/auth/google-challenge", null), 429);
    }

    [Theory]
    [InlineData("password-login")]
    [InlineData("google-login")]
    [InlineData("refresh-token")]
    [InlineData("sign-out")]
    public async Task PublicAuthBodyRoutes_MalformedJsonHasValidationProblemDetails(string action)
    {
        using var response = await _client.PostAsync($"/api/v1/auth/{action}",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        var problem = await AssertProblem(response, 400);
        problem.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
    }

    private IEnumerable<RouteEndpoint> ProductionEndpoints() =>
        _app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?
                .ControllerTypeInfo.Assembly == typeof(AuthController).Assembly);

    [Theory]
    [InlineData("/api/v1/appeals")]
    [InlineData("/api/v1/student/appeals")]
    public async Task AppealWindowConflict_Returns409ProblemDetails(string path)
    {
        Authenticate("student");
        _sender.Setup(sender => sender.Send(It.IsAny<OralExamination.Application.Features.Appeals.Commands.CreateAppeal.CreateAppealCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OralExamination.Application.Features.Appeals.DTOs.AppealResponseDto>.Failure("CONFLICT: Appeal window expired"));
        var response = await _client.PostAsJsonAsync(path, new { ticketId = Guid.NewGuid(), reason = "Please review my grade." });
        var problem = await AssertProblem(response, 409);
        problem.GetProperty("detail").GetString().Should().Contain("Appeal window expired");
    }

    [Theory]
    [InlineData("/api/v1/student/official-exams/{0}/acknowledge-grade")]
    [InlineData("/api/v1/official-exams/tickets/{0}/acknowledge")]
    public async Task AcknowledgementConflict_Returns409ProblemDetails(string template)
    {
        Authenticate("student");
        _sender.Setup(sender => sender.Send(It.IsAny<OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade.AcknowledgeOfficialGradeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OralExamination.Application.Features.Student.DTOs.AcknowledgeOfficialGradeResponseDto>.Failure("CONFLICT: Active appeal"));
        await AssertProblem(await _client.PostAsync(string.Format(template, Guid.NewGuid()), null), 409);
    }

    private static string ConcretePath(string pattern) => "/" + Regex.Replace(pattern.TrimStart('/'), @"\{([^{}]+)\}", match =>
    {
        var parameter = match.Groups[1].Value;
        if (parameter.Contains(":int") || parameter.Contains(":long")) return "1";
        if (parameter.Contains(":bool")) return "false";
        return parameter.Contains(":guid") || parameter.Split(':')[0].EndsWith("id", StringComparison.OrdinalIgnoreCase)
            ? "11111111-1111-1111-1111-111111111125" : "contract-test";
    });

    private void Authenticate(string role)
    {
        var userId = Guid.NewGuid();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.Users.Add(new User
            {
                Id = userId,
                Email = $"{userId:N}@example.test",
                FullName = "Contract User",
                Role = role,
                IsActive = true,
                AuthVersion = 1
            });
            db.SaveChanges();
        }

        var issuer = new JwtAccessTokenIssuer(_app.Configuration);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.Issue(userId, "wire@example.test", "Test", role, authVersion: 1).Token);
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, int status)
    {
        ((int)response.StatusCode).Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        body.GetProperty("type").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("status").GetInt32().Should().Be(status);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("errorCode").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("instance").GetString().Should().StartWith("/api/");
        return body;
    }
}

[ApiController]
[Route("api/v1/contract-probe")]
public sealed class ErrorContractProbeController : ControllerBase
{
    [HttpGet("error/{status:int}")]
    public IActionResult Error(int status) => StatusCode(status, new { error = "Known business error" });
    [HttpGet("existing")]
    public IActionResult Existing()
    {
        var problem = new ProblemDetails { Status = 409, Detail = "CLO đang được dùng" };
        problem.Extensions["errorCode"] = "CLO_IN_USE";
        problem.Extensions["references"] = 2;
        return Conflict(problem);
    }
    [HttpGet("success")] public IActionResult Success() => Ok(new { message = "ok" });
    [HttpGet("binding")] public IActionResult Binding([FromQuery] int count) => Ok(count);
    [HttpGet("too-large")] public IActionResult TooLarge() => throw new Microsoft.AspNetCore.Http.BadHttpRequestException("Body too large", 413);
    [HttpGet("legacy-validation")] public IActionResult LegacyValidation() => UnprocessableEntity(new { type = "https://example.test/errors/validation", detail = "Invalid", errors = new { name = new[] { "Required" } } });
    [HttpGet("domain/{failure}")] public IActionResult DomainFailure(string failure) => failure == "locked"
        ? throw new OralExamination.Domain.Exceptions.OneWayLockException("Locked")
        : throw new OralExamination.Domain.Exceptions.QuotaExceededException("Quota exceeded");
}
