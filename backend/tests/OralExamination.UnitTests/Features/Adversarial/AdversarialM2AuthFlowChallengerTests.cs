using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OralExamination.API.Middlewares;
using OralExamination.Application.Common.Auth;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Empirical Adversarial Verification Suite for Milestone M2 (Auth & MustChangePassword Flow).
/// Executed independently by teamwork_challenger (Auth Flow Stress Verifier).
/// </summary>
public class AdversarialM2AuthFlowChallengerTests
{
    private static OralExamDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: $"M2AuthChallengerDb_{Guid.NewGuid():N}")
            .Options;

        return new OralExamDbContext(options);
    }

    private static ClaimsPrincipal CreatePrincipalWithClaim(string? mustChangeClaimValue, bool isAuthenticated = true)
    {
        if (!isAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.Email, "testuser@fpt.edu.vn"),
            new(ClaimTypes.Role, "student")
        };

        if (mustChangeClaimValue != null)
        {
            claims.Add(new Claim(AuthTokenDefaults.MustChangePasswordClaim, mustChangeClaimValue));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    // =========================================================================
    // 1. MustChangePasswordMiddleware — Direct Call to Protected Endpoints (403)
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-01: Direct call to /api/v1/student/dashboard with must_change_password=true MUST be blocked with 403 ProblemDetails")]
    public async Task MustChangePasswordMiddleware_WhenCallingStudentDashboard_WithMustChangeTrue_Returns403ProblemDetails()
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/student/dashboard";
        context.User = CreatePrincipalWithClaim("true");

        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeFalse("Downstream middleware/controller must NOT be invoked when user must change password");
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/problem+json");

        bodyStream.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(bodyStream);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(403);
        root.GetProperty("title").GetString().Should().Be("Cần đổi mật khẩu.");
        root.GetProperty("detail").GetString().Should().Be("Bạn phải đổi mật khẩu trước khi dùng các chức năng khác.");
        root.GetProperty("code").GetString().Should().Be("MUST_CHANGE_PASSWORD");
        root.GetProperty("errorCode").GetString().Should().Be("MUST_CHANGE_PASSWORD");
    }

    // =========================================================================
    // 2. MustChangePasswordMiddleware — Whitelist Verification (/api/v1/auth/me)
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-02: Endpoint /api/v1/auth/me MUST be accessible when user has must_change_password=true")]
    public async Task MustChangePasswordMiddleware_WhenCallingAuthMe_WithMustChangeTrue_PassesThrough()
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auth/me";
        context.User = CreatePrincipalWithClaim("true");

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue("Endpoint /api/v1/auth/me must be allowed through so user profile can be fetched");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    // =========================================================================
    // 3. MustChangePasswordMiddleware — Comprehensive Whitelist Matrix
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-03: All whitelisted authentication and health endpoints must pass through")]
    [InlineData("/api/v1/auth/change-password")]
    [InlineData("/api/v1/auth/refresh-token")]
    [InlineData("/api/v1/auth/sign-out")]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/auth/me/subpath")]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task MustChangePasswordMiddleware_WhitelistedPaths_MustPassThrough(string path)
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = CreatePrincipalWithClaim("true");

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue($"Path '{path}' must be whitelisted when must_change_password=true");
    }

    // =========================================================================
    // 4. MustChangePasswordMiddleware — Protected Business Endpoints Matrix
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-04: Business endpoints across all roles must be strictly blocked 403")]
    [InlineData("/api/v1/student/dashboard")]
    [InlineData("/api/v1/student/practice/sessions")]
    [InlineData("/api/v1/student/mock-exams")]
    [InlineData("/api/v1/student/in-class-shifts")]
    [InlineData("/api/v1/lecturer/in-class-shifts")]
    [InlineData("/api/v1/lecturer/in-class-shifts/00000000-0000-0000-0000-000000000001/submissions")]
    [InlineData("/api/v1/semesters")]
    [InlineData("/api/v1/courses")]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/official-exams/shifts")]
    [InlineData("/api/v1/proctor/shifts/1/violations")]
    public async Task MustChangePasswordMiddleware_BusinessPaths_MustBeBlocked403(string path)
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = CreatePrincipalWithClaim("true");

        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeFalse($"Path '{path}' must be strictly blocked when must_change_password=true");
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        bodyStream.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(bodyStream);
        doc.RootElement.GetProperty("code").GetString().Should().Be("MUST_CHANGE_PASSWORD");
    }

    // =========================================================================
    // 5. MustChangePasswordMiddleware — Segment Traversal and Bypass Resistance
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-05: Path segment spoofing or suffix trickery must NOT bypass the 403 guard")]
    [InlineData("/api/v1/student/dashboard/api/v1/auth/me")]
    [InlineData("/api/v1/auth/me_malicious_suffix")]
    [InlineData("/api/v1/auth/change-password-fake")]
    [InlineData("/health_unauthorized")]
    public async Task MustChangePasswordMiddleware_PathSegmentSpoofing_IsBlocked(string path)
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = CreatePrincipalWithClaim("true");

        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeFalse($"Path '{path}' attempted segment spoofing and must be blocked 403");
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // =========================================================================
    // 6. MustChangePasswordMiddleware — Normal Pass-Through (false / absent / unauth)
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-06A: User with must_change_password='false' must pass through to business endpoints")]
    public async Task MustChangePasswordMiddleware_WhenClaimIsFalse_PassesThrough()
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/student/dashboard";
        context.User = CreatePrincipalWithClaim("false");

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue("User with must_change_password=false must not be blocked");
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "ADV-M2-06B: User without must_change_password claim must pass through to business endpoints")]
    public async Task MustChangePasswordMiddleware_WhenClaimIsMissing_PassesThrough()
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/student/dashboard";
        context.User = CreatePrincipalWithClaim(null);

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue("User without must_change_password claim must not be blocked");
    }

    [Fact(DisplayName = "ADV-M2-06C: Unauthenticated request passes through to downstream authentication handler")]
    public async Task MustChangePasswordMiddleware_WhenUnauthenticated_PassesThrough()
    {
        var nextInvoked = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/student/dashboard";
        context.User = CreatePrincipalWithClaim(null, isAuthenticated: false);

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue("Unauthenticated request must pass through to downstream auth handler (for 401)");
    }

    // =========================================================================
    // 7. DevelopmentDataSeeder — Idempotency and MustChangePassword Invariant
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-07: DevelopmentDataSeeder enforces MustChangePassword=false on all default test users across multiple runs even if previously corrupted")]
    public async Task DevelopmentDataSeeder_EnforcesMustChangePasswordFalse_EvenIfCorrupted()
    {
        await using var context = CreateInMemoryContext();

        // 1. Lần seed đầu tiên trên DB trống
        await DevelopmentDataSeeder.SeedAsync(context);

        var initialUsers = await context.Users.ToListAsync();
        initialUsers.Should().NotBeEmpty();

        var expectedDefaultUserIds = new[]
        {
            DevelopmentDataSeeder.AdminUserId,
            DevelopmentDataSeeder.DepartmentHeadUserId,
            DevelopmentDataSeeder.LecturerUserId,
            DevelopmentDataSeeder.Lecturer2UserId,
            DevelopmentDataSeeder.Lecturer3UserId,
            DevelopmentDataSeeder.Lecturer4UserId,
            DevelopmentDataSeeder.ProctorUserId,
            DevelopmentDataSeeder.StudentHoangId,
            DevelopmentDataSeeder.StudentMaiId,
            DevelopmentDataSeeder.StudentThanhId,
            DevelopmentDataSeeder.ExaminationOfficerUserId
        };

        foreach (var userId in expectedDefaultUserIds)
        {
            var user = initialUsers.FirstOrDefault(u => u.Id == userId);
            user.Should().NotBeNull($"User {userId} must exist in seeded database");
            user!.MustChangePassword.Should().BeFalse($"User {user.Username} must have MustChangePassword=false on initial seed");
            user.IsActive.Should().BeTrue($"User {user.Username} must be active on initial seed");
        }

        // 2. Tấn công đối kháng: Cố tình làm sai lệch trạng thái CSDL
        // Giả lập trường hợp DB cũ bị migration hoặc lỗi admin gán MustChangePassword=true và thay đổi PasswordHash
        foreach (var user in initialUsers.Where(u => expectedDefaultUserIds.Contains(u.Id)))
        {
            user.MustChangePassword = true;
            user.PasswordHash = "corrupted_hash_different_from_seed";
            user.IsActive = false;
        }
        await context.SaveChangesAsync();

        // Xác nhận CSDL đã thực sự bị corrupt
        var corruptedUsers = await context.Users.Where(u => expectedDefaultUserIds.Contains(u.Id)).ToListAsync();
        corruptedUsers.Should().OnlyContain(u => u.MustChangePassword == true && u.IsActive == false,
            "Corrupted state must be established before re-seeding");

        // 3. Lần seed thứ hai (Idempotent Recovery)
        await DevelopmentDataSeeder.SeedAsync(context);

        var recoveredUsers = await context.Users.Where(u => expectedDefaultUserIds.Contains(u.Id)).ToListAsync();
        recoveredUsers.Should().HaveCount(expectedDefaultUserIds.Length);

        foreach (var user in recoveredUsers)
        {
            user.MustChangePassword.Should().BeFalse(
                $"User {user.Username} must be forcefully reconciled to MustChangePassword=false by idempotent seeder");
            user.IsActive.Should().BeTrue(
                $"User {user.Username} must be restored to IsActive=true");
        }

        // 4. Lần seed thứ ba (Idempotency confirmation)
        await DevelopmentDataSeeder.SeedAsync(context);

        var stableUsers = await context.Users.Where(u => expectedDefaultUserIds.Contains(u.Id)).ToListAsync();
        stableUsers.Should().OnlyContain(u => u.MustChangePassword == false && u.IsActive == true,
            "Multiple consecutive seed runs must stably preserve MustChangePassword=false");
    }
}
