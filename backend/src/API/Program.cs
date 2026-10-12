using OralExamination.API.Configuration;
using OralExamination.API.Middlewares;
using OralExamination.Application;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Authentication;

DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOralExamAuthentication(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddHostedService<OralExamination.API.Workers.GradingQueueWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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

app.Run();
