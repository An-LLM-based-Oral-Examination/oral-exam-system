using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using OralExamination.API.Middlewares;

namespace OralExamination.API.Configuration;

public static class ApiErrorContracts
{
    public static IServiceCollection AddApiErrorContracts(this IServiceCollection services)
    {
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ApiProblemDetailsFilter>();
            options.Conventions.Add(new SuppressUploadImageConsumesConvention());
        });
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = 400, Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = "Vui lòng kiểm tra các trường dữ liệu được đánh dấu."
            };
            ApiProblems.Enrich(context.HttpContext, problem, 400, "VALIDATION_FAILED");
            return new BadRequestObjectResult(problem);
        });
        return services;
    }

    public static IApplicationBuilder UseApiErrorContracts(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseStatusCodePages(async context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
                await ApiProblems.WriteAsync(context.HttpContext,
                    ApiProblems.Create(context.HttpContext, context.HttpContext.Response.StatusCode));
        });
        return app;
    }
}

public static class ApiProblems
{
    public static ProblemDetails Create(HttpContext context, int status, string? detail = null, string? errorCode = null)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };
        Enrich(context, problem, status, errorCode);
        return problem;
    }

    public static ProblemDetails CreateForController(
        HttpContext? context,
        int status,
        string? detail = null,
        string? errorCode = null,
        string? title = null,
        string? type = null)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? ReasonPhrases.GetReasonPhrase(status) : title;
        if (string.IsNullOrWhiteSpace(resolvedTitle)) resolvedTitle = "Yêu cầu không thành công.";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = resolvedTitle,
            Detail = string.IsNullOrWhiteSpace(detail) ? resolvedTitle : detail,
            Type = string.IsNullOrWhiteSpace(type) ? "https://tools.ietf.org/html/rfc7807" : type
        };

        if (context is not null)
        {
            Enrich(context, problem, status, errorCode);
        }
        else if (!problem.Extensions.ContainsKey("errorCode"))
        {
            problem.Extensions["errorCode"] = errorCode ?? DefaultCode(status);
        }

        return problem;
    }

    public static void Enrich(HttpContext context, ProblemDetails problem, int status, string? errorCode = null)
    {
        problem.Status = status;
        problem.Title = string.IsNullOrWhiteSpace(problem.Title) ? ReasonPhrases.GetReasonPhrase(status) : problem.Title;
        if (string.IsNullOrWhiteSpace(problem.Title)) problem.Title = "Yêu cầu không thành công.";
        problem.Detail = string.IsNullOrWhiteSpace(problem.Detail) ? problem.Title : problem.Detail;
        problem.Type ??= "about:blank";
        problem.Instance = context.Request.Path;
        if (!problem.Extensions.ContainsKey("errorCode"))
            problem.Extensions["errorCode"] = errorCode ?? (problem.Extensions.TryGetValue("code", out var code) ? code : null) ?? DefaultCode(status);
        problem.Extensions["traceId"] = context.TraceIdentifier;
    }

    public static string DefaultCode(int status) => status switch
    {
        400 => "BAD_REQUEST", 401 => "UNAUTHORIZED", 403 => "FORBIDDEN", 404 => "NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED", 409 => "CONFLICT", 410 => "GONE", 413 => "PAYLOAD_TOO_LARGE",
        415 => "UNSUPPORTED_MEDIA_TYPE", 422 => "UNPROCESSABLE_ENTITY", 429 => "RATE_LIMITED",
        408 => "REQUEST_TIMEOUT", 502 => "BAD_GATEWAY", 503 => "SERVICE_UNAVAILABLE",
        504 => "GATEWAY_TIMEOUT", _ => status >= 500 ? "SERVER_ERROR" : $"HTTP_{status}"
    };

    public static Task WriteAsync(HttpContext context, ProblemDetails problem, CancellationToken? cancellationToken = null)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
            cancellationToken: cancellationToken ?? context.RequestAborted);
    }
}

/// <summary>Normalizes every MVC failure, including legacy anonymous results, without changing success contracts.</summary>
public sealed class ApiProblemDetailsFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api")) return;
        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode ?? (result.Value as ProblemDetails)?.Status,
            JsonResult result => result.StatusCode,
            StatusCodeResult result => result.StatusCode,
            _ => null
        };
        if (status is not (>= 400 and <= 599)) return;
        var value = context.Result switch { ObjectResult result => result.Value, JsonResult result => result.Value, _ => null };
        var problem = value as ProblemDetails;
        if (problem is null)
        {
            string? detail = value as string;
            string? code = null;
            string? type = null;
            string? title = null;
            JsonElement? errors = null;
            if (value is not null && value is not string)
            {
                var json = JsonSerializer.SerializeToElement(value);
                if (json.ValueKind == JsonValueKind.Object)
                    foreach (var property in json.EnumerateObject())
                    {
                        if (property.Name.Equals("errors", StringComparison.OrdinalIgnoreCase)) errors = property.Value.Clone();
                        if (property.Value.ValueKind != JsonValueKind.String) continue;
                        if (property.Name.Equals("type", StringComparison.OrdinalIgnoreCase)) type = property.Value.GetString();
                        if (property.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) title = property.Value.GetString();
                        if (property.Name.Equals("detail", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Equals("error", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Equals("message", StringComparison.OrdinalIgnoreCase)) detail ??= property.Value.GetString();
                        if (property.Name.Equals("errorCode", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Equals("code", StringComparison.OrdinalIgnoreCase)) code ??= property.Value.GetString();
                    }
            }
            problem = ApiProblems.Create(context.HttpContext, status.Value, detail, code);
            if (!string.IsNullOrWhiteSpace(type)) problem.Type = type;
            if (!string.IsNullOrWhiteSpace(title)) problem.Title = title;
            if (errors is not null) problem.Extensions["errors"] = errors.Value;
        }
        ApiProblems.Enrich(context.HttpContext, problem, status.Value);
        var normalized = new ObjectResult(problem) { StatusCode = status.Value };
        normalized.ContentTypes.Add("application/problem+json");
        context.Result = normalized;
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}

internal sealed class SuppressUploadImageConsumesConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (action.Controller.ControllerType != typeof(global::API.Controllers.v1.QuestionsController) ||
            !string.Equals(action.ActionName, nameof(global::API.Controllers.v1.QuestionsController.UploadQuestionImage), StringComparison.Ordinal))
        {
            return;
        }

        for (var i = action.Filters.Count - 1; i >= 0; i--)
        {
            if (action.Filters[i] is ConsumesAttribute)
            {
                action.Filters.RemoveAt(i);
            }
        }
    }
}
