using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Features.Speech.Commands.TranscribeSpeech;
using OralExamination.Application.Features.Speech.Queries.GetTtsVoices;
using OralExamination.Application.Features.Speech.Queries.SynthesizeSpeech;
using OralExamination.Infrastructure.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/speech")]
[Authorize]
public class SpeechController : ControllerBase
{
    private const long MaxAudioFileSizeBytes = 25 * 1024 * 1024; // 25 MB
    private readonly ISender _sender;

    public SpeechController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lấy danh sách các giọng đọc AI giám khảo khả dụng (Cô Hoài My - Nữ, Thầy Nam Minh - Nam).
    /// </summary>
    [HttpGet("voices")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVoices(CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetTtsVoicesQuery(), ct);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tổng hợp giọng đọc AI giám khảo (Text-to-Speech) qua phương thức POST.
    /// Hỗ trợ tự động chuyển đổi dự phòng từ Edge Neural TTS (Option 1) sang Google Neural TTS (Option 2).
    /// </summary>
    [HttpPost("tts")]
    [AllowAnonymous]
    public async Task<IActionResult> SynthesizeSpeechPost(
        [FromBody] SynthesizeSpeechHttpRequest? request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "Nội dung văn bản cần đọc không được để trống." });
        }

        return await ExecuteSynthesizeSpeechAsync(
            request.Text,
            request.VoiceName,
            request.SpeakingRate ?? 1.0,
            ct);
    }

    /// <summary>
    /// Tổng hợp giọng đọc AI giám khảo (Text-to-Speech) qua phương thức GET.
    /// </summary>
    [HttpGet("tts")]
    [AllowAnonymous]
    public async Task<IActionResult> SynthesizeSpeechGet(
        [FromQuery] string? text,
        [FromQuery] string? voiceName = null,
        [FromQuery] double speakingRate = 1.0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Nội dung văn bản cần đọc không được để trống." });
        }

        return await ExecuteSynthesizeSpeechAsync(text, voiceName, speakingRate, ct);
    }

    /// <summary>
    /// Bóc băng giọng nói sinh viên (Speech-to-Text) với cơ chế tự động dự phòng
    /// Cloudflare Whisper AI (Option 1) sang Gemini Multimodal STT (Option 2) và chuẩn hóa thuật ngữ SE.
    /// </summary>
    [HttpPost("stt")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> TranscribeSpeech(
        IFormFile? file,
        [FromForm] string? vocabularyHint = null,
        [FromForm] string? clientTranscript = null,
        CancellationToken ct = default)
    {
        var resolvedFile = file;
        if (resolvedFile is null && HttpContext?.Request?.HasFormContentType == true && HttpContext.Request.Form.Files.Count > 0)
        {
            resolvedFile = HttpContext.Request.Form.Files[0];
        }

        var resolvedClientTranscript = clientTranscript;
        if (string.IsNullOrWhiteSpace(resolvedClientTranscript) && HttpContext?.Request?.HasFormContentType == true)
        {
            resolvedClientTranscript = HttpContext.Request.Form["clientTranscript"].ToString();
            if (string.IsNullOrWhiteSpace(resolvedClientTranscript))
            {
                resolvedClientTranscript = HttpContext.Request.Form["transcript"].ToString();
            }
        }

        if (resolvedFile is null || resolvedFile.Length == 0)
        {
            return BadRequest(new { error = "File ghi âm không hợp lệ hoặc rỗng." });
        }

        if (resolvedFile.Length > MaxAudioFileSizeBytes)
        {
            return BadRequest(new { error = "Dung lượng file ghi âm vượt quá giới hạn 25MB." });
        }

        using var memoryStream = new MemoryStream();
        await resolvedFile.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        var fileName = string.IsNullOrWhiteSpace(resolvedFile.FileName) ? "speech.webm" : resolvedFile.FileName;
        var command = new TranscribeSpeechCommand(
            memoryStream,
            fileName,
            vocabularyHint,
            string.IsNullOrWhiteSpace(resolvedClientTranscript) ? null : resolvedClientTranscript);

        var result = await _sender.Send(command, ct);
        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    private async Task<IActionResult> ExecuteSynthesizeSpeechAsync(
        string text,
        string? voiceName,
        double speakingRate,
        CancellationToken ct)
    {
        var query = new SynthesizeSpeechQuery(text, voiceName, speakingRate);
        var result = await _sender.Send(query, ct);

        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { error = result.Error });
        }

        var synthesis = result.Value;
        if (Response?.Headers is not null)
        {
            Response.Headers["X-TTS-Provider"] = synthesis.Provider;
            Response.Headers["X-TTS-Voice"] = synthesis.VoiceName;
            Response.Headers["X-TTS-Rate"] = synthesis.SpeakingRate.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            Response.Headers["X-TTS-Fallback"] = synthesis.UsedFallback ? "true" : "false";
            Response.Headers["X-TTS-Cache"] = synthesis.FromCache ? "HIT" : "MISS";
        }

        var fileExtension = string.Equals(synthesis.ContentType, "audio/wav", System.StringComparison.OrdinalIgnoreCase)
            ? "wav"
            : "mp3";
        return File(synthesis.AudioBytes, synthesis.ContentType, $"question-speech.{fileExtension}");
    }

    /// <summary>
    /// Đặt lại thời gian đóng băng (cooldown) của Edge Neural TTS (dành cho Quản trị viên và Trưởng Bộ Môn).
    /// </summary>
    [HttpPost("edge-cooldown/reset")]
    [Authorize(Roles = "admin,department_head")]
    public IActionResult ResetEdgeCooldown()
    {
        ResilientTextToSpeechService.ResetEdgeCooldown();
        return Ok(new
        {
            success = true,
            message = "Thời gian đóng băng Edge Neural TTS đã được thiết lập lại thành công.",
            isEdgeInCooldown = ResilientTextToSpeechService.IsEdgeInCooldown
        });
    }

    /// <summary>
    /// Lấy trạng thái đóng băng hiện tại của Edge Neural TTS.
    /// </summary>
    [HttpGet("edge-cooldown/status")]
    [Authorize(Roles = "admin,department_head")]
    public IActionResult GetEdgeCooldownStatus()
    {
        return Ok(new
        {
            isEdgeInCooldown = ResilientTextToSpeechService.IsEdgeInCooldown,
            cooldownUntilUtc = ResilientTextToSpeechService.EdgeCooldownUntilUtc
        });
    }
}

public sealed record SynthesizeSpeechHttpRequest(
    string Text,
    string? VoiceName = null,
    double? SpeakingRate = 1.0
);
