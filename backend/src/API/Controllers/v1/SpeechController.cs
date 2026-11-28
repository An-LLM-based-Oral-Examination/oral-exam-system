using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Speech.Commands.TranscribeSpeech;
using OralExamination.Application.Features.Speech.Queries.GetTtsVoices;
using OralExamination.Application.Features.Speech.Queries.SynthesizeSpeech;
using OralExamination.Infrastructure.Services;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/speech")]
[Authorize(Roles = "student,lecturer,department_head,examination_officer,admin")]
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
            return BadRequest(ApiProblems.CreateForController(HttpContext, StatusCodes.Status400BadRequest, result.Error));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Tổng hợp giọng đọc AI giám khảo (Text-to-Speech) qua phương thức POST.
    /// Hỗ trợ tự động chuyển đổi dự phòng từ Edge Neural TTS (Option 1) sang Google Neural TTS (Option 2).
    /// </summary>
    [HttpPost("tts")]
    public async Task<IActionResult> SynthesizeSpeechPost(
        [FromBody] SynthesizeSpeechHttpRequest? request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(ApiProblems.CreateForController(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Nội dung văn bản cần đọc không được để trống."));
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
    public async Task<IActionResult> SynthesizeSpeechGet(
        [FromQuery] string? text,
        [FromQuery] string? voiceName = null,
        [FromQuery] double speakingRate = 1.0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(ApiProblems.CreateForController(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Nội dung văn bản cần đọc không được để trống."));
        }

        return await ExecuteSynthesizeSpeechAsync(text, voiceName, speakingRate, ct);
    }

    /// <summary>
    /// Bóc băng giọng nói sinh viên (Speech-to-Text) với cơ chế tự động dự phòng
    /// Cloudflare Whisper AI (Option 1) sang Gemini Multimodal STT (Option 2) và chuẩn hóa thuật ngữ SE.
    /// </summary>
    [HttpPost("stt")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(26 * 1024 * 1024)]
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

        var resolvedVocabularyHint = vocabularyHint;
        if (string.IsNullOrWhiteSpace(resolvedVocabularyHint) && HttpContext?.Request?.HasFormContentType == true)
        {
            resolvedVocabularyHint = HttpContext.Request.Form["vocabularyHint"].ToString();
        }

        if (resolvedFile is null || resolvedFile.Length == 0)
        {
            return BadRequest(ApiProblems.CreateForController(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "File ghi âm không hợp lệ hoặc rỗng."));
        }

        if (resolvedFile.Length > MaxAudioFileSizeBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status413PayloadTooLarge,
                    "Dung lượng file ghi âm vượt quá giới hạn 25MB.",
                    errorCode: "PAYLOAD_TOO_LARGE"));
        }

        using var sourceStream = resolvedFile.OpenReadStream();
        var headerProbe = new byte[AudioHeaderValidator.HeaderProbeLength];
        var headerBytesRead = await ReadHeaderBytesAsync(sourceStream, headerProbe, ct);
        if (!AudioHeaderValidator.HasValidAudioMagicBytes(headerProbe.AsSpan(0, headerBytesRead)))
        {
            return StatusCode(
                StatusCodes.Status415UnsupportedMediaType,
                ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status415UnsupportedMediaType,
                    "Định dạng file ghi âm không được hỗ trợ. Chỉ chấp nhận file âm thanh hợp lệ (WebM, WAV, OGG, MP3, MP4/M4A, FLAC).",
                    errorCode: "UNSUPPORTED_AUDIO_MEDIA_TYPE"));
        }

        Stream audioStream = sourceStream;
        MemoryStream? bufferedStream = null;
        try
        {
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }
            else
            {
                var initialCapacity = (int)Math.Min(Math.Max(resolvedFile.Length, headerBytesRead), MaxAudioFileSizeBytes);
                bufferedStream = new MemoryStream(initialCapacity);
                bufferedStream.Write(headerProbe, 0, headerBytesRead);
                var withinLimit = await CopyRemainingBoundedAsync(
                    sourceStream,
                    bufferedStream,
                    MaxAudioFileSizeBytes - headerBytesRead,
                    ct);
                if (!withinLimit)
                {
                    return StatusCode(
                        StatusCodes.Status413PayloadTooLarge,
                        ApiProblems.CreateForController(
                            HttpContext,
                            StatusCodes.Status413PayloadTooLarge,
                            "Dung lượng file ghi âm vượt quá giới hạn 25MB.",
                            errorCode: "PAYLOAD_TOO_LARGE"));
                }

                bufferedStream.Position = 0;
                audioStream = bufferedStream;
            }

            var fileName = string.IsNullOrWhiteSpace(resolvedFile.FileName) ? "speech.webm" : resolvedFile.FileName;
            var sanitizedVocabularyHint = SpeechVocabularyHintSanitizer.Sanitize(resolvedVocabularyHint);
            var command = new TranscribeSpeechCommand(
                audioStream,
                fileName,
                sanitizedVocabularyHint,
                string.IsNullOrWhiteSpace(resolvedClientTranscript) ? null : resolvedClientTranscript,
                ActorUserId: GetCurrentUserId());

            var result = await _sender.Send(command, ct);
            if (!result.IsSuccess || result.Value is null)
            {
                return MapSpeechUpstreamOrBadRequest(result.Error);
            }

            return Ok(result.Value);
        }
        finally
        {
            bufferedStream?.Dispose();
        }
    }

    private IActionResult MapSpeechUpstreamOrBadRequest(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error) &&
            error.StartsWith("GATEWAY_TIMEOUT:", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status504GatewayTimeout,
                    error,
                    errorCode: "GATEWAY_TIMEOUT"));
        }

        if (!string.IsNullOrWhiteSpace(error) &&
            error.StartsWith("BAD_GATEWAY:", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status502BadGateway,
                    error,
                    errorCode: "BAD_GATEWAY"));
        }

        return BadRequest(ApiProblems.CreateForController(
            HttpContext,
            StatusCodes.Status400BadRequest,
            error));
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : null;
    }

    private static async Task<int> ReadHeaderBytesAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static async Task<bool> CopyRemainingBoundedAsync(
        Stream source,
        MemoryStream destination,
        long maxRemainingBytes,
        CancellationToken ct)
    {
        var chunk = new byte[81920];
        long totalCopied = 0;
        while (true)
        {
            var read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), ct);
            if (read == 0)
            {
                return true;
            }

            totalCopied += read;
            if (totalCopied > maxRemainingBytes)
            {
                return false;
            }

            destination.Write(chunk, 0, read);
        }
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
            return MapSpeechUpstreamOrBadRequest(result.Error);
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
