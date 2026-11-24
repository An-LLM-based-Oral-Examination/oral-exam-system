using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace OralExamination.Infrastructure.Resilience;

public enum ExternalDependency { Gemini, Whisper, CloudflareAi, R2, GoogleTts }

/// <summary>One circuit per dependency for the lifetime of the application, shared by every client/scope.</summary>
public sealed class ExternalDependencyResilience : IDisposable
{
    private readonly Dictionary<ExternalDependency, ResiliencePipeline> _attempts = [];
    private readonly Dictionary<ExternalDependency, ResiliencePipeline> _withRetries = [];
    private readonly Meter _meter = new("OralExamination.Dependencies", "1.0");
    private readonly Counter<long> _requests;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _transitions;
    private readonly Counter<long> _httpResponses;
    private readonly Histogram<double> _duration;

    public ExternalDependencyResilience(
        IConfiguration configuration,
        ILogger<ExternalDependencyResilience> logger,
        TimeProvider? timeProvider = null)
    {
        _requests = _meter.CreateCounter<long>("dependency_requests_total");
        _failures = _meter.CreateCounter<long>("dependency_failures_total");
        _retries = _meter.CreateCounter<long>("dependency_retries_total");
        _transitions = _meter.CreateCounter<long>("dependency_circuit_transitions_total");
        _httpResponses = _meter.CreateCounter<long>("dependency_http_responses_total");
        _duration = _meter.CreateHistogram<double>("dependency_duration_ms", "ms");
        foreach (var dependency in Enum.GetValues<ExternalDependency>())
        {
            var settings = DependencyResilienceSettings.Read(configuration, dependency);
            var tag = new KeyValuePair<string, object?>("dependency", dependency.ToString());
            ValueTask Transition(string state)
            {
                _transitions.Add(1, tag, new("state", state));
                logger.LogInformation("Dependency {Dependency} circuit changed to {State}.", dependency, state);
                return ValueTask.CompletedTask;
            }

            var attempts = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = settings.FailureRatio,
                    MinimumThroughput = settings.MinimumThroughput,
                    SamplingDuration = TimeSpan.FromSeconds(settings.SamplingDurationSeconds),
                    BreakDuration = TimeSpan.FromSeconds(settings.BreakDurationSeconds),
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                    OnOpened = _ => Transition("open"),
                    OnHalfOpened = _ => Transition("half_open"),
                    OnClosed = _ => Transition("closed")
                })
                .AddTimeout(TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds))
                .Build();
            _attempts.Add(dependency, attempts);
            var retryBuilder = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
                .AddTimeout(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds));
            if (settings.RetryCount > 0)
            {
                retryBuilder.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = settings.RetryCount,
                    Delay = TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                    OnRetry = _ =>
                    {
                        _retries.Add(1, tag);
                        return ValueTask.CompletedTask;
                    }
                });
            }
            _withRetries.Add(dependency, retryBuilder.AddPipeline(attempts).Build());
        }
    }

    public async Task<T> ExecuteAsync<T>(
        ExternalDependency dependency,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default,
        bool retry = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tag = new KeyValuePair<string, object?>("dependency", dependency.ToString());
        _requests.Add(1, tag);
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await (retry ? _withRetries[dependency] : _attempts[dependency])
                .ExecuteAsync(async ct => await action(ct), cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            _failures.Add(1, tag);
            // Keep the existing service fallback/worker retry/DLQ contract: a recoverable dependency failure.
            throw new HttpRequestException($"Dependency {dependency} circuit is open.", exception, HttpStatusCode.ServiceUnavailable);
        }
        catch (TimeoutRejectedException exception)
        {
            _failures.Add(1, tag);
            throw new HttpRequestException($"Dependency {dependency} exceeded its timeout budget.", exception, HttpStatusCode.RequestTimeout);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _failures.Add(1, tag);
            throw;
        }
        finally
        {
            _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tag);
        }
    }

    public static bool IsTransient(Exception exception) => exception switch
    {
        TimeoutRejectedException => true,
        HttpRequestException error => error.StatusCode is null || IsTransientStatus(error.StatusCode.Value),
        AmazonS3Exception error => IsTransientStatus(error.StatusCode) ||
            error.StatusCode == 0 && error.InnerException is HttpRequestException or IOException,
        IOException => true,
        // Caller cancellation, broken circuits, configuration/auth/validation errors must never retry.
        _ => false
    };

    public static bool IsTransientStatus(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    internal void RecordHttpResponse(ExternalDependency dependency, HttpStatusCode status) =>
        _httpResponses.Add(1, new("dependency", dependency.ToString()), new("status", (int)status));

    /// <summary>
    /// Computes an outer defensive timeout strictly above the Polly TotalTimeoutSeconds budget so Polly always
    /// owns attempt timeouts, retries, and circuit-breaker accounting when the HTTP handler is wired.
    /// </summary>
    public static TimeSpan ResolveOuterDefensiveTimeout(IConfiguration configuration, ExternalDependency dependency)
    {
        var totalSeconds = DependencyResilienceSettings.Read(configuration, dependency).TotalTimeoutSeconds;
        return TimeSpan.FromSeconds(totalSeconds + 2);
    }

    public void Dispose() => _meter.Dispose();
}

