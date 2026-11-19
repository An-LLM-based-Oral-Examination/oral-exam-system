using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Authentication;

/// <summary>
/// Mã hóa AES-256-GCM với khóa dẫn xuất HKDF-SHA256 từ khóa bí mật ứng dụng
/// để bảo vệ mật khẩu tham gia lớp học (classes.join_password_protected) tại tầng lưu trữ.
/// </summary>
public sealed class AesGcmClassJoinPasswordProtector : IClassJoinPasswordProtector
{
    private const string Prefix = "aes256gcm.v1";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("oes-class-join-password-salt-v1");
    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("classes.join_password_protected.aes256gcm.v1");

    private readonly byte[] _key;
    private readonly ILogger<AesGcmClassJoinPasswordProtector>? _logger;

    [ActivatorUtilitiesConstructor]
    public AesGcmClassJoinPasswordProtector(
        IConfiguration configuration,
        ILogger<AesGcmClassJoinPasswordProtector>? logger = null)
        : this(ResolveMasterSecret(configuration), logger)
    {
    }

    public AesGcmClassJoinPasswordProtector(
        string masterSecret,
        ILogger<AesGcmClassJoinPasswordProtector>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(masterSecret) || Encoding.UTF8.GetByteCount(masterSecret) < 32)
        {
            throw new InvalidOperationException(
                "Khóa bí mật mã hóa mật khẩu lớp học phải có tối thiểu 32 byte.");
        }

        _logger = logger;
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(masterSecret),
            outputLength: 32,
            salt: HkdfSalt,
            info: HkdfInfo);
    }

    public string? Protect(string? plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
        {
            return null;
        }

        var trimmed = plainPassword.Trim();
        var plainBytes = Encoding.UTF8.GetBytes(trimmed);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return $"{Prefix}.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(tag)}.{Convert.ToBase64String(cipherBytes)}";
    }

    public string? Unprotect(string? protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        var parts = protectedPassword.Trim().Split('.');
        if (parts.Length != 5 || !string.Equals($"{parts[0]}.{parts[1]}", Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var nonce = Convert.FromBase64String(parts[2]);
            var tag = Convert.FromBase64String(parts[3]);
            var cipherBytes = Convert.FromBase64String(parts[4]);

            if (nonce.Length != NonceSizeBytes || tag.Length != TagSizeBytes)
            {
                return null;
            }

            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(_key, TagSizeBytes);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            _logger?.LogWarning(
                ex,
                "Không thể giải mã classes.join_password_protected (dữ liệu không hợp lệ hoặc khóa mã hóa đã xoay vòng).");
            return null;
        }
    }

    private static string ResolveMasterSecret(IConfiguration configuration)
    {
        var explicitKey = configuration["Security:ClassJoinPasswordKey"]
            ?? configuration["CLASS_JOIN_PASSWORD_KEY"];
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey.Trim();
        }

        var jwtSigningKey = AuthConfiguration.JwtSigningKey(configuration);
        if (!string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            return jwtSigningKey;
        }

        throw new InvalidOperationException(
            "Thiếu khóa bí mật (CLASS_JOIN_PASSWORD_KEY hoặc JWT_SIGNING_KEY) để mã hóa mật khẩu tham gia lớp.");
    }
}
