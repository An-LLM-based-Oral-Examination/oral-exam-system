using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddScoped<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>();

        services.AddDbContext<OralExamDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<OralExamination.Infrastructure.Persistence.Interceptors.OneWayLockInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<OralExamDbContext>());

        services.AddHttpClient<IAiQuestionGenerationService, OralExamination.Infrastructure.Services.GeminiQuestionGenerationService>();
        services.AddHttpClient<IAiGradingService, OralExamination.Infrastructure.Services.GeminiAiGradingService>();
        services.AddHttpClient<ISpeechToTextService, OralExamination.Infrastructure.Services.CloudflareWhisperService>();
        services.AddSingleton<IStorageService, OralExamination.Infrastructure.Services.CloudflareR2StorageService>();
        services.AddSingleton<IGradingQueueChannel, OralExamination.Infrastructure.Channels.BoundedGradingQueueChannel>();

        return services;
    }
}
