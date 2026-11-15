using System.Net;
using System.Net.Http.Headers;
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
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Authorization;

// Real HTTP/JWT/controller/MediatR coverage for the six-role Task 09 boundary.
// The database is isolated EF InMemory; no production service or database is used.
public sealed class Task09AuthorizationRouteTests : IAsyncLifetime
{
    private const string SigningKey = "task09-route-signing-key-not-production-123456789";
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly Dictionary<string, Guid> _actors = new(StringComparer.OrdinalIgnoreCase);

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
        builder.Services.AddScoped<IApplicationDbContext>(services =>
            services.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AppealsController).Assembly);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();

        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };

        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        foreach (var role in new[]
                 {
                     "student", "lecturer", "department_head",
                     "examination_officer", "proctor", "admin"
                 })
        {
            var actor = new User
            {
                Email = $"task09-{role}@example.test",
                FullName = $"Task 09 {role}",
                Role = role,
                IsActive = true
            };
            db.Users.Add(actor);
            _actors[role] = actor.Id;
        }

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [InlineData("student", HttpStatusCode.Forbidden)]
    [InlineData("lecturer", HttpStatusCode.Forbidden)]
    [InlineData("department_head", HttpStatusCode.Forbidden)]
    [InlineData("examination_officer", HttpStatusCode.OK)]
    [InlineData("proctor", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.Forbidden)]
    public async Task AppealAllocationProposal_RealRouteEnforcesSixRoleMatrix(
        string role,
        HttpStatusCode expected)
    {
        Authenticate(_actors[role], role);

        var response = await _client.GetAsync(
            "/api/v1/examination-officer/appeals/allocation-proposal");

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ExaminationOfficerAliases_UseTheSameAuthorizationBoundary()
    {
        Authenticate(_actors["examination_officer"], "examination_officer");

        (await _client.GetAsync("/api/v1/appeals/allocation-proposal"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/api/v1/examination-officer/appeals/propose-allocation"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgedOfficerClaim_ForDatabaseStudent_IsDeniedByHandler()
    {
        Authenticate(_actors["student"], "examination_officer");

        var response = await _client.GetAsync(
            "/api/v1/examination-officer/appeals/allocation-proposal");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void Authenticate(Guid actorId, string role)
    {
        var token = new JwtAccessTokenIssuer(_app.Configuration)
            .Issue(actorId, "task09@example.test", "Task 09", role).Token;
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
