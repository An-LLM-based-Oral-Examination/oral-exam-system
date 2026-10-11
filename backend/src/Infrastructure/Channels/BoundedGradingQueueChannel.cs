using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Infrastructure.Channels;

public class BoundedGradingQueueChannel : IGradingQueueChannel
{
    private const int MaxMessagesInChannel = 1000;
    private readonly Channel<GradingTask> _channel;
    private readonly ILogger<BoundedGradingQueueChannel> _logger;

    public BoundedGradingQueueChannel(ILogger<BoundedGradingQueueChannel> logger)
    {
        var options = new BoundedChannelOptions(MaxMessagesInChannel)
        {
            FullMode = BoundedChannelFullMode.Wait // Chờ nếu channel đã đầy 1000 slots
        };
        
        _channel = Channel.CreateBounded<GradingTask>(options);
        _logger = logger;
    }

    public async ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        
        await _channel.Writer.WriteAsync(task, cancellationToken);
        _logger.LogInformation("Đã đẩy GradingTask {AnswerId} vào hàng đợi. Hiện có {Count} tác vụ đang chờ.", task.AnswerId, _channel.Reader.Count);
    }

    public IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
