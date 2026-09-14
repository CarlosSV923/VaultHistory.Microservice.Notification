using Microsoft.Extensions.Options;
using SlimMessageBus;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed class NotifyOutboxConsumer(
    INotificationWorkflow workflow,
    IOptions<KafkaOptions> options) : IConsumer<NotifyOutboxMessage>
{
    public async Task OnHandle(NotifyOutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.OutboxId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ProcessingTimeoutSeconds));

        var result = await workflow.HandleOutboxAsync(message, timeout.Token);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error?.Description ?? "Outbox notification workflow failed.");
        }
    }
}
