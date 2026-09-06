namespace VaultHistory.Notification.Worker;

public sealed class NotificationWorker(ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification worker started. Kafka consumers will be registered in HU-03.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Notification worker stopped.");
        }
    }
}
