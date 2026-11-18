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
using Moq;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

/// <summary>
/// T8A-F010: Kiểm thử toàn bộ HTTP Pipeline thực tế với TestServer, Authentication/Authorization Middleware
/// và JWT Access Token thật. Chứng minh 100% các endpoint MF-01 Practice từ chối 401 khi không đăng nhập (anonymous)
/// và từ chối 403 với 5 role cán bộ (lecturer, department_head, admin, proctor, examination_officer).
/// </summary>
public class PracticeHttpPipelineRbacTests : IDisposable
{
    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly string _signingKey = "SuperSecretKeyForPracticeRbacPipelineMustBeAtLeast32BytesLong!";
    private readonly string _issuer = "OralExamPracticeTestIssuer";
    private readonly string _audience = "OralExamPracticeTestAudience";

    private readonly User _studentUserA;
    private readonly User _studentUserB;
    private readonly User _adminUser;
    private readonly User _lecturerUser;
    private readonly User _deptHeadUser;
    private readonly User _proctorUser;
    private readonly User _officerUser;

    private readonly Guid _sampleCourseId = Guid.NewGuid();
    private readonly Guid _sessionAId = Guid.NewGuid();

    public PracticeHttpPipelineRbacTests()
    {
        var dbName = Guid.NewGuid().ToString();

        _studentUserA = new User
        {
            Id = Guid.NewGuid(),
            Email = "studentA@fpt.edu.vn",
            FullName = "Student A",
            StudentCode = "SE180001",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1
        };

        _studentUserB = new User
        {
            Id = Guid.NewGuid(),
            Email = "studentB@fpt.edu.vn",
            FullName = "Student B",
            StudentCode = "SE180002",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1
        };

        _adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Admin User",
            Role = UserRole.Admin,
            IsActive = true,
            AuthVersion = 1
        };

