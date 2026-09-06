using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Domain.Notifications;

namespace VaultHistory.Notification.Application.Abstractions;

public interface INotificationResultPublisher
{
    Task<Result> PublishAsync(NotificationResult notificationResult, CancellationToken cancellationToken);
}
