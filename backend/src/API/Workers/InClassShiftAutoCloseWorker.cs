using MediatR;
using OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.ExpirePersonalInClassExams;

namespace OralExamination.API.Workers;

/// <summary>
/// Đóng ca tới giờ ScheduledEnd; tự nộp bài khi hết thời gian làm bài cá nhân.
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
        await TickAsync(stoppingToken);

        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
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

            var expired = await sender.Send(new ExpirePersonalInClassExamsCommand(), cancellationToken);
            if (expired > 0)
            {
                _logger.LogInformation(
                    "Đã tự nộp {Count} bài khi hết thời gian làm bài cá nhân.",
                    expired);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể tự đóng ca / hết giờ làm bài cá nhân.");
        }
    }
}
