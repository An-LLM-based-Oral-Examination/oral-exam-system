using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using OralExamination.Infrastructure.Diagnostics;

namespace OralExamination.API.Configuration;

public sealed class AbuseProtectionOptions
{
    public int WindowSeconds { get; set; } = 60;
    public int LoginPermits { get; set; } = 360;
    public int AuthLoginPolicyPermits { get; set; } = 360;
    public int RefreshPermits { get; set; } = 600;
    public int HubNegotiatePermits { get; set; } = 360;
    public int UnauthenticatedChallengePermits { get; set; } = 360;
    public int SpeechPermits { get; set; } = 30;
    public int UploadPermits { get; set; } = 20;
    public int AiPermits { get; set; } = 15;
    public int SubmitPermits { get; set; } = 60;
    public int ClassJoinPermits { get; set; } = 15;
    public int PublicVerifyPermits { get; set; } = 30;
    public int GeneralPermits { get; set; } = 600;
    public long MaxUploadBytes { get; set; } = 26 * 1024 * 1024;
    public int ExpensiveRequestSeconds { get; set; } = 90;
}

public static class AbuseProtection
{
    private sealed class RegistrationMarker { }

    public static IServiceCollection AddApiAbuseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RegistrationMarker)))
            return services;
        services.AddSingleton<RegistrationMarker>();
        var settings = BindAndValidateOptions(configuration);
        services.AddSingleton(settings);
        services.AddAbuseTelemetry(configuration);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            // Keep framework loopback defaults. Never trust arbitrary forwarded headers.
            foreach (var address in ReadConfiguredEntries(configuration, "Security:TrustedProxies", "SECURITY_TRUSTED_PROXIES"))
            {
                if (!IPAddress.TryParse(address, out var parsedProxy))
                    throw new InvalidOperationException($"Invalid IP address in Security:TrustedProxies: '{address}'.");
                options.KnownProxies.Add(parsedProxy);
            }

            foreach (var cidr in ReadConfiguredEntries(configuration, "Security:TrustedNetworks", "SECURITY_TRUSTED_NETWORKS"))
            {
                options.KnownNetworks.Add(ParseCanonicalNetwork(cidr));
            }
        });
        services.AddRequestTimeouts(options => options.AddPolicy("expensive", new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(settings.ExpensiveRequestSeconds), TimeoutStatusCode = 408,
            // WriteAsJsonAsync replaces CancellationToken.None with the cancelled request token.
            // Use a separate bounded, cancellable token so the 408 body is actually sent.
            WriteTimeoutResponse = async context =>
            {
                using var responseBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await ApiProblems.WriteAsync(context, ApiProblems.Create(context, 408), responseBudget.Token);
            }
        }));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api") && !IsHubNegotiate(context))
                    return RateLimitPartition.GetNoLimiter("non-api");
                if (IsUnauthenticatedBearerChallenge(context))
                {
                    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"unauth-challenge:ip:{remoteIp}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.UnauthenticatedChallengePermits,
                            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                }

                var category = Category(context);
                var identity = context.User.Identity?.IsAuthenticated == true
                    ? context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : null;
                var key = identity is null ? $"ip:{context.Connection.RemoteIpAddress}" : $"user:{identity}";
                var permits = category switch
                {
                    "login" => settings.LoginPermits,
                    "refresh" => settings.RefreshPermits,
                    "hub-negotiate" => settings.HubNegotiatePermits,
                    "class-join" => settings.ClassJoinPermits,
                    "speech" => settings.SpeechPermits,
                    "upload" => settings.UploadPermits,
                    "ai" => settings.AiPermits,
                    "submit" => settings.SubmitPermits,
                    _ => settings.GeneralPermits
                };
                return RateLimitPartition.GetFixedWindowLimiter($"{category}:{key}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits, Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0, AutoReplenishment = true
                });
            });
            options.OnRejected = (rejection, cancellationToken) =>
                WriteRateLimitRejectedAsync(rejection, settings.WindowSeconds, cancellationToken);
            OralExamRateLimiterExtensions.ConfigurePolicies(options, settings);
        });
        return services;
    }

    public static AbuseProtectionOptions BindAndValidateOptions(IConfiguration? configuration)
    {
        var settings = new AbuseProtectionOptions();
        configuration?.GetSection("Security:Abuse").Bind(settings);
        if (settings.WindowSeconds < 1 || settings.LoginPermits < 1 || settings.AuthLoginPolicyPermits < 1
            || settings.RefreshPermits < 1 || settings.HubNegotiatePermits < 1
            || settings.UnauthenticatedChallengePermits < 1
            || settings.SpeechPermits < 1 || settings.UploadPermits < 1
            || settings.AiPermits < 1 || settings.SubmitPermits < 1 || settings.ClassJoinPermits < 1
            || settings.PublicVerifyPermits < 1 || settings.GeneralPermits < 1
            || settings.MaxUploadBytes < 1024 || settings.ExpensiveRequestSeconds < 1)
        {
            throw new InvalidOperationException("Security:Abuse limits must be positive.");
        }

        return settings;
    }

    public static async ValueTask WriteRateLimitRejectedAsync(
        OnRejectedContext rejection,
        int defaultWindowSeconds,
        CancellationToken cancellationToken)
    {
        var context = rejection.HttpContext;
        var seconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var remaining)
            ? Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))
            : Math.Max(1, defaultWindowSeconds);

        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        var metricCategory = IsUnauthenticatedBearerChallenge(context) ? "unauth-challenge" : Category(context);
        context.RequestServices?.GetService<AbuseMetrics>()?.Rejected(metricCategory);

        var problem = ApiProblems.Create(
            context,
            StatusCodes.Status429TooManyRequests,
            "Bạn gửi yêu cầu quá nhanh. Vui lòng thử lại sau.",
            "RATE_LIMITED");
        problem.Type = "https://tools.ietf.org/html/rfc7807";
        problem.Title = "Too Many Requests";

        await ApiProblems.WriteAsync(context, problem, cancellationToken);
    }

    public static string Category(HttpContext context)
    {
        if (IsHubNegotiate(context)) return "hub-negotiate";
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ActionName.ToLowerInvariant() ?? "";
        if (path.StartsWith("/api/v1/auth/") && HttpMethods.IsPost(context.Request.Method))
        {
            if (path.EndsWith("/sign-out")) return "general";
            if (path.EndsWith("/refresh-token")) return "refresh";
            return "login";
        }
        if (HttpMethods.IsPost(context.Request.Method) && IsClassJoinPath(path)) return "class-join";
        if (path.Contains("/speech/") && !path.EndsWith("/voices")) return "speech";
        if (path.Contains("upload") || action.Contains("upload")) return "upload";
        if (path.Contains("generate") || action.Contains("generate") || path.Contains("/grade")) return "ai";
        if (path.Contains("/submit") || path.EndsWith("-submit") || action.Contains("submit")) return "submit";
        return "general";
    }

    private static bool IsClassJoinPath(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.EndsWith("/classes/join", StringComparison.Ordinal) ||
               (trimmed.Contains("/classes/", StringComparison.Ordinal) &&
                trimmed.EndsWith("/join", StringComparison.Ordinal));
    }

    internal static bool IsHubNegotiate(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase) &&
        context.Request.Path.Value?.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true &&
        HttpMethods.IsPost(context.Request.Method);

    private static bool RequiresAuthorization(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null) return false;
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) return false;
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
    }

    private static bool IsUnauthenticatedBearerChallenge(HttpContext context) =>
        context.User.Identity?.IsAuthenticated != true &&
        RequiresAuthorization(context) &&
        HasPresentedBearerCredential(context);

    private static bool HasPresentedBearerCredential(HttpContext context)
    {
        foreach (var header in context.Request.Headers.Authorization)
        {
            if (!string.IsNullOrWhiteSpace(header) &&
                header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(context.Request.Query["access_token"]);
    }

    private static async Task WriteUnauthorizedChallengeAsync(HttpContext context)
    {
        var schemeProvider = context.RequestServices?.GetService<IAuthenticationSchemeProvider>();
        if (schemeProvider is not null && await schemeProvider.GetDefaultChallengeSchemeAsync() is not null)
        {
            await context.ChallengeAsync();
            return;
        }

        context.Response.Headers.WWWAuthenticate = "Bearer";
        var problem = ApiProblems.Create(
            context,
            StatusCodes.Status401Unauthorized,
            "Cần access token hợp lệ để truy cập tài nguyên này.",
            "UNAUTHORIZED");
        problem.Title = "Không xác thực được.";
        problem.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2";
        await ApiProblems.WriteAsync(context, problem, context.RequestAborted);
    }

    private static IEnumerable<string> ReadConfiguredEntries(IConfiguration configuration, string sectionKey, string flatEnvKey)
    {
        var sectionValues = configuration.GetSection(sectionKey).Get<string[]>() ?? [];
        var flatValue = configuration[flatEnvKey] ?? string.Empty;

        foreach (var raw in sectionValues.Append(flatValue))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var token in raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrEmpty(token))
                    yield return token;
            }
        }
    }

    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork ParseCanonicalNetwork(string cidr)
    {
        var parts = cidr.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
            throw new InvalidOperationException($"Invalid CIDR network in Security:TrustedNetworks: '{cidr}'.");

        var bytes = ip.GetAddressBytes();
        var maxPrefix = bytes.Length * 8;
        // Reject /0 wildcard (0.0.0.0/0, ::/0) so X-Forwarded-For can never be trusted from the entire Internet.
        if (prefixLength <= 0 || prefixLength > maxPrefix)
            throw new InvalidOperationException($"Prefix length out of range in Security:TrustedNetworks: '{cidr}'.");

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        if (remainingBits > 0 && fullBytes < bytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            bytes[fullBytes] &= mask;
            fullBytes++;
        }
        for (var i = fullBytes; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }

        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(new IPAddress(bytes), prefixLength);
    }

    public static IApplicationBuilder UseApiAbuseProtection(this IApplicationBuilder app)
    {
        app.UseRequestTimeouts();
        app.UseMiddleware<UploadBudgetMiddleware>();
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            if (IsUnauthenticatedBearerChallenge(context))
            {
                await WriteUnauthorizedChallengeAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }

    public sealed class UploadBudgetMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, AbuseProtectionOptions options, AbuseMetrics metrics)
        {
            var category = Category(context);
            if (context.Request.Path.StartsWithSegments("/api") || IsHubNegotiate(context)) metrics.Request(category);
            if ((category is "upload" or "speech") && HttpMethods.IsPost(context.Request.Method))
            {
                if (context.Request.ContentLength > options.MaxUploadBytes)
                {
                    await ApiProblems.WriteAsync(context, ApiProblems.Create(context, 413)); return;
                }
                var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = options.MaxUploadBytes;
            }
            await next(context);
        }
    }
}
