using System;
using System.Threading.Tasks;
using FluentAssertions;
using OralExamination.Infrastructure.Diagnostics;
using Xunit;

namespace OralExamination.UnitTests.Diagnostics;

public class RuntimeMetricsTrackerTests
{
    [Fact]
    public void RecordRequest_IncrementsTotalRequests()
    {
        var tracker = new RuntimeMetricsTracker();

        tracker.RecordRequest(200, 10.5);
        tracker.RecordRequest(204, 15.2);
        tracker.RecordRequest(404, 8.0);

        tracker.TotalRequests.Should().Be(3);
        tracker.ServerErrors5xx.Should().Be(0);

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(3);
        snapshot.ServerErrors5xx.Should().Be(0);
        snapshot.ServerErrorRatePercent.Should().Be(0.0);
    }

    [Fact]
    public void RecordRequest_Tracks5xxErrorsAndPercentage()
    {
        var tracker = new RuntimeMetricsTracker();

        // 8 success, 2 server errors
        for (int i = 0; i < 8; i++) tracker.RecordRequest(200, 10.0);
        tracker.RecordRequest(500, 20.0);
        tracker.RecordRequest(503, 30.0);

        tracker.TotalRequests.Should().Be(10);
        tracker.ServerErrors5xx.Should().Be(2);

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(10);
        snapshot.ServerErrors5xx.Should().Be(2);
        snapshot.ServerErrorRatePercent.Should().Be(20.0);
    }

    [Fact]
    public void RecordRequest_CalculatesAverageAndP95Latency()
    {
        var tracker = new RuntimeMetricsTracker();

        // 100 requests with latencies 1.0, 2.0, ..., 100.0
        for (int i = 1; i <= 100; i++)
        {
            tracker.RecordRequest(200, i);
        }

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(100);
        snapshot.AverageLatencyMs.Should().Be(50.5);
        snapshot.P95LatencyMs.Should().Be(95.0);
    }

    [Fact]
    public void GetSnapshot_WhenNoRequests_ReturnsZeros()
    {
        var tracker = new RuntimeMetricsTracker();

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(0);
        snapshot.ServerErrors5xx.Should().Be(0);
        snapshot.ServerErrorRatePercent.Should().Be(0.0);
        snapshot.AverageLatencyMs.Should().Be(0.0);
        snapshot.P95LatencyMs.Should().Be(0.0);
    }

    [Fact]
    public void GetSnapshot_ReturnsProcessAndSystemMetrics()
    {
        var tracker = new RuntimeMetricsTracker();
        tracker.RecordRequest(200, 5.0);

        var snapshot = tracker.GetSnapshot();
        snapshot.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
        snapshot.WorkingSetMb.Should().BeGreaterThan(0);
        snapshot.GcTotalMemoryMb.Should().BeGreaterThan(0);
        snapshot.ThreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Reset_ClearsAllCountersAndReservoir()
    {
        var tracker = new RuntimeMetricsTracker();
        for (int i = 0; i < 10; i++) tracker.RecordRequest(500, 25.0);

        tracker.TotalRequests.Should().Be(10);
        tracker.Reset();

        tracker.TotalRequests.Should().Be(0);
        tracker.ServerErrors5xx.Should().Be(0);

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(0);
        snapshot.ServerErrors5xx.Should().Be(0);
        snapshot.AverageLatencyMs.Should().Be(0.0);
        snapshot.P95LatencyMs.Should().Be(0.0);
    }

    [Fact]
    public void RecordRequest_IsThreadSafe_UnderConcurrentLoad()
    {
        var tracker = new RuntimeMetricsTracker();
        const int concurrentRequests = 1000;

        Parallel.For(0, concurrentRequests, i =>
        {
            var statusCode = (i % 10 == 0) ? 500 : 200;
            tracker.RecordRequest(statusCode, (i % 50) + 1.0);
        });

        tracker.TotalRequests.Should().Be(concurrentRequests);
        tracker.ServerErrors5xx.Should().Be(concurrentRequests / 10);

        var snapshot = tracker.GetSnapshot();
        snapshot.TotalRequests.Should().Be(concurrentRequests);
        snapshot.ServerErrors5xx.Should().Be(concurrentRequests / 10);
        snapshot.ServerErrorRatePercent.Should().Be(10.0);
        snapshot.AverageLatencyMs.Should().BeGreaterThan(0);
        snapshot.P95LatencyMs.Should().BeGreaterThan(0);
    }
}
