using System.ComponentModel.DataAnnotations;

namespace VaultHistory.Notification.Infrastructure.Options;

public sealed class HistoryOptions
{
    public const string SectionName = "History";

    [Required, Url] public string BaseUrl { get; init; } = string.Empty;
    [Required] public string SubscriptionPath { get; init; } = string.Empty;
    [Required] public string AuthorizationToken { get; init; } = string.Empty;
    [Range(1, 300)] public int TimeoutSeconds { get; init; } = 30;
}
