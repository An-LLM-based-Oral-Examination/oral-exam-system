using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OralExamination.API.Configuration;
using OralExamination.API.Middlewares;
using OralExamination.Application;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;

DotEnvLoader.Load();

if (!CliCommandOptions.TryParse(args, out var cliOptions, out var cliParseError))
{
    Console.Error.WriteLine(cliParseError);
    Environment.ExitCode = 1;
    return;
}

var builder = WebApplication.CreateBuilder(cliOptions.RemainingArgs);
DatabaseSeedPolicy.IsDevelopmentSeedEnabled(builder.Configuration, builder.Environment.EnvironmentName);
DatabaseSeedPolicy.IsAcademicCourseBankImportEnabled(builder.Configuration);

// ASP.NET Core tự đăng ký Windows Event Log provider. Tài khoản dev/CI không có
// quyền ghi Event Log sẽ khiến một cảnh báo EF Core biến thành AggregateException
// và làm API dừng khởi động. Console logging vẫn giữ nguyên; chỉ vô hiệu provider
// đặc thù Windows khi tiến trình không có quyền ghi hệ thống.
if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
}
else if (OperatingSystem.IsWindows())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

ConfigureServices(builder, cliOptions);

var app = builder.Build();

using (var startupCts = new CancellationTokenSource())
{
    using var sigTermReg = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM,
        ctx => { ctx.Cancel = true; startupCts.Cancel(); });
    using var sigIntReg = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGINT,
        ctx => { ctx.Cancel = true; startupCts.Cancel(); });

    await DatabaseReadinessCoordinator.RunStartupSequenceAsync(
        app.Services,
        app.Configuration,
        app.Environment,
        cancellationToken: startupCts.Token);

    if (cliOptions.Mode == CliMode.ImportCourseBanks)
    {
        var cliLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CliImportCourseBanks");
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var importResult = await AcademicCourseBankImporter.ImportAsync(
                db,
                cliLogger,
                allowDeferExamStructuresWhenNoUsers: false,
                cancellationToken: startupCts.Token);

            cliLogger.LogInformation(
                "CLI --import-course-banks succeeded (Courses={Courses}, Clos={Clos}, QuestionsInserted={QuestionsInserted}, Backfilled={Backfilled}, ExamStructures={ExamStructures}, LegacyHidden={LegacyHidden}).",
                importResult.CoursesProcessed,
                importResult.ClosUpserted,
                importResult.QuestionsInserted,
                importResult.QuestionsBackfilled,
                importResult.ExamStructuresUpserted,
                importResult.LegacyCoursesSoftHidden);
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            cliLogger.LogError(ex, "CLI --import-course-banks failed.");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }

        await app.DisposeAsync();
        return;
    }

    if (cliOptions.Mode == CliMode.ProvisionTestAccounts)
    {
        var cliLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CliProvisionTestAccounts");
        try
        {
            var configJson = await ResolveProvisionConfigJsonAsync(cliOptions.ConfigPath, app.Configuration, startupCts.Token);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var classJoinPasswordProtector = scope.ServiceProvider.GetRequiredService<IClassJoinPasswordProtector>();

            var provisionResult = await ProductionAccountClassProvisioner.ProvisionAsync(
                db,
                passwordHasher,
                classJoinPasswordProtector,
                configJson,
                cliLogger,
                startupCts.Token);

            cliLogger.LogInformation(
                "CLI --provision-test-accounts succeeded (UsersCreated={UsersCreated}, UsersUpdated={UsersUpdated}, CourseLecturers={CourseLecturers}, Classes={Classes}, EnrollmentsCreated={EnrollmentsCreated}).",
                provisionResult.UsersCreated,
                provisionResult.UsersUpdated,
                provisionResult.CourseLecturersUpserted,
                provisionResult.ClassesUpserted,
                provisionResult.EnrollmentsCreated);
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            cliLogger.LogError(ex, "CLI --provision-test-accounts failed.");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.ExitCode = 1;
        }

        await app.DisposeAsync();
        return;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TSYNC-F001: Sử dụng cấu hình ForwardedHeadersOptions đã được đăng ký bởi AddApiAbuseProtection
// Giữ framework loopback defaults và Security:TrustedProxies làm allowlist thực tế; ForwardLimit = 1.
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();

// Thứ tự Middleware bắt buộc: UseCors() phải nằm trước UseStaticFiles() để không bị chặn CORS trên tài nguyên tĩnh (ảnh/audio).
app.UseCors(OralExamCorsExtensions.PolicyName);

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}
app.UseStaticFiles();

