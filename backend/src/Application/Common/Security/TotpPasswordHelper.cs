using System;
using System.Security.Cryptography;
using System.Text;

namespace OralExamination.Application.Common.Security;

/// <summary>
/// Bộ sinh và kiểm thực mã mật khẩu thời gian dùng một lần (TOTP - Time-based One-Time Password)
/// tuân thủ đặc tả RFC 6238 / RFC 4226, sử dụng thuật toán HMAC-SHA256 thuần .NET 8 BCL.
/// Thiết kế phi trạng thái (Stateless & Deterministic), không sử dụng RAM Cache hay biến tĩnh.
/// </summary>
public static class TotpPasswordHelper
{
    public const int DefaultStepSeconds = 300; // 5 phút
    public const int DefaultGracePeriodSeconds = 60; // 60 giây gia hạn
    public const int DefaultDigits = 6; // 6 chữ số

    /// <summary>
    /// Sinh chuỗi khóa bí mật ngẫu nhiên bảo mật cao (CSPRNG) dạng Hex string.
    /// </summary>
    public static string GenerateSecretKey(int byteCount = 32)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Tính toán mã OTP tại một mốc thời gian Unix Epoch Seconds cụ thể.
    /// </summary>
    public static string ComputeTotp(
        string secretKey,
        long unixTimeSeconds,
        int stepSeconds = DefaultStepSeconds,
        int digits = DefaultDigits)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentException("Secret key không được để trống.", nameof(secretKey));
        }

        if (stepSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Chu kỳ timestep phải lớn hơn 0.");
        }

        if (digits is < 4 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "Số lượng chữ số phải từ 4 đến 10.");
        }

        long counter = unixTimeSeconds / stepSeconds;

        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        using var hmac = new HMACSHA256(keyBytes);
        byte[] hash = hmac.ComputeHash(counterBytes);

        // Dynamic Truncation (RFC 4226 Section 5.4 / RFC 6238)
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                   | ((hash[offset + 1] & 0xFF) << 16)
                   | ((hash[offset + 2] & 0xFF) << 8)
                   | (hash[offset + 3] & 0xFF);

        int mod = (int)Math.Pow(10, digits);
        int otp = binary % mod;

        return otp.ToString().PadLeft(digits, '0');
    }

    /// <summary>
    /// Sinh mã OTP hiện tại cho ca thi theo UTC Now.
    /// </summary>
    public static string GenerateCode(
        string secretKey,
        DateTime? utcNow = null,
        int stepSeconds = DefaultStepSeconds,
        int digits = DefaultDigits)
    {
        var now = utcNow ?? DateTime.UtcNow;
        long unixTimeSeconds = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds();
        return ComputeTotp(secretKey, unixTimeSeconds, stepSeconds, digits);
    }

    /// <summary>
    /// Lấy thông tin chi tiết về mã mật khẩu hiện tại kèm số giây còn lại và thời điểm hết hạn.
    /// </summary>
    public static (string Password, int RemainingSeconds, DateTime ValidFrom, DateTime ValidTo) GetCurrentPasswordDetails(
        string secretKey,
        DateTime? utcNow = null,
        int stepSeconds = DefaultStepSeconds,
        int digits = DefaultDigits)
    {
        var now = utcNow ?? DateTime.UtcNow;
        long unixTimeSeconds = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds();
        long currentStep = unixTimeSeconds / stepSeconds;
        long stepStartSeconds = currentStep * stepSeconds;
        long stepEndSeconds = stepStartSeconds + stepSeconds;

        string password = ComputeTotp(secretKey, unixTimeSeconds, stepSeconds, digits);
        int remainingSeconds = (int)(stepEndSeconds - unixTimeSeconds);

        var validFrom = DateTimeOffset.FromUnixTimeSeconds(stepStartSeconds).UtcDateTime;
        var validTo = DateTimeOffset.FromUnixTimeSeconds(stepEndSeconds).UtcDateTime;

        return (password, remainingSeconds, validFrom, validTo);
    }

    /// <summary>
    /// Tính số giây còn lại trong chu kỳ hiện tại.
    /// </summary>
    public static int GetRemainingSeconds(DateTime? utcNow = null, int stepSeconds = DefaultStepSeconds)
    {
        var now = utcNow ?? DateTime.UtcNow;
        long unixTimeSeconds = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds();
        long currentStep = unixTimeSeconds / stepSeconds;
        long stepEndSeconds = (currentStep + 1) * stepSeconds;
        return (int)(stepEndSeconds - unixTimeSeconds);
    }

    /// <summary>
    /// Xác thực mã OTP người dùng nhập vào.
    /// Hỗ trợ kiểm tra mã của chu kỳ hiện tại VÀ mã của chu kỳ liền trước (nếu còn trong grace period).
    /// </summary>
    public static bool ValidateCode(
        string secretKey,
        string? enteredCode,
        DateTime? utcNow = null,
        int stepSeconds = DefaultStepSeconds,
        int gracePeriodSeconds = DefaultGracePeriodSeconds,
        int digits = DefaultDigits)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(enteredCode))
        {
            return false;
        }

        string normalizedInput = enteredCode.Trim();
        if (normalizedInput.Length != digits)
        {
            return false;
        }

        var now = utcNow ?? DateTime.UtcNow;
        long unixTimeSeconds = new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds();
        long currentStep = unixTimeSeconds / stepSeconds;

        // 1. Kiểm tra khớp mã chu kỳ hiện tại
        string currentCode = ComputeTotp(secretKey, unixTimeSeconds, stepSeconds, digits);
        if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(normalizedInput),
                Encoding.UTF8.GetBytes(currentCode)))
        {
            return true;
        }

        // 2. Kiểm tra Grace Period cho mã chu kỳ liền trước
        if (gracePeriodSeconds > 0)
        {
            long stepStartSeconds = currentStep * stepSeconds;
            long elapsedInCurrentStep = unixTimeSeconds - stepStartSeconds;

            if (elapsedInCurrentStep <= gracePeriodSeconds && currentStep > 0)
            {
                long prevUnixTimeSeconds = (currentStep - 1) * stepSeconds;
                string prevCode = ComputeTotp(secretKey, prevUnixTimeSeconds, stepSeconds, digits);

                if (CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(normalizedInput),
                        Encoding.UTF8.GetBytes(prevCode)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Bí danh của ValidateCode để tương thích thuận tiện.
    /// </summary>
    public static bool ValidateOtp(
        string secretKey,
        string? enteredOtp,
        DateTime? utcNow = null,
        int stepSeconds = DefaultStepSeconds,
        int gracePeriodSeconds = DefaultGracePeriodSeconds,
        int digits = DefaultDigits) =>
        ValidateCode(secretKey, enteredOtp, utcNow, stepSeconds, gracePeriodSeconds, digits);
}
