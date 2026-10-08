using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OralExamination.Domain.Exceptions;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OralExamination.API.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Lỗi hệ thống nội bộ.",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        switch (exception)
        {
            case OneWayLockException:
                statusCode = StatusCodes.Status403Forbidden;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Thao tác bị từ chối.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                break;
            case QuotaExceededException:
                statusCode = StatusCodes.Status429TooManyRequests;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Vượt quá hạn ngạch cho phép.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc6585#section-4";
                break;
            case DomainValidationException:
            case ArgumentException:
            case InvalidOperationException: // For interceptor thrown exception if it throws IOE instead of OWLE
                // Map the interceptor's throw if it wasn't caught as OneWayLockException
                if (exception.Message.Contains("One-Way Lock"))
                {
                    statusCode = StatusCodes.Status403Forbidden;
                    problemDetails.Status = statusCode;
                    problemDetails.Title = "Thao tác bị từ chối.";
                    problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                }
                else
                {
                    statusCode = StatusCodes.Status400BadRequest;
                    problemDetails.Status = statusCode;
                    problemDetails.Title = "Dữ liệu đầu vào không hợp lệ.";
                    problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                }
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
