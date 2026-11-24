using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Auth;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;

namespace OralExamination.Application.Features.Admin.Queries.GetRuntimeDiagnostics;

public sealed record BoundedQueueStats(int Count, int Capacity);

public sealed record BoundedChannelsStats(BoundedQueueStats GradingQueue);

public sealed record DlqByFlowTypeStats(int Practice, int MockExam, int InClassExam, int OfficialExam = 0);

public sealed record DeadLetterQueueStats(int TotalPending, DlqByFlowTypeStats ByFlowType);

public sealed record AbuseMetricsDto(long Requests, long Rejected, long LoginLockouts);

public sealed record PerformanceStats(
    long TotalRequests,
    long ServerErrors5xx,
    double ServerErrorRatePercent,
    double AverageLatencyMs,
    double P95LatencyMs);

public sealed record SystemStats(
    double WorkingSetMb,
    double GcTotalMemoryMb,
    int ThreadCount);

public sealed record RuntimeDiagnosticsDto(
    DateTime Timestamp,
    string Uptime,
    BoundedChannelsStats BoundedChannels,
    DeadLetterQueueStats DeadLetterQueue,
    AbuseMetricsDto AbuseMetrics,
    PerformanceStats Performance,
    SystemStats System);

public sealed record GetRuntimeDiagnosticsQuery : IRequest<RuntimeDiagnosticsDto>;

public sealed class GetRuntimeDiagnosticsQueryHandler(
    IApplicationDbContext dbContext,
    IGradingQueueChannel gradingQueue,
    IAbuseMetricsReader abuseMetricsReader,
    ILoginAttemptGuard loginGuard,
    IRuntimeMetricsTracker metricsTracker)
    : IRequestHandler<GetRuntimeDiagnosticsQuery, RuntimeDiagnosticsDto>
{
    public async Task<RuntimeDiagnosticsDto> Handle(GetRuntimeDiagnosticsQuery request, CancellationToken cancellationToken)
    {
        var snapshot = metricsTracker.GetSnapshot();
        var abuse = abuseMetricsReader.Snapshot(loginGuard.Lockouts);

        var pendingFlowCounts = await dbContext.DeadLetterQueues
            .AsNoTracking()
            .Where(d => d.Status == "pending")
            .GroupBy(d =>
                d.TaskType == "PracticeGrading" ? "PRACTICE" :
                d.TaskType == "MockExamGrading" ? "MOCK_EXAM" :
                d.TaskType == "OfficialExamGrading" ? "OFFICIAL_EXAM" :
                d.TaskType == "InClassGrading" ? "IN_CLASS_EXAM" :
                d.FlowType)
            .Select(g => new { Flow = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var practice = pendingFlowCounts.Where(x => x.Flow == "PRACTICE").Sum(x => x.Count);
        var mockExam = pendingFlowCounts.Where(x => x.Flow == "MOCK_EXAM").Sum(x => x.Count);
        var inClassExam = pendingFlowCounts.Where(x => x.Flow == "IN_CLASS_EXAM").Sum(x => x.Count);
        var officialExam = pendingFlowCounts.Where(x => x.Flow == "OFFICIAL_EXAM").Sum(x => x.Count);
        var totalPending = practice + mockExam + inClassExam + officialExam;

        var boundedChannels = new BoundedChannelsStats(
            new BoundedQueueStats(gradingQueue.Count, gradingQueue.Capacity));

        var dlqStats = new DeadLetterQueueStats(
            totalPending,
            new DlqByFlowTypeStats(practice, mockExam, inClassExam, officialExam));

        var abuseStats = new AbuseMetricsDto(abuse.Requests, abuse.Rejected, abuse.LoginLockouts);

        var perfStats = new PerformanceStats(
            snapshot.TotalRequests,
            snapshot.ServerErrors5xx,
            snapshot.ServerErrorRatePercent,
            snapshot.AverageLatencyMs,
            snapshot.P95LatencyMs);

        var sysStats = new SystemStats(
            snapshot.WorkingSetMb,
            snapshot.GcTotalMemoryMb,
            snapshot.ThreadCount);

        var uptimeStr = snapshot.Uptime.ToString(@"d\.hh\:mm\:ss");

        return new RuntimeDiagnosticsDto(
            DateTime.UtcNow,
            uptimeStr,
            boundedChannels,
            dlqStats,
            abuseStats,
            perfStats,
            sysStats);
    }
}
