using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Oes.WindowsAgent.Models;

namespace Oes.WindowsAgent.Services;

public sealed class EnvironmentProbeService
{
    private const int SmCMonitors = 80;

    private static readonly HashSet<string> RemoteControlProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "teamviewer",
        "teamviewer_service",
        "anydesk",
        "ultraviewer",
        "ultraviewer_desktop",
        "ultraviewer_service",
        "rustdesk",
        "parsec",
        "parsecd",
        "ammyy",
        "vncviewer",
        "tvnserver",
        "radmin",
        "screenconnect.clientservice",
        "remoting_host",
        "mstsc"
    };

    private static readonly HashSet<string> VmProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "vboxservice",
        "vboxtray",
        "vmtoolsd",
        "vmwaretray",
        "vmwareuser",
        "vmacthlp",
        "qemu-ga",
        "xenservice",
        "prl_tools"
    };

    private static readonly HashSet<string> DisallowedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "obs64",
        "obs32",
        "obs",
        "bandicam",
        "camtasia",
        "sharex",
        "snagit32",
        "snagiteditor",
        "discord",
        "zalo",
        "telegram",
        "chatgpt",
        "copilot"
    };

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public AgentProbeResult Probe()
    {
        var vmIndicators = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var remoteProcesses = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var disallowedProcesses = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (VmProcessNames.Contains(name))
                {
                    vmIndicators.Add(name);
                }

                if (RemoteControlProcessNames.Contains(name))
                {
                    remoteProcesses.Add(name);
                }

                if (DisallowedProcessNames.Contains(name))
                {
                    disallowedProcesses.Add(name);
                }
            }
            catch
            {
                // Protected system processes may deny access under standard user privileges; skip safely.
            }
            finally
            {
                process.Dispose();
            }
        }

        return new AgentProbeResult(
            DeviceFingerprint: BuildDeviceFingerprint(),
            DisplayCount: DetectDisplayCount(),
            VmDetected: vmIndicators.Count > 0,
            VmIndicators: vmIndicators.ToArray(),
            RemoteControlDetected: remoteProcesses.Count > 0,
            RemoteControlProcesses: remoteProcesses.ToArray(),
            DisallowedProcesses: disallowedProcesses.ToArray(),
            ProbedAtUtc: DateTimeOffset.UtcNow
        );
    }

    private static int DetectDisplayCount()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        try
        {
            var count = GetSystemMetrics(SmCMonitors);
            return count > 0 ? count : 1;
        }
        catch
        {
            return 1;
        }
    }

    private static string BuildDeviceFingerprint()
    {
        var raw = string.Join("|",
            Environment.MachineName,
            Environment.UserDomainName,
            Environment.ProcessorCount.ToString(),
            RuntimeInformation.OSArchitecture.ToString(),
            OperatingSystem.IsWindows() ? "win" : "other");

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var shortHex = Convert.ToHexString(hashBytes)[..32].ToLowerInvariant();
        return $"oes-win-{shortHex}";
    }
}
