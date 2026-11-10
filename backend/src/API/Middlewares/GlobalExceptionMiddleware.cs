using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OralExamination.Domain.Exceptions;
using System;
using System.Collections.Generic;
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
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
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
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Instance = context.Request.Path
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

            case KeyNotFoundException knfEx:
                statusCode = StatusCodes.Status404NotFound;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Không tìm thấy tài nguyên.";
                problemDetails.Detail = string.IsNullOrWhiteSpace(knfEx.Message)
                    ? "Tài nguyên được yêu cầu không tồn tại trong hệ thống."
                    : knfEx.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
                break;

            case BadHttpRequestException badHttpEx:
                statusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Yêu cầu không hợp lệ.";
                problemDetails.Detail = badHttpEx.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;

            case UnauthorizedAccessException unauthEx:
                statusCode = StatusCodes.Status403Forbidden;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Truy cập bị từ chối.";
                problemDetails.Detail = string.IsNullOrWhiteSpace(unauthEx.Message)
                    ? "Bạn không có quyền thực hiện thao tác này."
                    : unauthEx.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                break;

            case DbUpdateConcurrencyException:
                statusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Xung đột dữ liệu đồng thời.";
                problemDetails.Detail = "Bản ghi đã bị thay đổi hoặc cập nhật bởi tác vụ khác. Vui lòng làm mới trang và thử lại.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
                break;

            case DbUpdateException dbUpdateEx when dbUpdateEx.InnerException is PostgresException pgInnerEx:
                MapPostgresException(pgInnerEx, problemDetails, ref statusCode, isDevelopment);
                break;

            case DbUpdateException dbUpdateEx:
                statusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Xung đột hoặc lỗi cập nhật cơ sở dữ liệu.";
                problemDetails.Detail = isDevelopment
                    ? dbUpdateEx.InnerException?.Message ?? dbUpdateEx.Message
                    : "Không thể cập nhật cơ sở dữ liệu do vi phạm ràng buộc dữ liệu hoặc xung đột đồng thời.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
                break;

            case PostgresException pgEx:
                MapPostgresException(pgEx, problemDetails, ref statusCode, isDevelopment);
                break;

            case OperationCanceledException:
                statusCode = 499; // Client Closed Request
                problemDetails.Status = statusCode;
                problemDetails.Title = "Thao tác đã bị hủy.";
                problemDetails.Detail = "Yêu cầu đã bị hủy bởi người dùng hoặc quá thời gian chờ (timeout).";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }

    private static void MapPostgresException(
        PostgresException pgEx,
        ProblemDetails problemDetails,
        ref int statusCode,
        bool isDevelopment)
    {
        problemDetails.Extensions["sqlState"] = pgEx.SqlState;

        switch (pgEx.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:
                statusCode = StatusCodes.Status409Conflict;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dữ liệu đã tồn tại.";
                problemDetails.Detail = isDevelopment
                    ? pgEx.MessageText
                    : "Dữ liệu bị trùng lặp hoặc vi phạm ràng buộc duy nhất.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
                break;

            case PostgresErrorCodes.ForeignKeyViolation:
                statusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dữ liệu tham chiếu không hợp lệ.";
                problemDetails.Detail = isDevelopment
                    ? pgEx.MessageText
                    : "Thực thể tham chiếu liên quan không tồn tại hoặc đã bị xóa.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;

            case PostgresErrorCodes.CheckViolation:
                statusCode = StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Vi phạm ràng buộc dữ liệu.";
                problemDetails.Detail = isDevelopment
                    ? pgEx.MessageText
                    : "Dữ liệu không thỏa mãn các điều kiện ràng buộc hợp lệ của hệ thống.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;

            case PostgresErrorCodes.UndefinedColumn:
            case PostgresErrorCodes.UndefinedTable:
                statusCode = StatusCodes.Status500InternalServerError;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Lỗi cấu trúc cơ sở dữ liệu.";
                problemDetails.Detail = isDevelopment
                    ? $"Database schema drift: {pgEx.MessageText} (Table/Column: {pgEx.TableName}/{pgEx.ColumnName})"
                    : "Hệ thống đang phát hiện sự sai lệch cấu trúc CSDL và đang thực hiện tự bù lược đồ.";
                problemDetails.Type = "https://oralexam.io.vn/errors/database-schema";
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Lỗi cơ sở dữ liệu.";
                problemDetails.Detail = isDevelopment
                    ? pgEx.MessageText
                    : GenericServerErrorDetail;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
                break;
        }
    }
}
