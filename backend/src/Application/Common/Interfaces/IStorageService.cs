namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Cổng giao tiếp với kho lưu trữ đối tượng riêng tư.
/// Các phương thức chỉ nhận và trả object key, không làm lộ thông tin xác thực hạ tầng.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Tải một luồng dữ liệu lên kho riêng tư và trả về object key đã lưu.
    /// </summary>
    Task<string> UploadFileAsync(Stream fileStream, string objectKey, string contentType);

    Task<StoragePresignedUploadResult> CreatePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<StoragePresignedDownloadResult> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> ObjectExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> VerifySha256Async(
        string objectKey,
        string expectedSha256Hex,
        CancellationToken cancellationToken = default);
}

public sealed record StoragePresignedUploadResult(
    string UploadUrl,
    string ObjectKey,
    DateTime ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);

public sealed record StoragePresignedDownloadResult(
    string DownloadUrl,
    string ObjectKey,
    DateTime ExpiresAt);

public sealed record StorageObjectMetadata(
    string ObjectKey,
    long ContentLength,
    string? ContentType,
    string? ETag,
    DateTime? LastModified,
    string? ChecksumSha256Base64);
