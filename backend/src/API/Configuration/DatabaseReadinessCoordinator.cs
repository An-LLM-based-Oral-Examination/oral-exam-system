using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.API.Configuration;

public sealed class DatabaseStartupState
{
    private volatile bool _isStartupCompleted;
    private volatile bool _isRelational;
    private volatile string? _lastFailureReason;
    private readonly SemaphoreSlim _healthProbeGate = new(1, 1);
    private readonly object _probeCacheLock = new();
    private bool _hasCachedHealthProbe;
    private bool _cachedHealthProbeIsHealthy;
    private DateTimeOffset _cachedHealthProbeExpiresAt;

    public bool IsStartupCompleted => _isStartupCompleted;
    public bool IsRelational => _isRelational;
    public string? LastFailureReason => _lastFailureReason;
    internal SemaphoreSlim HealthProbeGate => _healthProbeGate;

    public void MarkRelationalReady()
    {
        lock (_probeCacheLock)
        {
            _hasCachedHealthProbe = false;
        }
        _isRelational = true;
        _lastFailureReason = null;
        _isStartupCompleted = true;
    }

    public void MarkNonRelationalReady()
    {
        lock (_probeCacheLock)
        {
            _hasCachedHealthProbe = false;
        }
        _isRelational = false;
        _lastFailureReason = null;
        _isStartupCompleted = true;
    }

    public void MarkFailed(string reason)
    {
        lock (_probeCacheLock)
        {
            _hasCachedHealthProbe = false;
        }
        _lastFailureReason = reason;
        _isStartupCompleted = false;
    }

    internal bool TryGetCachedHealthProbe(DateTimeOffset now, int cacheDurationMs, out bool isHealthy)
    {
        if (cacheDurationMs <= 0)
        {
            isHealthy = false;
            return false;
        }

        lock (_probeCacheLock)
        {
            if (_hasCachedHealthProbe && now < _cachedHealthProbeExpiresAt)
            {
                isHealthy = _cachedHealthProbeIsHealthy;
                return true;
            }
        }

        isHealthy = false;
        return false;
    }

    internal void SetCachedHealthProbe(bool isHealthy, DateTimeOffset now, int cacheDurationMs)
    {
        lock (_probeCacheLock)
        {
            if (cacheDurationMs <= 0)
            {
                _hasCachedHealthProbe = false;
                return;
            }

            _hasCachedHealthProbe = true;
            _cachedHealthProbeIsHealthy = isHealthy;
            _cachedHealthProbeExpiresAt = now.AddMilliseconds(cacheDurationMs);
        }
    }
}

public static class DatabaseReadinessCoordinator
{
    private const int DefaultMaxAttempts = 10;
    private const int DefaultRetryDelayMs = 2000;
    private const int DefaultHealthTimeoutMs = 3000;
    private const int DefaultHealthCacheDurationMs = 3000;
    private const int MinBootstrapPasswordLength = 12;

    public static IServiceCollection AddDatabaseReadiness(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseStartupState>();
        return services;
    }

