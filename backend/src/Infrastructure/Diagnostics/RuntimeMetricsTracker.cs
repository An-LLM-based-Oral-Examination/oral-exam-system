using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Diagnostics;

public sealed class RuntimeMetricsTracker : IRuntimeMetricsTracker
{
    private const int ReservoirSize = 2048;
    private readonly double[] _reservoir = new double[ReservoirSize];
    private readonly DateTime _startTime = DateTime.UtcNow;

    private long _totalRequests;
    private long _serverErrors5xx;
    private long _reservoirIndex;

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long ServerErrors5xx => Interlocked.Read(ref _serverErrors5xx);

    public void RecordRequest(int statusCode, double elapsedMilliseconds)
    {
        Interlocked.Increment(ref _totalRequests);

        if (statusCode is >= 500 and <= 599)
        {
            Interlocked.Increment(ref _serverErrors5xx);
        }

        var index = Interlocked.Increment(ref _reservoirIndex) - 1;
        var slot = (int)(Math.Abs(index) % ReservoirSize);
        _reservoir[slot] = elapsedMilliseconds;
    }

    public RuntimeMetricsSnapshot GetSnapshot()
    {
        var total = Interlocked.Read(ref _totalRequests);
        var errors = Interlocked.Read(ref _serverErrors5xx);

        var errorRate = total > 0
            ? Math.Round(((double)errors / total) * 100.0, 2)
            : 0.0;

        var sampleCount = Math.Min((int)Math.Min(total, int.MaxValue), ReservoirSize);
        double averageLatencyMs = 0.0;
        double p95LatencyMs = 0.0;

        if (sampleCount > 0)
        {
            var samples = new double[sampleCount];
            Array.Copy(_reservoir, samples, sampleCount);

            var sum = 0.0;
            for (var i = 0; i < sampleCount; i++)
            {
                sum += samples[i];
            }

            averageLatencyMs = Math.Round(sum / sampleCount, 2);

            Array.Sort(samples);
            var p95Index = (int)Math.Ceiling(sampleCount * 0.95) - 1;
            if (p95Index < 0) p95Index = 0;
            if (p95Index >= sampleCount) p95Index = sampleCount - 1;

            p95LatencyMs = Math.Round(samples[p95Index], 2);
        }

        TimeSpan uptime;
        double workingSetMb = 0.0;
        int threadCount = 0;

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            try
            {
                uptime = DateTime.UtcNow - currentProcess.StartTime.ToUniversalTime();
            }
            catch
            {
                uptime = DateTime.UtcNow - _startTime;
            }

            workingSetMb = Math.Round((double)currentProcess.WorkingSet64 / (1024 * 1024), 2);
            threadCount = currentProcess.Threads.Count;
        }
        catch
        {
            uptime = DateTime.UtcNow - _startTime;
        }

        var gcTotalMemoryMb = Math.Round((double)GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024), 2);

        return new RuntimeMetricsSnapshot(
            TotalRequests: total,
            ServerErrors5xx: errors,
            ServerErrorRatePercent: errorRate,
            AverageLatencyMs: averageLatencyMs,
            P95LatencyMs: p95LatencyMs,
            Uptime: uptime,
            WorkingSetMb: workingSetMb,
            GcTotalMemoryMb: gcTotalMemoryMb,
            ThreadCount: threadCount);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _serverErrors5xx, 0);
        Interlocked.Exchange(ref _reservoirIndex, 0);
        Array.Clear(_reservoir, 0, _reservoir.Length);
    }
}
