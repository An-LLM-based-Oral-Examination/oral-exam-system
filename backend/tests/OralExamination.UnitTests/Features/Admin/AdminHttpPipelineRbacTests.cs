using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.API.Hubs;
using OralExamination.API.Middlewares;
using OralExamination.Application;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.UnitTests.Features.Admin;

public sealed class AdminHttpPipelineRbacTests : IDisposable
{
    private const string SigningKey = "AdminHttpPipelineRbacTestsSigningKeyMinimum32Bytes!!";
    private const string Issuer = "OralExamAdminRbacIssuer";
    private const string Audience = "OralExamAdminRbacAudience";

    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly Dictionary<string, User> _users = new();
    private readonly User _otherHead;
    private readonly User _mustChangeAdmin;
    private readonly User _targetStudent;

    private readonly AcademicProgram _program;
    private readonly Semester _semester;
    private readonly Course _course;
    private readonly CourseClo _unreferencedClo;
    private readonly CourseClo _referencedClo;
    private readonly Notification _notification;
    private readonly DlqEntity _practiceDlq;
    private readonly DlqEntity _mockDlq;
    private readonly DlqEntity _inClassDlq;
    private readonly DlqEntity _officialDlq;

    private readonly Guid _questionId = Guid.NewGuid();
    private readonly Guid _rubricId = Guid.NewGuid();
    private readonly Guid _courseOfferingId = Guid.NewGuid();

    public AdminHttpPipelineRbacTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = SigningKey,
            ["Authentication:JwtIssuer"] = Issuer,
            ["Authentication:JwtAudience"] = Audience,
            ["Authentication:AccessTokenMinutes"] = "60"
        }).Build();

        var dbName = Guid.NewGuid().ToString();

        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        gradingQueueMock.Setup(q => q.Count).Returns(3);
        gradingQueueMock.Setup(q => q.Capacity).Returns(1000);
        gradingQueueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var abuseReaderMock = new Mock<IAbuseMetricsReader>();
        abuseReaderMock.Setup(r => r.Snapshot(It.IsAny<long>()))
            .Returns((long lockouts) => new AbuseMetricsSnapshot(200, 4, lockouts, "api-process"));

        var loginGuardMock = new Mock<ILoginAttemptGuard>();
        loginGuardMock.Setup(g => g.Lockouts).Returns(2);

        var metricsTrackerMock = new Mock<IRuntimeMetricsTracker>();
        metricsTrackerMock.Setup(m => m.GetSnapshot()).Returns(new RuntimeMetricsSnapshot(
            TotalRequests: 200,
            ServerErrors5xx: 0,
            ServerErrorRatePercent: 0.0,
            AverageLatencyMs: 12.0,
            P95LatencyMs: 25.0,
            Uptime: TimeSpan.FromHours(1),
            WorkingSetMb: 120.0,
            GcTotalMemoryMb: 40.0,
            ThreadCount: 8));

        var hubContextMock = new Mock<IHubContext<InClassShiftHub, IInClassShiftClient>>();

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddApiErrorContracts();
                services.AddSingleton(gradingQueueMock.Object);
                services.AddSingleton(abuseReaderMock.Object);
                services.AddSingleton(loginGuardMock.Object);
                services.AddSingleton(metricsTrackerMock.Object);
                services.AddSingleton(hubContextMock.Object);
                services.AddControllers().AddApplicationPart(typeof(AdminController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseApiErrorContracts();
                app.UseAuthentication();
                app.UseMiddleware<MustChangePasswordMiddleware>();
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
                Email = $"{role}@admin-rbac.test",
                FullName = $"Test {role}",
                StudentCode = role == UserRole.Student ? "SE180900" : null,
                Role = role,
                IsActive = true,
                AuthVersion = 1,
                MustChangePassword = false
            };
            _users[role] = user;
            db.Users.Add(user);
        }

        _otherHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "other_head@admin-rbac.test",
            FullName = "Other Department Head",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            AuthVersion = 1
        };
        _mustChangeAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "must_change_admin@admin-rbac.test",
            FullName = "Must Change Admin",
            Role = UserRole.Admin,
            IsActive = true,
            AuthVersion = 1,
            MustChangePassword = true
        };
        _targetStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "target_student@admin-rbac.test",
            FullName = "Target Student",
            StudentCode = "SE180901",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1
        };
        db.Users.AddRange(_otherHead, _mustChangeAdmin, _targetStudent);

        _program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            IsActive = true
        };
        _semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        _course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform Backend",
            Credits = 3,
            SemesterId = _semester.Id,
            AcademicProgramId = _program.Id,
            DepartmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = false,
            MaxMockExamsPerDay = 3,
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            IsActive = true
        };
        _courseOfferingId = Guid.NewGuid();
        var offering = new CourseOffering
        {
            Id = _courseOfferingId,
            CourseId = _course.Id,
            SemesterId = _semester.Id,
            IsEnabled = true
        };
        _unreferencedClo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            CloCode = "CLO1",
            Description = "Unreferenced CLO",
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1,
            IsActive = true
        };
        _referencedClo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            CloCode = "CLO2",
            Description = "Referenced CLO",
            BloomLevel = BloomLevel.Apply,
            OrderIndex = 2,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = _rubricId,
            CourseId = _course.Id,
            Name = "Default Rubric",
            TotalMaxScore = 10m,
            IsActive = true
        };
        var practiceQ = new PracticeQuestion
        {
            Id = _questionId,
            CourseId = _course.Id,
            CloId = _referencedClo.Id,
            RubricId = rubric.Id,
            Title = "Referenced Practice Question",
            Content = "Explain Clean Architecture",
            SampleAnswer = new string('A', 60),
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        _notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _users[UserRole.Student].Id,
            Title = "Initial Notification",
            Message = "Seeded notification",
            Type = "SYSTEM_ANNOUNCEMENT",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.SystemConfigs.AddRange(
            new SystemConfig { Key = "MinMixedPracticeQuestions", Value = "3", Description = "Min mixed" },
            new SystemConfig { Key = "MaxMixedPracticeQuestions", Value = "10", Description = "Max mixed" },
            new SystemConfig { Key = "TranscriptBufferSeconds", Value = "60", Description = "Buffer" });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _users[UserRole.Admin].Id,
            Action = "SEED_INIT",
            EntityName = "system",
            CreatedAt = DateTime.UtcNow
        });

        _practiceDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(new GradingTask
            {
                AnswerId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                QuestionId = _questionId,
                StudentId = _users[UserRole.Student].Id,
                AnswerText = "Clean architecture separates domain from infrastructure."
            }),
            ErrorMessage = "Practice transient failure"
        };
        _mockDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "MockExamGrading",
            FlowType = "MOCK_EXAM",
            ManagedByRole = "admin",
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(new { sessionId = Guid.NewGuid() }),
            ErrorMessage = "Mock transient failure"
        };
        _inClassDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(new { submissionId = Guid.NewGuid() }),
            ErrorMessage = "InClass transient failure"
        };
        _officialDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            Status = "pending",
            PayloadJson = JsonSerializer.Serialize(new { submissionId = Guid.NewGuid() }),
            ErrorMessage = "Official transient failure"
        };

        db.AcademicPrograms.Add(_program);
        db.Semesters.Add(_semester);
        db.Courses.Add(_course);
        db.CourseOfferings.Add(offering);
        db.CourseClos.AddRange(_unreferencedClo, _referencedClo);
        db.Rubrics.Add(rubric);
        db.PracticeQuestions.Add(practiceQ);
        db.Notifications.Add(_notification);
        db.DeadLetterQueues.AddRange(_practiceDlq, _mockDlq, _inClassDlq, _officialDlq);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private string IssueJwt(User user, bool includeMustChangeClaim = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("role", user.Role),
            new("auth_version", user.AuthVersion.ToString())
        };
        if (includeMustChangeClaim || user.MustChangePassword)
        {
            claims.Add(new Claim(AuthTokenDefaults.MustChangePasswordClaim, "true"));
        }

        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string route, User? actor, string? jsonBody = null, string? jwtOverride = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (route.EndsWith("/upload-image", StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new MultipartFormDataContent();
        }
        else if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }
        else if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var token = jwtOverride ?? (actor is not null ? IssueJwt(actor) : null);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    [Fact(DisplayName = "ADMIN-RBAC-01: Unauthenticated trả 401 và 5 non-admin roles bị chặn 403 trên các endpoints Admin")]
    public async Task AdminOnlyEndpoints_RejectAnonymous401_AndNonAdmin403()
    {
        var endpoints = new (string Method, string Route, string? Body)[]
        {
            ("GET", "/api/v1/admin/users", null),
            ("POST", "/api/v1/admin/users", "{}"),
            ("PUT", $"/api/v1/admin/users/{_targetStudent.Id}/status", "{}"),
            ("PUT", $"/api/v1/admin/users/{_targetStudent.Id}/role", "{}"),
            ("POST", $"/api/v1/admin/users/{_targetStudent.Id}/reset-password", "{}"),
            ("GET", "/api/v1/admin/notifications", null),
            ("POST", "/api/v1/admin/notifications", "{}"),
            ("DELETE", $"/api/v1/admin/notifications/{_notification.Id}", null),
            ("GET", "/api/v1/admin/system-configs", null),
            ("GET", "/api/v1/admin/configs", null),
            ("GET", "/api/v1/system-configs", null),
            ("PUT", "/api/v1/admin/system-configs/MinMixedPracticeQuestions", "{}"),
            ("GET", "/api/v1/admin/audit-logs", null),
            ("GET", "/api/v1/admin/diagnostics/runtime", null),
            ("GET", "/api/v1/diagnostics/abuse", null),
            ("GET", "/api/v1/admin/dlq/tasks", null),
            ("POST", $"/api/v1/admin/dlq/tasks/{_practiceDlq.Id}/replay", "{}"),
            ("POST", $"/api/v1/admin/dlq/tasks/{_practiceDlq.Id}/resolve", "{}"),
            ("PUT", $"/api/v1/semesters/{_semester.Id}/course-offerings", "{}"),
            ("PUT", $"/api/v1/academic-programs/{_program.Id}/department-head", "{}"),
            ("GET", "/api/v1/admin/courses", null)
        };

        var nonAdminRoles = new[]
        {
            UserRole.Student,
            UserRole.Lecturer,
            UserRole.DepartmentHead,
            UserRole.Proctor,
            UserRole.ExaminationOfficer
        };

        foreach (var (method, route, body) in endpoints)
        {
            using var anonResp = await SendAsync(method, route, actor: null, jsonBody: body);
            anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {route} without token");

            foreach (var role in nonAdminRoles)
            {
                using var resp = await SendAsync(method, route, _users[role], jsonBody: body);
                resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} for role {role}");
            }
        }
    }

    [Fact(DisplayName = "ADMIN-RBAC-02: MustChangePasswordMiddleware chặn Admin chưa đổi mật khẩu trên /api/v1/admin/* với 403 MUST_CHANGE_PASSWORD")]
    public async Task MustChangePasswordAdmin_IsBlockedWith403()
    {
        foreach (var route in new[] { "/api/v1/admin/users", "/api/v1/admin/system-configs" })
        {
            using var resp = await SendAsync("GET", route, _mustChangeAdmin);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var json = await resp.Content.ReadAsStringAsync();
            json.Should().Contain("MUST_CHANGE_PASSWORD");
        }
    }

    [Fact(DisplayName = "ADMIN-GROUP-01: Quản trị người dùng, MustChangePassword=true, AuthVersion revocation & chống tự khóa/hạ quyền 409")]
    public async Task AdminGroup1_UserLifecycle_And_AuthVersionRevocation()
    {
        var admin = _users[UserRole.Admin];

        // 1. List users
        using var listResp = await SendAsync("GET", "/api/v1/admin/users", admin);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Create user -> mustChangePassword = true
        using var createResp = await SendAsync("POST", "/api/v1/admin/users", admin, JsonSerializer.Serialize(new
        {
            email = "new_lecturer@fpt.edu.vn",
            fullName = "New Lecturer",
            password = "TempPassword@123",
            role = UserRole.Lecturer
        }));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        createdJson.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();
        var createdUserId = createdJson.GetProperty("id").GetGuid();

        // Issue a valid JWT for _targetStudent before mutations
        var staleJwt = IssueJwt(_targetStudent);
        using (var beforeResp = await SendAsync("GET", "/api/v1/courses", _targetStudent, jwtOverride: staleJwt))
        {
            beforeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 3. Reset password -> bumps AuthVersion and sets MustChangePassword = true -> stale JWT returns 401
        using var resetResp = await SendAsync("POST", $"/api/v1/admin/users/{_targetStudent.Id}/reset-password", admin,
            JsonSerializer.Serialize(new { temporaryPassword = "ResetPass@2026", reason = "Quên mật khẩu" }));
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resetResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();

        using (var afterResetResp = await SendAsync("GET", "/api/v1/courses", actor: null, jwtOverride: staleJwt))
        {
            afterResetResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 4. Update role -> bumps AuthVersion
        using var roleResp = await SendAsync("PUT", $"/api/v1/admin/users/{createdUserId}/role", admin,
            JsonSerializer.Serialize(new { role = UserRole.DepartmentHead }));
        roleResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await roleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString().Should().Be(UserRole.DepartmentHead);

        // 5. Lock user -> bumps AuthVersion
        using var lockResp = await SendAsync("PUT", $"/api/v1/admin/users/{createdUserId}/status", admin,
            JsonSerializer.Serialize(new { isActive = false, reason = "Tạm khóa" }));
        lockResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await lockResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean().Should().BeFalse();

        // 6. Self-lock and self-demotion -> 409 Conflict
        using var selfLockResp = await SendAsync("PUT", $"/api/v1/admin/users/{admin.Id}/status", admin,
            JsonSerializer.Serialize(new { isActive = false, reason = "Self lock" }));
        selfLockResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var selfDemoteResp = await SendAsync("PUT", $"/api/v1/admin/users/{admin.Id}/role", admin,
            JsonSerializer.Serialize(new { role = UserRole.Student }));
        selfDemoteResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "ADMIN-GROUP-02: Học kỳ (cấm xóa), Course Offerings, Academic Programs & Môn học + gán Trưởng BM")]
    public async Task AdminGroup2_SemestersNoDelete_Offerings_Programs_And_Courses()
    {
        var admin = _users[UserRole.Admin];

        // 1. Semesters have NO DELETE endpoint -> 405 Method Not Allowed
        using var delSem1 = await SendAsync("DELETE", $"/api/v1/semesters/{_semester.Id}", admin);
        delSem1.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        using var delSem2 = await SendAsync("DELETE", $"/api/v1/admin/semesters/{_semester.Id}", admin);
        delSem2.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);

        // 2. Create & Update Semester
        using var createSem = await SendAsync("POST", "/api/v1/admin/semesters", admin, JsonSerializer.Serialize(new
        {
            code = "SP27",
            name = "Spring 2027",
            startDate = "2027-01-01",
            endDate = "2027-04-30",
            isActive = true
        }));
        createSem.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdSemId = (await createSem.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var updateSem = await SendAsync("PUT", $"/api/v1/admin/semesters/{createdSemId}", admin, JsonSerializer.Serialize(new
        {
            code = "SP27",
            name = "Spring 2027 Updated",
            startDate = "2027-01-05",
            endDate = "2027-04-30",
            isActive = true
        }));
        updateSem.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Get & Sync Course Offerings
        using var getOfferings = await SendAsync("GET", $"/api/v1/semesters/{_semester.Id}/course-offerings", admin);
        getOfferings.StatusCode.Should().Be(HttpStatusCode.OK);
        using var putOfferings = await SendAsync("PUT", $"/api/v1/semesters/{_semester.Id}/course-offerings", admin,
            JsonSerializer.Serialize(new { selectedCourseIds = new[] { _course.Id } }));
        putOfferings.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Academic Programs & DepartmentHead assignment
        using var getPrograms = await SendAsync("GET", "/api/v1/academic-programs", admin);
        getPrograms.StatusCode.Should().Be(HttpStatusCode.OK);
        using var assignProgHead = await SendAsync("PUT", $"/api/v1/academic-programs/{_program.Id}/department-head", admin,
            JsonSerializer.Serialize(new { departmentHeadUserId = _otherHead.Id }));
        assignProgHead.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Create & Update Course + Assign DeptHead
        using var createCourse = await SendAsync("POST", "/api/v1/admin/courses", admin, JsonSerializer.Serialize(new
        {
            code = "SWD392",
            name = "Software Architecture",
            credits = 3,
            semesterId = _semester.Id,
            departmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            maxMockExamsPerDay = 4,
            isActive = true
        }));
        createCourse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCourseId = (await createCourse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var updateCourse = await SendAsync("PUT", $"/api/v1/admin/courses/{createdCourseId}", admin, JsonSerializer.Serialize(new
        {
            code = "SWD392",
            name = "Software Architecture & Design",
            credits = 3,
            semesterId = _semester.Id,
            departmentHeadUserId = _users[UserRole.DepartmentHead].Id,
            maxMockExamsPerDay = 6,
            isActive = true
        }));
        updateCourse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var assignCourseHead = await SendAsync("POST", $"/api/v1/admin/courses/{createdCourseId}/assign-department-head", admin,
            JsonSerializer.Serialize(new { departmentHeadUserId = _otherHead.Id }));
        assignCourseHead.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "ADMIN-GROUP-03: course_clos CRUD cho Admin & DeptHead sở hữu, chặn xóa 409 khi CLO đang tham chiếu")]
    public async Task AdminGroup3_CourseClosCrud_And_409Guard()
    {
        var admin = _users[UserRole.Admin];
        var owningHead = _users[UserRole.DepartmentHead];

        // 1. Admin & owning Head can view managed courses and CRUD unreferenced CLO
        using var managedResp = await SendAsync("GET", "/api/v1/courses/managed-for-clos", admin);
        managedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createClo = await SendAsync("POST", $"/api/v1/courses/{_course.Id}/clos", admin, JsonSerializer.Serialize(new
        {
            cloCode = "CLO3",
            description = "Design resilient APIs",
            bloomLevel = BloomLevel.Create,
            orderIndex = 3
        }));
        createClo.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCloId = (await createClo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var updateClo = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/clos/{createdCloId}", owningHead, JsonSerializer.Serialize(new
        {
            cloCode = "CLO3",
            description = "Design resilient REST APIs",
            bloomLevel = BloomLevel.Create,
            orderIndex = 3,
            isActive = true
        }));
        updateClo.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete unreferenced CLO -> 204
        using var deleteClo = await SendAsync("DELETE", $"/api/v1/courses/{_course.Id}/clos/{createdCloId}", admin);
        deleteClo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Delete referenced CLO -> 409 Conflict (CLO_IN_USE)
        using var deleteRefClo = await SendAsync("DELETE", $"/api/v1/courses/{_course.Id}/clos/{_referencedClo.Id}", admin);
        deleteRefClo.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await deleteRefClo.Content.ReadAsStringAsync()).Should().Contain("CLO_IN_USE");

        // Non-owning head and other roles -> 403 Forbidden
        using var otherHeadCreate = await SendAsync("POST", $"/api/v1/courses/{_course.Id}/clos", _otherHead, JsonSerializer.Serialize(new
        {
            cloCode = "CLO9",
            description = "Forbidden",
            bloomLevel = BloomLevel.Remember,
            orderIndex = 9
        }));
        otherHeadCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        foreach (var role in new[] { UserRole.Lecturer, UserRole.Student, UserRole.Proctor, UserRole.ExaminationOfficer })
        {
            using var resp = await SendAsync("DELETE", $"/api/v1/courses/{_course.Id}/clos/{_unreferencedClo.Id}", _users[role]);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact(DisplayName = "ADMIN-GROUP-04: MF-01 system_configs & MF-02 maxMockExamsPerDay boundary trên Courses.UpdateConfiguration")]
    public async Task AdminGroup4_SystemConfigs_And_CourseQuotaBoundary()
    {
        var admin = _users[UserRole.Admin];
        var head = _users[UserRole.DepartmentHead];

        // 1. Valid MF-01 system config update
        using var updateMin = await SendAsync("PUT", "/api/v1/admin/system-configs/MinMixedPracticeQuestions", admin,
            JsonSerializer.Serialize(new { value = "5", description = "Updated min" }));
        updateMin.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cross-field invalid: MinMixedPracticeQuestions > MaxMixedPracticeQuestions (10) -> 400
        using var invalidCross = await SendAsync("PUT", "/api/v1/admin/system-configs/MinMixedPracticeQuestions", admin,
            JsonSerializer.Serialize(new { value = "11" }));
        invalidCross.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 2. Department Head cannot set MaxMockExamsPerDay -> 403
        using var headQuota = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", head, JsonSerializer.Serialize(new
        {
            transcriptBufferSeconds = 60,
            maxFollowUpQuestions = 2,
            hasFollowUp = false,
            maxMockExamsPerDay = 5
        }));
        headQuota.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Admin can update MaxMockExamsPerDay when academic fields match existing course values -> 200
        using var adminQuotaOk = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            transcriptBufferSeconds = 60,
            maxFollowUpQuestions = 2,
            hasFollowUp = false,
            examInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            maxMockExamsPerDay = 7
        }));
        adminQuotaOk.StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminQuotaOk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("maxMockExamsPerDay").GetInt32().Should().Be(7);

        // 4. Admin attempting to alter academic fields (e.g. hasFollowUp=true or transcriptBufferSeconds=90) -> 403 Forbidden
        using var adminMutateAcademic = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            transcriptBufferSeconds = 90,
            maxFollowUpQuestions = 3,
            hasFollowUp = true,
            examInputMode = ExamInputMode.VoiceOnly,
            maxMockExamsPerDay = 7
        }));
        adminMutateAcademic.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 5. Admin calling UpdateConfiguration without MaxMockExamsPerDay -> 403 Forbidden
        using var adminNoQuota = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            transcriptBufferSeconds = 60,
            maxFollowUpQuestions = 2,
            hasFollowUp = false
        }));
        adminNoQuota.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. Admin minimal { "maxMockExamsPerDay": 5 } payload -> 200 OK
        using var adminMinimal = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            maxMockExamsPerDay = 5
        }));
        adminMinimal.StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminMinimal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("maxMockExamsPerDay").GetInt32().Should().Be(5);

        // 7. Admin with empty strings (examInputMode = "", mockExamDifficultyDistribution = "" on null DB) + reordered/minified Bloom JSON -> 200 OK
        using var adminEmptyAndReordered = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            transcriptBufferSeconds = 0,
            maxFollowUpQuestions = 0,
            examInputMode = "",
            mockExamDifficultyDistribution = "",
            mockExamBloomDistribution = "{\"Apply\":2,\"Remember\":1,\"Understand\":2}",
            maxMockExamsPerDay = 6
        }));
        adminEmptyAndReordered.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedJson = await adminEmptyAndReordered.Content.ReadFromJsonAsync<JsonElement>();
        updatedJson.GetProperty("maxMockExamsPerDay").GetInt32().Should().Be(6);
        updatedJson.GetProperty("examInputMode").GetString().Should().Be(ExamInputMode.VoiceWithTranscriptEdit);

        // 8. Admin altering Bloom distribution values semantically -> 403 Forbidden
        using var adminSemanticDiff = await SendAsync("PUT", $"/api/v1/courses/{_course.Id}/configurations", admin, JsonSerializer.Serialize(new
        {
            mockExamBloomDistribution = "{\"Remember\":2,\"Understand\":2,\"Apply\":1}",
            maxMockExamsPerDay = 6
        }));
        adminSemanticDiff.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "ADMIN-GROUP-05: Thông báo 3NF, Audit Logs, Abuse Diagnostics & Runtime Diagnostics (loại trừ OFFICIAL_EXAM)")]
    public async Task AdminGroup5_Notifications_AuditLogs_And_Diagnostics()
    {
        var admin = _users[UserRole.Admin];

        // 1. Notifications CRUD
        using var createNotif = await SendAsync("POST", "/api/v1/admin/notifications", admin, JsonSerializer.Serialize(new
        {
            targetScope = "USER",
            targetUserId = _users[UserRole.Student].Id,
            type = "SYSTEM_ANNOUNCEMENT",
            title = "Maintenance Notice",
            message = "Scheduled window tonight."
        }));
        createNotif.StatusCode.Should().Be(HttpStatusCode.Created);

        using var listNotif = await SendAsync("GET", "/api/v1/admin/notifications", admin);
        listNotif.StatusCode.Should().Be(HttpStatusCode.OK);

        using var delNotif = await SendAsync("DELETE", $"/api/v1/admin/notifications/{_notification.Id}", admin);
        delNotif.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 2. Audit logs & Abuse metrics
        using var auditResp = await SendAsync("GET", "/api/v1/admin/audit-logs", admin);
        auditResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var abuseResp = await SendAsync("GET", "/api/v1/diagnostics/abuse", admin);
        abuseResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Runtime Diagnostics excludes _officialDlq (TotalPending == 3: practice + mock + inClass)
        using var runtimeResp = await SendAsync("GET", "/api/v1/admin/diagnostics/runtime", admin);
        runtimeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var runtimeJson = await runtimeResp.Content.ReadFromJsonAsync<JsonElement>();
        var dlq = runtimeJson.GetProperty("deadLetterQueue");
        dlq.GetProperty("totalPending").GetInt32().Should().Be(3);
        dlq.GetProperty("byFlowType").GetProperty("practice").GetInt32().Should().Be(1);
        dlq.GetProperty("byFlowType").GetProperty("mockExam").GetInt32().Should().Be(1);
        dlq.GetProperty("byFlowType").GetProperty("inClassExam").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "ADMIN-GROUP-06: DLQ Admin chỉ thấy & replay/resolve MF-01/02/05; chặn 403 OFFICIAL_EXAM")]
    public async Task AdminGroup6_DlqIsolation_BlocksOfficialExam()
    {
        var admin = _users[UserRole.Admin];

        // 1. Admin list returns Practice/Mock/InClass, never OfficialExam
        using var listResp = await SendAsync("GET", "/api/v1/admin/dlq/tasks", admin);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await listResp.Content.ReadAsStringAsync();
        listJson.Should().Contain(_practiceDlq.Id.ToString());
        listJson.Should().Contain(_mockDlq.Id.ToString());
        listJson.Should().NotContain(_officialDlq.Id.ToString());

        // 2. Admin can replay Practice & resolve MockExam
        using var replayPractice = await SendAsync("POST", $"/api/v1/admin/dlq/tasks/{_practiceDlq.Id}/replay", admin);
        replayPractice.StatusCode.Should().Be(HttpStatusCode.OK);

        using var resolveMock = await SendAsync("POST", $"/api/v1/admin/dlq/tasks/{_mockDlq.Id}/resolve", admin,
            JsonSerializer.Serialize(new { resolutionNotes = "Verified manually" }));
        resolveMock.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Admin is strictly Forbidden (403) on OFFICIAL_EXAM DLQ across all routes
        var forbiddenDlqCalls = new (string Method, string Route)[]
        {
            ("GET", "/api/v1/admin/dlq/tasks?flowType=OFFICIAL_EXAM"),
            ("GET", "/api/v1/dlq/tasks?flowType=OFFICIAL_EXAM"),
            ("POST", $"/api/v1/admin/dlq/tasks/{_officialDlq.Id}/replay"),
            ("POST", $"/api/v1/admin/dlq/tasks/{_officialDlq.Id}/resolve"),
            ("POST", $"/api/v1/dlq/tasks/{_officialDlq.Id}/replay"),
            ("POST", $"/api/v1/dlq/tasks/{_officialDlq.Id}/resolve"),
            ("GET", "/api/v1/examination-officer/dlq/tasks"),
            ("POST", $"/api/v1/examination-officer/dlq/tasks/{_officialDlq.Id}/replay"),
            ("POST", $"/api/v1/examination-officer/dlq/tasks/{_officialDlq.Id}/resolve")
        };

        foreach (var (method, route) in forbiddenDlqCalls)
        {
            using var resp = await SendAsync(method, route, admin);
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} must forbid admin");
        }
    }

    public static IEnumerable<object[]> ForbiddenEndpointsForAdmin()
    {
        var sessionId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var examSetId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var reviewRequestId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var inClassShiftId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var offeringId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        // MF-04 OfficialExamsController
        yield return ["GET", "/api/v1/official-exams/sessions", null!];
        yield return ["GET", "/api/v1/official-exams/seasons", null!];
        yield return ["POST", "/api/v1/official-exams/sessions", "{}"];
        yield return ["GET", $"/api/v1/official-exams/semesters/{semesterId}/eligible-courses", null!];
        yield return ["POST", $"/api/v1/official-exams/semesters/{semesterId}/sessions/batch", "{}"];
        yield return ["GET", $"/api/v1/official-exams/sessions/{sessionId}/enrollment-estimate", null!];
        yield return ["GET", $"/api/v1/official-exams/seasons/{sessionId}/enrollment-estimate", null!];
        yield return ["PUT", $"/api/v1/official-exams/sessions/{sessionId}/follow-up-config", "{}"];
        yield return ["GET", "/api/v1/official-exams/shifts", null!];
        yield return ["POST", "/api/v1/official-exams/shifts", "{}"];
        yield return ["POST", $"/api/v1/official-exams/sessions/{sessionId}/shifts/batch", "{}"];
        yield return ["POST", $"/api/v1/official-exams/sessions/{sessionId}/auto-allocate-students", "{}"];
        yield return ["PUT", $"/api/v1/official-exams/shifts/{shiftId}", "{}"];
        yield return ["DELETE", $"/api/v1/official-exams/shifts/{shiftId}", null!];
        yield return ["POST", $"/api/v1/official-exams/shifts/{shiftId}/publish", "{}"];
        yield return ["PUT", $"/api/v1/official-exams/shifts/{shiftId}/status", "{\"status\":\"in_progress\"}"];
        yield return ["PATCH", $"/api/v1/official-exams/shifts/{shiftId}/status", "{\"status\":\"in_progress\"}"];
        yield return ["POST", $"/api/v1/official-exams/shifts/{shiftId}/distribute-tickets", "{}"];
        yield return ["POST", $"/api/v1/official-exams/shifts/{shiftId}/publish-grades", "{}"];
        yield return ["GET", $"/api/v1/official-exams/shifts/{shiftId}/audit-evidence", null!];
        yield return ["GET", $"/api/v1/audit/submissions?shiftId={shiftId}", null!];
        yield return ["GET", $"/api/v1/audit/shifts/{shiftId}/submissions", null!];
        yield return ["PUT", $"/api/v1/official-exams/tickets/{ticketId}/override-score", "{}"];
        yield return ["PUT", "/api/v1/audit/override", "{}"];
        yield return ["POST", $"/api/v1/audit/submissions/{ticketId}/override-score", "{}"];
        yield return ["POST", $"/api/v1/official-exams/tickets/{ticketId}/lock", "{}"];
        yield return ["POST", "/api/v1/audit/lock", "{}"];
        yield return ["GET", $"/api/v1/official-exams/shifts/{shiftId}/export-excel", null!];
        yield return ["GET", $"/api/v1/audit/export-excel?shiftId={shiftId}", null!];
        yield return ["GET", $"/api/v1/official-exams/shifts/{shiftId}/export-pdf", null!];
        yield return ["GET", $"/api/v1/audit/export-pdf?shiftId={shiftId}", null!];
        yield return ["POST", "/api/v1/official-exams/tickets", "{}"];
        yield return ["POST", $"/api/v1/official-exams/tickets/{ticketId}/check-in", "{}"];
        yield return ["POST", $"/api/v1/official-exams/shifts/{shiftId}/tickets/import/preview", null!];
        yield return ["POST", $"/api/v1/official-exams/shifts/{shiftId}/tickets/import/commit", null!];
        yield return ["GET", "/api/v1/official-exams/tickets/current", null!];
        yield return ["GET", "/api/v1/official-exams/tickets/my-workstation", null!];
        yield return ["POST", "/api/v1/official-exams/anti-cheat/log", "{}"];
        yield return ["GET", $"/api/v1/official-exams/shifts/{shiftId}/anti-cheat/logs", null!];
        yield return ["GET", $"/api/v1/official-exams/exam-sets?courseId={courseId}", null!];
        yield return ["POST", "/api/v1/official-exams/exam-sets", "{}"];
        yield return ["POST", $"/api/v1/official-exams/exam-sets/{examSetId}/submit-review", "{}"];
        yield return ["POST", $"/api/v1/official-exams/exam-sets/{examSetId}/approve", "{}"];
        yield return ["POST", $"/api/v1/official-exams/exam-sets/{examSetId}/hand-over", "{}"];

        // OfficialExamSubmissionsController & StudentController MF-04
        yield return ["POST", $"/api/v1/official-exams/tickets/{ticketId}/audio-upload-authorizations", "{}"];
        yield return ["PUT", $"/api/v1/official-exams/tickets/{ticketId}/audio", "{}"];
        yield return ["PUT", $"/api/v1/official-exams/tickets/{ticketId}/submission", "{}"];
        yield return ["GET", $"/api/v1/official-exams/tickets/{ticketId}/submission", null!];
        yield return ["GET", $"/api/v1/official-exams/tickets/{ticketId}/grade", null!];
        yield return ["POST", $"/api/v1/official-exams/tickets/{ticketId}/acknowledge", "{}"];

        // ProctorController
        yield return ["GET", $"/api/v1/proctor/shifts/{shiftId}/attendance", null!];
        yield return ["POST", $"/api/v1/proctor/shifts/{shiftId}/attendance", "{}"];
        yield return ["POST", $"/api/v1/proctor/tickets/{ticketId}/suspend", "{}"];
        yield return ["POST", $"/api/v1/proctor/shifts/{shiftId}/violations", "{}"];

        // AppealsController
        yield return ["POST", "/api/v1/appeals", "{}"];
        yield return ["POST", "/api/v1/student/appeals", "{}"];
        yield return ["GET", "/api/v1/appeals", null!];
        yield return ["GET", $"/api/v1/appeals/{appealId}", null!];
        yield return ["PUT", $"/api/v1/appeals/{appealId}/decision", "{}"];
        yield return ["POST", $"/api/v1/examination-officer/appeals/{appealId}/decision", "{}"];
        yield return ["POST", $"/api/v1/appeals/{appealId}/assign-lecturer", "{}"];
        yield return ["GET", "/api/v1/examination-officer/appeals/allocation-proposal", null!];
        yield return ["POST", "/api/v1/examination-officer/appeals/batch-assign", "{}"];
        yield return ["PUT", $"/api/v1/appeals/{appealId}/regrade", "{}"];

        // MF-03 QuestionsController, QuestionReviewRequestsController, RubricsController, FlmController
        yield return ["GET", "/api/v1/questions", null!];
        yield return ["GET", "/api/v1/questions/my-approved", null!];
        yield return ["GET", $"/api/v1/questions/{questionId}", null!];
        yield return ["POST", "/api/v1/questions", "{}"];
        yield return ["POST", "/api/v1/questions/draft", "{}"];
        yield return ["POST", "/api/v1/practice/questions", "{}"];
        yield return ["POST", "/api/v1/official-exams/questions", "{}"];
        yield return ["POST", "/api/v1/questions/generate-from-clo", "{}"];
        yield return ["POST", "/api/v1/questions/batch-approve", "{}"];
        yield return ["POST", "/api/v1/questions/batch-submit-review", "{}"];
        yield return ["POST", "/api/v1/questions/upload-image", null!];
        yield return ["POST", $"/api/v1/questions/{questionId}/review-decision", "{}"];
        yield return ["PUT", $"/api/v1/questions/{questionId}", "{}"];
        yield return ["PATCH", $"/api/v1/questions/{questionId}/active", "{}"];
        yield return ["DELETE", $"/api/v1/questions/{questionId}", null!];
        yield return ["GET", "/api/v1/department-head/question-review-requests", null!];
        yield return ["GET", $"/api/v1/department-head/question-review-requests/{reviewRequestId}", null!];
        yield return ["POST", $"/api/v1/department-head/question-review-requests/{reviewRequestId}/decisions", "{}"];
        yield return ["GET", "/api/v1/rubrics", null!];
        yield return ["GET", $"/api/v1/rubrics/{rubricId}", null!];
        yield return ["POST", "/api/v1/rubrics", "{}"];
        yield return ["GET", $"/api/v1/flm/courses/{courseId}/syllabus", null!];

        // MF-05 InClassShiftsController
        yield return ["GET", $"/api/v1/in-class-shifts/pool-stats?classId={classId}", null!];
        yield return ["GET", $"/api/v1/in-class-shifts/{inClassShiftId}", null!];
        yield return ["GET", $"/api/v1/in-class-shifts/by-class/{classId}", null!];
        yield return ["POST", "/api/v1/in-class-shifts", "{}"];
        yield return ["PUT", $"/api/v1/in-class-shifts/{inClassShiftId}", "{}"];
        yield return ["DELETE", $"/api/v1/in-class-shifts/{inClassShiftId}", null!];
        yield return ["POST", $"/api/v1/in-class-shifts/{inClassShiftId}/activate", "{}"];
        yield return ["POST", $"/api/v1/in-class-shifts/{inClassShiftId}/close", "{}"];
        yield return ["POST", $"/api/v1/in-class-shifts/{inClassShiftId}/finalize-grades", "{}"];
        yield return ["GET", $"/api/v1/in-class-shifts/{inClassShiftId}/current-password", null!];
        yield return ["GET", $"/api/v1/in-class-shifts/{inClassShiftId}/submissions", null!];
        yield return ["PUT", $"/api/v1/in-class-shifts/{inClassShiftId}/submissions/{submissionId}/grade", "{}"];

        // Classes, CourseOfferings, Courses.GetLecturers, LecturerController
        yield return ["POST", "/api/v1/classes", "{}"];
        yield return ["POST", "/api/v1/lecturer/classes", "{}"];
        yield return ["POST", $"/api/v1/lecturer/course-offerings/{offeringId}/classes", "{}"];
        yield return ["GET", "/api/v1/classes", null!];
        yield return ["GET", "/api/v1/lecturer/classes", null!];
        yield return ["GET", "/api/v1/classes/search", null!];
        yield return ["GET", $"/api/v1/classes/{classId}", null!];
        yield return ["POST", $"/api/v1/classes/{classId}/reset-password", "{}"];
        yield return ["POST", "/api/v1/classes/join", "{}"];
        yield return ["GET", "/api/v1/classes/my-classes", null!];
        yield return ["GET", "/api/v1/lecturer/classes/students/lookup", null!];
        yield return ["POST", $"/api/v1/classes/{classId}/students", "{}"];
        yield return ["DELETE", $"/api/v1/classes/{classId}/students/{studentId}", null!];
        yield return ["GET", $"/api/v1/course-offerings/{offeringId}/lecturers", null!];
        yield return ["PUT", $"/api/v1/course-offerings/{offeringId}/lecturers", "{}"];
        yield return ["GET", $"/api/v1/course-offerings/{offeringId}/lecturer-directory", null!];
        yield return ["GET", "/api/v1/lecturer/course-offerings", null!];
        yield return ["GET", $"/api/v1/courses/{courseId}/lecturers", null!];
        yield return ["POST", $"/api/v1/courses/{courseId}/lecturers", "{}"];
        yield return ["GET", "/api/v1/lecturer/dashboard", null!];
        yield return ["GET", "/api/v1/lecturer/courses", null!];
    }

    [Theory(DisplayName = "ADMIN-FORBIDDEN-MATRIX: Admin nhận 403 Forbidden trên toàn bộ endpoints MF-04/Appeals/Proctor/MF-03/MF-05/Classes/Offerings/Lecturer")]
    [MemberData(nameof(ForbiddenEndpointsForAdmin))]
    public async Task Admin_IsForbidden_OnAllNonAdminDomainEndpoints(string method, string route, string? jsonBody)
    {
        var admin = _users[UserRole.Admin];
        using var response = await SendAsync(method, route, admin, jsonBody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} must return 403 Forbidden for admin");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "ADMIN-UPLOAD-403: POST /api/v1/questions/upload-image trả 403 cho Admin dù Content-Type là multipart hay application/json")]
    public async Task UploadQuestionImage_Returns403ForAdmin_RegardlessOfContentType_And_415ForLecturerWithJson()
    {
        var admin = _users[UserRole.Admin];
        var lecturer = _users[UserRole.Lecturer];

        using var adminMultipartReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/questions/upload-image")
        {
            Content = new MultipartFormDataContent()
        };
        adminMultipartReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(admin));
        using var adminMultipartResp = await _client.SendAsync(adminMultipartReq);
        adminMultipartResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var adminJsonReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/questions/upload-image")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        adminJsonReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(admin));
        using var adminJsonResp = await _client.SendAsync(adminJsonReq);
        adminJsonResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var lecturerJsonReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/questions/upload-image")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        lecturerJsonReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt(lecturer));
        using var lecturerJsonResp = await _client.SendAsync(lecturerJsonReq);
        lecturerJsonResp.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact(DisplayName = "ADMIN-DLQ-CONSISTENCY: Diagnostics.TotalPending khớp 100% Admin DLQ List với bản ghi legacy & mis-tagged")]
    public async Task RuntimeDiagnostics_TotalPending_Matches_AdminDlqList_ForLegacyAndMistaggedRows()
    {
        var admin = _users[UserRole.Admin];

        using (var scope = _server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            db.DeadLetterQueues.AddRange(
                new DlqEntity
                {
                    Id = Guid.NewGuid(),
                    TaskType = "InClassGrading",
                    FlowType = "OFFICIAL_EXAM",
                    ManagedByRole = "examination_officer",
                    Status = "pending",
                    PayloadJson = "{}",
                    ErrorMessage = "Legacy InClass default flow"
                },
                new DlqEntity
                {
                    Id = Guid.NewGuid(),
                    TaskType = "OfficialExamGrading",
                    FlowType = "IN_CLASS_EXAM",
                    ManagedByRole = "admin",
                    Status = "pending",
                    PayloadJson = "{}",
                    ErrorMessage = "Mistagged OfficialExam"
                });
            await db.SaveChangesAsync();
        }

        using var diagResp = await SendAsync("GET", "/api/v1/admin/diagnostics/runtime", admin);
        diagResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var diagJson = await diagResp.Content.ReadFromJsonAsync<JsonElement>();
        var totalPending = diagJson.GetProperty("deadLetterQueue").GetProperty("totalPending").GetInt32();

        using var listResp = await SendAsync("GET", "/api/v1/admin/dlq/tasks?status=pending", admin);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listItems = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        listItems.GetArrayLength().Should().Be(totalPending);
    }

    [Fact(DisplayName = "QUESTIONS-CTRL-403: QuestionsController.GetQuestionById trả 403 khi Handler trả về FORBIDDEN")]
    public async Task QuestionsController_GetQuestionById_MapsForbiddenTo403_DirectCall()
    {
        var senderMock = new Mock<MediatR.ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById.GetQuestionByIdQuery>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(Result<OralExamination.Application.Features.QuestionBank.DTOs.QuestionDetailDto>.Failure("FORBIDDEN: Không có quyền tra cứu câu hỏi."));

        var controller = new QuestionsController(senderMock.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, _users[UserRole.Admin].Id.ToString()),
                        new Claim(ClaimTypes.Role, UserRole.Admin)
                    }, "TestAuth"))
                }
            }
        };

        var actionResult = await controller.GetQuestionById(Guid.NewGuid());
        var objectResult = actionResult.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden);
    }
}
