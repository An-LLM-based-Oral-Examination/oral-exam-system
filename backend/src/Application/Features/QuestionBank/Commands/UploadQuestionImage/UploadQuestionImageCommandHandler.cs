using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;

public sealed class UploadQuestionImageCommandHandler
    : IRequestHandler<UploadQuestionImageCommand, Result<UploadQuestionImageResponseDto>>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private readonly IStorageService? _storageService;

    public UploadQuestionImageCommandHandler(IStorageService? storageService = null)
    {
        _storageService = storageService;
    }

    public async Task<Result<UploadQuestionImageResponseDto>> Handle(
        UploadQuestionImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileStream == null || request.FileSizeBytes <= 0)
        {
            return Result<UploadQuestionImageResponseDto>.Failure(
                "BAD_REQUEST: Không tìm thấy tệp hình ảnh hoặc tệp rỗng.");
        }

        if (request.FileSizeBytes > MaxImageBytes)
        {
            return Result<UploadQuestionImageResponseDto>.Failure(
                "PAYLOAD_TOO_LARGE: Dung lượng hình ảnh câu hỏi vượt quá giới hạn tối đa 5MB.");
        }

        byte[] bytes;
        using (var memoryStream = new MemoryStream())
        {
            if (request.FileStream.CanSeek)
            {
                request.FileStream.Position = 0;
            }

            await request.FileStream.CopyToAsync(memoryStream, cancellationToken);
            bytes = memoryStream.ToArray();
        }

        if (bytes.Length == 0)
        {
            return Result<UploadQuestionImageResponseDto>.Failure(
                "BAD_REQUEST: Không tìm thấy tệp hình ảnh hoặc tệp rỗng.");
        }

        if (bytes.Length > MaxImageBytes)
        {
            return Result<UploadQuestionImageResponseDto>.Failure(
                "PAYLOAD_TOO_LARGE: Dung lượng hình ảnh câu hỏi vượt quá giới hạn tối đa 5MB.");
        }

        string? canonicalExt = null;
        string? canonicalMime = null;

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            canonicalExt = ".png";
            canonicalMime = "image/png";
        }
        else if (bytes.Length >= 3 &&
                 bytes[0] == 0xFF &&
                 bytes[1] == 0xD8 &&
                 bytes[2] == 0xFF)
        {
            canonicalExt = ".jpg";
            canonicalMime = "image/jpeg";
        }
        else if (bytes.Length >= 12 &&
                 bytes[0] == 0x52 &&
                 bytes[1] == 0x49 &&
                 bytes[2] == 0x46 &&
                 bytes[3] == 0x46 &&
                 bytes[8] == 0x57 &&
                 bytes[9] == 0x45 &&
                 bytes[10] == 0x42 &&
                 bytes[11] == 0x50)
        {
            canonicalExt = ".webp";
            canonicalMime = "image/webp";
        }

        if (canonicalExt == null || canonicalMime == null)
        {
            return Result<UploadQuestionImageResponseDto>.Failure(
                "UNSUPPORTED_MEDIA_TYPE: Định dạng tệp không hợp lệ. Hệ thống chỉ chấp nhận hình ảnh PNG, JPG/JPEG hoặc WEBP hợp lệ.");
        }

        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var storedFileName = $"{Guid.NewGuid():N}{canonicalExt}";
        var objectKey = $"questions/{dateFolder}/{storedFileName}";
        var localFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "questions", dateFolder);

        Directory.CreateDirectory(localFolder);
        var localFilePath = Path.Combine(localFolder, storedFileName);
        await File.WriteAllBytesAsync(localFilePath, bytes, cancellationToken);

        if (_storageService != null)
        {
            try
            {
                using var r2Stream = new MemoryStream(bytes);
                await _storageService.UploadFileAsync(r2Stream, objectKey, canonicalMime);
            }
            catch
            {
                // Bỏ qua lỗi hạ tầng đám mây khi chạy offline hoặc chưa cấu hình R2, đã lưu bản sao tĩnh tại local wwwroot
            }
        }

        var imageUrl = $"/uploads/questions/{dateFolder}/{storedFileName}";
        var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(request.FileName) ? storedFileName : request.FileName);
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        return Result<UploadQuestionImageResponseDto>.Success(new UploadQuestionImageResponseDto(
            imageUrl,
            objectKey,
            safeFileName,
            canonicalMime,
            bytes.Length,
            normalizedDescription));
    }
}
