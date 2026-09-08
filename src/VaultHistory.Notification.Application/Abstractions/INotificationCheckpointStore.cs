namespace VaultHistory.Notification.Application.Abstractions;

public interface INotificationCheckpointStore
{
    Task<NotificationCheckpoint?> GetAsync(string notificationId, CancellationToken cancellationToken);
    Task SaveAsync(NotificationCheckpoint checkpoint, CancellationToken cancellationToken);
}

public sealed record NotificationCheckpoint(string NotificationId, string UserId, string? Story, DateTimeOffset? EmailSentAt, DateTimeOffset? ResultPublishedAt);
