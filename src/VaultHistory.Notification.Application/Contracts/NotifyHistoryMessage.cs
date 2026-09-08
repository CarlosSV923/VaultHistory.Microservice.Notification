namespace VaultHistory.Notification.Application.Contracts;

public sealed record NotifyHistoryMessage(
    string UserId,
    string Email,
    string Fullname,
    DateTimeOffset? BirthDate,
    string? Theme,
    string? Character,
    string? NotificationId = null);
