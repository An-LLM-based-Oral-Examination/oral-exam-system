using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;

namespace API.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "student,lecturer,department_head,examination_officer,admin")]
public class StorageController : ControllerBase
{
    private const long MaxAudioFileSizeBytes = 25 * 1024 * 1024; // 25 MB
    private readonly ISender _sender;

    public StorageController(ISender sender)
    {
        _sender = sender;
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

    /// <summary>
    /// Nhận file ghi âm trực tiếp dạng stream và bóc băng Whisper STT phục vụ Buffer Screen cho MF-01/MF-02.
    /// Không lưu trữ lên Cloudflare R2 và không lưu URL vào database.
    /// </summary>
    [HttpPost("upload-audio")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAudio(
        IFormFile? file,
        [FromForm] string? clientTranscript = null,
        [FromForm] string? vocabularyHint = null,
        CancellationToken ct = default)
    {
        IFormFile? targetFile = file;
        if (targetFile == null && HttpContext != null && Request != null && Request.HasFormContentType && Request.Form.Files.Count > 0)
        {
            targetFile = Request.Form.Files[0];
        }

        var resolvedClientTranscript = clientTranscript;
        if (string.IsNullOrWhiteSpace(resolvedClientTranscript) && HttpContext != null && Request?.HasFormContentType == true)
        {
            resolvedClientTranscript = Request.Form["clientTranscript"].FirstOrDefault()
                ?? Request.Form["transcript"].FirstOrDefault();
        }

        var resolvedVocabularyHint = vocabularyHint;
        if (string.IsNullOrWhiteSpace(resolvedVocabularyHint) && HttpContext != null && Request?.HasFormContentType == true)
        {
            resolvedVocabularyHint = Request.Form["vocabularyHint"].FirstOrDefault();
        }

        if (targetFile == null || targetFile.Length == 0)
        {
            return BadRequest(ApiProblems.CreateForController(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Không tìm thấy file ghi âm."));
        }

        if (targetFile.Length > MaxAudioFileSizeBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status413PayloadTooLarge,
                    "Dung lượng file ghi âm vượt quá giới hạn cho phép (tối đa 25MB).",
                    errorCode: "PAYLOAD_TOO_LARGE"));
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

        using var sourceStream = targetFile.OpenReadStream();
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
                var initialCapacity = (int)Math.Min(Math.Max(targetFile.Length, headerBytesRead), MaxAudioFileSizeBytes);
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
                            "Dung lượng file ghi âm vượt quá giới hạn cho phép (tối đa 25MB).",
                            errorCode: "PAYLOAD_TOO_LARGE"));
                }

                bufferedStream.Position = 0;
                audioStream = bufferedStream;
            }

            var sanitizedHint = SpeechVocabularyHintSanitizer.Sanitize(resolvedVocabularyHint);
            var command = new UploadAudioPracticeCommand(
                audioStream,
                targetFile.FileName ?? "audio.webm",
                contentType,
                string.IsNullOrWhiteSpace(resolvedClientTranscript) ? null : resolvedClientTranscript,
                sanitizedHint,
                ActorUserId: GetCurrentUserId());
            var result = await _sender.Send(command, ct);

            if (!result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(result.Error) &&
                    result.Error.StartsWith("GATEWAY_TIMEOUT:", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(
                        StatusCodes.Status504GatewayTimeout,
                        ApiProblems.CreateForController(
                            HttpContext,
                            StatusCodes.Status504GatewayTimeout,
                            result.Error,
                            errorCode: "GATEWAY_TIMEOUT"));
                }

                if (!string.IsNullOrWhiteSpace(result.Error) &&
                    result.Error.StartsWith("BAD_GATEWAY:", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(
                        StatusCodes.Status502BadGateway,
                        ApiProblems.CreateForController(
                            HttpContext,
                            StatusCodes.Status502BadGateway,
                            result.Error,
                            errorCode: "BAD_GATEWAY"));
                }

                return BadRequest(ApiProblems.CreateForController(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    result.Error));
            }

            return Ok(result.Value);
        }
        finally
        {
            bufferedStream?.Dispose();
        }
    }
}
