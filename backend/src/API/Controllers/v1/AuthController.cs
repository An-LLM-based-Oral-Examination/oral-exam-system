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
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Enums;
using System;
using System.Text.RegularExpressions;
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
    private static readonly Regex ValidKioskVersionRegex = new(
        "^[A-Za-z0-9._+-]{2,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
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
            result.Value.User,
            result.Value.MustChangePassword));
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
        Guid? authenticatedUserId = null;
        if (User?.Identity?.IsAuthenticated == true)
        {
            var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
            if (Guid.TryParse(sub, out var parsedId) && parsedId != Guid.Empty)
            {
                authenticatedUserId = parsedId;
            }
        }

        var command = new SignOutCommand(
            Request.Cookies[RefreshSessionCookieName] ?? request?.RefreshToken,
            authenticatedUserId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Xung đột đăng xuất đồng thời.",
                    Detail = result.Error,
                    Type = "https://tools.ietf.org/html/rfc7231"
                });
            }

            DeleteRefreshSessionCookie();
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231"
            });
        }

        DeleteRefreshSessionCookie();
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
    [EnableRateLimiting("auth-login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(BrowserChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
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

    /// <summary>
    /// Đăng nhập dành riêng cho ứng dụng Native Desktop Kiosk (MF-04, chỉ sinh viên).
    /// Bắt buộc có X-Device-Id và X-Kiosk-Version hợp lệ; trả refreshToken trong JSON body và KHÔNG đặt cookie trình duyệt.
    /// </summary>
    [HttpPost("kiosk/password-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(PasswordLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> KioskPasswordLogin(
        [FromBody] PasswordLoginRequest? request,
        CancellationToken cancellationToken = default)
    {
        var headerProblem = ValidateKioskClientHeaders();
        if (headerProblem is not null)
        {
            return headerProblem;
        }

        if (request is null)
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu đầu vào không hợp lệ.",
                "Nội dung đăng nhập không được để trống.",
                "INVALID_REQUEST");
        }

        var result = await _sender.Send(
            new PasswordLoginCommand(request.Username, request.Password, UserRole.Student),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Làm mới phiên đăng nhập dành riêng cho Native Desktop Kiosk (MF-04, chỉ sinh viên).
    /// Tuyệt đối KHÔNG đọc HttpOnly cookie trình duyệt (chống Cookie-to-JSON downgrade) và KHÔNG đặt Set-Cookie.
    /// </summary>
    [HttpPost("kiosk/refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> KioskRefreshToken(
        [FromBody] RefreshTokenRequest? request,
        CancellationToken cancellationToken = default)
    {
        var headerProblem = ValidateKioskClientHeaders();
        if (headerProblem is not null)
        {
            return headerProblem;
        }

        var refreshToken = request?.RefreshToken?.Trim();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status401Unauthorized,
                "Token không hợp lệ.",
                "Thiếu refresh token trong yêu cầu khôi phục phiên Kiosk.",
                "MISSING_REFRESH_TOKEN");
        }

        var result = await _sender.Send(
            new RefreshTokenCommand(refreshToken, UserRole.Student),
            cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                AuthErrors.AccountLocked or AuthErrors.KioskStudentOnly => StatusCodes.Status403Forbidden,
                var err when err.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status401Unauthorized
            };

            var errorCode = result.Error switch
            {
                AuthErrors.AccountLocked => "ACCOUNT_LOCKED",
                AuthErrors.KioskStudentOnly => "KIOSK_STUDENT_ONLY",
                var err when err.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase) => "REFRESH_TOKEN_CONFLICT",
                _ => "INVALID_REFRESH_TOKEN"
            };

            var title = result.Error switch
            {
                AuthErrors.AccountLocked => "Tài khoản bị khóa.",
                AuthErrors.KioskStudentOnly => "Tài khoản không được phép sử dụng Kiosk.",
                var err when err.StartsWith("CONFLICT", StringComparison.OrdinalIgnoreCase) => "Xung đột token đồng thời.",
                _ => "Token không hợp lệ."
            };

            return CreateKioskAuthProblem(statusCode, title, result.Error, errorCode);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Đổi mật khẩu lần đầu dành riêng cho sinh viên trên Native Desktop Kiosk (MF-04).
    /// Trả refreshToken mới trong JSON body và KHÔNG đặt cookie trình duyệt.
    /// </summary>
    [HttpPost("kiosk/change-password")]
    [Authorize(Roles = "student")]
    [EnableRateLimiting("auth-login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> KioskChangePassword(
        [FromBody] ChangePasswordRequest? request,
        CancellationToken cancellationToken = default)
    {
        var headerProblem = ValidateKioskClientHeaders();
        if (headerProblem is not null)
        {
            return headerProblem;
        }

        if (request is null)
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status400BadRequest,
                "Dữ liệu đầu vào không hợp lệ.",
                "Nội dung đổi mật khẩu không được để trống.",
                "INVALID_REQUEST");
        }

        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status401Unauthorized,
                "Không xác thực được.",
                "Danh tính trong token không hợp lệ.",
                "INVALID_SUBJECT");
        }

        var result = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword, UserRole.Student),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult? ValidateKioskClientHeaders()
    {
        var rawDeviceId = Request?.Headers != null
            && Request.Headers.TryGetValue(OfficialExamDeviceGuard.DeviceIdHeaderName, out var deviceValues)
                ? deviceValues.ToString()
                : null;
        if (!OfficialExamDeviceGuard.TryNormalizeDeviceId(rawDeviceId, out _))
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status400BadRequest,
                "Thiếu định danh thiết bị Kiosk.",
                "Header X-Device-Id bắt buộc phải hợp lệ (16–128 ký tự [A-Za-z0-9._:-]).",
                "INVALID_DEVICE_ID");
        }

        var rawVersion = Request?.Headers != null
            && Request.Headers.TryGetValue(OfficialExamDeviceGuard.KioskVersionHeaderName, out var versionValues)
                ? versionValues.ToString().Trim()
                : null;
        if (string.IsNullOrWhiteSpace(rawVersion) || !ValidKioskVersionRegex.IsMatch(rawVersion))
        {
            return CreateKioskAuthProblem(
                StatusCodes.Status400BadRequest,
                "Thiếu phiên bản ứng dụng Kiosk.",
                "Header X-Kiosk-Version bắt buộc phải hợp lệ (2–64 ký tự [A-Za-z0-9._+-]).",
                "INVALID_KIOSK_VERSION");
        }

        return null;
    }

    private IActionResult CreateKioskAuthProblem(int statusCode, string title, string detail, string errorCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231"
        };
        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = HttpContext?.TraceIdentifier;
        return StatusCode(statusCode, problem);
    }

    private IActionResult AuthProblem(string error)
    {
        var statusCode = error switch
        {
            AuthErrors.AccountNotFound or AuthErrors.WrongPassword or AuthErrors.PasswordNotSet or AuthErrors.InvalidCredentials => StatusCodes.Status401Unauthorized,
            AuthErrors.GoogleTokenInvalid or AuthErrors.GoogleNonceInvalid => StatusCodes.Status401Unauthorized,
            AuthErrors.AccountLocked or AuthErrors.KioskStudentOnly => StatusCodes.Status403Forbidden,
            AuthErrors.GoogleStudentCodeConflict => StatusCodes.Status409Conflict,
            AuthErrors.LoginTemporarilyLocked => StatusCodes.Status429TooManyRequests,
            AuthErrors.CurrentPasswordWrong => StatusCodes.Status400BadRequest,
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
                StatusCodes.Status403Forbidden => error == AuthErrors.KioskStudentOnly
                    ? "Tài khoản không được phép sử dụng Kiosk."
                    : "Tài khoản bị khóa.",
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
            AuthErrors.KioskStudentOnly => "KIOSK_STUDENT_ONLY",
            AuthErrors.GoogleStudentCodeConflict => "STUDENT_CODE_CONFLICT",
            AuthErrors.LoginTemporarilyLocked => "LOGIN_TEMPORARILY_LOCKED",
            AuthErrors.GoogleTokenInvalid => "GOOGLE_TOKEN_INVALID",
            AuthErrors.GoogleNonceInvalid => "GOOGLE_NONCE_INVALID",
            AuthErrors.CurrentPasswordWrong => "CURRENT_PASSWORD_WRONG",
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
