namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed record UpdateUserNotificationMessage(string Id, UpdateUserNotificationData Data);

public sealed record UpdateUserNotificationData(string NotificationStatus, DateTimeOffset? NotificationDate);
