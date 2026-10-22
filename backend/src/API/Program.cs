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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-STT-Confidence", "X-STT-Provider", "X-TTS-Provider", "X-TTS-Voice", "X-TTS-Rate", "X-TTS-Fallback", "X-TTS-Cache");
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán accessToken lấy từ password-login. Không gõ thêm chữ Bearer."
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
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();
app.MapHub<OralExamination.API.Hubs.PracticeHub>("/hubs/practice");

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "OralExamination.API",
    timestamp = DateTime.UtcNow
}))
.AllowAnonymous()
.WithName("GetHealthStatus")
.WithOpenApi();

await ApplyMigrationsAsync(app);

if (args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase))
{
    return;
}

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    const int maxAttempts = 5;
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
                    "Applying EF Core migrations (attempt {Attempt}/{MaxAttempts}).",
                    attempt,
                    maxAttempts);

                await context.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation(
                    "PostgreSQL container on port 5432 is offline; running with seeded In-Memory database for uninterrupted live operation.");
            }

            await DevelopmentDataSeeder.SeedAsync(context);

            logger.LogInformation("Database schema and development seed data applied successfully.");
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
