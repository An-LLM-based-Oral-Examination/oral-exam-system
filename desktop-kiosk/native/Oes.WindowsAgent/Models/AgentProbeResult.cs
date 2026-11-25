namespace Oes.WindowsAgent.Models;

public sealed record AgentProbeResult(
    string DeviceFingerprint,
    int DisplayCount,
    bool VmDetected,
    IReadOnlyList<string> VmIndicators,
    bool RemoteControlDetected,
    IReadOnlyList<string> RemoteControlProcesses,
    IReadOnlyList<string> DisallowedProcesses,
    DateTimeOffset ProbedAtUtc
);
