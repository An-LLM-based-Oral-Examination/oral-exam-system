using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Auth;

namespace OralExamination.API.Middlewares;

public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(AuthTokenDefaults.MustChangePasswordClaim, "true")
            && !IsAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Cần đổi mật khẩu.",
                Detail = "Bạn phải đổi mật khẩu trước khi dùng các chức năng khác.",
                Type = "https://tools.ietf.org/html/rfc7231"
            };
            problem.Extensions["code"] = "MUST_CHANGE_PASSWORD";
            problem.Extensions["errorCode"] = "MUST_CHANGE_PASSWORD";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
            return;
        }

        await _next(context);
    }

    public static bool IsAllowed(PathString path)
    {
        return path.StartsWithSegments("/api/v1/auth/change-password")
            || path.StartsWithSegments("/api/v1/auth/refresh-token")
            || path.StartsWithSegments("/api/v1/auth/sign-out")
            || path.StartsWithSegments("/api/v1/auth/me")
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/healthz")
            || path.StartsWithSegments("/swagger");
    }
}
