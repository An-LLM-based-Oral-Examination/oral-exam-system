using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddOralExamAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signingKey = AuthConfiguration.JwtSigningKey(configuration);
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Thiếu JWT_SIGNING_KEY. Thêm khóa ký vào file .env ở thư mục oral-exam-system. Không dùng Client Secret của Google.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = AuthConfiguration.JwtIssuer(configuration),
                    ValidAudience = AuthConfiguration.JwtAudience(configuration),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Không xác thực được.",
                            Detail = "Cần access token hợp lệ để truy cập tài nguyên này.",
                            Instance = context.Request.Path,
                            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2"
                        };
                        problem.Extensions["errorCode"] = "UNAUTHORIZED";
                        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                        await context.Response.WriteAsJsonAsync(problem,
                            options: null, contentType: "application/problem+json",
                            cancellationToken: context.HttpContext.RequestAborted);
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var sub = context.Principal?.FindFirst("sub")?.Value
                            ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
                        {
                            context.Fail("Thiếu hoặc sai định dạng claim sub (User ID).");
                            return;
                        }

                        var tokenRole = context.Principal?.FindFirst("role")?.Value
                            ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                        if (string.IsNullOrWhiteSpace(tokenRole))
                        {
                            context.Fail("Thiếu claim role bắt buộc.");
                            return;
                        }

                        var tokenAuthVersion = context.Principal?.FindFirst("auth_version")?.Value;
                        if (string.IsNullOrWhiteSpace(tokenAuthVersion) || !int.TryParse(tokenAuthVersion, out var ver))
                        {
                            context.Fail("Thiếu hoặc sai định dạng claim auth_version.");
                            return;
                        }

                        IApplicationDbContext dbContext;
                        try
                        {
                            dbContext = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                        }
                        catch (Exception)
                        {
                            context.Fail("Không thể khởi tạo dịch vụ xác thực phiên (DB verifier).");
                            return;
                        }

                        var user = await dbContext.Users.AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new { u.IsActive, u.Role, u.AuthVersion })
                            .FirstOrDefaultAsync();

                        if (user is null || !user.IsActive)
                        {
                            context.Fail("Tài khoản không tồn tại hoặc đã bị khóa.");
                            return;
                        }

                        if (!string.Equals(tokenRole, user.Role, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Fail("Quyền tài khoản đã thay đổi. Vui lòng đăng nhập lại.");
                            return;
                        }

                        if (ver != user.AuthVersion)
                        {
                            context.Fail("Phiên đăng nhập đã hết hiệu lực hoặc không hợp lệ. Vui lòng đăng nhập lại.");
                            return;
                        }
                    }
                };
            });

        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IGoogleTokenSignatureVerifier, GoogleTokenSignatureVerifier>();
        services.AddSingleton<IGoogleLoginNonceStore, GoogleLoginNonceStore>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClassJoinPasswordProtector, AesGcmClassJoinPasswordProtector>();
        return services;
    }
}
