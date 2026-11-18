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

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class OfficialExamFourStepHttpPipelineRbacTests : IDisposable
{
    private const string SigningKey = "OfficialExamFourStepRbacPipelineSigningKeyMinimum32Bytes!!";
    private const string Issuer = "OralExamOfficialRbacIssuer";
    private const string Audience = "OralExamOfficialRbacAudience";

    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly Dictionary<string, User> _users = new();
    private readonly User _otherHead;
    private readonly Semester _semester;
    private readonly Course _ownedCourse;
    private readonly OfficialExamSession _session;

    public OfficialExamFourStepHttpPipelineRbacTests()
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
                services.AddControllers().AddApplicationPart(typeof(OfficialExamsController).Assembly);
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

        foreach (var role in UserRole.All)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"{role}@fpt.edu.vn",
                FullName = $"Test {role}",
                Role = role,
                IsActive = true,
                AuthVersion = 1
            };
            _users[role] = user;
            db.Users.Add(user);
        }

        _otherHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "other_head@fpt.edu.vn",
            FullName = "Other Head",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            AuthVersion = 1
        };
        db.Users.Add(_otherHead);

        _semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            IsActive = true
        };

        _ownedCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "DBI202",
            Name = "Databases",
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            SemesterId = _semester.Id,
            IsActive = true
        };

        _session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = _ownedCourse.Id,
            SemesterId = _semester.Id,
            Title = "Kỳ thi FA26 - DBI202",
            ExamDate = new DateOnly(2026, 12, 15),
            Status = "scheduled"
        };

        db.Semesters.Add(_semester);
        db.AcademicPrograms.Add(program);
        db.Courses.Add(_ownedCourse);
        db.OfficialExamSessions.Add(_session);
        db.SaveChanges();
    }

    private string CreateJwt(User user)
    {
        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("role", user.Role),
                new Claim("auth_version", user.AuthVersion.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpResponseMessage> SendRawAsync(string method, string route, User? actor, object? jsonBody = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");
        }
        else if (method == "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (actor is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(actor));
        }

        return await _client.SendAsync(request);
    }

    [Theory(DisplayName = "MF04-RBAC-01: Anonymous trả 401; student, proctor, lecturer, department_head bị chặn 403 trên 4 endpoints điều hành Khảo thí")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(UserRole.Student, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Proctor, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Lecturer, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.DepartmentHead, HttpStatusCode.Forbidden)]
    public async Task OperationalEndpoints_BlockNonOfficerAndNonAdminRoles(string? role, HttpStatusCode expected)
    {
        var actor = role is null ? null : _users[role];
        var routes = new (string Method, string Route)[]
        {
            ("GET", $"/api/v1/official-exams/semesters/{_semester.Id}/eligible-courses"),
            ("POST", $"/api/v1/official-exams/semesters/{_semester.Id}/sessions/batch"),
            ("POST", $"/api/v1/official-exams/sessions/{_session.Id}/shifts/batch"),
            ("POST", $"/api/v1/official-exams/sessions/{_session.Id}/auto-allocate-students")
        };

        foreach (var (method, route) in routes)
        {
            using var response = await SendRawAsync(method, route, actor);
            response.StatusCode.Should().Be(expected, $"{method} {route} cho role {role ?? "anonymous"}");
        }
    }

    [Fact(DisplayName = "MF04-RBAC-02: Enrollment-estimate cho phép Khảo thí, Admin và Trưởng BM đúng môn; chặn 403 Trưởng BM môn khác và các role khác")]
    public async Task EnrollmentEstimate_EnforcesRoleAndDepartmentHeadCourseOwnership()
    {
        var estimateRoute = $"/api/v1/official-exams/sessions/{_session.Id}/enrollment-estimate";

        using (var officerResp = await SendRawAsync("GET", estimateRoute, _users[UserRole.ExaminationOfficer]))
        {
            officerResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var adminResp = await SendRawAsync("GET", estimateRoute, _users[UserRole.Admin]))
        {
            adminResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var owningHeadResp = await SendRawAsync("GET", estimateRoute, _users[UserRole.DepartmentHead]))
        {
            owningHeadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var otherHeadResp = await SendRawAsync("GET", estimateRoute, _otherHead))
        {
            otherHeadResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        foreach (var blockedRole in new[] { UserRole.Student, UserRole.Proctor, UserRole.Lecturer })
        {
            using var resp = await SendRawAsync("GET", estimateRoute, _users[blockedRole]);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact(DisplayName = "MF04-HTTP-CONTRACT: Chuỗi HTTP JSON camelCase trả đúng mã 200 / 201 / 409 / 422 chuẩn RFC 7807 ProblemDetails")]
    public async Task FullHttpContract_ReturnsExpectedStatusCodes_AndCamelCaseJson()
    {
        var officer = _users[UserRole.ExaminationOfficer];

        // 1. GET eligible-courses -> 200 OK camelCase
        using var eligibleResp = await SendRawAsync("GET", $"/api/v1/official-exams/semesters/{_semester.Id}/eligible-courses", officer);
        eligibleResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var eligibleJson = await eligibleResp.Content.ReadAsStringAsync();
        eligibleJson.Should().Contain("\"courseCode\":\"DBI202\"").And.Contain("\"maxStudentsPerRoom\":24");

        // 2. POST shifts/batch -> 201 Created
        using var batchShiftsResp = await SendRawAsync(
            "POST",
            $"/api/v1/official-exams/sessions/{_session.Id}/shifts/batch",
            officer,
            new
            {
                shiftGroupName = "Ca 1",
                startTime = "2026-12-15T00:30:00Z",
                endTime = "2026-12-15T02:30:00Z",
                rooms = new[]
                {
                    new { roomLab = "LAB-301", maxSeats = 24 },
                    new { roomLab = "LAB-302", maxSeats = 24 }
                }
            });
        batchShiftsResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var batchShiftsJson = await batchShiftsResp.Content.ReadAsStringAsync();
        batchShiftsJson.Should().Contain("\"createdRoomCount\":2").And.Contain("\"totalCapacityCreated\":48");

        // 3. POST shifts/batch trùng phòng cùng giờ -> 409 Conflict RFC 7807
        using var conflictShiftResp = await SendRawAsync(
            "POST",
            $"/api/v1/official-exams/sessions/{_session.Id}/shifts/batch",
            officer,
            new
            {
                shiftGroupName = "Ca Trùng",
                startTime = "2026-12-15T01:00:00Z",
                endTime = "2026-12-15T03:00:00Z",
                rooms = new[] { new { roomLab = "LAB-301", maxSeats = 24 } }
            });
        conflictShiftResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. POST auto-allocate-students khi chưa có SV enrolled -> 422 Unprocessable Entity RFC 7807
        using var emptyAllocResp = await SendRawAsync(
            "POST",
            $"/api/v1/official-exams/sessions/{_session.Id}/auto-allocate-students",
            officer,
            new { assignHandedOverExamSets = false, dryRun = false });
        emptyAllocResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }
}
