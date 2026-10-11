using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Common.Interfaces;

public interface IGradingQueueChannel
{
    ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default);
    IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default);
}
