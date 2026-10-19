using System.Threading.Channels;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Infrastructure.Channels;

public sealed class BoundedOfficialExamGradingQueue : IOfficialExamGradingQueue
{
    public const int Capacity = 1000;
    private readonly Channel<OfficialExamGradingTask> _channel = Channel.CreateBounded<OfficialExamGradingTask>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(OfficialExamGradingTask task, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(task, cancellationToken);

    public IAsyncEnumerable<OfficialExamGradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
