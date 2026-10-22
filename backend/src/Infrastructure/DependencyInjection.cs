using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing.");

        var usePostgres = IsPostgresEndpointReachable(connectionString);

        services.AddScoped<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>();

        services.AddDbContext<OralExamDbContext>((sp, options) =>
        {
            if (usePostgres)
            {
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            else
            {
                options.UseInMemoryDatabase("OralExamLiveDb");
            }

            options.AddInterceptors(sp.GetRequiredService<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<OralExamDbContext>());

        services.AddHttpClient<IAiQuestionGenerationService, OralExamination.Infrastructure.Services.GeminiQuestionGenerationService>();
        services.AddHttpClient<IAiGradingService, OralExamination.Infrastructure.Services.GeminiAiGradingService>();
        services.AddHttpClient<OralExamination.Infrastructure.Services.CloudflareWhisperService>();
        services.AddHttpClient<OralExamination.Infrastructure.Services.GeminiSpeechToTextService>();
        services.AddScoped<ISpeechToTextService, OralExamination.Infrastructure.Services.ResilientSpeechToTextService>();
        services.AddHttpClient<ITextToSpeechService, OralExamination.Infrastructure.Services.ResilientTextToSpeechService>();
        services.AddSingleton<IStorageService, OralExamination.Infrastructure.Services.CloudflareR2StorageService>();
        services.AddSingleton<IGradingQueueChannel, OralExamination.Infrastructure.Channels.BoundedGradingQueueChannel>();
        services.AddSingleton<IOfficialExamGradingQueue, OralExamination.Infrastructure.Channels.BoundedOfficialExamGradingQueue>();
        services.AddSingleton<IExamGradeReportExporter, OralExamination.Infrastructure.Reporting.ExamGradeReportExporter>();

        return services;
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
            if (connectTask.Wait(TimeSpan.FromMilliseconds(3000)) && tcpClient.Connected) return true; using var retryClient = new TcpClient(); return retryClient.ConnectAsync(host, port).Wait(TimeSpan.FromMilliseconds(3000)) && retryClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}
