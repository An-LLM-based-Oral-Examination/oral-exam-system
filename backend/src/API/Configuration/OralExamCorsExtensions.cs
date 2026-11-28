using System;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OralExamination.API.Configuration;

public static class OralExamCorsExtensions
{
    public const string PolicyName = "AllowFrontend";

    public static IServiceCollection AddOralExamCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? (configuration["CORS_ALLOWED_ORIGINS"]?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        ?? Array.Empty<string>());

                if (configuredOrigins.Length > 0)
                {
                    policy.WithOrigins(configuredOrigins);
                }
                else if (environment.IsDevelopment())
                {
                    // Development fallback: cho phép các cổng localhost chuẩn
                    policy.WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "https://localhost:3000",
                        "https://localhost:5173");
                }
                else
                {
                    throw new InvalidOperationException(
                        "[TASK-003 GUARD BẢO MẬT] CORS_ALLOWED_ORIGINS hoặc Cors:AllowedOrigins bắt buộc phải được cấu hình trong môi trường Production. Tuyệt đối không cho phép chạy Production mà không có allowlist nguồn gốc tin cậy!");
                }

                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()
                      .WithExposedHeaders(
                          "Content-Disposition",
                          "X-Certificate-Hash",
                          "X-Signature-Thumbprint",
                          "Retry-After",
                          "X-Correlation-ID",
                          "X-TTS-Provider",
                          "X-TTS-Voice",
                          "X-TTS-Rate",
                          "X-TTS-Fallback",
                          "X-TTS-Cache");
            });
        });

        return services;
    }
}
