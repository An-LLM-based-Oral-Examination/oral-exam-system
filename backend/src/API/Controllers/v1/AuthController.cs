using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Auth;
using OralExamination.Application.Features.Auth.Commands.ChangePassword;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.Commands.RefreshToken;
using OralExamination.Application.Features.Auth.Commands.SignOut;
using OralExamination.Application.Features.Auth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers.v1;

/// <summary>
/// Controller xử lý xác thực Google OAuth 2.0. Toàn bộ endpoint trong controller này là Public.
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
    /// Đăng nhập bằng Google ID Token. Field role trong body bị bỏ qua.
    /// Email chưa có trong bảng users thì tạo tài khoản student, student_code để trống.
    /// Email đã có và còn active thì giữ role trong database.
    /// Tài khoản bị khóa thì trả 403.
    /// </summary>
    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var command = new GoogleLoginCommand(request.IdToken);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var statusCode = result.Error switch
            {
                "ID Token của Google không hợp lệ." => StatusCodes.Status401Unauthorized,
                "Tài khoản đã bị khóa." => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status401Unauthorized => "Không xác thực được.",
                    StatusCodes.Status403Forbidden => "Tài khoản bị khóa.",
                    _ => "Dữ liệu đầu vào không hợp lệ."
                },
                Detail = result.Error,
                Type = "https://tools.ietf.org/html/rfc7231"
            });
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
        var sub = User.FindFirst("sub")?.Value;
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
            AuthErrors.WrongPassword or AuthErrors.CurrentPasswordWrong => StatusCodes.Status401Unauthorized,
            AuthErrors.AccountLocked => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Không tìm thấy tài khoản.",
                StatusCodes.Status401Unauthorized => "Không xác thực được.",
                StatusCodes.Status403Forbidden => "Tài khoản bị khóa.",
                _ => "Dữ liệu đầu vào không hợp lệ."
            },
            Detail = error,
            Type = "https://tools.ietf.org/html/rfc7231"
        });
    }
}
