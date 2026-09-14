namespace VaultHistory.Notification.Domain.Notifications;

public sealed record UserNotificationResult(
    string UserId,
    string NotificationStatus,
    DateTimeOffset? NotificationDate,
    string? FailureStage = null,
    string? FailureReason = null);

public sealed record OutboxNotificationResult(
    string OutboxId,
    string Status,
    string? Error,
    string? FailureStage = null);
