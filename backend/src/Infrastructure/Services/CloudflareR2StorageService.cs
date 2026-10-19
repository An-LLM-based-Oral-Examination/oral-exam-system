using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Services;

public sealed class CloudflareR2StorageService : IStorageService, IDisposable
{
    private const string ImmutableUploadHeader = "If-None-Match";
    private const string ImmutableUploadHeaderValue = "*";
    private static readonly TimeSpan MaximumPresignedUrlLifetime = TimeSpan.FromDays(7);

    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string? _configurationError;

    public CloudflareR2StorageService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var accessKey = configuration["Storage:AccessKey"]?.Trim();
        var secretKey = configuration["Storage:SecretKey"]?.Trim();
        var serviceUrl = configuration["Storage:ServiceUrl"]?.Trim();
        _bucketName = configuration["Storage:BucketName"]?.Trim() ?? "oral-exam-bucket";

        if (string.IsNullOrWhiteSpace(accessKey) ||
            string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(serviceUrl))
        {
            _configurationError = "Thiếu cấu hình Storage:AccessKey, Storage:SecretKey hoặc Storage:ServiceUrl.";
            accessKey = "storage-not-configured";
            secretKey = "storage-not-configured";
            serviceUrl = "https://storage-not-configured.invalid";
        }

        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var serviceUri)
            || !string.Equals(serviceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage:ServiceUrl phải là HTTPS URL tuyệt đối hợp lệ.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = serviceUri.AbsoluteUri.TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string objectKey, string contentType)
    {
        EnsureConfigured();
        ArgumentNullException.ThrowIfNull(fileStream);
        ValidateObjectKey(objectKey);
        ValidateContentType(contentType);

        using var fileTransferUtility = new TransferUtility(_s3Client);
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = objectKey,
            BucketName = _bucketName,
            ContentType = contentType
        };

        await fileTransferUtility.UploadAsync(uploadRequest);

        // Bucket thi thật là private; Application chỉ lưu object key.
        return objectKey;
    }

    public async Task<StoragePresignedUploadResult> CreatePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateObjectKey(objectKey);
        ValidateContentType(contentType);

        if (expiresIn <= TimeSpan.Zero || expiresIn > MaximumPresignedUrlLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresIn),
                "Thời hạn presigned URL phải lớn hơn 0 và không vượt quá 7 ngày.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAt,
            ContentType = contentType
        };

        // R2 đánh giá điều kiện này nguyên tử, ngăn ghi đè audio đã niêm phong.
        request.Headers[ImmutableUploadHeader] = ImmutableUploadHeaderValue;

        var uploadUrl = await _s3Client.GetPreSignedURLAsync(request);
        var requiredHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = contentType,
            [ImmutableUploadHeader] = ImmutableUploadHeaderValue
        };

        return new StoragePresignedUploadResult(
            uploadUrl,
            objectKey,
            expiresAt,
            requiredHeaders);
    }

    public async Task<StoragePresignedDownloadResult> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateObjectKey(objectKey);
        if (expiresIn <= TimeSpan.Zero || expiresIn > MaximumPresignedUrlLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresIn),
                "Thời hạn presigned URL phải lớn hơn 0 và không vượt quá 7 ngày.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        var downloadUrl = await _s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt
        });

        return new StoragePresignedDownloadResult(downloadUrl, objectKey, expiresAt);
    }

    public async Task<StorageObjectMetadata?> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateObjectKey(objectKey);

        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    ChecksumMode = ChecksumMode.ENABLED
                },
                cancellationToken);

            return new StorageObjectMetadata(
                objectKey,
                response.Headers.ContentLength,
                response.Headers.ContentType,
                response.ETag,
                response.LastModified,
                response.ChecksumSHA256);
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.NotFound
            || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.Ordinal))
        {
            return null;
        }
    }

    public async Task<bool> ObjectExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        return await GetObjectMetadataAsync(objectKey, cancellationToken) is not null;
    }

    public async Task<bool> VerifySha256Async(
        string objectKey,
        string expectedSha256Hex,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateObjectKey(objectKey);

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedSha256Hex);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentNullException)
        {
            throw new ArgumentException(
                "SHA-256 phải là chuỗi hexadecimal hợp lệ gồm 64 ký tự.",
                nameof(expectedSha256Hex),
                exception);
        }

        if (expectedHash.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException(
                "SHA-256 phải là chuỗi hexadecimal hợp lệ gồm 64 ký tự.",
                nameof(expectedSha256Hex));
        }

        using var response = await _s3Client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            },
            cancellationToken);

        var actualHash = await SHA256.HashDataAsync(response.ResponseStream, cancellationToken);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }

    private void EnsureConfigured()
    {
        if (_configurationError is not null)
        {
            throw new InvalidOperationException(_configurationError);
        }
    }

    private static void ValidateObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("Object key không được để trống.", nameof(objectKey));
        }

        if (objectKey.StartsWith("/", StringComparison.Ordinal)
            || objectKey.Contains("..", StringComparison.Ordinal)
            || objectKey.Contains('\\'))
        {
            throw new ArgumentException("Object key không hợp lệ.", nameof(objectKey));
        }
    }

    private static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type không được để trống.", nameof(contentType));
        }
    }
}
