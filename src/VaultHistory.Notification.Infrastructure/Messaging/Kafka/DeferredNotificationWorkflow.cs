using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed class DeferredNotificationWorkflow : INotificationWorkflow
{
    private static readonly Error Error = new("notification.workflow_not_ready", "Notification workflows are introduced in HU-07 and HU-08.");

    public Task<Result> HandleHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(Error));

    public Task<Result> HandleOutboxAsync(NotifyOutboxMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(Error));
}
