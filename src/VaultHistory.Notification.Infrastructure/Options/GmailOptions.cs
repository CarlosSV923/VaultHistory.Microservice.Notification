using System.ComponentModel.DataAnnotations;

namespace VaultHistory.Notification.Infrastructure.Options;

public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    [Required, EmailAddress] public string SenderAddress { get; init; } = string.Empty;
    [Required] public string SenderName { get; init; } = string.Empty;
    [Required] public string ClientId { get; init; } = string.Empty;
    [Required] public string ClientSecret { get; init; } = string.Empty;
    [Required] public string RefreshToken { get; init; } = string.Empty;
    [Range(1, 300)] public int TimeoutSeconds { get; init; } = 30;
}
