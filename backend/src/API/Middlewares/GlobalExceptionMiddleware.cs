using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OralExamination.Domain.Exceptions;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OralExamination.API.Middlewares;

public class GlobalExceptionMiddleware
{
    public const string GenericServerErrorDetail = "Đã xảy ra lỗi nội bộ hệ thống. Vui lòng liên hệ quản trị viên.";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment? _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment? environment = null)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            var isDevelopment = _environment?.IsDevelopment() == true;
            await HandleExceptionAsync(context, ex, isDevelopment);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Lỗi hệ thống nội bộ.",
            Detail = isDevelopment ? exception.Message : GenericServerErrorDetail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        switch (exception)
        {
            case OneWayLockException:
            case InvalidOperationException ioe when ioe.Message.Contains("One-Way Lock", StringComparison.OrdinalIgnoreCase):
                statusCode = StatusCodes.Status403Forbidden;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Thao tác bị từ chối.";
                problemDetails.Detail = exception.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                break;
            case QuotaExceededException:
                statusCode = StatusCodes.Status429TooManyRequests;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Vượt quá hạn ngạch cho phép.";
                problemDetails.Detail = exception.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc6585#section-4";
                break;
            case FluentValidation.ValidationException fvEx:
                statusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dữ liệu đầu vào không hợp lệ.";
                problemDetails.Detail = string.Join("; ", fvEx.Errors.Select(e => e.ErrorMessage));
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                problemDetails.Extensions["errors"] = fvEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                break;
            case DomainValidationException:
            case ArgumentNullException:
            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dữ liệu đầu vào không hợp lệ.";
                problemDetails.Detail = exception.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
