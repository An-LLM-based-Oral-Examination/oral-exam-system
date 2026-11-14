using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using OralExamination.Infrastructure.Diagnostics;

namespace OralExamination.API.Configuration;

public sealed class AbuseProtectionOptions
{
    public int WindowSeconds { get; set; } = 60;
    public int LoginPermits { get; set; } = 10;
    public int SpeechPermits { get; set; } = 30;
    public int UploadPermits { get; set; } = 20;
    public int AiPermits { get; set; } = 15;
    public int GeneralPermits { get; set; } = 600;
    public long MaxUploadBytes { get; set; } = 26 * 1024 * 1024;
    public int ExpensiveRequestSeconds { get; set; } = 90;
}

public static class AbuseProtection
{
    public static IServiceCollection AddApiAbuseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new AbuseProtectionOptions();
        configuration.GetSection("Security:Abuse").Bind(settings);
        if (settings.WindowSeconds < 1 || settings.LoginPermits < 1 || settings.SpeechPermits < 1 || settings.UploadPermits < 1
            || settings.AiPermits < 1 || settings.GeneralPermits < 1 || settings.MaxUploadBytes < 1024 || settings.ExpensiveRequestSeconds < 1)
            throw new InvalidOperationException("Security:Abuse limits must be positive.");
        services.AddSingleton(settings);
        services.AddAbuseTelemetry(configuration);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Keep framework loopback defaults. Never trust arbitrary forwarded headers.
            foreach (var address in configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(address));
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
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api")) return RateLimitPartition.GetNoLimiter("non-api");
                var category = Category(context);
                var identity = context.User.Identity?.IsAuthenticated == true
                    ? context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : null;
                var key = identity is null ? $"ip:{context.Connection.RemoteIpAddress}" : $"user:{identity}";
                var permits = category switch
                {
                    "login" => settings.LoginPermits, "speech" => settings.SpeechPermits,
                    "upload" => settings.UploadPermits, "ai" => settings.AiPermits, _ => settings.GeneralPermits
                };
                return RateLimitPartition.GetFixedWindowLimiter($"{category}:{key}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits, Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0, AutoReplenishment = true
                });
            });
            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var context = rejection.HttpContext;
                var seconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var remaining)
                    ? Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)) : settings.WindowSeconds;
                context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                context.RequestServices.GetRequiredService<AbuseMetrics>().Rejected(Category(context));
                await ApiProblems.WriteAsync(context, ApiProblems.Create(context, 429,
                    "Bạn gửi yêu cầu quá nhanh. Vui lòng thử lại sau.", "RATE_LIMITED"));
            };
        });
        return services;
    }

    public static string Category(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ActionName.ToLowerInvariant() ?? "";
        if (path.StartsWith("/api/v1/auth/") && HttpMethods.IsPost(context.Request.Method)) return "login";
        if (path.Contains("/speech/") && !path.EndsWith("/voices")) return "speech";
        if (path.Contains("upload") || action.Contains("upload")) return "upload";
        if (path.Contains("generate") || action.Contains("generate") || path.Contains("/grade") || action.Contains("submit")) return "ai";
        return "general";
    }

    public static IApplicationBuilder UseApiAbuseProtection(this IApplicationBuilder app)
    {
        app.UseRequestTimeouts();
        app.UseMiddleware<UploadBudgetMiddleware>();
        app.UseRateLimiter();
        return app;
    }

    public sealed class UploadBudgetMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, AbuseProtectionOptions options, AbuseMetrics metrics)
        {
            var category = Category(context);
            if (context.Request.Path.StartsWithSegments("/api")) metrics.Request(category);
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
