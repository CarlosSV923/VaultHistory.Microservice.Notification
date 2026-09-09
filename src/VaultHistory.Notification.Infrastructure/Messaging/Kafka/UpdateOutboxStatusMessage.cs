namespace VaultHistory.Notification.Infrastructure.Messaging.Kafka;

public sealed record UpdateOutboxStatusMessage(string Id, UpdateOutboxStatusData Data);

public sealed record UpdateOutboxStatusData(string Status, string? Error, string? FailureStage = null);
