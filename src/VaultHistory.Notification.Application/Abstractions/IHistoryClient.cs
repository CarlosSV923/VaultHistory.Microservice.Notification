using VaultHistory.Notification.Domain.Abstractions;

namespace VaultHistory.Notification.Application.Abstractions;

public interface IHistoryClient
{
    Task<Result> GenerateSubscriptionAsync(string userId, CancellationToken cancellationToken);
}
