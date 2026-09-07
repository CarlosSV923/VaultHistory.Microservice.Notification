using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlimMessageBus;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Domain.Notifications;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed class KafkaNotificationResultPublisher(
    IPublishBus publishBus,
    IOptions<KafkaOptions> options,
    ILogger<KafkaNotificationResultPublisher> logger) : INotificationResultPublisher
{
    public Task<Result> PublishUserResultAsync(UserNotificationResult notificationResult, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationResult.UserId);

        return PublishAsync(
            new UpdateUserNotificationMessage(
                notificationResult.UserId,
                new UpdateUserNotificationData(notificationResult.NotificationStatus, notificationResult.NotificationDate)),
            cancellationToken);
    }

    public Task<Result> PublishOutboxResultAsync(OutboxNotificationResult notificationResult, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationResult.OutboxId);

        return PublishAsync(
            new UpdateOutboxStatusMessage(
                notificationResult.OutboxId,
                new UpdateOutboxStatusData(notificationResult.Status, notificationResult.Error)),
            cancellationToken);
    }

    private async Task<Result> PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= settings.PublishMaxAttempts; attempt++)
        {
            try
            {
                await publishBus.Publish(message, cancellationToken: cancellationToken);
                return Result.Success();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
                logger.LogWarning(exception, "Kafka result publication attempt {Attempt} of {MaxAttempts} failed for {MessageType}.", attempt, settings.PublishMaxAttempts, typeof(TMessage).Name);

                if (attempt < settings.PublishMaxAttempts)
                {
                    await Task.Delay(settings.RetryDelayMilliseconds, cancellationToken);
                }
            }
        }

        return Result.Failure(new Error("kafka.publish_failed", lastError?.Message ?? "Kafka result publication failed."));
    }
}
