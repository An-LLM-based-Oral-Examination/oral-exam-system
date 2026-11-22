using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OralExamination.API.Configuration;

public static class OralExamRateLimiterExtensions
{
    public const string LoginPolicy = "auth-login";
    public const string RefreshPolicy = "auth-refresh";
    public const string PublicVerifyPolicy = "public-verify";

    /// <summary>
    /// Lấy Client IP an toàn từ Connection.RemoteIpAddress đã được chuẩn hóa bởi UseForwardedHeaders.
    /// Tuyệt đối không đọc trực tiếp client header X-Forwarded-For chưa qua xác thực để tránh IP spoofing / bucket rotation.
    /// </summary>
    public static string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }

    public static IServiceCollection AddOralExamRateLimiting(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        return services.AddApiAbuseProtection(configuration ?? new ConfigurationBuilder().Build());
    }

    internal static void ConfigurePolicies(RateLimiterOptions options, AbuseProtectionOptions settings)
    {
        var window = TimeSpan.FromSeconds(settings.WindowSeconds);

        options.AddPolicy(LoginPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.AuthLoginPolicyPermits,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        options.AddPolicy(RefreshPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.RefreshPermits,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        options.AddPolicy(PublicVerifyPolicy, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PublicVerifyPermits,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    }
}
