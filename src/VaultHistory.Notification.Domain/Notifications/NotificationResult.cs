namespace VaultHistory.Notification.Domain.Notifications;

public sealed record UserNotificationResult(string UserId, string NotificationStatus, DateTimeOffset? NotificationDate);

public sealed record OutboxNotificationResult(string OutboxId, string Status, string? Error);
