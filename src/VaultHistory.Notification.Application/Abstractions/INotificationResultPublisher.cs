using VaultHistory.Notification.Domain.Abstractions;
using VaultHistory.Notification.Domain.Notifications;

namespace VaultHistory.Notification.Application.Abstractions;

public interface INotificationResultPublisher
{
    Task<Result> PublishUserResultAsync(UserNotificationResult notificationResult, CancellationToken cancellationToken);

    Task<Result> PublishOutboxResultAsync(OutboxNotificationResult notificationResult, CancellationToken cancellationToken);
}
