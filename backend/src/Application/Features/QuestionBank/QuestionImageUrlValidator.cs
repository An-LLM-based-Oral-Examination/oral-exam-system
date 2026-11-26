using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace OralExamination.Application.Features.QuestionBank;

public static class QuestionImageUrlValidator
{
    public const string InvalidImageUrlError =
        "Đường dẫn hình ảnh câu hỏi không hợp lệ. Chỉ chấp nhận đường dẫn /uploads/questions/... hoặc HTTPS công khai an toàn.";

    private static readonly Regex SafeLocalQuestionImagePathRegex = new(
        @"^/uploads/questions/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.(png|jpg|jpeg|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500 || url.Any(char.IsControl))
        {
            return false;
        }

        var trimmed = url.Trim();

        if (trimmed.StartsWith("/uploads/questions/", StringComparison.OrdinalIgnoreCase))
        {
            return SafeLocalQuestionImagePathRegex.IsMatch(trimmed);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || HasAtSignInAuthority(trimmed)
            || uri.IsLoopback)
        {
            return false;
        }

        var host = uri.DnsSafeHost.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localdomain", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            return !IsRestrictedIpAddress(ip);
        }

        return true;
    }

    private static bool HasAtSignInAuthority(string url)
    {
        var schemeDelimiterIndex = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeDelimiterIndex < 0)
        {
            return true;
        }

        var authorityStart = schemeDelimiterIndex + 3;
        var authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        var authority = authorityEnd >= 0
            ? url[authorityStart..authorityEnd]
            : url[authorityStart..];

        return authority.Contains('@', StringComparison.Ordinal);
    }

    private static bool IsRestrictedIpAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] == 0
                || bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
        {
            // Link-local (fe80::/10), Unique Local (fc00::/7), Site-local (fec0::/10), Multicast (ff00::/8)
            return ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || ip.IsIPv6Multicast
                || (bytes[0] & 0xFE) == 0xFC;
        }

        return true;
    }
}
