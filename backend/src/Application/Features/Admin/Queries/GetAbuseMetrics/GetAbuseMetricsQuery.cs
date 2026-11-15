using MediatR;
using OralExamination.Application.Common.Auth;

namespace OralExamination.Application.Features.Admin.Queries.GetAbuseMetrics;

public sealed record AbuseMetricsSnapshot(long Requests, long Rejected, long LoginLockouts, string Scope);

public interface IAbuseMetricsReader
{
    AbuseMetricsSnapshot Snapshot(long lockouts);
}

public sealed record GetAbuseMetricsQuery : IRequest<AbuseMetricsSnapshot>;

public sealed class GetAbuseMetricsQueryHandler(IAbuseMetricsReader metrics, ILoginAttemptGuard loginGuard)
    : IRequestHandler<GetAbuseMetricsQuery, AbuseMetricsSnapshot>
{
    public Task<AbuseMetricsSnapshot> Handle(GetAbuseMetricsQuery request, CancellationToken cancellationToken)
        => Task.FromResult(metrics.Snapshot(loginGuard.Lockouts));
}