    public static async Task RunStartupSequenceAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        CancellationToken cancellationToken = default)
    {
        var maxAttempts = Math.Max(1, configuration.GetValue<int?>("Database:StartupMaxAttempts") ?? DefaultMaxAttempts);
        var retryDelayMs = Math.Max(1, configuration.GetValue<int?>("Database:StartupRetryDelayMs") ?? DefaultRetryDelayMs);
        var delay = TimeSpan.FromMilliseconds(retryDelayMs);
        var waitAsync = delayAsync ?? Task.Delay;

        var startupState = services.GetRequiredService<DatabaseStartupState>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var scope = services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

                if (!context.Database.IsRelational())
                {
                    logger.LogInformation("Database is not relational. Skipping schema reconciliation.");
                    startupState.MarkNonRelationalReady();
                    return;
                }

                var canConnect = await context.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    throw new InvalidOperationException("Cannot connect to PostgreSQL server yet.");
                }

                logger.LogInformation(
                    "PostgreSQL connection established. Initiating 4-stage indestructible database startup (attempt {Attempt}/{MaxAttempts}).",
                    attempt,
                    maxAttempts);

                // Stage 1/4: Pre-Migration Reconciliation
                try
                {
                    logger.LogInformation("Stage 1/4: Running PostgresSchemaReconciler.ReconcilePreMigrationAsync...");
                    await PostgresSchemaReconciler.ReconcilePreMigrationAsync(context);
                    logger.LogInformation("Stage 1/4: Pre-migration reconciliation completed.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogCritical(ex, "Stage 1/4: Pre-migration schema reconciliation failed. Stopping API startup.");
                    throw;
                }

                // Stage 2/4: EF Core Migrations
                try
                {
                    logger.LogInformation("Stage 2/4: Applying EF Core migrations via context.Database.MigrateAsync()...");
                    await context.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Stage 2/4: EF Core migrations applied successfully.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogCritical(ex, "Stage 2/4: EF Core migration failed. Stopping API startup.");
                    throw;
                }

                // Stage 3/4: Post-Migration Reconciliation
                try
                {
                    logger.LogInformation("Stage 3/4: Running PostgresSchemaReconciler.ReconcilePostMigrationAsync...");
                    await PostgresSchemaReconciler.ReconcilePostMigrationAsync(context);
                    logger.LogInformation("Stage 3/4: Post-migration reconciliation and schema self-healing completed successfully.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogCritical(ex, "Stage 3/4: Post-migration schema reconciliation failed. Stopping API startup.");
                    throw;
                }

                // Stage 3b/4: Environment-based Initial Admin Bootstrap (Production-safe, zero hardcoded password)
                await EnsureBootstrapAdminAsync(context, scope.ServiceProvider, configuration, logger, cancellationToken);

                // Stage 4/4: Optional Development Seed (strictly gated to Development + explicit flag)
                var seedConfig = configuration["Database:EnableDevelopmentSeed"]
                    ?? configuration["DATABASE_ENABLE_DEVELOPMENT_SEED"];
                var shouldSeed = environment.IsDevelopment()
                    && bool.TryParse(seedConfig, out var parsedSeed)
                    && parsedSeed;

                if (shouldSeed)
                {
                    try
                    {
                        logger.LogInformation("Stage 4/4: Running DevelopmentDataSeeder.SeedAsync...");
                        await DevelopmentDataSeeder.SeedAsync(context);
                        logger.LogInformation("Stage 4/4: Initial academic catalog and development data verified successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Stage 4/4 Warning: DevelopmentDataSeeder encountered an error; continuing API startup.");
                    }
                }

                var reviewSeedConfig = configuration["Database:EnableTask04ReviewSeed"]
                    ?? configuration["DATABASE_ENABLE_TASK04_REVIEW_SEED"];
                var shouldSeedTask04Review = bool.TryParse(reviewSeedConfig, out var parsedReviewSeed) && parsedReviewSeed;
                if (environment.IsDevelopment() && shouldSeedTask04Review)
                {
                    try
                    {
                        logger.LogInformation("Running isolated Task 04 review fixture...");
                        await Task04ReviewDataSeeder.SeedAsync(context);
                        logger.LogInformation("Task 04 review fixture is ready.");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Task 04 review fixture could not be created; continuing API startup.");
                    }
                }

                // Final Schema Health Check Probe
                try
                {
                    var conn = context.Database.GetDbConnection();
                    var openedHere = conn.State != ConnectionState.Open;
                    if (openedHere)
                    {
                        await conn.OpenAsync(cancellationToken);
                    }

                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = """
                            SELECT COUNT(*)
                            FROM information_schema.tables
                            WHERE table_schema = 'public'
                              AND table_name IN ('academic_programs', 'course_offerings', 'in_class_exam_shifts', 'notifications', 'course_clos')
                            """;
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
                        if (count < 5)
                        {
                            throw new InvalidOperationException($"[FAIL CLOSED] Schema Health Check Probe found {count}/5 required tables.");
                        }

                        logger.LogInformation(
                            "Schema Health Check Probe: Verified physical presence of all {FoundCount}/5 key academic and in-class exam tables.",
                            count);
                    }
                    finally
                    {
                        if (openedHere && conn.State == ConnectionState.Open)
                        {
                            await conn.CloseAsync();
                        }
                    }

                    await PostgresSchemaReconciler.VerifyPracticeAnswersUniqueConstraintsAsync(context);
                    logger.LogInformation("Schema Health Check Probe: Verified physical presence and validity of MF-01 practice_answers schema invariants.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogCritical(ex, "Schema Health Check Probe failed. Stopping API startup.");
                    throw;
                }

                startupState.MarkRelationalReady();
                logger.LogInformation("Indestructible database startup sequence completed successfully.");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                startupState.MarkFailed("Database startup cancelled by host shutdown.");
                throw;
            }
            catch (Exception ex) when (ex.Message.Contains("[FAIL CLOSED]", StringComparison.Ordinal))
            {
                startupState.MarkFailed("Schema invariant failure during startup.");
                logger.LogCritical(
                    ex,
                    "CRITICAL: Schema invariant validation failed with [FAIL CLOSED]. Aborting API startup immediately.");
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database connection or startup was not ready (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds} seconds.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);

                await waitAsync(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                startupState.MarkFailed("Database startup exhausted retry budget.");
                logger.LogCritical(
                    ex,
                    "[FAIL CLOSED] Database startup did not complete after {MaxAttempts} attempts. Aborting API startup instead of running in degraded mode.",
                    maxAttempts);
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Database startup failed after {maxAttempts} attempts.",
                    ex);
            }
        }
    }

    public static Task EnsureBootstrapAdminAsync(
        OralExamDbContext context,
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            EnsureBootstrapAdminCoreAsync(context, services, configuration, logger, cancellationToken));

    private static async Task EnsureBootstrapAdminCoreAsync(
        OralExamDbContext context,
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Serialize startup replicas even when their configured identities differ.
        await using var transaction = context.Database.IsNpgsql()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (transaction is not null)
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(627418501)", cancellationToken);
        }

        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive, cancellationToken))
        {
            return;
        }

        var email = configuration["BootstrapAdmin:Email"] ?? configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["BootstrapAdmin:Password"] ?? configuration["BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Database has 0 active admin accounts and BootstrapAdmin:Email/BOOTSTRAP_ADMIN_EMAIL or BootstrapAdmin:Password/BOOTSTRAP_ADMIN_PASSWORD is not configured; skipping initial admin bootstrap.");
            return;
        }

        var trimmedPassword = password.Trim();
        if (!string.Equals(password, trimmedPassword, StringComparison.Ordinal)
            || trimmedPassword.Length < MinBootstrapPasswordLength)
        {
            throw new InvalidOperationException(
                $"[FAIL CLOSED] Bootstrap admin password must be at least {MinBootstrapPasswordLength} characters long without leading or trailing whitespace.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var rawUsername = configuration["BootstrapAdmin:Username"] ?? configuration["BOOTSTRAP_ADMIN_USERNAME"];
        var username = string.IsNullOrWhiteSpace(rawUsername) ? "admin" : rawUsername.Trim();
        var normalizedUsername = username.ToLowerInvariant();
        var rawFullName = configuration["BootstrapAdmin:FullName"] ?? configuration["BOOTSTRAP_ADMIN_FULL_NAME"];
        var fullName = string.IsNullOrWhiteSpace(rawFullName) ? "System Administrator" : rawFullName.Trim();

        var collidingUsers = await context.Users
            .Where(u => u.Email.ToLower() == normalizedEmail
                || (u.Username != null && u.Username.ToLower() == normalizedUsername))
            .ToListAsync(cancellationToken);

        var hasher = services.GetService<IPasswordHasher>() ?? new Pbkdf2PasswordHasher();
        var now = DateTime.UtcNow;
        User targetAdmin;
        var isReactivation = false;

        if (collidingUsers.Count > 0)
        {
            if (collidingUsers.Count > 1
                || collidingUsers[0].Role != UserRole.Admin
                || collidingUsers[0].IsActive)
            {
                throw new InvalidOperationException(
                    $"[FAIL CLOSED] Cannot bootstrap admin because email '{normalizedEmail}' or username '{username}' collides with an existing user.");
            }

            targetAdmin = collidingUsers[0];
            targetAdmin.Email = normalizedEmail;
            targetAdmin.Username = username;
            targetAdmin.FullName = fullName;
            targetAdmin.PasswordHash = hasher.Hash(trimmedPassword);
            targetAdmin.MustChangePassword = true;
            targetAdmin.IsActive = true;
            targetAdmin.AuthVersion += 1;
            targetAdmin.UpdatedAt = now;
            isReactivation = true;
        }
        else
        {
            targetAdmin = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Username = username,
                FullName = fullName,
                PasswordHash = hasher.Hash(trimmedPassword),
                Role = UserRole.Admin,
                MustChangePassword = true,
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Users.Add(targetAdmin);
        }

        var audit = new AuditLog
        {
            UserId = targetAdmin.Id,
            Action = isReactivation ? "BOOTSTRAP_ADMIN_REACTIVATED" : "BOOTSTRAP_ADMIN_CREATED",
            EntityName = nameof(User),
            EntityId = targetAdmin.Id,
            NewValues = "{\"role\":\"admin\",\"isActive\":true,\"mustChangePassword\":true}",
            CreatedAt = now
        };
        context.AuditLogs.Add(audit);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            if (isReactivation)
            {
                logger.LogInformation(
                    "Reactivated inactive bootstrap admin account '{Username}' with MustChangePassword = true.",
                    username);
            }
            else
            {
                logger.LogInformation(
                    "Bootstrapped initial admin account '{Username}' with MustChangePassword = true.",
                    username);
            }
        }
        catch (Exception exception)
        {
            context.Entry(targetAdmin).State = EntityState.Detached;
            context.Entry(audit).State = EntityState.Detached;
            // PostgreSQL replicas are serialized above. Never hide an audit/write failure.
            // Disposing the transaction rolls back before the execution strategy retries.
            if (transaction is not null || exception is not DbUpdateException)
                throw;
            if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive, cancellationToken))
            {
                logger.LogInformation(
                    "Concurrent bootstrap detected; an active admin account already exists.");
                return;
            }

            throw;
        }
    }

    public static async Task HandleHealthRequestAsync(
        HttpContext httpContext,
        Func<DbConnection, CancellationToken, Task<bool>>? relationalPingOverride = null)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        var isHead = HttpMethods.IsHead(httpContext.Request.Method);

        var startupState = httpContext.RequestServices.GetService<DatabaseStartupState>();
        if (startupState is not { IsStartupCompleted: true })
        {
            await WriteUnhealthyResponseAsync(httpContext, isHead);
            return;
        }

        var configuration = httpContext.RequestServices.GetService<IConfiguration>();
        var parsedTimeout = int.TryParse(
            configuration?["Database:HealthProbeTimeoutMs"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredTimeout)
            ? configuredTimeout
            : DefaultHealthTimeoutMs;
        var timeoutMs = Math.Clamp(parsedTimeout, 100, 15000);

        var parsedCacheDuration = int.TryParse(
            configuration?["Database:HealthProbeCacheDurationMs"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredCacheDuration)
            ? configuredCacheDuration
            : DefaultHealthCacheDurationMs;
        var cacheDurationMs = Math.Clamp(parsedCacheDuration, 0, 10000);

        var timeProvider = httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;

        if (startupState.TryGetCachedHealthProbe(timeProvider.GetUtcNow(), cacheDurationMs, out var cachedHealthy))
        {
            if (cachedHealthy)
            {
                await WriteHealthyResponseAsync(
                    httpContext,
                    isHead,
                    startupState.IsRelational ? "Ready" : "InMemory",
                    timeProvider.GetUtcNow().UtcDateTime);
            }
            else
            {
                await WriteUnhealthyResponseAsync(httpContext, isHead);
            }
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        var lockAcquired = false;
        var probeExecuted = false;
        try
        {
            await startupState.HealthProbeGate.WaitAsync(timeoutCts.Token);
            lockAcquired = true;
            try
            {
                if (startupState.TryGetCachedHealthProbe(timeProvider.GetUtcNow(), cacheDurationMs, out cachedHealthy))
                {
                    if (cachedHealthy)
                    {
                        await WriteHealthyResponseAsync(
                            httpContext,
                            isHead,
                            startupState.IsRelational ? "Ready" : "InMemory",
                            timeProvider.GetUtcNow().UtcDateTime);
                    }
                    else
                    {
                        await WriteUnhealthyResponseAsync(httpContext, isHead);
                    }
                    return;
                }

                probeExecuted = true;
                await using var scope = httpContext.RequestServices.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

                if (!context.Database.IsRelational())
                {
                    var nowUtc = timeProvider.GetUtcNow();
                    startupState.SetCachedHealthProbe(true, nowUtc, cacheDurationMs);
                    await WriteHealthyResponseAsync(httpContext, isHead, "InMemory", nowUtc.UtcDateTime);
                    return;
                }

                var conn = context.Database.GetDbConnection();
                var commandTimeoutSeconds = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));
                var isAlive = relationalPingOverride is not null
                    ? await relationalPingOverride(conn, timeoutCts.Token)
                    : await ExecuteSelectOneProbeAsync(conn, commandTimeoutSeconds, timeoutCts.Token);

                var checkedAt = timeProvider.GetUtcNow();
                startupState.SetCachedHealthProbe(isAlive, checkedAt, cacheDurationMs);

                if (!isAlive)
                {
                    await WriteUnhealthyResponseAsync(httpContext, isHead);
                    return;
                }

                await WriteHealthyResponseAsync(httpContext, isHead, "Ready", checkedAt.UtcDateTime);
            }
            finally
            {
                startupState.HealthProbeGate.Release();
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (probeExecuted)
            {
                startupState.SetCachedHealthProbe(false, timeProvider.GetUtcNow(), cacheDurationMs);
            }
            var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("HealthProbe");
            logger?.LogWarning(
                ex,
                lockAcquired
                    ? "Database readiness probe /health failed."
                    : "Database readiness probe /health timed out waiting for probe gate.");
            await WriteUnhealthyResponseAsync(httpContext, isHead);
        }
    }

    private static async Task WriteHealthyResponseAsync(
        HttpContext httpContext,
        bool isHead,
        string databaseState,
        DateTime timestampUtc)
    {
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        if (!isHead)
        {
            await httpContext.Response.WriteAsJsonAsync(new
            {
                status = "Healthy",
                database = databaseState,
                schemaReconciled = true,
                timestamp = timestampUtc
            }, httpContext.RequestAborted);
        }
    }

    private static async Task<bool> ExecuteSelectOneProbeAsync(
        DbConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = commandTimeoutSeconds;
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return scalar is not null && Convert.ToInt32(scalar) == 1;
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Task WriteUnhealthyResponseAsync(HttpContext httpContext, bool isHead)
    {
        if (isHead)
        {
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        var problem = ApiProblems.Create(
            httpContext,
            StatusCodes.Status503ServiceUnavailable,
            "Hệ thống cơ sở dữ liệu hiện không sẵn sàng.",
            "DATABASE_UNAVAILABLE");
        problem.Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
        problem.Title = "Service Unavailable";

        return ApiProblems.WriteAsync(httpContext, problem, httpContext.RequestAborted);
    }
}
