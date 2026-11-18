using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

/// <summary>
/// T4A-F007: Kiểm thử toàn bộ HTTP Pipeline thực tế với TestServer, Authentication/Authorization Middleware
/// và JWT Access Token thật. Chứng minh 100% các endpoint alias quản trị học thuật từ chối 401 khi không đăng nhập
/// và từ chối 403 với 5 role không phải admin (student, lecturer, department_head, proctor, examination_officer).
/// </summary>
public class AcademicAdminHttpPipelineRbacTests : IDisposable
{
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly string _signingKey = "SuperSecretKeyForAcademicRbacPipelineMustBeAtLeast32BytesLong!";
    private readonly string _issuer = "OralExamAcademicTestIssuer";
    private readonly string _audience = "OralExamAcademicTestAudience";

    private readonly User _adminUser;
    private readonly User _studentUser;
    private readonly User _lecturerUser;
    private readonly User _deptHeadUser;
    private readonly User _proctorUser;
    private readonly User _officerUser;

    private readonly Guid _sampleSemesterId = Guid.NewGuid();
    private readonly Guid _sampleCourseId = Guid.NewGuid();

    public AcademicAdminHttpPipelineRbacTests()
    {
        var dbName = Guid.NewGuid().ToString();

        _adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Admin Pipeline User",
            Role = UserRole.Admin,
            IsActive = true,
            AuthVersion = 1
        };

