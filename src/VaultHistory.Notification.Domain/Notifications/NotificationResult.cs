namespace VaultHistory.Notification.Domain.Notifications;

public sealed record NotificationResult(string NotificationId, NotificationStatus Status, string? Error = null);

public enum NotificationStatus
{
    Succeeded,
    Failed
}
