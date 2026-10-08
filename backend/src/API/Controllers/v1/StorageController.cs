using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class StorageController : ControllerBase
{
    private readonly ISender _sender;

    public StorageController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Nhận file ghi âm trực tiếp dạng stream và bóc băng Whisper STT phục vụ Buffer Screen cho MF-01/MF-02.
    /// Không lưu trữ lên Cloudflare R2 và không lưu URL vào database.
    /// </summary>
    [HttpPost("upload-audio")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAudio([FromForm] IFormFile? file)
    {
        IFormFile? targetFile = file;
        if (targetFile == null && HttpContext != null && Request != null && Request.HasFormContentType && Request.Form.Files.Count > 0)
        {
            targetFile = Request.Form.Files[0];
        }

        if (targetFile == null || targetFile.Length == 0)
        {
            return BadRequest(new { error = "Không tìm thấy file ghi âm." });
        }

        if (targetFile.Length > 25 * 1024 * 1024)
        {
            return BadRequest(new { error = "Dung lượng file ghi âm vượt quá giới hạn cho phép (tối đa 25MB)." });
        }

        string contentType = "application/octet-stream";
        try
        {
            contentType = !string.IsNullOrWhiteSpace(targetFile.ContentType) ? targetFile.ContentType : "application/octet-stream";
        }
        catch
        {
            contentType = "application/octet-stream";
        }

        using var stream = targetFile.OpenReadStream();
        var command = new UploadAudioPracticeCommand(stream, targetFile.FileName ?? "audio.webm", contentType);
        var result = await _sender.Send(command);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