        _studentUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Student Pipeline User",
            StudentCode = "SE180001",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1
        };

        _lecturerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Lecturer Pipeline User",
            Role = UserRole.Lecturer,
            IsActive = true,
            AuthVersion = 1
        };

        _deptHeadUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fpt.edu.vn",
            FullName = "Head Pipeline User",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            AuthVersion = 1
        };

        _proctorUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fpt.edu.vn",
            FullName = "Proctor Pipeline User",
            Role = UserRole.Proctor,
            IsActive = true,
            AuthVersion = 1
        };

        _officerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fpt.edu.vn",
            FullName = "Officer Pipeline User",
            Role = UserRole.ExaminationOfficer,
            IsActive = true,
            AuthVersion = 1
        };

        var configDict = new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = _signingKey,
            ["Authentication:JwtIssuer"] = _issuer,
            ["Authentication:JwtAudience"] = _audience,
            ["Authentication:AccessTokenMinutes"] = "60"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddControllers()
                    .AddApplicationPart(typeof(SemestersController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

        _server = new TestServer(builder);
        _client = _server.CreateClient();

        // Seed base database
        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        db.Users.AddRange(_adminUser, _studentUser, _lecturerUser, _deptHeadUser, _proctorUser, _officerUser);

        db.Semesters.Add(new Semester
        {
            Id = _sampleSemesterId,
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        });

        db.Courses.Add(new Course
        {
            Id = _sampleCourseId,
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            Credits = 3,
            SemesterId = _sampleSemesterId,
            DepartmentHeadUserId = _deptHeadUser.Id,
            IsActive = true
        });

        db.SaveChanges();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_signingKey);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("email", user.Email),
            new("name", user.FullName),
            new("role", user.Role),
            new(ClaimTypes.Role, user.Role),
            new("auth_version", user.AuthVersion.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return request;
    }

    public static IEnumerable<object[]> GetAdminEndpointAliases()
    {
        // 1. Create Semester aliases
        yield return new object[] { HttpMethod.Post, "/api/v1/semesters", "CreateSemester" };
        yield return new object[] { HttpMethod.Post, "/api/v1/admin/semesters", "CreateSemester" };
        yield return new object[] { HttpMethod.Post, "/api/v1/academic/semesters", "CreateSemester" };

        // 2. Update Semester aliases
        yield return new object[] { HttpMethod.Put, "/api/v1/semesters/{id}", "UpdateSemester" };
        yield return new object[] { HttpMethod.Put, "/api/v1/admin/semesters/{id}", "UpdateSemester" };
        yield return new object[] { HttpMethod.Put, "/api/v1/academic/semesters/{id}", "UpdateSemester" };

        // 3. Create Course aliases
        yield return new object[] { HttpMethod.Post, "/api/v1/courses", "CreateCourse" };
        yield return new object[] { HttpMethod.Post, "/api/v1/admin/courses", "CreateCourse" };
        yield return new object[] { HttpMethod.Post, "/api/v1/academic/courses", "CreateCourse" };

        // 4. Update Course aliases
        yield return new object[] { HttpMethod.Put, "/api/v1/courses/{id}", "UpdateCourse" };
        yield return new object[] { HttpMethod.Put, "/api/v1/admin/courses/{id}", "UpdateCourse" };
        yield return new object[] { HttpMethod.Put, "/api/v1/academic/courses/{id}", "UpdateCourse" };

        // 5. Assign Department Head aliases
        yield return new object[] { HttpMethod.Post, "/api/v1/courses/{id}/assign-department-head", "AssignDepartmentHead" };
        yield return new object[] { HttpMethod.Post, "/api/v1/admin/courses/{id}/assign-department-head", "AssignDepartmentHead" };
        yield return new object[] { HttpMethod.Post, "/api/v1/academic/courses/{id}/assign-department-head", "AssignDepartmentHead" };
    }

    private string ResolveUrl(string template)
    {
        return template
            .Replace("{id}", template.Contains("semesters") ? _sampleSemesterId.ToString() : _sampleCourseId.ToString());
    }

    private object CreatePayloadForAction(string actionName)
    {
        return actionName switch
        {
            "CreateSemester" or "UpdateSemester" => new
            {
                Code = "SP27",
                Name = "Spring 2027",
                StartDate = "2027-01-01",
                EndDate = "2027-04-30",
                IsActive = true
            },
            "CreateCourse" or "UpdateCourse" => new
            {
                Code = "SWD392",
                Name = "Software Architecture",
                Credits = 3,
                SemesterId = _sampleSemesterId,
                DepartmentHeadUserId = _deptHeadUser.Id,
                IsActive = true
            },
            "AssignDepartmentHead" => new
            {
                DepartmentHeadUserId = _deptHeadUser.Id
            },
            _ => new { }
        };
    }

    [Theory(DisplayName = "T4A-F007: Mọi endpoint alias từ chối 401 Unauthorized khi không có JWT token")]
    [MemberData(nameof(GetAdminEndpointAliases))]
    public async Task AdminEndpoint_WithoutToken_Returns_401Unauthorized(HttpMethod method, string urlTemplate, string actionName)
    {
        var url = ResolveUrl(urlTemplate);
        var payload = CreatePayloadForAction(actionName);

        using var request = CreateRequest(method, url, payload);
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"Endpoint alias '{method} {urlTemplate}' must require authentication");
    }

    [Theory(DisplayName = "T4A-F007: Mọi endpoint alias từ chối 403 Forbidden cho cả 5 role không phải admin")]
    [MemberData(nameof(GetAdminEndpointAliases))]
    public async Task AdminEndpoint_WithNonAdminRoles_Returns_403Forbidden(HttpMethod method, string urlTemplate, string actionName)
    {
        var url = ResolveUrl(urlTemplate);
        var payload = CreatePayloadForAction(actionName);

        var nonAdminUsers = new[] { _studentUser, _lecturerUser, _deptHeadUser, _proctorUser, _officerUser };

        foreach (var user in nonAdminUsers)
        {
            var token = GenerateJwtToken(user);

            using var request = CreateRequest(method, url, payload);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"Endpoint alias '{method} {urlTemplate}' must return 403 Forbidden for role '{user.Role}'");
        }
    }

    [Theory(DisplayName = "T4A-F007: Mọi endpoint alias cho phép role Admin vượt qua Authentication & Authorization Gate")]
    [MemberData(nameof(GetAdminEndpointAliases))]
    public async Task AdminEndpoint_WithAdminRole_Passes_AuthGates(HttpMethod method, string urlTemplate, string actionName)
    {
        var url = ResolveUrl(urlTemplate);
        var payload = CreatePayloadForAction(actionName);
        var token = GenerateJwtToken(_adminUser);

        using var request = CreateRequest(method, url, payload);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            $"Admin token must not receive 401 Unauthorized on '{method} {urlTemplate}'");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            $"Admin token must not receive 403 Forbidden on '{method} {urlTemplate}'");
    }
}
