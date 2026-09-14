namespace VaultHistory.Notification.Application.Contracts;

public sealed record GenerateSubscriptionHistoryRequest(
    string UserId,
    DateTimeOffset? BirthDate,
    string? Theme,
    string? Character,
    string? IdempotencyKey = null);
