using System.ComponentModel.DataAnnotations;

namespace VaultHistory.Notification.Infrastructure.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required] public string BootstrapServers { get; init; } = string.Empty;
    [Required] public string ClientId { get; init; } = string.Empty;
    [Required] public string GroupId { get; init; } = string.Empty;
    [Required] public KafkaTopicsOptions Topics { get; init; } = new();
    [Range(1, 300)] public int ProcessingTimeoutSeconds { get; init; } = 60;
    [Range(1, 10)] public int PublishMaxAttempts { get; init; } = 3;
    [Range(1, 10)] public int ConsumerMaxAttempts { get; init; } = 3;
    [Range(1, 60000)] public int RetryDelayMilliseconds { get; init; } = 1000;
}

public sealed class KafkaTopicsOptions
{
    [Required] public string NotifyHistory { get; init; } = string.Empty;
    [Required] public string NotifyOutbox { get; init; } = string.Empty;
    [Required] public string UpdateUsers { get; init; } = string.Empty;
    [Required] public string UpdateOutbox { get; init; } = string.Empty;
}
