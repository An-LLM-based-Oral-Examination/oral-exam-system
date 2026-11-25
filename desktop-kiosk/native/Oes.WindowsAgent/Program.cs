using System.Text.Json;
using Oes.WindowsAgent.Services;

var serializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

var probeService = new EnvironmentProbeService();
var isWatchMode = args.Any(arg => string.Equals(arg, "--watch", StringComparison.OrdinalIgnoreCase));

if (!isWatchMode)
{
    var snapshot = probeService.Probe();
    Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));
    return 0;
}

var intervalMs = 15_000;
var watchIndex = Array.FindIndex(args, arg => string.Equals(arg, "--watch", StringComparison.OrdinalIgnoreCase));
if (watchIndex >= 0 && watchIndex + 1 < args.Length && int.TryParse(args[watchIndex + 1], out var parsedInterval))
{
    intervalMs = Math.Clamp(parsedInterval, 2_000, 120_000);
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

while (!cts.IsCancellationRequested)
{
    var snapshot = probeService.Probe();
    Console.WriteLine(JsonSerializer.Serialize(snapshot, serializerOptions));
    await Console.Out.FlushAsync(cts.Token);

    try
    {
        await Task.Delay(intervalMs, cts.Token);
    }
    catch (TaskCanceledException)
    {
        break;
    }
}

return 0;
