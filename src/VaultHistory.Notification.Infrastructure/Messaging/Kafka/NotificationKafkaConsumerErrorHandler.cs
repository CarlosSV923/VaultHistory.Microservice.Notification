using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Kafka;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed class NotificationKafkaConsumerErrorHandler<T>(
    IOptions<KafkaOptions> options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<NotificationKafkaConsumerErrorHandler<T>> logger) : KafkaConsumerErrorHandler<T>
{
    public override async Task<ProcessResult> OnHandleError(T message, IConsumerContext consumerContext, Exception exception, int attempts)
    {
        var settings = options.Value;

        if (attempts < settings.ConsumerMaxAttempts)
        {
            logger.LogWarning(exception, "Kafka consumer attempt {Attempt} of {MaxAttempts} failed for {MessageType}; retrying without committing its offset.", attempts, settings.ConsumerMaxAttempts, typeof(T).Name);
            await Task.Delay(settings.RetryDelayMilliseconds, consumerContext.CancellationToken);
            return Retry();
        }

        logger.LogCritical(exception, "Kafka consumer exhausted retries for {MessageType}. Stopping the worker before the pending result can be silently acknowledged.", typeof(T).Name);
        applicationLifetime.StopApplication();
        return Failure();
    }
}
