using System.Net;
using System.Net.Sockets;

namespace OralExamination.Application.Features.OfficialExams;

public static class WorkstationAddress
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        normalized = address.ToString();
        return true;
    }

    public static bool Same(string? left, string? right)
    {
        return TryNormalize(left, out var normalizedLeft)
            && TryNormalize(right, out var normalizedRight)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}