app.UseRouting();
app.UseApiErrorContracts();

app.UseAuthentication();
app.UseApiAbuseProtection();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapControllers().Add(endpoint =>
{
    var action = endpoint.Metadata.OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>().FirstOrDefault();
    if (action is not null && (action.ControllerName == "Speech"
        || action.ActionName.Contains("Upload", StringComparison.OrdinalIgnoreCase)
        || action.ActionName.Contains("Generate", StringComparison.OrdinalIgnoreCase)
        || action.ActionName.Contains("FollowUp", StringComparison.OrdinalIgnoreCase)
        || action.ActionName.Contains("Import", StringComparison.OrdinalIgnoreCase)
        || (action.ControllerName == "MockExams" && string.Equals(action.ActionName, "SubmitAnswer", StringComparison.OrdinalIgnoreCase))))
        endpoint.Metadata.Add(new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutAttribute("expensive"));
});
app.MapHub<OralExamination.API.Hubs.PracticeHub>("/hubs/practice");
app.MapHub<OralExamination.API.Hubs.InClassShiftHub>("/hubs/in-class-shift");
app.MapHub<OralExamination.API.Hubs.AntiCheatHub>("/hubs/anti-cheat");
app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) => DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx)).AllowAnonymous();
app.MapMethods("/healthz", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) => DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx)).AllowAnonymous();

app.Run();

static async Task<string> ResolveProvisionConfigJsonAsync(
    string? configPath,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    if (!string.IsNullOrWhiteSpace(configPath))
    {
        if (string.Equals(configPath, "-", StringComparison.Ordinal))
        {
            var stdinContent = await Console.In.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(stdinContent))
            {
                throw new InvalidOperationException("[FAIL CLOSED] Standard input is empty for '--config -'.");
            }

            return stdinContent;
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"[FAIL CLOSED] Provisioning config file not found: '{configPath}'.", configPath);
        }

        return await File.ReadAllTextAsync(configPath, cancellationToken);
    }

    var envPath = configuration["PROVISION_TEST_ACCOUNTS_CONFIG_PATH"];
    if (!string.IsNullOrWhiteSpace(envPath))
    {
        if (!File.Exists(envPath))
        {
            throw new FileNotFoundException($"[FAIL CLOSED] PROVISION_TEST_ACCOUNTS_CONFIG_PATH file not found: '{envPath}'.", envPath);
        }

        return await File.ReadAllTextAsync(envPath, cancellationToken);
    }

    var inlineJson = configuration["PROVISION_TEST_ACCOUNTS_JSON"];
    if (!string.IsNullOrWhiteSpace(inlineJson))
    {
        return inlineJson;
    }

    throw new InvalidOperationException(
        "[FAIL CLOSED] Missing provisioning config. Pass '--config <path|->' or set PROVISION_TEST_ACCOUNTS_CONFIG_PATH / PROVISION_TEST_ACCOUNTS_JSON.");
}

internal partial class Program
{
    internal static void ConfigureServices(WebApplicationBuilder builder, CliCommandOptions cliOptions)
    {
        builder.Services.AddInfrastructure(builder.Configuration);
        if (cliOptions.Mode != CliMode.ImportCourseBanks)
        {
            builder.Services.AddOralExamAuthentication(builder.Configuration);
        }
        builder.Services.AddDatabaseReadiness();

        if (cliOptions.Mode == CliMode.None)
        {
            builder.Services.Configure<HostOptions>(options =>
            {
                var configuredShutdownSeconds = builder.Configuration.GetValue<int?>("Host:ShutdownTimeoutSeconds");
                var shutdownSeconds = configuredShutdownSeconds.HasValue
                    ? Math.Clamp(configuredShutdownSeconds.Value, 45, 300)
                    : 60;
                options.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds);
            });

            builder.Services.AddApplication();
            builder.Services.AddApiErrorContracts();
            builder.Services.AddApiAbuseProtection(builder.Configuration);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
                options.Limits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(240, TimeSpan.FromSeconds(10));
            });

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSignalR();
            builder.Services.AddHostedService<OralExamination.API.Workers.GradingQueueWorker>();
            builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamGradingDispatcher>();
            builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamGradingWorker>();
            builder.Services.AddHostedService<OralExamination.API.Workers.OfficialExamDlqReplayWorker>();
            builder.Services.AddHostedService<OralExamination.API.Workers.InClassShiftAutoCloseWorker>();

            builder.Services.AddOralExamCors(builder.Configuration, builder.Environment);

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
        }
    }
}
