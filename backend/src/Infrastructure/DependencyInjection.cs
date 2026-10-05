using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;

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

        services.AddScoped<IAiQuestionGenerationService, GeminiQuestionGenerationService>();

        return services;
    }
}