public sealed record DependencyResilienceSettings
{
    public int RetryCount { get; init; } = 1;
    public int RetryDelayMilliseconds { get; init; } = 400;
    public int MinimumThroughput { get; init; } = 6;
    public double FailureRatio { get; init; } = 0.5;
    public int SamplingDurationSeconds { get; init; } = 30;
    public int BreakDurationSeconds { get; init; } = 30;
    public int AttemptTimeoutSeconds { get; init; } = 10;
    public int TotalTimeoutSeconds { get; init; } = 25;

    internal static DependencyResilienceSettings Read(IConfiguration configuration, ExternalDependency dependency)
    {
        string? Value(string name) => configuration[$"Resilience:{dependency}:{name}"]
            ?? configuration[$"Resilience:Defaults:{name}"];
        int Integer(string name, int fallback, int min, int max) => Value(name) is { } raw
            ? int.TryParse(raw, out var value) && value >= min && value <= max
                ? value : throw new InvalidOperationException($"Invalid resilience setting {dependency}:{name}.")
            : fallback;
        var ratio = Value(nameof(FailureRatio));
        var failureRatio = ratio is null ? 0.5
            : double.TryParse(ratio, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0 && parsed <= 1 ? parsed
                : throw new InvalidOperationException($"Invalid resilience setting {dependency}:FailureRatio.");
        var isGoogleTts = dependency == ExternalDependency.GoogleTts;
        return new DependencyResilienceSettings
        {
            RetryCount = Integer(nameof(RetryCount), isGoogleTts ? 2 : 1, 0, 5),
            RetryDelayMilliseconds = Integer(nameof(RetryDelayMilliseconds), isGoogleTts ? 1000 : 400, 0, 5000),
            MinimumThroughput = Integer(nameof(MinimumThroughput), isGoogleTts ? 20 : 6, 2, 10000),
            FailureRatio = failureRatio,
            SamplingDurationSeconds = Integer(nameof(SamplingDurationSeconds), 30, 1, 3600),
            BreakDurationSeconds = Integer(nameof(BreakDurationSeconds), isGoogleTts ? 15 : 30, 1, 600),
            AttemptTimeoutSeconds = Integer(nameof(AttemptTimeoutSeconds), dependency == ExternalDependency.R2 ? 15 : 10, 1, 120),
            TotalTimeoutSeconds = Integer(nameof(TotalTimeoutSeconds), dependency == ExternalDependency.R2 ? 35 : (isGoogleTts ? 30 : 25), 1, 300)
        };
    }
}
