using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;

namespace API.Controllers.v1;

/// <summary>
/// Public API tra cứu & xác thực chữ ký số bảng điểm điện tử qua mã QR (BE-V02).
/// Không yêu cầu đăng nhập (AllowAnonymous) để thanh tra Khảo thí hoặc bên thứ ba quét QR xác minh trực tiếp.
/// </summary>
[ApiController]
[Route("api/v1/verification")]
[AllowAnonymous]
public class VerificationController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// Tra cứu và xác thực tính toàn vẹn của bảng điểm ca thi thông qua mã băm SHA-256 (certificateHash).
    /// </summary>
    [HttpGet("grades/{certificateHash}")]
    public async Task<IActionResult> VerifyGradeCertificate(
        [FromRoute] string certificateHash,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new VerifyGradeCertificateQuery(certificateHash), cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Chứng thư bảng điểm không hợp lệ hoặc không tồn tại.",
                Detail = result.Error,
                Type = "https://oralexam.fpt.edu.vn/errors/certificate-verification-failed"
            });
        }

        return Ok(result.Value);
    }
}
