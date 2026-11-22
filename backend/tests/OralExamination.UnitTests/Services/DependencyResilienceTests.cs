using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Resilience;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Services;

public sealed class DependencyResilienceTests
{
    [Fact]
    public async Task SharedCircuit_StopsAnotherHttpClient_ThenRecoversAfterBreakDuration()
    {
        var clock = new ManualClock();
        using var resilience = Create(clock);
        var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var first = Client(resilience, transport);
        using var second = Client(resilience, transport);

        await Assert.ThrowsAsync<HttpRequestException>(() => first.GetAsync("https://example.test/gemini"));
        Assert.Equal(2, transport.Calls);
        await Assert.ThrowsAsync<HttpRequestException>(() => second.GetAsync("https://example.test/gemini"));
        Assert.Equal(2, transport.Calls);

        clock.Advance(TimeSpan.FromSeconds(31));
        transport.Response = _ => new HttpResponseMessage(HttpStatusCode.OK);
        using var recovered = await second.GetAsync("https://example.test/gemini");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(3, transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    public async Task PermanentHttpFailure_IsNotRetried_AndDoesNotOpenCircuit(HttpStatusCode status)
    {
        using var resilience = Create(new ManualClock());
        var transport = new StubTransport(_ => new HttpResponseMessage(status));
        using var client = Client(resilience, transport);
        using var first = await client.GetAsync("https://example.test/gemini");
        using var second = await client.GetAsync("https://example.test/gemini");
        using var third = await client.GetAsync("https://example.test/gemini");
        Assert.Equal(status, first.StatusCode);
        Assert.Equal(3, transport.Calls);
    }

    [Fact]
    public async Task Retry_RecreatesRequestBody_AndStopsAfterConfiguredBudget()
    {
        using var resilience = Create(new ManualClock());
        var bodies = new List<string>();
        var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))
        {
            Observe = async (request, ct) => bodies.Add(await request.Content!.ReadAsStringAsync(ct))
        };
        using var client = Client(resilience, transport);
        using var body = new StringContent("audio-payload");

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsync("https://example.test/gemini", body));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(new[] { "audio-payload", "audio-payload" }, bodies);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated_WithoutRetryOrOpeningCircuit()
    {
        using var resilience = Create(new ManualClock());
        var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = Client(resilience, transport);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync("https://example.test/gemini", canceled.Token));
        Assert.Equal(0, transport.Calls);
        using var success = await client.GetAsync("https://example.test/gemini");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
    }

    [Fact]
    public async Task OpenGeminiCircuit_DoesNotBlockWhisperOrR2()
    {
        using var resilience = Create(new ManualClock());
        await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync<int>(
            ExternalDependency.Gemini, _ => throw new HttpRequestException("offline")));
        Assert.Equal(42, await resilience.ExecuteAsync(ExternalDependency.Whisper, _ => Task.FromResult(42)));
        Assert.Equal(43, await resilience.ExecuteAsync(ExternalDependency.R2, _ => Task.FromResult(43)));
    }

    [Fact]
    public async Task UnsafeWrite_UsesOneAttempt_ButSharesCircuitWithReads()
    {
        using var resilience = Create(new ManualClock());
        var calls = 0;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync<int>(
                ExternalDependency.R2,
                _ => { calls++; throw new HttpRequestException("offline"); },
                retry: false));
        }
        await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync(
            ExternalDependency.R2, _ => { calls++; return Task.FromResult(1); }));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AttemptTimeout_IsDeterministic_AndReturnsRecoverableWorkerError()
    {
        var clock = new ManualClock();
        using var resilience = Create(clock);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = resilience.ExecuteAsync(ExternalDependency.Gemini, async ct =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 1;
        }, retry: false);
        await started.Task;
        clock.Advance(TimeSpan.FromSeconds(11));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => operation);
        Assert.Equal(HttpStatusCode.RequestTimeout, error.StatusCode);
    }

    [Fact]
    public async Task R2Metadata_UsesSharedCircuit_AndDoesNotRetryMissingObjects()
    {
        using var resilience = Create(new ManualClock());
        var sdk = new Mock<IAmazonS3>();
        sdk.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("outage") { StatusCode = HttpStatusCode.ServiceUnavailable });
        using var storage = new CloudflareR2StorageService(StorageConfiguration(), resilience, sdk.Object);
        await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.GetObjectMetadataAsync("exam/audio.webm"));
        await Assert.ThrowsAsync<HttpRequestException>(() => storage.GetObjectMetadataAsync("exam/audio.webm"));
        sdk.Verify(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        using var healthyResilience = Create(new ManualClock());
        var missing = new Mock<IAmazonS3>();
        missing.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });
        using var healthyStorage = new CloudflareR2StorageService(StorageConfiguration(), healthyResilience, missing.Object);
        Assert.Null(await healthyStorage.GetObjectMetadataAsync("exam/missing.webm"));
        missing.Verify(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InfrastructureRegistration_ActuallyProtectsAllTypedProviderClientsAcrossScopes()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=isolated;Username=isolated;Password=not-used",
            ["Resilience:Defaults:RetryCount"] = "1",
            ["Resilience:Defaults:RetryDelayMilliseconds"] = "0",
            ["Resilience:Defaults:MinimumThroughput"] = "2",
            ["Resilience:Defaults:FailureRatio"] = "1"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructure(config);
        var transports = new List<StubTransport>();
        foreach (var name in new[] { nameof(IAiGradingService), nameof(IAiQuestionGenerationService), nameof(GeminiSpeechToTextService), nameof(CloudflareWhisperService) })
        {
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() =>
            {
                var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                transports.Add(transport);
                return transport;
            });
        }
        using var provider = services.BuildServiceProvider();
        var clients = provider.GetRequiredService<IHttpClientFactory>();
        using var grading = clients.CreateClient(nameof(IAiGradingService));
        await Assert.ThrowsAsync<HttpRequestException>(() => grading.GetAsync("https://generativelanguage.googleapis.com/test"));
        using var generation = clients.CreateClient(nameof(IAiQuestionGenerationService));
        await Assert.ThrowsAsync<HttpRequestException>(() => generation.GetAsync("https://generativelanguage.googleapis.com/test"));
        using var stt = clients.CreateClient(nameof(GeminiSpeechToTextService));
        await Assert.ThrowsAsync<HttpRequestException>(() => stt.GetAsync("https://generativelanguage.googleapis.com/test"));
        using var whisper = clients.CreateClient(nameof(CloudflareWhisperService));
        await Assert.ThrowsAsync<HttpRequestException>(() => whisper.GetAsync("https://api.cloudflare.com/test"));
        Assert.Equal(4, transports.Count);
        Assert.Equal(new[] { 2, 0, 0, 2 }, transports.Select(t => t.Calls));
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        Assert.Same(firstScope.ServiceProvider.GetRequiredService<ExternalDependencyResilience>(),
            secondScope.ServiceProvider.GetRequiredService<ExternalDependencyResilience>());
    }

    [Fact]
    public async Task WhisperCircuitOpen_UsesRealSpeechOrchestratorGeminiFallback()
    {
        using var resilience = Create(new ManualClock());
        await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync<int>(
            ExternalDependency.Whisper, _ => throw new HttpRequestException("offline")));
        var whisperTransport = new StubTransport(_ => throw new InvalidOperationException("Open circuit must not reach network"));
        var geminiTransport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"CQRS separates commands and queries in the application layer."}]}}]}""")
        });
        using var whisperClient = new HttpClient(new ExternalDependencyHttpHandler(resilience, ExternalDependency.Whisper) { InnerHandler = whisperTransport });
        using var geminiClient = Client(resilience, geminiTransport);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "test-only",
            ["Cloudflare:ApiToken"] = "test-only",
            ["Gemini:ApiKey"] = "test-only"
        }).Build();
        var orchestrator = new ResilientSpeechToTextService(
            new CloudflareWhisperService(whisperClient, config), new GeminiSpeechToTextService(geminiClient, config));
        using var audio = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await orchestrator.TranscribeDetailedAsync(audio, "exam.webm");

        Assert.Equal("GeminiMultimodalStt", result.Provider);
        Assert.True(result.UsedFallback);
        Assert.Contains("CQRS", result.Text);
        Assert.Equal(0, whisperTransport.Calls);
        Assert.Equal(1, geminiTransport.Calls);
    }

    [Fact]
    public async Task GeminiGradingCircuitOpen_FailsRecoverably_WithoutInventingScoreOrCallingOtherModels()
    {
        using var resilience = Create(new ManualClock());
        await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync<int>(
            ExternalDependency.Gemini, _ => throw new HttpRequestException("offline")));
        var transport = new StubTransport(_ => throw new InvalidOperationException("Open circuit must not reach network"));
        using var client = Client(resilience, transport);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-only",
            ["Cloudflare:AccountId"] = "",
            ["Cloudflare:ApiToken"] = ""
        }).Build();
        var grading = new GeminiAiGradingService(client, config);
        var request = new AiGradingRequest
        {
            Transcript = "CQRS separates write commands from read queries and uses independent application handlers.",
            QuestionContent = "Explain CQRS command query separation and its handlers.",
            SampleAnswer = "Commands mutate state; queries return data through independent handlers.",
            Criteria = [new AiGradingCriterion { Id = "criterion", Name = "CQRS", MaxScore = 10, Weight = 1 }],
            ThrowOnTransientFailure = true
        };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => grading.GradeAnswerAsync(request));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task InProgressCallerCancellation_DoesNotRetryTransport()
    {
        using var resilience = Create(new ManualClock());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))
        {
            Observe = async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
        };
        using var client = Client(resilience, transport);
        using var cancel = new CancellationTokenSource();
        var operation = client.GetAsync("https://example.test/gemini", cancel.Token);
        await entered.Task;
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task ProductionConfiguration_SetsClassroomResilienceBudgetsForAllDependencies()
    {
        var productionSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API", "appsettings.Production.json"));
        Assert.True(File.Exists(productionSettingsPath), $"Expected production settings at '{productionSettingsPath}'.");

        var config = new ConfigurationBuilder()
            .AddJsonFile(productionSettingsPath, optional: false)
            .Build();

        var clock = new ManualClock();
        using var resilience = new ExternalDependencyResilience(config, NullLogger<ExternalDependencyResilience>.Instance, clock);

        // Gemini attempt timeout in production must survive 24s (slow AI generation/grading) and trip at 25s.
        var geminiStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var geminiOp = resilience.ExecuteAsync(ExternalDependency.Gemini, async ct =>
        {
            geminiStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 1;
        }, retry: false);
        await geminiStarted.Task;
        clock.Advance(TimeSpan.FromSeconds(24));
        Assert.False(geminiOp.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));
        var geminiError = await Assert.ThrowsAsync<HttpRequestException>(() => geminiOp);
        Assert.Equal(HttpStatusCode.RequestTimeout, geminiError.StatusCode);

        // Whisper attempt timeout in production must survive 19s and trip at 20s.
        var whisperStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var whisperOp = resilience.ExecuteAsync(ExternalDependency.Whisper, async ct =>
        {
            whisperStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 1;
        }, retry: false);
        await whisperStarted.Task;
        clock.Advance(TimeSpan.FromSeconds(19));
        Assert.False(whisperOp.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));
        var whisperError = await Assert.ThrowsAsync<HttpRequestException>(() => whisperOp);
        Assert.Equal(HttpStatusCode.RequestTimeout, whisperError.StatusCode);

        // CloudflareAi and R2 attempt timeouts in production must also survive 19s and trip at 20s.
        foreach (var dep in new[] { ExternalDependency.CloudflareAi, ExternalDependency.R2 })
        {
            var depStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var depOp = resilience.ExecuteAsync(dep, async ct =>
            {
                depStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return 1;
            }, retry: false);
            await depStarted.Task;
            clock.Advance(TimeSpan.FromSeconds(19));
            Assert.False(depOp.IsCompleted);
            clock.Advance(TimeSpan.FromSeconds(2));
            var depError = await Assert.ThrowsAsync<HttpRequestException>(() => depOp);
            Assert.Equal(HttpStatusCode.RequestTimeout, depError.StatusCode);
        }

        // MinimumThroughput = 20 prevents 6 transient failures from prematurely opening the shared Gemini circuit.
        for (var i = 0; i < 6; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => resilience.ExecuteAsync<int>(
                ExternalDependency.Gemini,
                _ => throw new HttpRequestException("transient", null, HttpStatusCode.TooManyRequests),
                retry: false));
        }

        Assert.Equal(200, await resilience.ExecuteAsync(ExternalDependency.Gemini, _ => Task.FromResult(200), retry: false));
    }

    [Fact]
    public async Task GeminiAiGradingService_WithProductionResilienceHandler_RetriesAfterAttemptTimeoutAndSucceeds()
    {
        var productionSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "API", "appsettings.Production.json"));
        var config = new ConfigurationBuilder()
            .AddJsonFile(productionSettingsPath, optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:Gemini:RetryDelayMilliseconds"] = "0",
                ["Gemini:ApiKey"] = "test-only-key",
                ["Cloudflare:AccountId"] = "",
                ["Cloudflare:ApiToken"] = ""
            })
            .Build();

        var clock = new ManualClock();
        using var resilience = new ExternalDependencyResilience(config, NullLogger<ExternalDependencyResilience>.Instance, clock);
        var attempt1Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var transport = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"candidates":[{"content":{"parts":[{"text":"{\"Score\":8.5,\"ConfidenceScore\":0.92,\"IsSuspicious\":false,\"Feedback\":\"Tốt\",\"CotTrace\":\"Đạt\",\"Details\":[{\"CriterionId\":\"c1\",\"Score\":8.5,\"Comment\":\"Rõ ràng\"}]}"}]}}]}
                """)
        })
        {
            Observe = async (_, ct) =>
            {
                if (++calls == 1)
                {
                    attempt1Started.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
            }
        };

        using var client = new HttpClient(new ExternalDependencyHttpHandler(resilience, ExternalDependency.Gemini) { InnerHandler = transport })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var grading = new GeminiAiGradingService(client, config);
        var request = new AiGradingRequest
        {
            Transcript = "CQRS separates write commands from read queries with independent handlers.",
            QuestionContent = "Explain CQRS command query separation.",
            SampleAnswer = "Commands mutate state while queries read state.",
            Criteria = [new AiGradingCriterion { Id = "c1", Name = "CQRS", MaxScore = 10, Weight = 1 }],
            ThrowOnTransientFailure = true
        };

        var gradingTask = grading.GradeAnswerAsync(request);
        await attempt1Started.Task;
        // Advance past AttemptTimeoutSeconds (25s): Polly must cancel Attempt 1 and retry Attempt 2 without outer timeoutCts aborting.
        clock.Advance(TimeSpan.FromSeconds(26));

        var result = await gradingTask;
        Assert.Equal(8.5m, result.Score);
        Assert.Equal(2, transport.Calls);
    }

    private static IConfiguration StorageConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:ServiceUrl"] = "https://r2.example.test",
            ["Storage:AccessKey"] = "test-only",
            ["Storage:SecretKey"] = "test-only"
        }).Build();

    private static ExternalDependencyResilience Create(TimeProvider clock) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Resilience:Defaults:RetryCount"] = "1",
            ["Resilience:Defaults:RetryDelayMilliseconds"] = "0",
            ["Resilience:Defaults:MinimumThroughput"] = "2",
            ["Resilience:Defaults:FailureRatio"] = "1",
            ["Resilience:Defaults:BreakDurationSeconds"] = "30"
        }).Build(), NullLogger<ExternalDependencyResilience>.Instance, clock);

    private static HttpClient Client(ExternalDependencyResilience resilience, HttpMessageHandler transport) => new(
        new ExternalDependencyHttpHandler(resilience, ExternalDependency.Gemini) { InnerHandler = transport }, false);

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = response;
        public Func<HttpRequestMessage, CancellationToken, Task>? Observe { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Observe is not null) await Observe(request, cancellationToken);
            return Response(request);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        private readonly List<ManualTimer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan duration)
        {
            _ticks += duration.Ticks;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
        }

        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state, TimeSpan due, TimeSpan period) : ITimer
        {
            private long _next = due == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + due.Ticks;
            private TimeSpan _period = period;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan timerPeriod)
            {
                if (_disposed) return false;
                _next = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
                _period = timerPeriod;
                return true;
            }
            public void FireIfDue()
            {
                if (_disposed || _next > clock._ticks) return;
                _next = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + _period.Ticks;
                callback(state);
            }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
