using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public sealed class MockExamHttpPipelineRbacTests : IDisposable
{
    private const string SigningKey = "SuperSecretKeyForMockRbacPipelineMustBeAtLeast32BytesLong!";
    private const string Issuer = "OralExamMockTestIssuer";
    private const string Audience = "OralExamMockTestAudience";
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Dictionary<string, User> _users = new();

    public MockExamHttpPipelineRbacTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = SigningKey,
            ["Authentication:JwtIssuer"] = Issuer,
            ["Authentication:JwtAudience"] = Audience,
            ["Authentication:AccessTokenMinutes"] = "60"
        }).Build();
        var dbName = Guid.NewGuid().ToString();
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddSingleton(new Mock<IGradingQueueChannel>().Object);
                services.AddControllers().AddApplicationPart(typeof(MockExamsController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        _server = new TestServer(builder);
        _client = _server.CreateClient();

        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        foreach (var role in new[] { "student", "lecturer", "department_head", "admin", "proctor", "examination_officer" })
        {
            var user = new User
            {
                Email = role + "@fpt.edu.vn", FullName = role, Role = role, IsActive = true, AuthVersion = 1
            };
            _users.Add(role, user);
            db.Users.Add(user);
        }
        db.SaveChanges();
    }

    private string JwtFor(string role)
    {
        var user = _users[role];
        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, role),
                new Claim("role", role),
                new Claim("auth_version", user.AuthVersion.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256Signature)
        });
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpStatusCode> SendAsync(string method, string route, string? role)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (route.EndsWith("/answers", StringComparison.Ordinal))
            request.Content = new MultipartFormDataContent();
        else if (method == "POST")
            request.Content = new StringContent("{\"courseId\":\"" + _courseId + "\"}", Encoding.UTF8, "application/json");
        if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtFor(role));
        using var response = await _client.SendAsync(request);
        return response.StatusCode;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("lecturer", HttpStatusCode.Forbidden)]
    [InlineData("department_head", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.Forbidden)]
    [InlineData("proctor", HttpStatusCode.Forbidden)]
    [InlineData("examination_officer", HttpStatusCode.Forbidden)]
    public async Task ExamActions_EnforceStudentRoleInRealPipeline(string? role, HttpStatusCode expected)
    {
        var routes = new (string Method, string Route)[]
        {
            ("POST", "/api/v1/mock-exams/sessions"),
            ("POST", $"/api/v1/mock-exams/sessions/{_sessionId}/answers"),
            ("POST", $"/api/v1/mock-exams/sessions/{_sessionId}/submit"),
            ("POST", $"/api/v1/mock-exams/sessions/{_sessionId}/pause"),
            ("POST", $"/api/v1/mock-exams/sessions/{_sessionId}/resume"),
            ("GET", "/api/v1/mock-exams/history"),
            ("GET", $"/api/v1/mock-exams/history/{_sessionId}")
        };
        foreach (var (method, route) in routes)
        {
            (await SendAsync(method, route, role)).Should().Be(expected, $"{method} {route} với role {role ?? "anonymous"}");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }
}
