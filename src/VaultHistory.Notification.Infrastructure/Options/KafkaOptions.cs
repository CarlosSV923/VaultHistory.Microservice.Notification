using System.ComponentModel.DataAnnotations;

namespace VaultHistory.Notification.Infrastructure.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required] public string BootstrapServers { get; init; } = string.Empty;
    [Required] public string ClientId { get; init; } = string.Empty;
    [Required] public string GroupId { get; init; } = string.Empty;
    [Required] public KafkaTopicsOptions Topics { get; init; } = new();
}

public sealed class KafkaTopicsOptions
{
    [Required] public string NotifyHistory { get; init; } = string.Empty;
    [Required] public string NotifyOutbox { get; init; } = string.Empty;
    [Required] public string UpdateUsers { get; init; } = string.Empty;
    [Required] public string UpdateOutbox { get; init; } = string.Empty;
}
