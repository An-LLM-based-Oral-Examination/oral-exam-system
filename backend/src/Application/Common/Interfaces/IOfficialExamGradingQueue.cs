using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Common.Interfaces;

public interface IOfficialExamGradingQueue
{
    ValueTask EnqueueAsync(OfficialExamGradingTask task, CancellationToken cancellationToken = default);
    IAsyncEnumerable<OfficialExamGradingTask> ReadAllAsync(CancellationToken cancellationToken = default);
}
