namespace VaultHistory.Notification.Application.Contracts;

public sealed record NotifyOutboxMessage(
    string OutboxId,
    string UserId,
    string Email,
    string Fullname,
    DateTimeOffset? BirthDate,
    string Type,
    DateTimeOffset? OccurredOn);
