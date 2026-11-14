using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;

namespace OralExamination.Infrastructure.Diagnostics;

public sealed class AbuseMetrics : IAbuseMetricsReader, IDisposable
{
    private readonly Meter _meter = new("OralExamination.Abuse", "1.0");
    private readonly Counter<long> _requests;
    private readonly Counter<long> _rejections;
    private long _total;
    private long _rejected;
    public AbuseMetrics()
    {
        _requests = _meter.CreateCounter<long>("abuse_requests_total");
        _rejections = _meter.CreateCounter<long>("abuse_rejected_total");
    }
    public void Request(string policy) { Interlocked.Increment(ref _total); _requests.Add(1, new KeyValuePair<string, object?>("policy", policy)); }
    public void Rejected(string policy) { Interlocked.Increment(ref _rejected); _rejections.Add(1, new KeyValuePair<string, object?>("policy", policy)); }
    public AbuseMetricsSnapshot Snapshot(long lockouts) => new(Interlocked.Read(ref _total), Interlocked.Read(ref _rejected), lockouts, "api-process");
    public void Dispose() => _meter.Dispose();
}

public static class AbuseTelemetry
{
    public static IServiceCollection AddAbuseTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var login = new LoginProtectionOptions();
        configuration.GetSection("Security:LoginProtection").Bind(login);
        services.TryAddSingleton(login);
        services.TryAddSingleton<AbuseMetrics>();
        services.TryAddSingleton<IAbuseMetricsReader>(provider => provider.GetRequiredService<AbuseMetrics>());
        services.TryAddSingleton<ILoginAttemptGuard>(provider => new LoginAttemptGuard(provider.GetRequiredService<LoginProtectionOptions>()));
        return services;
    }
}
