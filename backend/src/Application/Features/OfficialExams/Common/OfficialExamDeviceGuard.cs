using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Common;

public static partial class OfficialExamDeviceGuard
{
    public const string DeviceIdHeaderName = "X-Device-Id";
    public const string SessionTokenHeaderName = "X-Kiosk-Session-Token";
    public const string KioskVersionHeaderName = "X-Kiosk-Version";

    [GeneratedRegex("^[A-Za-z0-9._:-]{16,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidDeviceIdRegex();

    public static bool TryNormalizeDeviceId(string? raw, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            normalized = string.Empty;
            return false;
        }

        var trimmed = raw.Trim();
        if (!ValidDeviceIdRegex().IsMatch(trimmed))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = trimmed;
        return true;
    }

    public static string GenerateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashSessionToken(string rawSessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSessionToken);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawSessionToken.Trim()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool VerifySessionToken(string? rawSessionToken, string? storedHashHex)
    {
        if (string.IsNullOrWhiteSpace(rawSessionToken) || string.IsNullOrWhiteSpace(storedHashHex))
        {
            return false;
        }

        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromHexString(storedHashHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        if (storedBytes.Length != SHA256.HashSizeInBytes)
        {
            return false;
        }

        var actualBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawSessionToken.Trim()));
        return CryptographicOperations.FixedTimeEquals(actualBytes, storedBytes);
    }

    /// <summary>
    /// Validates BYOD device binding and single active session hash (constant-time SHA-256 comparison).
    /// Returns null when valid, or an error string (BAD_REQUEST / CONFLICT) when rejected.
    /// </summary>
    public static string? ValidateBoundDeviceAndSession(
        StudentExamTicket ticket,
        string? rawDeviceId,
        string? rawSessionToken,
        bool requireSessionToken,
        bool requireBoundDevice = false)
    {
        var hasProvidedDevice = !string.IsNullOrWhiteSpace(rawDeviceId);
        var normalizedProvidedDevice = string.Empty;
        if (hasProvidedDevice && !TryNormalizeDeviceId(rawDeviceId, out normalizedProvidedDevice))
        {
            return OfficialExamErrors.DeviceIdInvalid;
        }

        if (string.IsNullOrWhiteSpace(ticket.DeviceId))
        {
            if (requireBoundDevice)
            {
                return OfficialExamErrors.DeviceMismatch;
            }

            return ticket.DeviceUnlockCount > 0 && requireSessionToken
                ? OfficialExamErrors.DeviceMismatch
                : null;
        }

        if (!hasProvidedDevice
            || !string.Equals(ticket.DeviceId, normalizedProvidedDevice, StringComparison.Ordinal))
        {
            return OfficialExamErrors.DeviceMismatch;
        }

        if (string.IsNullOrWhiteSpace(ticket.DeviceSessionTokenHash))
        {
            return requireBoundDevice && requireSessionToken
                ? OfficialExamErrors.ActiveSessionConflict
                : null;
        }

        var hasProvidedToken = !string.IsNullOrWhiteSpace(rawSessionToken);
        if (!hasProvidedToken)
        {
            return requireSessionToken ? OfficialExamErrors.ActiveSessionConflict : null;
        }

        return VerifySessionToken(rawSessionToken, ticket.DeviceSessionTokenHash)
            ? null
            : OfficialExamErrors.ActiveSessionConflict;
    }
}
