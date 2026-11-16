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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
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

    public static IEnumerable<object[]> ShiftMutationRoleCases()
    {
        foreach (var method in new[] { "PUT", "DELETE" })
        foreach (var official in new[] { true, false })
        foreach (var role in new[] { "student", "lecturer", "department_head", "examination_officer", "proctor", "admin" })
        {
            var allowed = official ? role == "examination_officer" : role is "lecturer" or "department_head" or "admin";
            yield return new object[] { method, official, role, allowed ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden };
        }
    }

    [Theory]
    [MemberData(nameof(ShiftMutationRoleCases))]
    public async Task ShiftMutations_RealRoutesEnforceRoleMatrix(string method, bool official, string role, HttpStatusCode expected)
    {
        Authenticate(_actors[role], role);
        using var request = ShiftRequest(method, official, Guid.NewGuid());
        using var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    public async Task ShiftMutations_AnonymousRequestIsUnauthorized(string method, bool official)
    {
        using var request = ShiftRequest(method, official, Guid.NewGuid());
        using var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteOfficialShift_RealHttpReturnsSpecificConflictAndPreservesTicket()
    {
        var shift = new RealExamSessionShift { SessionId = Guid.NewGuid(), ShiftName = "Future", RoomLab = "Lab",
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var ticket = new StudentExamTicket { ShiftId = shift.Id, StudentId = _actors["student"], SeatNumber = 1 };
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.AddRange(shift, ticket);
            await db.SaveChangesAsync();
        }
        Authenticate(_actors["examination_officer"], "examination_officer");
        using var response = await _client.DeleteAsync($"/api/v1/official-exams/shifts/{shift.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("SHIFT_HAS_TICKETS");
        body.GetProperty("detail").GetString().Should().Be(ShiftMutationErrors.HasTickets);
        using var verifyScope = _app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        (await verify.StudentExamTickets.AnyAsync(x => x.Id == ticket.Id)).Should().BeTrue();
        (await verify.RealExamSessionShifts.AnyAsync(x => x.Id == shift.Id)).Should().BeTrue();
    }

    private static HttpRequestMessage ShiftRequest(string method, bool official, Guid id)
    {
        var prefix = official ? "official-exams/shifts" : "in-class-shifts";
        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/{prefix}/{id}");
        if (method == "PUT") request.Content = JsonContent.Create(new
        {
            shiftName = "Future shift", roomLab = "Lab", startTime = DateTime.UtcNow.AddDays(1),
            endTime = DateTime.UtcNow.AddDays(1).AddHours(1), proctorUserId = Guid.NewGuid(), examinerUserId = Guid.NewGuid()
        });
        return request;
    }

    [Theory]
    [InlineData("student", HttpStatusCode.Forbidden)]
    [InlineData("lecturer", HttpStatusCode.Forbidden)]
    [InlineData("department_head", HttpStatusCode.OK)]
    [InlineData("examination_officer", HttpStatusCode.Forbidden)]
    [InlineData("proctor", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task MyApprovedQuestions_EnforcesRoleMatrix(string role, HttpStatusCode expected)
    {
        Authenticate(_actors[role], role);
        (await _client.GetAsync("/api/v1/questions/my-approved")).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task MyApprovedQuestions_RejectsAnonymousForgedInactiveAndUnknownActors()
    {
        (await _client.GetAsync("/api/v1/questions/my-approved")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Authenticate(_actors["lecturer"], "department_head");
        (await _client.GetAsync("/api/v1/questions/my-approved")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Authenticate(Guid.NewGuid(), "department_head");
        (await _client.GetAsync("/api/v1/questions/my-approved")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            (await db.Users.FindAsync(_actors["department_head"]))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        Authenticate(_actors["department_head"], "department_head");
        (await _client.GetAsync("/api/v1/questions/my-approved")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MyApprovedQuestions_IgnoresSpoofedApproverAndStatus_AndReturnsPersistedTime()
    {
        var time = DateTime.UtcNow.AddHours(-1);
        var mine = Guid.NewGuid();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var semester = new Semester { Code = "FA26", Name = "Fall", StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) };
            var course = new Course { Code = "PRN", Name = "Course", Semester = semester };
            var rubric = new Rubric { Course = course, Name = "Rubric" };
            db.AddRange(
                new ExamQuestion { Id = mine, Course = course, Rubric = rubric, Title = "Mine", Content = "Question",
                    ApprovalStatus = "APPROVED", ApprovedBy = _actors["department_head"], ApprovedAt = time },
                new ExamQuestion { Course = course, Rubric = rubric, Title = "Other", Content = "Question",
                    ApprovalStatus = "APPROVED", ApprovedBy = _actors["admin"], ApprovedAt = time },
                new PracticeQuestion { Course = course, Rubric = rubric, Title = "Rejected", Content = "Question",
                    ApprovalStatus = "REJECTED", ApprovedBy = _actors["department_head"], ApprovedAt = time });
            await db.SaveChangesAsync();
        }
        Authenticate(_actors["department_head"], "department_head");
        var response = await _client.GetAsync($"/api/v1/questions/my-approved?approvedBy={_actors["admin"]}&approvalStatus=REJECTED");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
        var item = body.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().Should().Be(mine);
        item.GetProperty("approvedAt").GetDateTime().Should().Be(time);
        item.GetProperty("usageScope").GetString().Should().Be("exam");
    }

    private void Authenticate(Guid actorId, string role)
    {
        var token = new JwtAccessTokenIssuer(_app.Configuration)
            .Issue(actorId, "task09@example.test", "Task 09", role).Token;
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
