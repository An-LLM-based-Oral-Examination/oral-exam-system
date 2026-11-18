using System;

namespace OralExamination.Application.Common.Interfaces;

public sealed record RuntimeMetricsSnapshot(
    long TotalRequests,
    long ServerErrors5xx,
    double ServerErrorRatePercent,
    double AverageLatencyMs,
    double P95LatencyMs,
    TimeSpan Uptime,
    double WorkingSetMb,
    double GcTotalMemoryMb,
    int ThreadCount);

public interface IRuntimeMetricsTracker
{
    long TotalRequests { get; }
    long ServerErrors5xx { get; }
    void RecordRequest(int statusCode, double elapsedMilliseconds);
    RuntimeMetricsSnapshot GetSnapshot();
    void Reset();
}
