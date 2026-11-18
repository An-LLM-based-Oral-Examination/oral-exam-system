using System.Data.Common;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Infrastructure;

public sealed class DatabaseReadinessAndStartupFailClosedTests
{
    [Fact(DisplayName = "TASK-THANH-02: Startup cạn lượt thử kết nối PostgreSQL bắt buộc ném [FAIL CLOSED] và cấm chạy degraded mode")]
    public async Task RunStartupSequence_WhenPostgresUnreachableAfterMaxAttempts_ThrowsFailClosedAndStaysNotReady()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:StartupMaxAttempts"] = "3",
                ["Database:StartupRetryDelayMs"] = "25"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabaseReadiness();
        services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable_db;Username=oralexam;Password=secret_pg_pwd;Timeout=1;Pooling=false"));

        await using var provider = services.BuildServiceProvider();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var delayCalls = 0;
        Task DeterministicDelay(TimeSpan _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            delayCalls++;
            return Task.CompletedTask;
        }

        var act = () => DatabaseReadinessCoordinator.RunStartupSequenceAsync(
            provider,
            config,
            env.Object,
            DeterministicDelay);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[FAIL CLOSED]*3 attempts*");

        // 3 attempts => 2 retry delays between attempt 1->2 and 2->3
        delayCalls.Should().Be(2);
        var state = provider.GetRequiredService<DatabaseStartupState>();
        state.IsStartupCompleted.Should().BeFalse();
        state.LastFailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "TASK-THANH-02: Môi trường InMemory non-relational đánh dấu sẵn sàng ngay và trả 200 OK cho cả GET lẫn HEAD /health")]
    public async Task HealthEndpoint_WhenNonRelationalStartupCompleted_Returns200ForGetAndHead()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddDatabaseReadiness();
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));

        await using var app = builder.Build();
        await DatabaseReadinessCoordinator.RunStartupSequenceAsync(app.Services, app.Configuration, app.Environment);
        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx));
        await app.StartAsync();

        using var client = app.GetTestClient();

        var getResp = await client.GetAsync("/health");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        getResp.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.GetProperty("schemaReconciled").GetBoolean().Should().BeTrue();

        // Docker/Alpine wget --spider sends HEAD /health
        using var headReq = new HttpRequestMessage(HttpMethod.Head, "/health");
        var headResp = await client.SendAsync(headReq);
        headResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await headResp.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "TASK-THANH-02: Khi Startup chưa hoàn tất hoặc mất DB, /health và /healthz trả 503 RFC 7807 không rò rỉ thông tin nhạy cảm")]
    public async Task HealthEndpoint_WhenStartupIncompleteOrRelationalPingFails_Returns503WithoutLeakingSecrets()
    {
        var isDbHealthy = false;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "3000",
            ["Database:HealthProbeCacheDurationMs"] = "0"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=SuperSecretDbPassword123!"));

        await using var app = builder.Build();

        Task<bool> SimulatedPing(DbConnection _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!isDbHealthy)
                throw new InvalidOperationException("FATAL: password authentication failed for SuperSecretDbPassword123!");
            return Task.FromResult(true);
        }

        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx, SimulatedPing));
        app.MapMethods("/healthz", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx, SimulatedPing));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var startupState = app.Services.GetRequiredService<DatabaseStartupState>();

        // Case 1: Startup not yet completed -> 503 immediately
        var beforeReadyResp = await client.GetAsync("/health");
        beforeReadyResp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // Case 2: Startup completed, DB healthy -> 200 OK
        startupState.MarkRelationalReady();
        isDbHealthy = true;
        var healthyResp = await client.GetAsync("/health");
        healthyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var healthyJson = JsonDocument.Parse(await healthyResp.Content.ReadAsStringAsync()).RootElement;
        healthyJson.GetProperty("database").GetString().Should().Be("Ready");

        // Case 3: PostgreSQL drops mid-flight -> /health & /healthz flip to 503 for rollback trigger
        isDbHealthy = false;
        var downResp = await client.GetAsync("/health");
        downResp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        downResp.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await downResp.Content.ReadAsStringAsync();
        body.Should().NotContain("SuperSecretDbPassword123!");
        var problem = JsonDocument.Parse(body).RootElement;
        problem.GetProperty("status").GetInt32().Should().Be(503);
        problem.GetProperty("errorCode").GetString().Should().Be("DATABASE_UNAVAILABLE");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();

        // HEAD /health (wget --spider) also returns 503 with empty body
        using var headDownReq = new HttpRequestMessage(HttpMethod.Head, "/healthz");
        var headDownResp = await client.SendAsync(headDownReq);
        headDownResp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await headDownResp.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "TASK-THANH-02: Khi PostgreSQL bị treo vượt quá HealthProbeTimeoutMs, /health chủ động cắt timeout và trả 503")]
    public async Task HealthEndpoint_WhenDatabaseProbeHangsBeyondTimeout_CancelsAndReturns503()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "150"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=test_pwd"));

        await using var app = builder.Build();
        await using (var warmScope = app.Services.CreateAsyncScope())
        {
            _ = warmScope.ServiceProvider.GetRequiredService<OralExamDbContext>().Model;
        }
        app.Services.GetRequiredService<DatabaseStartupState>().MarkRelationalReady();

        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(
                ctx,
                async (conn, ct) =>
                {
                    _ = conn;
                    var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    await using var registration = ct.Register(() => neverCompletes.TrySetCanceled(ct));
                    return await neverCompletes.Task;
                }));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "TASK-THANH-02: Đường dò thật SELECT 1 không dùng override trả 503 khi không kết nối được PostgreSQL")]
    public async Task HealthEndpoint_RealWireProbe_WhenPostgresPortClosed_Returns503()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "1500"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=1;Database=oralexam_db;Username=oralexam;Password=test_pwd;Timeout=1;Pooling=false"));

        await using var app = builder.Build();
        await using (var warmScope = app.Services.CreateAsyncScope())
        {
            _ = warmScope.ServiceProvider.GetRequiredService<OralExamDbContext>().Model;
        }
        app.Services.GetRequiredService<DatabaseStartupState>().MarkRelationalReady();
        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "TASK-THANH-02: Tín hiệu hủy SIGTERM lúc Startup dừng ngay lập tức mà không lặp retry")]
    public async Task RunStartupSequence_WhenShutdownCancellationTriggered_PropagatesOperationCanceledWithoutRetrying()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:StartupMaxAttempts"] = "5" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabaseReadiness();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        await using var provider = services.BuildServiceProvider();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var delayCalls = 0;
        var act = () => DatabaseReadinessCoordinator.RunStartupSequenceAsync(
            provider,
            config,
            env.Object,
            (_, _) => { delayCalls++; return Task.CompletedTask; },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        delayCalls.Should().Be(0);
    }

    [Fact(DisplayName = "AZ-B01: EnsureBootstrapAdminAsync tạo 1 admin với MustChangePassword = true khi DB chưa có admin, và giữ idempotent")]
    public async Task EnsureBootstrapAdminAsync_CreatesAdminWhenNoneExists_AndIsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "Initial.Admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Str0ng!BootstrapPass123",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "rootadmin",
                ["BOOTSTRAP_ADMIN_FULL_NAME"] = "Root Admin"
            })
            .Build();

        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
            context,
            sp,
            config,
            NullLogger.Instance);

        var admins = await context.Users.Where(u => u.Role == UserRole.Admin).ToListAsync();
        admins.Should().ContainSingle();
        var admin = admins[0];
        admin.Email.Should().Be("initial.admin@fpt.edu.vn");
        admin.Username.Should().Be("rootadmin");
        admin.FullName.Should().Be("Root Admin");
        admin.MustChangePassword.Should().BeTrue();
        admin.IsActive.Should().BeTrue();
        admin.AuthVersion.Should().Be(1);
        new Pbkdf2PasswordHasher().Verify("Str0ng!BootstrapPass123", admin.PasswordHash!).Should().BeTrue();

        // Second call must be idempotent even with different env email
        var secondConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "second.admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "AnotherStrongPass123!"
            })
            .Build();

        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
            context,
            sp,
            secondConfig,
            NullLogger.Instance);

        (await context.Users.CountAsync(u => u.Role == UserRole.Admin)).Should().Be(1);
    }

    [Fact(DisplayName = "AZ-B01: EnsureBootstrapAdminAsync bỏ qua an toàn khi thiếu env và chặn mật khẩu yếu < 12 ký tự")]
    public async Task EnsureBootstrapAdminAsync_SkipsWhenUnset_AndRejectsWeakPassword()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var emptyConfig = new ConfigurationBuilder().Build();
        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(context, sp, emptyConfig, NullLogger.Instance);
        (await context.Users.AnyAsync()).Should().BeFalse();

        var weakConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "short123"
            })
            .Build();

        var act = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
            context,
            sp,
            weakConfig,
            NullLogger.Instance);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least 12 characters*");
        (await context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact(DisplayName = "AZ-B08: /health cache kết quả probe trong 3 giây và chỉ probe lại sau khi hết hạn")]
    public async Task HealthEndpoint_CachesProbeResultForThreeSeconds_AndReprobesAfterExpiry()
    {
        var clock = new ControllableTimeProvider(new DateTimeOffset(2026, 11, 17, 6, 0, 0, TimeSpan.Zero));
        var probeCallCount = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "10000"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=test_pwd"));

        await using var app = builder.Build();
        await using (var warmScope = app.Services.CreateAsyncScope())
        {
            _ = warmScope.ServiceProvider.GetRequiredService<OralExamDbContext>().Model;
        }
        app.Services.GetRequiredService<DatabaseStartupState>().MarkRelationalReady();
        Task<bool> CountingPing(DbConnection _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref probeCallCount);
            return Task.FromResult(true);
        }
        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx, CountingPing));
        await app.StartAsync();

        using var client = app.GetTestClient();

        // First call probes DB
        var resp1 = await client.GetAsync("/health");
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(1);

        // Concurrent/subsequent calls within 3s hit cache without probing DB
        clock.Advance(TimeSpan.FromMilliseconds(2500));
        var concurrentTasks = Enumerable.Range(0, 5).Select(_ => client.GetAsync("/health")).ToArray();
        var concurrentResponses = await Task.WhenAll(concurrentTasks);
        concurrentResponses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        probeCallCount.Should().Be(1);

        // After 3s expiry, next call re-probes DB once
        clock.Advance(TimeSpan.FromMilliseconds(500));
        var respAfterExpiry = await client.GetAsync("/health");
        respAfterExpiry.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(2);
    }

    [Fact(DisplayName = "AZ-B08: /health fallback an toàn khi cấu hình HealthProbeTimeoutMs/CacheDurationMs không phải số nguyên")]
    public async Task HealthEndpoint_WhenNumericConfigIsMalformed_FallsBackToDefaultsWithoutThrowing500()
    {
        var clock = new ControllableTimeProvider(new DateTimeOffset(2026, 11, 17, 6, 0, 0, TimeSpan.Zero));
        var probeCallCount = 0;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "not-an-int",
            ["Database:HealthProbeCacheDurationMs"] = "invalid-duration"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=test_pwd"));

        await using var app = builder.Build();
        await using (var warmScope = app.Services.CreateAsyncScope())
        {
            _ = warmScope.ServiceProvider.GetRequiredService<OralExamDbContext>().Model;
        }
        app.Services.GetRequiredService<DatabaseStartupState>().MarkRelationalReady();
        Task<bool> CountingPing(DbConnection _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref probeCallCount);
            return Task.FromResult(true);
        }
        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx, CountingPing));
        await app.StartAsync();

        using var client = app.GetTestClient();

        var resp1 = await client.GetAsync("/health");
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(1);

        // Falls back to DefaultHealthCacheDurationMs = 3000ms
        clock.Advance(TimeSpan.FromMilliseconds(2500));
        var respCached = await client.GetAsync("/health");
        respCached.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(1);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        var respReprobed = await client.GetAsync("/health");
        respReprobed.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(2);
    }

    [Fact(DisplayName = "AZ-B08: Request /health hết hạn khi chờ HealthProbeGate trả 503 nhưng KHÔNG đầu độc cache thành unhealthy")]
    public async Task HealthEndpoint_WhenWaiterTimesOutOnProbeGate_Returns503WithoutPoisoningCache()
    {
        var clock = new ControllableTimeProvider(new DateTimeOffset(2026, 11, 17, 6, 0, 0, TimeSpan.Zero));
        var probeCallCount = 0;
        var pingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPingCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:HealthProbeTimeoutMs"] = "10000",
            ["Database:HealthProbeCacheDurationMs"] = "3000"
        });
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddDatabaseReadiness();
        builder.Services.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=test_pwd"));

        await using var app = builder.Build();
        await using (var warmScope = app.Services.CreateAsyncScope())
        {
            _ = warmScope.ServiceProvider.GetRequiredService<OralExamDbContext>().Model;
        }
        var startupState = app.Services.GetRequiredService<DatabaseStartupState>();
        startupState.MarkRelationalReady();

        Task<bool> GatedPing(DbConnection _, CancellationToken ct)
        {
            var callIndex = Interlocked.Increment(ref probeCallCount);
            if (callIndex == 1)
            {
                pingEntered.TrySetResult();
                return WaitWithCancellationAsync(allowPingCompletion.Task, ct);
            }

            return Task.FromResult(true);
        }

        app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) =>
            DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx, GatedPing));
        await app.StartAsync();

        // Build a second HttpContext sharing the exact same DatabaseStartupState, but with a 100ms timeout
        var waiterConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:HealthProbeTimeoutMs"] = "100",
                ["Database:HealthProbeCacheDurationMs"] = "3000"
            })
            .Build();
        var waiterServices = new ServiceCollection();
        waiterServices.AddLogging();
        waiterServices.AddSingleton<IConfiguration>(waiterConfig);
        waiterServices.AddSingleton<TimeProvider>(clock);
        waiterServices.AddSingleton(startupState);
        waiterServices.AddDbContext<OralExamDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=test_pwd"));
        await using var waiterProvider = waiterServices.BuildServiceProvider();

        // 1. Start Request 1 and pause it inside GatedPing while it holds HealthProbeGate
        using var req1AbortCts = new CancellationTokenSource();
        var req1ServicesScope = app.Services.CreateAsyncScope();
        var req1Context = new DefaultHttpContext
        {
            RequestServices = req1ServicesScope.ServiceProvider,
            RequestAborted = req1AbortCts.Token
        };
        req1Context.Request.Method = HttpMethods.Get;
        req1Context.Response.Body = new MemoryStream();

        var req1Task = DatabaseReadinessCoordinator.HandleHealthRequestAsync(req1Context, GatedPing);
        await pingEntered.Task;

        // 2. Send concurrent Request 2 with 100ms timeout -> times out waiting for HealthProbeGate -> 503
        var waiterContext = new DefaultHttpContext
        {
            RequestServices = waiterProvider
        };
        waiterContext.Request.Method = HttpMethods.Get;
        waiterContext.Response.Body = new MemoryStream();

        await DatabaseReadinessCoordinator.HandleHealthRequestAsync(waiterContext, GatedPing);
        waiterContext.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        probeCallCount.Should().Be(1);

        // 3. Cancel Request 1 via RequestAborted (so Request 1 leaves the probe cache unset)
        req1AbortCts.Cancel();
        var abortAct = async () => await req1Task;
        await abortAct.Should().ThrowAsync<OperationCanceledException>();
        await req1ServicesScope.DisposeAsync();

        // 4. Send Request 3 -> if Request 2 had poisoned the cache with false, Request 3 would get cached 503 without probing.
        // Because Request 2 did not poison the cache, Request 3 probes cleanly and returns 200 OK.
        using var client = app.GetTestClient();
        var recoveryResp = await client.GetAsync("/health");
        recoveryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        probeCallCount.Should().Be(2);
    }

    private static async Task<bool> WaitWithCancellationAsync(Task<bool> inner, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        var completed = await Task.WhenAny(inner, tcs.Task);
        return await completed;
    }

    [Fact(DisplayName = "AZ-B01: EnsureBootstrapAdminAsync vẫn tạo admin active mới khi chỉ tồn tại admin bị vô hiệu hóa (IsActive = false)")]
    public async Task EnsureBootstrapAdminAsync_WhenOnlyInactiveAdminExists_CreatesNewActiveAdmin()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "legacy.admin@fpt.edu.vn",
            Username = "legacy_admin",
            FullName = "Legacy Disabled Admin",
            Role = UserRole.Admin,
            IsActive = false,
            MustChangePassword = true,
            AuthVersion = 2
        });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "active.bootstrap@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Str0ng!BootstrapPass123",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "active_admin"
            })
            .Build();

        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(context, sp, config, NullLogger.Instance);

        var activeAdmins = await context.Users.Where(u => u.Role == UserRole.Admin && u.IsActive).ToListAsync();
        activeAdmins.Should().ContainSingle();
        activeAdmins[0].Email.Should().Be("active.bootstrap@fpt.edu.vn");
        activeAdmins[0].MustChangePassword.Should().BeTrue();
    }

    [Fact(DisplayName = "AZ-B01: EnsureBootstrapAdminAsync từ chối mật khẩu đệm khoảng trắng đầu/cuối")]
    public async Task EnsureBootstrapAdminAsync_RejectsWhitespacePaddedWeakPassword()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var paddedWeakConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "     a1!    "
            })
            .Build();

        var act = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
            context,
            sp,
            paddedWeakConfig,
            NullLogger.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[FAIL CLOSED]*at least 12 characters*");
        (await context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact(DisplayName = "AZ-B01: EnsureBootstrapAdminAsync chặn sạch lỗi trùng Email/Username không phân biệt hoa thường với user không phải Admin")]
    public async Task EnsureBootstrapAdminAsync_WhenNonAdminCollidesCaseInsensitively_ThrowsFailClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "Admin@fpt.edu.vn",
            Username = "ADMIN",
            FullName = "Existing Student",
            Role = UserRole.Student,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Str0ng!BootstrapPass123",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "admin"
            })
            .Build();

        var act = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
            context,
            sp,
            config,
            NullLogger.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[FAIL CLOSED]*collides with an existing user*");
        (await context.Users.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "M4-Item9: EnsureBootstrapAdminAsync kích hoạt lại an toàn 1 admin bị vô hiệu hóa khi trùng cả Email và Username (không phân biệt hoa thường)")]
    public async Task EnsureBootstrapAdminAsync_WhenSingleInactiveAdminCollidesOnEmailAndUsername_ReactivatesInPlace()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        var retainedAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var originalUpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Users.Add(new User
        {
            Id = retainedAdminId,
            Email = "Admin@FPT.EDU.VN",
            Username = "ADMIN",
            FullName = "Old Admin Name",
            Role = UserRole.Admin,
            PasswordHash = null,
            MustChangePassword = true,
            IsActive = false,
            AuthVersion = 2,
            CreatedAt = originalUpdatedAt,
            UpdatedAt = originalUpdatedAt
        });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "admin",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Re4ctivated!AdminPass123",
                ["BOOTSTRAP_ADMIN_FULL_NAME"] = "Reactivated Bootstrap Admin"
            })
            .Build();

        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(context, sp, config, NullLogger.Instance);

        var users = await context.Users.ToListAsync();
        users.Should().ContainSingle();
        var reactivated = users[0];
        reactivated.Id.Should().Be(retainedAdminId);
        reactivated.Email.Should().Be("admin@fpt.edu.vn");
        reactivated.Username.Should().Be("admin");
        reactivated.FullName.Should().Be("Reactivated Bootstrap Admin");
        reactivated.Role.Should().Be(UserRole.Admin);
        reactivated.IsActive.Should().BeTrue();
        reactivated.MustChangePassword.Should().BeTrue();
        reactivated.AuthVersion.Should().Be(3);
        reactivated.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        new Pbkdf2PasswordHasher().Verify("Re4ctivated!AdminPass123", reactivated.PasswordHash!).Should().BeTrue();
    }

    [Theory(DisplayName = "M4-Item9: EnsureBootstrapAdminAsync kích hoạt lại an toàn 1 admin bị vô hiệu hóa khi chỉ trùng Email hoặc chỉ trùng Username")]
    [InlineData("admin@fpt.edu.vn", "legacy_admin_username", "admin@fpt.edu.vn", "admin")]
    [InlineData("legacy.admin@fpt.edu.vn", "ADMIN", "admin@fpt.edu.vn", "admin")]
    public async Task EnsureBootstrapAdminAsync_WhenSingleInactiveAdminCollidesOnSingleKey_ReactivatesInPlace(
        string seededEmail,
        string seededUsername,
        string configuredEmail,
        string configuredUsername)
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var context = new OralExamDbContext(options);

        var retainedAdminId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = retainedAdminId,
            Email = seededEmail,
            Username = seededUsername,
            FullName = "Retained Inactive Admin",
            Role = UserRole.Admin,
            PasswordHash = null,
            MustChangePassword = true,
            IsActive = false,
            AuthVersion = 4
        });
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = configuredEmail,
                ["BOOTSTRAP_ADMIN_USERNAME"] = configuredUsername,
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "SingleKey!BootstrapPass123"
            })
            .Build();

        await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(context, sp, config, NullLogger.Instance);

        var users = await context.Users.ToListAsync();
        users.Should().ContainSingle();
        users[0].Id.Should().Be(retainedAdminId);
        users[0].Email.Should().Be(configuredEmail.ToLowerInvariant());
        users[0].Username.Should().Be(configuredUsername);
        users[0].IsActive.Should().BeTrue();
        users[0].MustChangePassword.Should().BeTrue();
        users[0].AuthVersion.Should().Be(5);
        new Pbkdf2PasswordHasher().Verify("SingleKey!BootstrapPass123", users[0].PasswordHash!).Should().BeTrue();
    }

    [Fact(DisplayName = "M4-Item9: EnsureBootstrapAdminAsync vẫn ném [FAIL CLOSED] khi trùng user không phải Admin đã vô hiệu hóa hoặc trùng >= 2 users")]
    public async Task EnsureBootstrapAdminAsync_WhenInactiveNonAdminOrMultipleUsersCollide_ThrowsFailClosed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "admin",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Str0ng!BootstrapPass123"
            })
            .Build();

        // Case 1: Inactive non-admin collision must remain fail-closed
        var nonAdminOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var nonAdminContext = new OralExamDbContext(nonAdminOptions))
        {
            nonAdminContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "Admin@fpt.edu.vn",
                Username = "other_user",
                FullName = "Inactive Lecturer",
                Role = UserRole.Lecturer,
                IsActive = false
            });
            await nonAdminContext.SaveChangesAsync();

            var actNonAdmin = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
                nonAdminContext,
                sp,
                config,
                NullLogger.Instance);
            await actNonAdmin.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*[FAIL CLOSED]*collides with an existing user*");
            (await nonAdminContext.Users.SingleAsync()).IsActive.Should().BeFalse();
        }

        // Case 2: Multi-row collision (1 inactive admin on email + another user on username) must remain fail-closed
        var multiOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var multiContext = new OralExamDbContext(multiOptions))
        {
            multiContext.Users.AddRange(
                new User
                {
                    Id = Guid.NewGuid(),
                    Email = "admin@fpt.edu.vn",
                    Username = "legacy_admin_1",
                    FullName = "Inactive Admin",
                    Role = UserRole.Admin,
                    IsActive = false
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Email = "student.collide@fpt.edu.vn",
                    Username = "ADMIN",
                    FullName = "Colliding Student",
                    Role = UserRole.Student,
                    IsActive = true
                });
            await multiContext.SaveChangesAsync();

            var actMulti = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(
                multiContext,
                sp,
                config,
                NullLogger.Instance);
            await actMulti.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*[FAIL CLOSED]*collides with an existing user*");
            (await multiContext.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive)).Should().Be(0);
        }
    }

    [Fact(DisplayName = "M4-Item9: DbUpdateException khi đang reactivate inactive admin sẽ detach targetAdmin sạch sẽ và kiểm tra active admin đồng thời")]
    public async Task EnsureBootstrapAdminAsync_WhenConcurrentDbUpdateExceptionDuringReactivation_DetachesAndChecksActiveAdmin()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(dbName).Options;

        var retainedAdminId = Guid.NewGuid();
        await using (var seedContext = new OralExamDbContext(options))
        {
            seedContext.Users.Add(new User
            {
                Id = retainedAdminId,
                Email = "admin@fpt.edu.vn",
                Username = "admin",
                FullName = "Inactive Admin",
                Role = UserRole.Admin,
                IsActive = false,
                MustChangePassword = true,
                AuthVersion = 1
            });
            await seedContext.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        await using var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@fpt.edu.vn",
                ["BOOTSTRAP_ADMIN_USERNAME"] = "admin",
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "Str0ng!BootstrapPass123"
            })
            .Build();

        // Case 1: Another instance concurrently activates an admin before throwing DbUpdateException -> returns cleanly with targetAdmin detached
        await using (var racingContext = new ConcurrentDbUpdateContext(options, activateAnotherAdminBeforeThrow: true))
        {
            await DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(racingContext, sp, config, NullLogger.Instance);
            var trackedTarget = racingContext.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == retainedAdminId);
            trackedTarget.Should().BeNull();
        }

        // Case 2: No active admin exists when DbUpdateException fires -> detaches targetAdmin and rethrows
        var noWinnerDbName = Guid.NewGuid().ToString();
        var noWinnerOptions = new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(noWinnerDbName).Options;
        await using (var seedNoWinner = new OralExamDbContext(noWinnerOptions))
        {
            seedNoWinner.Users.Add(new User
            {
                Id = retainedAdminId,
                Email = "admin@fpt.edu.vn",
                Username = "admin",
                FullName = "Inactive Admin",
                Role = UserRole.Admin,
                IsActive = false
            });
            await seedNoWinner.SaveChangesAsync();
        }

        await using (var failingContext = new ConcurrentDbUpdateContext(noWinnerOptions, activateAnotherAdminBeforeThrow: false))
        {
            var act = () => DatabaseReadinessCoordinator.EnsureBootstrapAdminAsync(failingContext, sp, config, NullLogger.Instance);
            await act.Should().ThrowAsync<DbUpdateException>();
            var trackedTarget = failingContext.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == retainedAdminId);
            trackedTarget.Should().BeNull();
        }
    }

    private sealed class ConcurrentDbUpdateContext : OralExamDbContext
    {
        private readonly DbContextOptions<OralExamDbContext> _options;
        private readonly bool _activateAnotherAdminBeforeThrow;

        public ConcurrentDbUpdateContext(
            DbContextOptions<OralExamDbContext> options,
            bool activateAnotherAdminBeforeThrow)
            : base(options)
        {
            _options = options;
            _activateAnotherAdminBeforeThrow = activateAnotherAdminBeforeThrow;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_activateAnotherAdminBeforeThrow)
            {
                await using var concurrent = new OralExamDbContext(_options);
                concurrent.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    Email = "winner.admin@fpt.edu.vn",
                    Username = "winner_admin",
                    FullName = "Winner Admin",
                    Role = UserRole.Admin,
                    IsActive = true,
                    MustChangePassword = true,
                    AuthVersion = 1
                });
                await concurrent.SaveChangesAsync(cancellationToken);
            }

            throw new DbUpdateException("Simulated concurrent bootstrap conflict.");
        }
    }

    private sealed class ControllableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
