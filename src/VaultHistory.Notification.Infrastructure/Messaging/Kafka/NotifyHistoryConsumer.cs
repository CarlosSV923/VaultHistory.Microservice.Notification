using Microsoft.Extensions.Options;
using SlimMessageBus;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed class NotifyHistoryConsumer(
    INotificationWorkflow workflow,
    IOptions<KafkaOptions> options) : IConsumer<NotifyHistoryMessage>
{
    public async Task OnHandle(NotifyHistoryMessage message, CancellationToken cancellationToken)
    {
        Validate(message.UserId, message.Email);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ProcessingTimeoutSeconds));

        var result = await workflow.HandleHistoryAsync(message, timeout.Token);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error?.Description ?? "History notification workflow failed.");
        }
    }

    private static void Validate(string userId, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
    }
}
