using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Application.Features.Auth.Commands.CreateGoogleLoginChallenge;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.Commands.SignOut;
using OralExamination.Application.Features.Auth.DTOs;
using OralExamination.Application.Features.Auth.Queries.GetCurrentUser;
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(GoogleLoginResponse), StatusCodes.Status200OK)]
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

        return Ok(result.Value);
    }

    /// <summary>
    /// Cấp lại Access Token mới bằng Refresh Token (Silent Refresh).
    /// Refresh Token cũ bị thu hồi, trả về cặp token mới (Token Rotation).
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                "Tài khoản đã bị khóa." => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status401Unauthorized
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status403Forbidden
                    ? "Tài khoản bị khóa."
                    : "Token không hợp lệ.",
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231"
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Đăng xuất — thu hồi Refresh Token phía server.
    /// User phải đăng nhập lại Google để lấy token mới.
    /// </summary>
    [HttpPost("sign-out")]
    [AllowAnonymous]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest request, CancellationToken cancellationToken)
    {
        var command = new SignOutCommand(request.RefreshToken);
        var result = await _sender.Send(command, cancellationToken);

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
    public async Task<IActionResult> PasswordLogin([FromBody] PasswordLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PasswordLoginCommand(request.Username, request.Password), cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        return Ok(result.Value);
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
        if (!Guid.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult AuthProblem(string error)
    {
        var statusCode = error switch
        {
            AuthErrors.AccountNotFound => StatusCodes.Status404NotFound,
            AuthErrors.WrongPassword or AuthErrors.CurrentPasswordWrong or AuthErrors.GoogleTokenInvalid or AuthErrors.GoogleNonceInvalid => StatusCodes.Status401Unauthorized,
            AuthErrors.AccountLocked => StatusCodes.Status403Forbidden,
            AuthErrors.LoginTemporarilyLocked => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Không tìm thấy tài khoản.",
                StatusCodes.Status401Unauthorized => "Không xác thực được.",
                StatusCodes.Status403Forbidden => "Tài khoản bị khóa.",
                StatusCodes.Status429TooManyRequests => "Đăng nhập tạm thời bị khóa.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = error,
            Type = "https://tools.ietf.org/html/rfc7231"
        };
        problem.Extensions["errorCode"] = error switch
        {
            AuthErrors.AccountNotFound => "ACCOUNT_NOT_FOUND",
            AuthErrors.AccountLocked => "ACCOUNT_LOCKED",
            AuthErrors.LoginTemporarilyLocked => "LOGIN_TEMPORARILY_LOCKED",
            AuthErrors.GoogleTokenInvalid => "GOOGLE_TOKEN_INVALID",
            AuthErrors.GoogleNonceInvalid => "GOOGLE_NONCE_INVALID",
            _ => "AUTH_FAILED"
        };
        problem.Extensions["traceId"] = HttpContext?.TraceIdentifier;
        if (statusCode == 429 && HttpContext is not null)
            Response.Headers.RetryAfter = (HttpContext.RequestServices.GetService<OralExamination.Application.Common.Auth.LoginProtectionOptions>()?.LockoutSeconds ?? 900).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(statusCode, problem);
    }

    private CookieOptions GoogleNonceCookie(TimeSpan? maxAge = null)
    {
        var environment = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        return new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Path = GoogleLoginRules.CookiePath, MaxAge = maxAge,
            Secure = Request.IsHttps || !(environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        };
    }
}
