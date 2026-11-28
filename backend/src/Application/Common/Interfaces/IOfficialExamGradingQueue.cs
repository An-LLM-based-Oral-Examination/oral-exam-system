using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Common.Interfaces;

public interface IOfficialExamGradingQueue
{
    int Count { get; }
    int Capacity { get; }
    ValueTask EnqueueAsync(OfficialExamGradingTask task, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OfficialExamGradingTask> ReadAllAsync(CancellationToken cancellationToken = default);
}