        _lecturerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Lecturer User",
            Role = UserRole.Lecturer,
            IsActive = true,
            AuthVersion = 1
        };

        _deptHeadUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fpt.edu.vn",
            FullName = "Head User",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            AuthVersion = 1
        };

        _proctorUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fpt.edu.vn",
            FullName = "Proctor User",
            Role = UserRole.Proctor,
            IsActive = true,
            AuthVersion = 1
        };

        _officerUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fpt.edu.vn",
            FullName = "Officer User",
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

                var queueMock = new Mock<IGradingQueueChannel>();
                services.AddSingleton(queueMock.Object);

                services.AddControllers()
                    .AddApplicationPart(typeof(PracticeController).Assembly);
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
        db.Users.AddRange(_studentUserA, _studentUserB, _adminUser, _lecturerUser, _deptHeadUser, _proctorUser, _officerUser);

        var course = new Course
        {
            Id = _sampleCourseId,
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            Credits = 3,
            IsActive = true
        };
        db.Courses.Add(course);

        db.PracticeSessions.Add(new PracticeSession
        {
            Id = _sessionAId,
            CourseId = _sampleCourseId,
            StudentId = _studentUserA.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            DifficultiesList = new List<string> { "easy", "medium" }
        });

        db.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "SessionInactivityTimeoutMinutes",
            Value = "5"
        });

        db.SaveChanges();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private string GenerateJwt(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_signingKey);
        var tokenDescriptor = new SecurityTokenDescriptor
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
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, object? body = null, string? jwt = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        if (!string.IsNullOrEmpty(jwt))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        }
        return request;
    }

    [Fact(DisplayName = "1. Anonymous: 100% các route Practice yêu cầu xác thực trả HTTP 401")]
    public async Task Anonymous_AllPracticeEndpoints_Return401Unauthorized()
    {
        var endpoints = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Post, "/api/v1/practice/sessions", new { courseId = _sampleCourseId, practiceMode = "per_question" }),
            (HttpMethod.Get, $"/api/v1/practice/sessions/{_sessionAId}", null),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/next-question", new { }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/answers", new { questionId = Guid.NewGuid(), answerText = "Sample Answer Text 123" }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/batch-answers", new { answers = new object[] { } }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/complete", null),
            (HttpMethod.Get, "/api/v1/practice/student/history", null)
        };

        foreach (var (method, url, body) in endpoints)
        {
            using var request = CreateJsonRequest(method, url, body, jwt: null);
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"Route {method} {url} không có token bắt buộc phải trả 401 Unauthorized.");
        }
    }

    [Theory(DisplayName = "2. Cấm 5 role cán bộ (lecturer, department_head, admin, proctor, examination_officer) truy cập mọi route Practice -> 403 Forbidden")]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Proctor)]
    [InlineData(UserRole.ExaminationOfficer)]
    public async Task ForbiddenRoles_AllPracticeEndpoints_Return403Forbidden(string role)
    {
        var user = role switch
        {
            UserRole.Lecturer => _lecturerUser,
            UserRole.DepartmentHead => _deptHeadUser,
            UserRole.Admin => _adminUser,
            UserRole.Proctor => _proctorUser,
            UserRole.ExaminationOfficer => _officerUser,
            _ => throw new ArgumentException()
        };

        var jwt = GenerateJwt(user);

        var endpoints = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Post, "/api/v1/practice/sessions", new { courseId = _sampleCourseId, practiceMode = "per_question" }),
            (HttpMethod.Get, $"/api/v1/practice/sessions/{_sessionAId}", null),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/next-question", new { }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/answers", new { questionId = Guid.NewGuid(), answerText = "Sample Answer Text 123" }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/batch-answers", new { answers = new object[] { } }),
            (HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/complete", null),
            (HttpMethod.Get, "/api/v1/practice/student/history", null)
        };

        foreach (var (method, url, body) in endpoints)
        {
            using var request = CreateJsonRequest(method, url, body, jwt);
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"Route {method} {url} với role '{role}' bắt buộc phải bị từ chối 403 Forbidden bởi HTTP Authorization Middleware.");
        }
    }

    [Fact(DisplayName = "3. Student Role: Vượt qua Authorization Middleware và truy cập đúng nghiệp vụ")]
    public async Task StudentRole_PassesAuthorizationMiddleware()
    {
        var jwt = GenerateJwt(_studentUserA);

        // GET /api/v1/practice/student/history
        using var historyReq = CreateJsonRequest(HttpMethod.Get, "/api/v1/practice/student/history", null, jwt);
        var historyRes = await _client.SendAsync(historyReq);
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK, "Student hợp lệ phải truy cập được lịch sử luyện tập của chính mình.");

        // GET /api/v1/practice/sessions/{sessionAId}
        using var sessionReq = CreateJsonRequest(HttpMethod.Get, $"/api/v1/practice/sessions/{_sessionAId}", null, jwt);
        var sessionRes = await _client.SendAsync(sessionReq);
        sessionRes.StatusCode.Should().Be(HttpStatusCode.OK, "Student sở hữu phiên phải đọc được thông tin phiên.");
    }

    [Fact(DisplayName = "4. BOLA Protection: Student B gửi token thao tác session của Student A bị từ chối 403 Forbidden")]
    public async Task Bola_StudentB_Accessing_StudentA_Session_Returns403Forbidden()
    {
        var jwtB = GenerateJwt(_studentUserB);

        // GET session của Student A
        using var getReq = CreateJsonRequest(HttpMethod.Get, $"/api/v1/practice/sessions/{_sessionAId}", null, jwtB);
        var getRes = await _client.SendAsync(getReq);
        getRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Student B không được phép đọc session của Student A.");

        // Complete session của Student A
        using var compReq = CreateJsonRequest(HttpMethod.Post, $"/api/v1/practice/sessions/{_sessionAId}/complete", null, jwtB);
        var compRes = await _client.SendAsync(compReq);
        compRes.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Student B không được phép kết thúc session của Student A.");
    }
}
