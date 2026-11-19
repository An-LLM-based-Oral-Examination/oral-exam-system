using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using OralExamination.API.Configuration;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public sealed class ClassesControllerNoStoreHttpTests : IDisposable
{
    private const string JwtSigningKey = "ClassesControllerNoStoreHttpTestsSigningKeyMin32Bytes!!";
    private const string ClassJoinSecretKey = "ClassesControllerNoStoreClassJoinSecretMin32Bytes!!";
    private const string Issuer = "OralExamClassesTestsIssuer";
    private const string Audience = "OralExamClassesTestsAudience";

    private readonly TestServer _server;
    private readonly HttpClient _client;
    private readonly User _lecturer;
    private readonly Course _course;
    private readonly Semester _semester;
    private readonly CourseOffering _offering;
    private readonly Class _existingClass;

    public ClassesControllerNoStoreHttpTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtSigningKey"] = JwtSigningKey,
                ["Authentication:JwtIssuer"] = Issuer,
                ["Authentication:JwtAudience"] = Audience,
                ["Authentication:AccessTokenMinutes"] = "60",
                ["Security:ClassJoinPasswordKey"] = "",
                ["CLASS_JOIN_PASSWORD_KEY"] = ClassJoinSecretKey
            })
            .Build();

        var dbName = $"ClassesNoStoreHttp_{Guid.NewGuid():N}";

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddApiErrorContracts();
                services.AddAuthorization();
                services.AddControllers().AddApplicationPart(typeof(ClassesController).Assembly);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseApiErrorContracts();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
        _client = _server.CreateClient();

        using var scope = _server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var passwordProtector = scope.ServiceProvider.GetRequiredService<IClassJoinPasswordProtector>();

        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.se@nostore.test",
            FullName = "Head SE",
            Role = UserRole.DepartmentHead,
            IsActive = true,
            AuthVersion = 1
        };
        _lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer.se@nostore.test",
            FullName = "Lecturer SE",
            Role = UserRole.Lecturer,
            IsActive = true,
            AuthVersion = 1
        };
        var program = new AcademicProgram
        {
            Id = Guid.NewGuid(),
            Code = "SE",
            Name = "Software Engineering",
            DepartmentHeadUserId = head.Id,
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
            Name = "Cross-Platform .NET",
            AcademicProgramId = program.Id,
            DepartmentHeadUserId = head.Id,
            SemesterId = _semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _offering = new CourseOffering
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            SemesterId = _semester.Id,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var assignment = new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = _course.Id,
            CourseOfferingId = _offering.Id,
            LecturerUserId = _lecturer.Id,
            AssignedByUserId = head.Id,
            SemesterId = _semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _existingClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-HTTP",
            Name = "Lớp HTTP Cache Test",
            JoinCode = "HTTP1801",
            JoinPasswordHash = passwordHasher.Hash("InitialPass#2026"),
            JoinPasswordProtected = passwordProtector.Protect("InitialPass#2026"),
            CourseId = _course.Id,
            SemesterId = _semester.Id,
            CourseOfferingId = _offering.Id,
            LecturerId = _lecturer.Id,
            MaxStudents = 35,
            AllowSelfEnrollment = true,
            IsJoinEnabled = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(head, _lecturer);
        db.AcademicPrograms.Add(program);
        db.Semesters.Add(_semester);
        db.Courses.Add(_course);
        db.CourseOfferings.Add(_offering);
        db.CourseLecturers.Add(assignment);
        db.Classes.Add(_existingClass);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact(DisplayName = "HTTP-NO-STORE: GetClassDetail, CreateClass và ResetClassPassword đều trả Cache-Control: no-store, no-cache và Pragma: no-cache")]
    public async Task ClassesController_EndpointsReturningJoinPassword_EmitNoStoreAndPragmaHeaders()
    {
        var tokenIssuer = _server.Services.GetRequiredService<IAccessTokenIssuer>();
        var token = tokenIssuer.Issue(
            _lecturer.Id,
            _lecturer.Email,
            _lecturer.FullName,
            _lecturer.Role,
            mustChangePassword: false,
            authVersion: _lecturer.AuthVersion).Token;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var detailResponse = await _client.GetAsync($"/api/v1/classes/{_existingClass.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStoreHeaders(detailResponse);
        var detailJson = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        detailJson.GetProperty("joinPassword").GetString().Should().Be("InitialPass#2026");

        using var createResponse = await _client.PostAsJsonAsync("/api/v1/classes", new
        {
            code = "SE1802-HTTP",
            name = "Lớp Mới HTTP",
            courseId = _course.Id,
            semesterId = _semester.Id,
            courseOfferingId = _offering.Id,
            password = "CreatedPass#2026",
            maxStudents = 30,
            allowSelfEnrollment = true,
            isJoinEnabled = true
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        AssertNoStoreHeaders(createResponse);
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createJson.GetProperty("joinPassword").GetString().Should().Be("CreatedPass#2026");

        using var resetResponse = await _client.PutAsJsonAsync(
            $"/api/v1/lecturer/classes/{_existingClass.Id}/password",
            new
            {
                newPassword = "RotatedHttpPass#2026"
            });
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStoreHeaders(resetResponse);
        var resetJson = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();
        resetJson.GetProperty("joinPassword").GetString().Should().Be("RotatedHttpPass#2026");
    }

    private static void AssertNoStoreHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.CacheControl.NoCache.Should().BeTrue();
        response.Headers.Pragma.ToString().Should().Contain("no-cache");
    }
}
