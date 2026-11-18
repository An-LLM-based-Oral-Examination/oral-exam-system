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
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

// Real HTTP/controller/JWT/handler pipeline. DB remains isolated EF InMemory, not PostgreSQL.
public sealed class LecturerAssignmentRouteTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private Guid _headId;
    private Guid _offeringId;
    private Guid _lecturerId;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:JwtSigningKey"] = "test-assignment-signing-key-not-production-1234567890" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddApplication();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(CourseOfferingsController).Assembly);
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var head = new User { Email = "head@example.test", FullName = "Head", Role = "department_head", IsActive = true };
        var lecturer = new User { Email = "lecturer@example.test", FullName = "Lecturer One", Role = "lecturer", IsActive = true };
        var semester = new Semester { Code = "FA26", Name = "Fall" };
        var program = new AcademicProgram { Code = "SE", Name = "SE", DepartmentHeadUserId = head.Id };
        var course = new Course { Code = "PRN212", Name = "Programming", AcademicProgram = program, Semester = semester };
        var offering = new CourseOffering { Course = course, Semester = semester };
        db.AddRange(head, lecturer, semester, program, course, offering);
        await db.SaveChangesAsync();
        _headId = head.Id;
        _lecturerId = lecturer.Id;
        _offeringId = offering.Id;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [InlineData("student", HttpStatusCode.Forbidden)]
    [InlineData("lecturer", HttpStatusCode.Forbidden)]
    [InlineData("department_head", HttpStatusCode.OK)]
    [InlineData("examination_officer", HttpStatusCode.Forbidden)]
    [InlineData("proctor", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.Forbidden)]
    public async Task Directory_RealRouteEnforcesSixRoleMatrix(string role, HttpStatusCode expected)
    {
        var userId = role == "department_head" ? _headId : await AddUser(role);
        Authenticate(userId, role);
        var response = await _client.GetAsync($"/api/v1/course-offerings/{_offeringId}/lecturer-directory?search=lecturer");
        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetArrayLength().Should().Be(1);
            json.RootElement[0].EnumerateObject().Select(x => x.Name).Should().BeEquivalentTo("id", "fullName", "email");
        }
    }

    [Fact]
    public async Task OtherHead_CannotSearchOrSyncAnotherOwnersOffering()
    {
        var other = await AddUser("department_head");
        Authenticate(other, "department_head");
        (await _client.GetAsync($"/api/v1/course-offerings/{_offeringId}/lecturer-directory?search=lecturer")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsJsonAsync($"/api/v1/course-offerings/{_offeringId}/lecturers", new { lecturerUserIds = new[] { _lecturerId } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/v1/course-offerings/{_offeringId}/lecturers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CannotWriteAssignmentsThroughNewOrLegacyRoute()
    {
        var admin = await AddUser("admin");
        Authenticate(admin, "admin");
        (await _client.PutAsJsonAsync($"/api/v1/course-offerings/{_offeringId}/lecturers", new { lecturerUserIds = new[] { _lecturerId } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var courseId = (await db.CourseOfferings.SingleAsync()).CourseId;
        (await _client.PostAsJsonAsync($"/api/v1/courses/{courseId}/lecturers", new { lecturerUserIds = new[] { _lecturerId } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_AddAndRemoveReflectInLecturersOwnOfferings()
    {
        Authenticate(_headId, "department_head");
        (await _client.PutAsJsonAsync($"/api/v1/course-offerings/{_offeringId}/lecturers", new { lecturerUserIds = new[] { _lecturerId, _lecturerId } })).StatusCode.Should().Be(HttpStatusCode.OK);
        Authenticate(_lecturerId, "lecturer");
        using var assigned = JsonDocument.Parse(await _client.GetStringAsync("/api/v1/lecturer/course-offerings"));
        assigned.RootElement.GetArrayLength().Should().Be(1);
        Authenticate(_headId, "department_head");
        (await _client.PutAsJsonAsync($"/api/v1/course-offerings/{_offeringId}/lecturers", new { lecturerUserIds = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.OK);
        Authenticate(_lecturerId, "lecturer");
        using var removed = JsonDocument.Parse(await _client.GetStringAsync("/api/v1/lecturer/course-offerings"));
        removed.RootElement.GetArrayLength().Should().Be(0);
    }

    private void Authenticate(Guid id, string role) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
        new JwtAccessTokenIssuer(_app.Configuration).Issue(id, "test@example.test", "Test", role).Token);

    private async Task<Guid> AddUser(string role)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var user = new User { Email = $"{Guid.NewGuid()}@example.test", FullName = "Other", Role = role, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
