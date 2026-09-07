using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Application.Abstractions;

public interface INotificationWorkflow
{
    Task<Result> HandleHistoryAsync(NotifyHistoryMessage message, CancellationToken cancellationToken);

    Task<Result> HandleOutboxAsync(NotifyOutboxMessage message, CancellationToken cancellationToken);
}
