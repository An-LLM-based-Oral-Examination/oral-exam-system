using MediatR;
using OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;

namespace OralExamination.API.Workers;

/// <summary>
/// Đóng các ca kiểm tra đang chờ hoặc đang mở khi tới giờ đóng.
/// </summary>
public sealed class InClassShiftAutoCloseWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InClassShiftAutoCloseWorker> _logger;

    public InClassShiftAutoCloseWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<InClassShiftAutoCloseWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CloseDueAsync(stoppingToken);

        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CloseDueAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CloseDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var closed = await sender.Send(new CloseDueInClassShiftsCommand(), cancellationToken);
            if (closed > 0)
            {
                _logger.LogInformation("Đã tự đóng {Count} ca kiểm tra tới giờ.", closed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể tự đóng ca kiểm tra tới giờ.");
        }
    }
}
