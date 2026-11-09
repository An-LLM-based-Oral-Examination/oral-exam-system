using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OralExamination.API.Configuration;
using OralExamination.API.Middlewares;
using OralExamination.Application;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;

DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOralExamAuthentication(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddHostedService<OralExamination.API.Workers.GradingQueueWorker>();
builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamGradingDispatcher>();
builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamGradingWorker>();
builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamDlqReplayWorker>();
builder.Services.AddHostedService<OralExamination.API.Workers.InClassShiftAutoCloseWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (builder.Configuration["CORS_ALLOWED_ORIGINS"]?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>());

        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins);
        }
        else
        {
            policy.SetIsOriginAllowed(origin => true);
        }

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Oral Examination API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await ApplyMigrationsAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Thứ tự Middleware bắt buộc: UseCors() phải nằm trước UseStaticFiles() để không bị chặn CORS trên tài nguyên tĩnh (ảnh/audio).
app.UseCors("AllowFrontend");

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}
app.UseStaticFiles();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OralExamination.API.Hubs.PracticeHub>("/hubs/practice");
app.MapHub<OralExamination.API.Hubs.AntiCheatHub>("/hubs/anti-cheat");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(2);
    var logger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();

            if (context.Database.IsRelational())
            {
                logger.LogInformation(
                    "Applying EF Core migrations and PostgreSQL schema reconciliation (attempt {Attempt}/{MaxAttempts}).",
                    attempt,
                    maxAttempts);

                await PostgresSchemaReconciler.ReconcilePreMigrationAsync(context);
                await context.Database.MigrateAsync();
                await PostgresSchemaReconciler.ReconcilePostMigrationAsync(context);
            }

            var seedConfig = app.Configuration["Database:EnableDevelopmentSeed"]
                ?? app.Configuration["DATABASE_ENABLE_DEVELOPMENT_SEED"];
            var shouldSeed = bool.TryParse(seedConfig, out var parsedSeed)
                ? parsedSeed
                : true;

            if (shouldSeed)
            {
                await DevelopmentDataSeeder.SeedAsync(context);
            }

            logger.LogInformation("Database schema and initial academic catalog verified successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "Database is not ready. Retrying in {DelaySeconds} seconds.",
                delay.TotalSeconds);

            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Database migration did not complete after {MaxAttempts} attempts; continuing API startup.",
                maxAttempts);
        }
    }
}
