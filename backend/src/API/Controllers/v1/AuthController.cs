using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.CreateGoogleLoginChallenge;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.Commands.SignOut;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Application.Features.Auth.Queries.GetCurrentUser;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý xác thực và danh tính của người dùng đang đăng nhập.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshSessionCookieName = "oes_refresh_session";
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Đọc hồ sơ hiện tại từ database, dùng danh tính đã xác thực trong JWT.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Không xác thực được.",
                Detail = "Danh tính trong token không hợp lệ.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2"
            };
            problem.Extensions["errorCode"] = "INVALID_SUBJECT";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return Unauthorized(problem);
        }

        var result = await _sender.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : AuthProblem(result.Error);
    }

    /// <summary>
    /// Tạo nonce dùng một lần cho GIS ID-token và ràng buộc vào cookie trình duyệt.
    /// </summary>
    [HttpPost("google-challenge")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(GoogleLoginChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GoogleChallenge(CancellationToken cancellationToken)
    {
        var challenge = await _sender.Send(new CreateGoogleLoginChallengeCommand(), cancellationToken);
        Response.Cookies.Append(GoogleLoginRules.NonceCookie, challenge.Nonce, GoogleNonceCookie(TimeSpan.FromSeconds(challenge.ExpiresIn)));
        return Ok(challenge);
    }

    /// <summary>
    /// Đăng nhập bằng Google ID-token có nonce khớp cookie và chưa sử dụng.
    /// Giữ đăng ký Google cho Student mới; tài khoản có sẵn giữ role/password-change flag trong database.
    /// </summary>
    [HttpPost("google-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(BrowserGoogleLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var command = new GoogleLoginCommand(request.IdToken, Request.Cookies[GoogleLoginRules.NonceCookie]);
        var result = await _sender.Send(command, cancellationToken);
        Response.Cookies.Delete(GoogleLoginRules.NonceCookie, GoogleNonceCookie());

        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        SetRefreshSessionCookie(result.Value.RefreshToken);
        return Ok(new BrowserGoogleLoginResponse(
            result.Value.AccessToken,
            result.Value.TokenType,
            result.Value.ExpiresIn,
            result.Value.User));
    }

    /// <summary>
    /// Cấp lại Access Token mới bằng Refresh Token (Silent Refresh).
    /// Refresh Token cũ bị thu hồi, trả về cặp token mới (Token Rotation).
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        var cookieToken = Request.Cookies[RefreshSessionCookieName];
        var refreshToken = cookieToken ?? request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Token không hợp lệ.",
                Detail = "Không tìm thấy phiên đăng nhập trong trình duyệt.",
                Type = "https://tools.ietf.org/html/rfc7231"
            });
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                "Tài khoản đã bị khóa." => StatusCodes.Status403Forbidden,
                var err when err.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status401Unauthorized
            };

            if (!string.IsNullOrWhiteSpace(cookieToken) && statusCode != StatusCodes.Status409Conflict)
            {
                DeleteRefreshSessionCookie();
            }

            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Tài khoản bị khóa.",
                    StatusCodes.Status409Conflict => "Xung đột token đồng thời.",
                    _ => "Token không hợp lệ."
                },
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231"
            });
        }

        SetRefreshSessionCookie(result.Value.RefreshToken);
        return Ok(new BrowserRefreshTokenResponse(
            result.Value.AccessToken,
            result.Value.TokenType,
            result.Value.ExpiresIn));
    }

    /// <summary>
    /// Đăng xuất — thu hồi Refresh Token phía server.
    /// User phải đăng nhập lại Google để lấy token mới.
    /// </summary>
    [HttpPost("sign-out")]
    [AllowAnonymous]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest? request, CancellationToken cancellationToken)
    {
        var command = new SignOutCommand(Request.Cookies[RefreshSessionCookieName] ?? request?.RefreshToken);
        var result = await _sender.Send(command, cancellationToken);
        DeleteRefreshSessionCookie();

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231"
            });
        }

        return Ok(new { message = "Đăng xuất thành công." });
    }

    /// <summary>
    /// Đăng nhập bằng cột users.username và mật khẩu. Role lấy từ database, không đọc từ body.
    /// </summary>
    [HttpPost("password-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> PasswordLogin([FromBody] PasswordLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PasswordLoginCommand(request.Username, request.Password), cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        SetRefreshSessionCookie(result.Value.RefreshToken);
        return Ok(new BrowserPasswordLoginResponse(
            result.Value.AccessToken,
            result.Value.TokenType,
            result.Value.ExpiresIn,
            result.Value.MustChangePassword,
            result.Value.User));
    }

    /// <summary>
    /// Đổi mật khẩu của chính người đang đăng nhập. UserId lấy từ claim sub.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Không xác thực được.",
                Detail = "Danh tính trong token không hợp lệ.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.2"
            });
        }

        var result = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        SetRefreshSessionCookie(result.Value.RefreshToken);
        return Ok(new BrowserChangePasswordResponse(
            result.Value.AccessToken,
            result.Value.TokenType,
            result.Value.ExpiresIn,
            result.Value.MustChangePassword));
    }

    private IActionResult AuthProblem(string error)
    {
        var statusCode = error switch
        {
            AuthErrors.AccountNotFound or AuthErrors.WrongPassword or AuthErrors.PasswordNotSet or AuthErrors.InvalidCredentials => StatusCodes.Status401Unauthorized,
            AuthErrors.GoogleTokenInvalid or AuthErrors.GoogleNonceInvalid or AuthErrors.CurrentPasswordWrong => StatusCodes.Status401Unauthorized,
            AuthErrors.AccountLocked => StatusCodes.Status403Forbidden,
            AuthErrors.GoogleStudentCodeConflict => StatusCodes.Status409Conflict,
            AuthErrors.LoginTemporarilyLocked => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest
        };

        var isCredentialError = error is AuthErrors.AccountNotFound or AuthErrors.WrongPassword or AuthErrors.PasswordNotSet or AuthErrors.InvalidCredentials;
        var detail = isCredentialError ? AuthErrors.InvalidCredentials : error;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Không tìm thấy tài khoản.",
                StatusCodes.Status401Unauthorized => "Không xác thực được.",
                StatusCodes.Status403Forbidden => "Tài khoản bị khóa.",
                StatusCodes.Status409Conflict => "Xung đột dữ liệu tài khoản.",
                StatusCodes.Status429TooManyRequests => "Đăng nhập tạm thời bị khóa.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231"
        };
        problem.Extensions["errorCode"] = error switch
        {
            AuthErrors.AccountNotFound or AuthErrors.WrongPassword or AuthErrors.PasswordNotSet or AuthErrors.InvalidCredentials => "INVALID_CREDENTIALS",
            AuthErrors.AccountLocked => "ACCOUNT_LOCKED",
            AuthErrors.GoogleStudentCodeConflict => "STUDENT_CODE_CONFLICT",
            AuthErrors.LoginTemporarilyLocked => "LOGIN_TEMPORARILY_LOCKED",
            AuthErrors.GoogleTokenInvalid => "GOOGLE_TOKEN_INVALID",
            AuthErrors.GoogleNonceInvalid => "GOOGLE_NONCE_INVALID",
            _ => "AUTH_FAILED"
        };
        problem.Extensions["traceId"] = HttpContext?.TraceIdentifier;
        if (statusCode == 429 && HttpContext is not null)
            Response.Headers.RetryAfter = (HttpContext.RequestServices.GetService<LoginProtectionOptions>()?.LockoutSeconds ?? 900).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(statusCode, problem);
    }

    private CookieOptions GoogleNonceCookie(TimeSpan? maxAge = null)
    {
        var environment = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        return new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Path = GoogleLoginRules.CookiePath, MaxAge = maxAge,
            Secure = Request.IsHttps || !(environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        };
    }

    private void SetRefreshSessionCookie(string refreshToken)
    {
        Response.Cookies.Append(
            RefreshSessionCookieName,
            refreshToken,
            RefreshSessionCookie(TimeSpan.FromDays(AuthTokenDefaults.RefreshTokenDays)));
    }

    private void DeleteRefreshSessionCookie()
    {
        Response.Cookies.Delete(RefreshSessionCookieName, RefreshSessionCookie());
    }

    private CookieOptions RefreshSessionCookie(TimeSpan? maxAge = null)
    {
        var environment = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            MaxAge = maxAge,
            Secure = Request.IsHttps || !(environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        };
    }
}
