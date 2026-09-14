using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Application.Contracts;

namespace VaultHistory.Notification.Application.Abstractions;

public interface IHistoryClient
{
    Task<Result<string>> GenerateSubscriptionAsync(GenerateSubscriptionHistoryRequest request, CancellationToken cancellationToken);
}
