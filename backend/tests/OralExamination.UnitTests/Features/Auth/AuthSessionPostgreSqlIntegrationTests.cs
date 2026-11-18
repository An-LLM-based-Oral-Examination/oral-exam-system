using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OralExamination.Application;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Admin.Commands.CreateUser;
using OralExamination.Application.Features.Admin.Commands.UpdateUserRole;
using OralExamination.Application.Features.Admin.Commands.UpdateUserStatus;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.Features.InClassShifts;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

[Trait("Category", "Integration")]
public class AuthSessionPostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = new();
    private OralExamDbContext? _context;
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();

    public Task InitializeAsync()
    {
        _context = CreateNewContext();
        return Task.CompletedTask;
    }

    private OralExamDbContext CreateNewContext()
    {
        var connStr = PostgresIntegrationTestDatabase.GetPostgresConnectionString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(connStr)
            .Options;
        return new OralExamDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            if (_createdUserIds.Count > 0)
            {
                var userIds = _createdUserIds.ToList();

                var tokens = await _context.RefreshTokens
                    .Where(rt => userIds.Contains(rt.UserId))
                    .ToListAsync();
                if (tokens.Count > 0)
                {
                    _context.RefreshTokens.RemoveRange(tokens);
                }

                var audits = await _context.AuditLogs
                    .Where(a => (a.UserId.HasValue && userIds.Contains(a.UserId.Value)) ||
                                (a.EntityId.HasValue && userIds.Contains(a.EntityId.Value)))
                    .ToListAsync();
                if (audits.Count > 0)
                {
                    _context.AuditLogs.RemoveRange(audits);
                }

                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToListAsync();
                if (users.Count > 0)
                {
                    _context.Users.RemoveRange(users);
                }

                await _context.SaveChangesAsync();
            }
            await _context.DisposeAsync();
        }
    }

    private User CreateTestUser(string role = UserRole.Student, bool isActive = true, bool mustChangePassword = false)
    {
        var id = Guid.NewGuid();
        var tag = id.ToString("N")[..8];
        var user = new User
        {
            Id = id,
            Email = $"authtest_{tag}@fpt.edu.vn",
            FullName = $"Auth Test User {tag}",
            StudentCode = role == UserRole.Student ? $"ST{tag}" : null,
            Username = $"user_{tag}",
            Role = role,
            PasswordHash = _passwordHasher.Hash("SecureP@ss123!"),
            MustChangePassword = mustChangePassword,
            IsActive = isActive,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _createdUserIds.Add(id);
        return user;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class StubAccessTokenIssuer : IAccessTokenIssuer
    {
        public int LastAuthVersion { get; private set; }
        public string? LastRole { get; private set; }

        public IssuedAccessToken Issue(Guid userId, string email, string fullName, string role, bool mustChangePassword = false, int authVersion = 1)
        {
            LastAuthVersion = authVersion;
            LastRole = role;
            return new IssuedAccessToken($"test-access-token-{authVersion}", 900);
        }
    }

    [Fact(DisplayName = "PostgreSQL: Token Rotation cấp token mới và thu hồi token cũ")]
    public async Task PostgreSql_RefreshToken_Rotation_ReplacesToken_And_RevokesOld()
    {
        _context.Should().NotBeNull();
        var user = CreateTestUser();
        _context!.Users.Add(user);

        var rawInitialToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var initialTokenHash = HashToken(rawInitialToken);
        var initialRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = initialTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(initialRefreshToken);
        await _context.SaveChangesAsync();

        var issuer = new StubAccessTokenIssuer();
        var handler = new RefreshTokenCommandHandler(_context, issuer, new RefreshTokenCommandValidator());

        var result = await handler.Handle(new RefreshTokenCommand(rawInitialToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.RefreshToken.Should().NotBe(rawInitialToken);

        // Kiểm tra trong PostgreSQL
        var oldTokenInDb = await _context.RefreshTokens.FirstAsync(rt => rt.TokenHash == initialTokenHash);
        oldTokenInDb.RevokedAt.Should().NotBeNull();
        oldTokenInDb.ReplacedByTokenHash.Should().Be(HashToken(result.Value.RefreshToken));

        var newTokenInDb = await _context.RefreshTokens.FirstAsync(rt => rt.TokenHash == HashToken(result.Value.RefreshToken));
        newTokenInDb.RevokedAt.Should().BeNull();
        newTokenInDb.UserId.Should().Be(user.Id);
    }

    [Fact(DisplayName = "PostgreSQL: Replay Attack thu hồi toàn bộ token family của người dùng")]
    public async Task PostgreSql_RefreshToken_ReplayAttack_RevokesAllUserTokens()
    {
        _context.Should().NotBeNull();
        var user = CreateTestUser();
        _context!.Users.Add(user);

        var rawToken1 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var token1Hash = HashToken(rawToken1);
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = token1Hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var issuer = new StubAccessTokenIssuer();
        var handler = new RefreshTokenCommandHandler(_context, issuer, new RefreshTokenCommandValidator());

        // Lần 1: Hợp lệ -> tạo token 2, revoke token 1
        var rotateResult = await handler.Handle(new RefreshTokenCommand(rawToken1), CancellationToken.None);
        rotateResult.IsSuccess.Should().BeTrue();
        var rawToken2 = rotateResult.Value.RefreshToken;

        // Giả lập kẻ tấn công replay token 1 đã bị revoke từ trước (ngoài grace window 10s)
        var oldRotatedToken = await _context.RefreshTokens.FirstAsync(rt => rt.TokenHash == token1Hash);
        oldRotatedToken.RevokedAt = DateTime.UtcNow.AddSeconds(-30);
        await _context.SaveChangesAsync();

        // Lần 2: Kẻ tấn công replay token 1 đã bị revoke
        var replayResult = await handler.Handle(new RefreshTokenCommand(rawToken1), CancellationToken.None);
        replayResult.IsSuccess.Should().BeFalse();
        replayResult.Error.Should().Contain("thu hồi");

        // Kiểm tra PostgreSQL: Token 2 cũng phải bị thu hồi ngay lập tức VÀ AuthVersion tăng lên 2
        var token2InDb = await _context.RefreshTokens.FirstAsync(rt => rt.TokenHash == HashToken(rawToken2));
        token2InDb.RevokedAt.Should().NotBeNull();

        var reloadedUser = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        reloadedUser.AuthVersion.Should().Be(2, "Replay attack phải tăng AuthVersion để vô hiệu hóa mọi access token JWT đang lưu hành");
    }

    [Fact(DisplayName = "PostgreSQL: Đổi mật khẩu tăng AuthVersion và thu hồi mọi RefreshToken")]
    public async Task PostgreSql_PasswordChange_IncrementsAuthVersion_And_RevokesRefreshTokens()
    {
        _context.Should().NotBeNull();
        var user = CreateTestUser(mustChangePassword: true);
        _context!.Users.Add(user);

        var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var issuer = new StubAccessTokenIssuer();
        var handler = new ChangePasswordCommandHandler(_context, _passwordHasher, issuer, new ChangePasswordCommandValidator());

        var result = await handler.Handle(new ChangePasswordCommand(user.Id, "SecureP@ss123!", "NewSuperSecret2026!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MustChangePassword.Should().BeFalse();

        // Kiểm tra PostgreSQL
        var updatedUser = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        updatedUser.AuthVersion.Should().Be(2);
        updatedUser.MustChangePassword.Should().BeFalse();

        var tokenInDb = await _context.RefreshTokens.FirstAsync(rt => rt.UserId == user.Id);
        tokenInDb.RevokedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "PostgreSQL: Khóa tài khoản tăng AuthVersion và thu hồi toàn bộ RefreshToken")]
    public async Task PostgreSql_UserStatusChange_Lock_IncrementsAuthVersion_And_RevokesRefreshTokens()
    {
        _context.Should().NotBeNull();
        var admin = CreateTestUser(role: UserRole.Admin);
        var target = CreateTestUser(role: UserRole.Student, isActive: true);
        _context!.Users.AddRange(admin, target);

        var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = target.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new UpdateUserStatusCommandHandler(_context);
        var result = await handler.Handle(new UpdateUserStatusCommand(admin.Id, target.Id, false, "Vi phạm quy chế"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();

        // Kiểm tra PostgreSQL
        var updatedTarget = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == target.Id);
        updatedTarget.IsActive.Should().BeFalse();
        updatedTarget.AuthVersion.Should().Be(2);

        var tokenInDb = await _context.RefreshTokens.FirstAsync(rt => rt.UserId == target.Id);
        tokenInDb.RevokedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "PostgreSQL: Đổi vai trò (Role) tăng AuthVersion và thu hồi toàn bộ RefreshToken")]
    public async Task PostgreSql_UserRoleChange_IncrementsAuthVersion_And_RevokesRefreshTokens()
    {
        _context.Should().NotBeNull();
        var admin = CreateTestUser(role: UserRole.Admin);
        var target = CreateTestUser(role: UserRole.Student);
        _context!.Users.AddRange(admin, target);

        var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = target.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var handler = new UpdateUserRoleCommandHandler(_context);
        var result = await handler.Handle(new UpdateUserRoleCommand(admin.Id, target.Id, UserRole.Lecturer), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(UserRole.Lecturer);

        // Kiểm tra PostgreSQL
        var updatedTarget = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == target.Id);
        updatedTarget.Role.Should().Be(UserRole.Lecturer);
        updatedTarget.AuthVersion.Should().Be(2);

        var tokenInDb = await _context.RefreshTokens.FirstAsync(rt => rt.UserId == target.Id);
        tokenInDb.RevokedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "PostgreSQL: Ràng buộc duy nhất Email, StudentCode, Username ném DbUpdateException")]
    public async Task PostgreSql_UniquenessConstraints_ThrowsDbUpdateException_OnDuplicates()
    {
        _context.Should().NotBeNull();
        var user1 = CreateTestUser();
        _context!.Users.Add(user1);
        await _context.SaveChangesAsync();

        // 1. Trùng Email
        var dupEmailUser = CreateTestUser();
        dupEmailUser.Email = user1.Email;
        _context.Users.Add(dupEmailUser);
        var actEmail = async () => await _context.SaveChangesAsync();
        await actEmail.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupEmailUser).State = EntityState.Detached;

        // 2. Trùng StudentCode
        var dupStudentCodeUser = CreateTestUser();
        dupStudentCodeUser.StudentCode = user1.StudentCode;
        _context.Users.Add(dupStudentCodeUser);
        var actCode = async () => await _context.SaveChangesAsync();
        await actCode.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupStudentCodeUser).State = EntityState.Detached;

        // 3. Trùng Username
        var dupUsernameUser = CreateTestUser();
        dupUsernameUser.Username = user1.Username;
        _context.Users.Add(dupUsernameUser);
        var actUsername = async () => await _context.SaveChangesAsync();
        await actUsername.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupUsernameUser).State = EntityState.Detached;
    }

    [Fact(DisplayName = "PostgreSQL: Ràng buộc duy nhất không phân biệt hoa thường (lower) ném DbUpdateException trên các biến thể")]
    public async Task PostgreSql_Uniqueness_CaseInsensitive_ThrowsDbUpdateException_OnVariants()
    {
        _context.Should().NotBeNull();
        var user1 = CreateTestUser();
        _context!.Users.Add(user1);
        await _context.SaveChangesAsync();

        // 1. Trùng Email khác case (Ví dụ: USER@FPT.EDU.VN vs user@fpt.edu.vn)
        var dupEmailUser = CreateTestUser();
        dupEmailUser.Email = user1.Email.ToUpperInvariant();
        _context.Users.Add(dupEmailUser);
        var actEmail = async () => await _context.SaveChangesAsync();
        await actEmail.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupEmailUser).State = EntityState.Detached;

        // 2. Trùng StudentCode khác case (Ví dụ: se180001 vs SE180001)
        var dupStudentCodeUser = CreateTestUser();
        dupStudentCodeUser.StudentCode = user1.StudentCode!.ToLowerInvariant();
        _context.Users.Add(dupStudentCodeUser);
        var actCode = async () => await _context.SaveChangesAsync();
        await actCode.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupStudentCodeUser).State = EntityState.Detached;

        // 3. Trùng Username khác case (Ví dụ: ADMIN_01 vs admin_01)
        var dupUsernameUser = CreateTestUser();
        dupUsernameUser.Username = user1.Username!.ToUpperInvariant();
        _context.Users.Add(dupUsernameUser);
        var actUsername = async () => await _context.SaveChangesAsync();
        await actUsername.Should().ThrowAsync<DbUpdateException>();
        _context.Entry(dupUsernameUser).State = EntityState.Detached;
    }

    [Fact(DisplayName = "PostgreSQL: Tạo người dùng đồng thời trên 2 DbContext — đúng một request thành công và request thứ 2 trả về CONFLICT")]
    public async Task PostgreSql_CreateUser_ConcurrentExecution_OnlyOneSucceeds_SecondReturnsConflict()
    {
        var admin = CreateTestUser(role: UserRole.Admin);
        _context!.Users.Add(admin);
        await _context.SaveChangesAsync();

        var sharedEmail = $"concurrent_user_{Guid.NewGuid():N}@fpt.edu.vn";
        var studentCode = $"SE{Random.Shared.Next(100000, 999999)}";

        await using var context1 = CreateNewContext();
        await using var context2 = CreateNewContext();

        var handler1 = new CreateUserCommandHandler(context1, _passwordHasher);
        var handler2 = new CreateUserCommandHandler(context2, _passwordHasher);

        var cmd1 = new CreateUserCommand(admin.Id, sharedEmail, "Sinh Vien A", "StudentPassword123!", UserRole.Student, studentCode);
        var cmd2 = new CreateUserCommand(admin.Id, sharedEmail.ToUpperInvariant(), "Sinh Vien B", "StudentPassword123!", UserRole.Student, studentCode.ToLowerInvariant());

        var task1 = handler1.Handle(cmd1, CancellationToken.None);
        var task2 = handler2.Handle(cmd2, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r.IsSuccess).Should().Be(1, "Chỉ được phép 1 request tạo tài khoản thành công khi trùng email/studentCode");
        results.Count(r => !r.IsSuccess).Should().Be(1, "Request thứ 2 phải bắt được lỗi 23505 và trả về CONFLICT");

        var failureResult = results.Single(r => !r.IsSuccess);
        failureResult.Error.Should().Contain("CONFLICT", "Mã lỗi phải được chuẩn hóa về CONFLICT theo RFC 7807");

        var createdUser = results.Single(r => r.IsSuccess).Value;
        _createdUserIds.Add(createdUser.Id);
    }

    [Fact(DisplayName = "PostgreSQL: Hai request refresh token đồng thời — đúng một request thành công và chỉ còn một descendant token hợp lệ")]
    public async Task PostgreSql_RefreshToken_ConcurrentRotation_OnlyOneSucceeds_ExactlyOneDescendantTokenRemains()
    {
        _context.Should().NotBeNull();
        var user = CreateTestUser();
        _context!.Users.Add(user);

        var rawInitialToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var initialTokenHash = HashToken(rawInitialToken);
        var initialRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = initialTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(initialRefreshToken);
        await _context.SaveChangesAsync();

        // Tạo 2 connection/DbContext riêng biệt kết nối đồng thời vào PostgreSQL
        await using var context1 = CreateNewContext();
        await using var context2 = CreateNewContext();

        var issuer = new StubAccessTokenIssuer();
        var validator = new RefreshTokenCommandValidator();
        var handler1 = new RefreshTokenCommandHandler(context1, issuer, validator);
        var handler2 = new RefreshTokenCommandHandler(context2, issuer, validator);

        // Chạy đồng thời 2 handler trên 2 connection khác nhau
        var task1 = handler1.Handle(new RefreshTokenCommand(rawInitialToken), CancellationToken.None);
        var task2 = handler2.Handle(new RefreshTokenCommand(rawInitialToken), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Đúng một request thành công và một request thất bại
        results.Count(r => r.IsSuccess).Should().Be(1, "Chỉ được phép đúng 1 request xoay vòng thành công");
        results.Count(r => !r.IsSuccess).Should().Be(1, "Request đồng thời còn lại phải thất bại vào nhánh xung đột hoặc replay");

        var successResult = results.Single(r => r.IsSuccess);
        var failureResult = results.Single(r => !r.IsSuccess);
        failureResult.Error.Should().Match(err => err.Contains("CONFLICT") || err.Contains("thu hồi"));

        // Kiểm tra trong PostgreSQL thật: chỉ còn duy nhất 1 token con hợp lệ (chưa bị thu hồi)
        await using var verifyContext = CreateNewContext();
        var activeDescendantTokens = await verifyContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync();

        activeDescendantTokens.Should().HaveCount(1, "Chỉ còn đúng một descendant token hợp lệ trong CSDL");
        activeDescendantTokens[0].TokenHash.Should().Be(HashToken(successResult.Value.RefreshToken));

        // Token ban đầu phải ở trạng thái đã bị thu hồi
        var oldToken = await verifyContext.RefreshTokens.FirstAsync(rt => rt.TokenHash == initialTokenHash);
        oldToken.RevokedAt.Should().NotBeNull();
        oldToken.ReplacedByTokenHash.Should().Be(HashToken(successResult.Value.RefreshToken));
    }

    private (TestServer server, IAccessTokenIssuer issuer, string signingKey) CreateTestHost()
    {
        var connStr = PostgresIntegrationTestDatabase.GetPostgresConnectionString();
        var signingKey = "SuperSecretKeyForIntegrationTestingMustBeAtLeast32BytesLong!";

        var configDict = new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = signingKey,
            ["Authentication:JwtIssuer"] = "OralExamTestIssuer",
            ["Authentication:JwtAudience"] = "OralExamTestAudience",
            ["Authentication:AccessTokenMinutes"] = "60",
            ["ConnectionStrings:DefaultConnection"] = connStr
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
            .ConfigureServices((ctx, services) =>
            {
                services.AddDbContext<OralExamDbContext>(opt => opt.UseNpgsql(connStr));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
                services.AddApplication();
                services.AddOralExamAuthentication(ctx.Configuration);
                services.AddSignalR();
                services.AddControllers()
                    .AddApplicationPart(typeof(global::API.Controllers.v1.AuthController).Assembly);
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
                    endpoints.MapHub<OralExamination.API.Hubs.PracticeHub>("/hubs/practice");
                });
            });

        var server = new TestServer(builder);
        var issuer = new JwtAccessTokenIssuer(configuration);
        return (server, issuer, signingKey);
    }

    private static string CreateCustomToken(
        string signingKey,
        string issuer = "OralExamTestIssuer",
        string audience = "OralExamTestAudience",
        string? sub = null,
        string? role = null,
        string? authVersion = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        if (sub != null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
        if (role != null) claims.Add(new Claim("role", role));
        if (authVersion != null) claims.Add(new Claim("auth_version", authVersion));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact(DisplayName = "T3-F009: HTTP pipeline thật kiểm thử OnTokenValidated trên PostgreSQL — từ chối user locked, đổi role, đổi auth_version và token thiếu claims")]
    public async Task PostgreSql_JwtHttpPipeline_ActualRoute_ValidatesUserSecurityState_FailClosed()
    {
        var (server, issuer, signingKey) = CreateTestHost();
        using var client = server.CreateClient();

        // 1. Tạo user hợp lệ trong PostgreSQL thật
        var user = CreateTestUser(role: UserRole.Student, isActive: true);
        user.AuthVersion = 1;
        _context!.Users.Add(user);
        await _context.SaveChangesAsync();
        _createdUserIds.Add(user.Id);

        // 2. Token hợp lệ -> Gọi GET /api/v1/auth/me thành công 200 OK
        var validToken = issuer.Issue(user.Id, user.Email, user.FullName, user.Role, authVersion: user.AuthVersion).Token;
        using var validReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        validReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
        var validResp = await client.SendAsync(validReq);
        validResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Token thiếu claim 'sub' -> Bị từ chối 401 Unauthorized
        var missingSubToken = CreateCustomToken(signingKey, role: user.Role, authVersion: "1");
        using var missingSubReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        missingSubReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", missingSubToken);
        var missingSubResp = await client.SendAsync(missingSubReq);
        missingSubResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Token thiếu claim 'role' -> Bị từ chối 401 Unauthorized
        var missingRoleToken = CreateCustomToken(signingKey, sub: user.Id.ToString(), authVersion: "1");
        using var missingRoleReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        missingRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", missingRoleToken);
        var missingRoleResp = await client.SendAsync(missingRoleReq);
        missingRoleResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Token thiếu claim 'auth_version' -> Bị từ chối 401 Unauthorized
        var missingVerToken = CreateCustomToken(signingKey, sub: user.Id.ToString(), role: user.Role);
        using var missingVerReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        missingVerReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", missingVerToken);
        var missingVerResp = await client.SendAsync(missingVerReq);
        missingVerResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 6. Khóa tài khoản trong DB (IsActive = false) -> Token cũ bị từ chối 401 Unauthorized
        user.IsActive = false;
        await _context.SaveChangesAsync();

        using var lockedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        lockedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
        var lockedResp = await client.SendAsync(lockedReq);
        lockedResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Mở lại tài khoản để test tiếp
        user.IsActive = true;
        await _context.SaveChangesAsync();

        // 7. Thay đổi quyền trong DB (Role: student -> lecturer) -> Token cũ mang role student bị từ chối 401 Unauthorized
        user.Role = UserRole.Lecturer;
        await _context.SaveChangesAsync();

        using var roleMismatchReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        roleMismatchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
        var roleMismatchResp = await client.SendAsync(roleMismatchReq);
        roleMismatchResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Trả lại role đúng nhưng tăng auth_version
        user.Role = UserRole.Student;
        user.AuthVersion = 2; // Bump version do đổi mật khẩu / reset phiên
        await _context.SaveChangesAsync();

        // 8. Phiên cũ mang AuthVersion = 1 -> Bị từ chối 401 Unauthorized vì DB đã tăng lên 2
        using var staleVerReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        staleVerReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
        var staleVerResp = await client.SendAsync(staleVerReq);
        staleVerResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "T3-F009: SignalR hub handshake thật kiểm thử OnTokenValidated trên PostgreSQL — từ chối user locked, đổi role, đổi auth_version và token thiếu claims")]
    public async Task PostgreSql_SignalRHandshake_ActualHub_ValidatesUserSecurityState_FailClosed()
    {
        var (server, issuer, signingKey) = CreateTestHost();

        // 1. Tạo user trong PostgreSQL thật
        var user = CreateTestUser(role: UserRole.Student, isActive: true);
        user.AuthVersion = 1;
        _context!.Users.Add(user);
        await _context.SaveChangesAsync();
        _createdUserIds.Add(user.Id);

        var validToken = issuer.Issue(user.Id, user.Email, user.FullName, user.Role, authVersion: user.AuthVersion).Token;

        async Task<HubConnection> BuildAndConnectHub(string token)
        {
            var hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/practice", options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .Build();

            await hubConnection.StartAsync();
            return hubConnection;
        }

        // 2. Token hợp lệ -> Handshake thành công và kết nối Connected
        var activeConnection = await BuildAndConnectHub(validToken);
        activeConnection.State.Should().Be(HubConnectionState.Connected);
        await activeConnection.StopAsync();

        // 3. Khóa tài khoản trong PostgreSQL -> SignalR handshake bị từ chối 401
        user.IsActive = false;
        await _context.SaveChangesAsync();

        var lockedAct = async () => await BuildAndConnectHub(validToken);
        await lockedAct.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);

        // Mở lại tài khoản
        user.IsActive = true;
        await _context.SaveChangesAsync();

        // 4. Đổi role trong PostgreSQL -> SignalR handshake bị từ chối 401
        user.Role = UserRole.Lecturer;
        await _context.SaveChangesAsync();

        var roleMismatchAct = async () => await BuildAndConnectHub(validToken);
        await roleMismatchAct.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);

        // Phục hồi role, tăng AuthVersion trong PostgreSQL -> SignalR handshake bị từ chối 401
        user.Role = UserRole.Student;
        user.AuthVersion = 2;
        await _context.SaveChangesAsync();

        var staleVerAct = async () => await BuildAndConnectHub(validToken);
        await staleVerAct.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);

        // 5. Token thiếu claim sub -> SignalR handshake bị từ chối 401
        var missingSubToken = CreateCustomToken(signingKey, role: user.Role, authVersion: "2");
        var missingSubAct = async () => await BuildAndConnectHub(missingSubToken);
        await missingSubAct.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }
}
