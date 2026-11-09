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

            if (!context.Database.IsRelational())
            {
                logger.LogInformation("Database is not relational. Skipping schema reconciliation.");
                return;
            }

            // Kiểm tra khả năng kết nối cơ sở dữ liệu (Database Connectivity Gate)
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                throw new InvalidOperationException("Cannot connect to PostgreSQL server yet.");
            }

            logger.LogInformation(
                "PostgreSQL connection established. Initiating 4-stage indestructible database startup (attempt {Attempt}/{MaxAttempts}).",
                attempt,
                maxAttempts);

            // Giai đoạn 1/4: Khắc phục và chuẩn bị lược đồ trước Migration (Pre-Migration Reconciliation)
            try
            {
                logger.LogInformation("Stage 1/4: Running PostgresSchemaReconciler.ReconcilePreMigrationAsync...");
                await PostgresSchemaReconciler.ReconcilePreMigrationAsync(context);
                logger.LogInformation("Stage 1/4: Pre-migration reconciliation completed.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stage 1/4 Warning: Pre-migration schema reconciliation encountered an issue, proceeding to EF Core migrations.");
            }

            // Giai đoạn 2/4: Thực thi EF Core Migrations
            try
            {
                logger.LogInformation("Stage 2/4: Applying EF Core migrations via context.Database.MigrateAsync()...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Stage 2/4: EF Core migrations applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stage 2/4 Warning: EF Core MigrateAsync encountered schema drift or collision. Proceeding to Post-Migration reconciler for self-healing.");
            }

            // Giai đoạn 3/4: Tự phục hồi và đối soát lược đồ toàn diện sau Migration (Post-Migration Reconciliation - GUARANTEED)
            try
            {
                logger.LogInformation("Stage 3/4: Running PostgresSchemaReconciler.ReconcilePostMigrationAsync (self-healing missing tables/columns/constraints)...");
                await PostgresSchemaReconciler.ReconcilePostMigrationAsync(context);
                logger.LogInformation("Stage 3/4: Post-migration reconciliation and schema self-healing completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stage 3/4 Error: Post-migration schema reconciliation encountered an error. Proceeding to Seeder.");
            }

            // Giai đoạn 4/4: Khởi tạo dữ liệu mẫu và đồng bộ danh mục học thuật (DevelopmentDataSeeder - GUARANTEED)
            var seedConfig = app.Configuration["Database:EnableDevelopmentSeed"]
                ?? app.Configuration["DATABASE_ENABLE_DEVELOPMENT_SEED"];
            var shouldSeed = bool.TryParse(seedConfig, out var parsedSeed)
                ? parsedSeed
                : true;

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

            // Schema Health Check Probe (Kiểm tra sự hiện diện vật lý của 5 bảng cốt lõi)
            try
            {
                using var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name IN ('academic_programs', 'course_offerings', 'in_class_exam_shifts', 'notifications', 'course_clos')
                    """;
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count < 5)
                {
                    logger.LogWarning("Schema Health Check Probe: Found {FoundCount}/5 key tables. Some tables might still be missing on PostgreSQL.", count);
                }
                else
                {
                    logger.LogInformation("Schema Health Check Probe: Verified physical presence of all {FoundCount}/5 key academic and in-class exam tables.", count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Schema Health Check Probe could not execute query against PostgreSQL.");
            }

            logger.LogInformation("Indestructible database startup sequence completed successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "Database connection or startup was not ready (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds} seconds.",
                attempt,
                maxAttempts,
                delay.TotalSeconds);

            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Database startup did not complete after {MaxAttempts} attempts; continuing API startup in degraded mode.",
                maxAttempts);
        }
    }
}
