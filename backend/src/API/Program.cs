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
DatabaseSeedPolicy.IsDevelopmentSeedEnabled(builder.Configuration, builder.Environment.EnvironmentName);

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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOralExamAuthentication(builder.Configuration);
builder.Services.AddDatabaseReadiness();
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
    if (action is not null && (action.ControllerName == "Speech" || action.ActionName.Contains("Upload", StringComparison.OrdinalIgnoreCase)
        || action.ActionName.Contains("Generate", StringComparison.OrdinalIgnoreCase)))
        endpoint.Metadata.Add(new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutAttribute("expensive"));
});
app.MapHub<OralExamination.API.Hubs.PracticeHub>("/hubs/practice");
app.MapHub<OralExamination.API.Hubs.InClassShiftHub>("/hubs/in-class-shift");
app.MapHub<OralExamination.API.Hubs.AntiCheatHub>("/hubs/anti-cheat");
app.MapMethods("/health", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) => DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx)).AllowAnonymous();
app.MapMethods("/healthz", [HttpMethods.Get, HttpMethods.Head], (HttpContext ctx) => DatabaseReadinessCoordinator.HandleHealthRequestAsync(ctx)).AllowAnonymous();

app.Run();
