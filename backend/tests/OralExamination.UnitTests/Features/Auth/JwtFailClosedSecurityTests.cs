using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class JwtFailClosedSecurityTests
{
    private readonly IConfiguration _configuration;

    public JwtFailClosedSecurityTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["JWT_SIGNING_KEY"] = "Test_Secret_Key_For_Jwt_Signing_Must_Be_At_Least_32_Bytes_Long_2026!",
            ["JWT_ISSUER"] = "oralexam-auth",
            ["JWT_AUDIENCE"] = "oralexam-client",
            ["JWT_ACCESS_TOKEN_MINUTES"] = "15"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private (JwtBearerEvents Events, IServiceProvider Services, OralExamDbContext DbContext) SetupAuthPipeline(bool registerDb = true)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var dbContext = new OralExamDbContext(dbOptions);

        if (registerDb)
        {
            services.AddSingleton<IApplicationDbContext>(dbContext);
        }

        services.AddOralExamAuthentication(_configuration);
        var provider = services.BuildServiceProvider();

        var jwtOptions = provider.GetRequiredService<IOptionsSnapshot<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        return (jwtOptions.Events!, provider, dbContext);
    }

    private static TokenValidatedContext CreateTokenContext(ClaimsPrincipal principal, IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();

        return new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = principal
        };
    }

    [Fact(DisplayName = "OnTokenValidated: Thiếu claim sub phải Fail-Closed")]
    public async Task OnTokenValidated_MissingSubClaim_FailsClosed()
    {
        var (events, services, _) = SetupAuthPipeline();
        var claims = new List<Claim>
        {
            new("role", UserRole.Student),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("sub");
    }

    [Fact(DisplayName = "OnTokenValidated: Claim sub không phải Guid hợp lệ phải Fail-Closed")]
    public async Task OnTokenValidated_InvalidSubClaim_FailsClosed()
    {
        var (events, services, _) = SetupAuthPipeline();
        var claims = new List<Claim>
        {
            new("sub", "not-a-valid-guid"),
            new("role", UserRole.Student),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("sub");
    }

    [Fact(DisplayName = "OnTokenValidated: Thiếu claim role bắt buộc phải Fail-Closed")]
    public async Task OnTokenValidated_MissingRoleClaim_FailsClosed()
    {
        var (events, services, _) = SetupAuthPipeline();
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("role");
    }

    [Fact(DisplayName = "OnTokenValidated: Thiếu hoặc sai định dạng claim auth_version phải Fail-Closed")]
    public async Task OnTokenValidated_MissingOrInvalidAuthVersionClaim_FailsClosed()
    {
        var (events, services, _) = SetupAuthPipeline();
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("role", UserRole.Student),
            new("auth_version", "not-an-int")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("auth_version");
    }

    [Fact(DisplayName = "OnTokenValidated: Không resolve được IApplicationDbContext phải Fail-Closed")]
    public async Task OnTokenValidated_DbVerifierMissing_FailsClosed()
    {
        var (events, services, _) = SetupAuthPipeline(registerDb: false);
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("role", UserRole.Student),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("verifier");
    }

    [Fact(DisplayName = "OnTokenValidated: Người dùng không tồn tại hoặc IsActive = false phải Fail-Closed")]
    public async Task OnTokenValidated_UserInactiveOrNotFound_FailsClosed()
    {
        var (events, services, db) = SetupAuthPipeline();
        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "locked@fpt.edu.vn",
            FullName = "Locked User",
            Role = UserRole.Student,
            IsActive = false,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(lockedUser);
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new("sub", lockedUser.Id.ToString()),
            new("role", UserRole.Student),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("bị khóa");
    }

    [Fact(DisplayName = "OnTokenValidated: Token role lệch với role hiện tại trong DB phải Fail-Closed")]
    public async Task OnTokenValidated_RoleMismatch_FailsClosed()
    {
        var (events, services, db) = SetupAuthPipeline();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Lecturer User",
            Role = UserRole.Lecturer, // Đã được admin nâng role lên Lecturer
            IsActive = true,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("role", UserRole.Student), // Token cũ mang role Student
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("thay đổi");
    }

    [Fact(DisplayName = "OnTokenValidated: Token auth_version lệch với DB phải Fail-Closed (thu hồi phiên tức thì)")]
    public async Task OnTokenValidated_AuthVersionMismatch_FailsClosed()
    {
        var (events, services, db) = SetupAuthPipeline();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "active@fpt.edu.vn",
            FullName = "Active User",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 2, // Đã đổi mật khẩu -> AuthVersion tăng lên 2
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("role", UserRole.Student),
            new("auth_version", "1") // Token cũ mang auth_version 1
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result.Failure.Should().NotBeNull();
        context.Result.Failure!.Message.Should().Contain("hết hiệu lực");
    }

    [Fact(DisplayName = "OnTokenValidated: Token đầy đủ claim hợp lệ và trùng khớp DB phải thành công")]
    public async Task OnTokenValidated_ValidTokenAndActiveUser_Succeeds()
    {
        var (events, services, db) = SetupAuthPipeline();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "valid@fpt.edu.vn",
            FullName = "Valid User",
            Role = UserRole.Student,
            IsActive = true,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("role", UserRole.Student),
            new("auth_version", "1")
        };
        var context = CreateTokenContext(new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")), services);

        await events.OnTokenValidated(context);

        context.Result?.Failure.Should().BeNull();
    }
}
