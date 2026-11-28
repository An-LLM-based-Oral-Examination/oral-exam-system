using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OralExamination.Domain.Exceptions;
using OralExamination.API.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            if (context.Response.HasStarted) throw;
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            var isDevelopment = _environment?.IsDevelopment() == true;
            await HandleExceptionAsync(context, ex, isDevelopment);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment)
    {
        var effectiveException = UnwrapAggregateException(exception);
        var statusCode = StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Lỗi hệ thống nội bộ.",
            Detail = isDevelopment ? effectiveException.Message : GenericServerErrorDetail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Instance = context.Request.Path
        };

        switch (effectiveException)
        {
            case PdfSigningCertificateException pdfCertEx:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dịch vụ ký số PDF tạm thời chưa sẵn sàng.";
                problemDetails.Detail = isDevelopment
                    ? pdfCertEx.Message
                    : "Hệ thống chưa được cấu hình chứng thư ký số PDF PKI hợp lệ. Vui lòng liên hệ quản trị viên.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4";
                problemDetails.Extensions["errorCode"] = "PDF_SIGNING_CERT_MISSING";
                break;

            case GoogleOAuthNotConfiguredException googleCfgEx:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Đăng nhập Google tạm thời chưa sẵn sàng.";
                problemDetails.Detail = isDevelopment
                    ? googleCfgEx.Message
                    : "Hệ thống chưa được cấu hình Google OAuth Client ID. Vui lòng đăng nhập bằng tài khoản nhà trường cấp hoặc liên hệ quản trị viên.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4";
                problemDetails.Extensions["errorCode"] = "GOOGLE_OAUTH_NOT_CONFIGURED";
                break;

            case OneWayLockException:
            case InvalidOperationException ioe when ioe.Message.Contains("One-Way Lock", StringComparison.OrdinalIgnoreCase):
                statusCode = StatusCodes.Status403Forbidden;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Thao tác bị từ chối.";
                problemDetails.Detail = effectiveException.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                break;

            case QuotaExceededException:
                statusCode = StatusCodes.Status429TooManyRequests;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Vượt quá hạn ngạch cho phép.";
                problemDetails.Detail = effectiveException.Message;
                problemDetails.Type = "https://tools.ietf.org/html/rfc6585#section-4";
                break;

            case FluentValidation.ValidationException fvEx:
                var isUnprocessableEntity = fvEx.Errors.Any() &&
                    fvEx.Errors.All(e => string.Equals(
                        e.ErrorCode,
                        OralExamination.Application.Common.Validation.ValidationErrorCodes.UnprocessableEntity,
                        StringComparison.OrdinalIgnoreCase));
                statusCode = isUnprocessableEntity
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status400BadRequest;
                problemDetails.Status = statusCode;
                problemDetails.Title = isUnprocessableEntity
                    ? "Dữ liệu vi phạm ràng buộc nghiệp vụ."
                    : "Dữ liệu đầu vào không hợp lệ.";
                problemDetails.Detail = string.Join("; ", fvEx.Errors.Select(e => e.ErrorMessage));
                problemDetails.Type = isUnprocessableEntity
                    ? "https://tools.ietf.org/html/rfc4918#section-11.2"
                    : "https://tools.ietf.org/html/rfc7231#section-6.5.1";
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
                problemDetails.Detail = effectiveException.Message;
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
                statusCode = badHttpEx.StatusCode;
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

            case HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.ServiceUnavailable:
                statusCode = StatusCodes.Status503ServiceUnavailable;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dịch vụ bên ngoài tạm thời không khả dụng.";
                problemDetails.Detail = isDevelopment
                    ? httpEx.Message
                    : "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4";
                break;

            case HttpRequestException httpEx when httpEx.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout:
                statusCode = StatusCodes.Status504GatewayTimeout;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dịch vụ bên ngoài phản hồi quá thời gian chờ.";
                problemDetails.Detail = isDevelopment
                    ? httpEx.Message
                    : "Dịch vụ AI bên ngoài phản hồi quá thời gian chờ. Vui lòng thử lại sau.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.5";
                break;

            case HttpRequestException httpEx:
                statusCode = StatusCodes.Status502BadGateway;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Lỗi kết nối dịch vụ bên ngoài.";
                problemDetails.Detail = isDevelopment
                    ? httpEx.Message
                    : "Không thể kết nối tới dịch vụ AI bên ngoài. Vui lòng thử lại sau.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.3";
                break;

            case TimeoutException:
            case TaskCanceledException when !context.RequestAborted.IsCancellationRequested:
                statusCode = StatusCodes.Status504GatewayTimeout;
                problemDetails.Status = statusCode;
                problemDetails.Title = "Dịch vụ bên ngoài phản hồi quá thời gian chờ.";
                problemDetails.Detail = isDevelopment
                    ? effectiveException.Message
                    : "Dịch vụ AI bên ngoài phản hồi quá thời gian chờ. Vui lòng thử lại sau.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.5";
                break;

            case OperationCanceledException:
                statusCode = 499; // Client Closed Request
                problemDetails.Status = statusCode;
                problemDetails.Title = "Thao tác đã bị hủy.";
                problemDetails.Detail = "Yêu cầu đã bị hủy bởi người dùng hoặc quá thời gian chờ (timeout).";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                break;
        }

        var postgres = effectiveException as PostgresException ?? (effectiveException as DbUpdateException)?.InnerException as PostgresException;
        if (postgres?.SqlState == PostgresErrorCodes.ForeignKeyViolation
            && (postgres.ConstraintName is "FK_practice_questions_course_clos_clo_id" or "FK_exam_questions_course_clos_clo_id")
            && HttpMethods.IsDelete(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/api/v1/courses"))
        {
            statusCode = StatusCodes.Status409Conflict;
            problemDetails.Title = "CLO đang được sử dụng.";
            problemDetails.Detail = "Không thể xóa CLO đang được câu hỏi tham chiếu. Vui lòng tải lại dữ liệu.";
            problemDetails.Type = "about:blank";
            problemDetails.Extensions["errorCode"] = "CLO_IN_USE";
        }
        ApiProblems.Enrich(context, problemDetails, statusCode);
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }

    private static Exception UnwrapAggregateException(Exception exception)
    {
        if (exception is not AggregateException aggregate)
        {
            return exception;
        }

        var flattened = aggregate.Flatten();
        return flattened.InnerExceptions.Count == 1
            ? flattened.InnerExceptions[0]
            : flattened.InnerException ?? exception;
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
