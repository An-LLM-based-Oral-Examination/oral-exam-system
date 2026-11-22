using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure.Resilience;

namespace OralExamination.Infrastructure.Services;

public sealed class CloudflareR2StorageService : IStorageService, IDisposable
{
    private const string ImmutableUploadHeader = "If-None-Match";
    private const string ImmutableUploadHeaderValue = "*";
    private static readonly TimeSpan MaximumPresignedUrlLifetime = TimeSpan.FromDays(7);

    private readonly IAmazonS3 _s3Client;
    private readonly ExternalDependencyResilience _resilience;
    private readonly bool _ownsResilience;
    private readonly string _bucketName;
    private readonly string? _configurationError;

    public CloudflareR2StorageService(
        IConfiguration configuration,
        ExternalDependencyResilience? resilience = null,
        IAmazonS3? s3Client = null)
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
            AuthenticationRegion = "auto",
            // Application pipeline owns retries; avoid SDK × service × worker attempt multiplication.
            MaxErrorRetry = 0
        };

        _s3Client = s3Client ?? new AmazonS3Client(accessKey, secretKey, config);
        _ownsResilience = resilience is null;
        _resilience = resilience ?? new ExternalDependencyResilience(
            configuration, NullLogger<ExternalDependencyResilience>.Instance);
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

        await _resilience.ExecuteAsync(ExternalDependency.R2, async ct =>
        {
            await fileTransferUtility.UploadAsync(uploadRequest, ct);
            return true;
        }, retry: false); // A streaming write must not be blindly replayed after an ambiguous network result.

        // Bucket thi thật là private; Application chỉ lưu object key.
        return objectKey;
    }

    public Task<StoragePresignedUploadResult> CreatePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default) =>
        CreatePresignedUploadUrlAsync(objectKey, contentType, expiresIn, null, cancellationToken);

    public async Task<StoragePresignedUploadResult> CreatePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiresIn,
        string? expectedSha256Base64,
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

        string? normalizedSha256Base64 = null;
        if (!string.IsNullOrWhiteSpace(expectedSha256Base64))
        {
            normalizedSha256Base64 = expectedSha256Base64.Trim();
            try
            {
                var decoded = Convert.FromBase64String(normalizedSha256Base64);
                if (decoded.Length != SHA256.HashSizeInBytes)
                {
                    throw new ArgumentException("x-amz-checksum-sha256 phải mã hóa đúng 32 byte SHA-256.", nameof(expectedSha256Base64));
                }
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("x-amz-checksum-sha256 không phải chuỗi Base64 hợp lệ.", nameof(expectedSha256Base64), exception);
            }
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

        var requiredHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = contentType,
            [ImmutableUploadHeader] = ImmutableUploadHeaderValue
        };

        if (normalizedSha256Base64 is not null)
        {
            request.Headers["x-amz-checksum-sha256"] = normalizedSha256Base64;
            requiredHeaders["x-amz-checksum-sha256"] = normalizedSha256Base64;
        }

        var uploadUrl = await _s3Client.GetPreSignedURLAsync(request);

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
            var response = await _resilience.ExecuteAsync(ExternalDependency.R2, ct => _s3Client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    ChecksumMode = ChecksumMode.ENABLED
                },
                ct), cancellationToken);

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

    public async Task<Stream> OpenReadStreamAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidateObjectKey(objectKey);

        var response = await _resilience.ExecuteAsync(
            ExternalDependency.R2,
            ct => _s3Client.GetObjectAsync(new GetObjectRequest { BucketName = _bucketName, Key = objectKey }, ct),
            cancellationToken);

        return new OwnedS3ResponseStream(response);
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

        // O(1) Fast-path: ưu tiên HeadObject ChecksumSHA256 để không tải lại file âm thanh
        var metadata = await GetObjectMetadataAsync(objectKey, cancellationToken);
        if (metadata is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.ChecksumSha256Base64))
        {
            try
            {
                var headerHash = Convert.FromBase64String(metadata.ChecksumSha256Base64.Trim());
                if (headerHash.Length == SHA256.HashSizeInBytes)
                {
                    return CryptographicOperations.FixedTimeEquals(headerHash, expectedHash);
                }
            }
            catch (FormatException)
            {
                // Fall back to streaming hash below if header format is unexpected.
            }
        }

        return await _resilience.ExecuteAsync(ExternalDependency.R2, async ct =>
        {
            using var response = await _s3Client.GetObjectAsync(
                new GetObjectRequest { BucketName = _bucketName, Key = objectKey }, ct);
            var actualHash = await SHA256.HashDataAsync(response.ResponseStream, ct);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }, cancellationToken);
    }

    private sealed class OwnedS3ResponseStream(GetObjectResponse response) : Stream
    {
        private readonly GetObjectResponse _response = response;
        private readonly Stream _inner = response.ResponseStream;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            await base.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _s3Client.Dispose();
        if (_ownsResilience) _resilience.Dispose();
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
