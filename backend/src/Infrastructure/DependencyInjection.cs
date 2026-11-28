using System;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Resilience;
using OralExamination.Infrastructure.Diagnostics;

namespace OralExamination.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IQuestionGenerationGuidanceProvider, OralExamination.Infrastructure.Services.QuestionGenerationGuidanceProvider>();
        services.AddAbuseTelemetry(configuration);
        var appealHours = configuration.GetValue<int?>("Appeals:WindowHours")
            ?? OralExamination.Application.Features.Appeals.AppealWindowOptions.DefaultHours;
        if (appealHours <= 0 || appealHours > 8760)
            throw new InvalidOperationException("Appeals:WindowHours must be between 1 and 8760.");
        services.AddSingleton(new OralExamination.Application.Features.Appeals.AppealWindowOptions { Hours = appealHours });
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing.");

        var envName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        var isProduction = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);

        var allowInMemoryFallback = !isProduction && (bool.TryParse(
            configuration["Database:AllowInMemoryFallback"] ?? configuration["DATABASE_ALLOW_IN_MEMORY_FALLBACK"],
            out var parsedAllowFallback) && parsedAllowFallback);

        var usePostgres = !allowInMemoryFallback || IsPostgresEndpointReachable(connectionString);
        if (usePostgres)
        {
            connectionString = EnsureHighConcurrencyPostgresPool(connectionString);
        }

        services.AddScoped<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>();

        services.AddDbContext<OralExamDbContext>((sp, options) =>
        {
            if (usePostgres)
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            }
            else
            {
                options.UseInMemoryDatabase("OralExamLiveDb");
            }

            options.AddInterceptors(sp.GetRequiredService<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<OralExamDbContext>());

        services.AddSingleton<ExternalDependencyResilience>();
        services.AddHttpClient<IAiQuestionGenerationService, OralExamination.Infrastructure.Services.GeminiQuestionGenerationService>(
                client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler(sp => new ExternalDependencyHttpHandler(sp.GetRequiredService<ExternalDependencyResilience>(), ExternalDependency.Gemini));
        services.AddHttpClient<IAiGradingService, OralExamination.Infrastructure.Services.GeminiAiGradingService>(
                client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler(sp => new ExternalDependencyHttpHandler(sp.GetRequiredService<ExternalDependencyResilience>(), ExternalDependency.Gemini));
        services.AddHttpClient<OralExamination.Infrastructure.Services.CloudflareWhisperService>(
                client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler(sp => new ExternalDependencyHttpHandler(sp.GetRequiredService<ExternalDependencyResilience>(), ExternalDependency.Whisper));
        services.AddHttpClient<OralExamination.Infrastructure.Services.GeminiSpeechToTextService>(
                client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler(sp => new ExternalDependencyHttpHandler(sp.GetRequiredService<ExternalDependencyResilience>(), ExternalDependency.Gemini));
        services.AddScoped<ISpeechToTextService, OralExamination.Infrastructure.Services.ResilientSpeechToTextService>();
        services.AddHttpClient<ITextToSpeechService, OralExamination.Infrastructure.Services.ResilientTextToSpeechService>(
                client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler(sp => new ExternalDependencyHttpHandler(sp.GetRequiredService<ExternalDependencyResilience>(), ExternalDependency.GoogleTts));
        services.AddSingleton<IStorageService, OralExamination.Infrastructure.Services.CloudflareR2StorageService>();
        services.AddSingleton<IGradingQueueChannel, OralExamination.Infrastructure.Channels.BoundedGradingQueueChannel>();
        services.AddSingleton<IRuntimeMetricsTracker, RuntimeMetricsTracker>();
        services.AddSingleton<IOfficialExamGradingQueue, OralExamination.Infrastructure.Channels.BoundedOfficialExamGradingQueue>();
        services.AddSingleton<IPdfDigitalSigner, OralExamination.Infrastructure.Reporting.PadesPdfDigitalSigner>();
        services.AddSingleton<IExamGradeReportExporter, OralExamination.Infrastructure.Reporting.ExamGradeReportExporter>();
        services.AddSingleton<IInClassShiftReportExporter, OralExamination.Infrastructure.Reporting.InClassShiftReportExporter>();
        services.AddSingleton<IExamRosterFileParser, OralExamination.Infrastructure.Services.ExamRosterFileParser>();
        services.AddSingleton<ICandidateRosterFileParser, OralExamination.Infrastructure.Services.CandidateRosterFileParser>();
        services.AddSingleton<IAdminUserSpreadsheetService, OralExamination.Infrastructure.Services.AdminUserSpreadsheetService>();

        return services;
    }

    private static string EnsureHighConcurrencyPostgresPool(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (builder.MinPoolSize < 10)
            {
                builder.MinPoolSize = 10;
            }

            if (builder.MinPoolSize > builder.MaxPoolSize)
            {
                builder.MinPoolSize = builder.MaxPoolSize;
            }

            if (builder.ConnectionIdleLifetime == 300)
            {
                builder.ConnectionIdleLifetime = 60;
            }

            return builder.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }

    private static bool IsPostgresEndpointReachable(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
            var port = builder.Port > 0 ? builder.Port : 5432;

            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            if (connectTask.Wait(TimeSpan.FromMilliseconds(3000)) && tcpClient.Connected) return true;
            using var retryClient = new TcpClient();
            return retryClient.ConnectAsync(host, port).Wait(TimeSpan.FromMilliseconds(3000)) && retryClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}
