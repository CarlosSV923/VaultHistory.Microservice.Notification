using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SlimMessageBus;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Domain.Notifications;
using VaultHistory.Notification.Infrastructure.Messaging.Kafka;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.IntegrationTests;

public sealed class KafkaContractTests
{
    [Fact]
    public void KafkaJs_history_payload_deserializes_without_message_type_headers()
    {
        const string payload = """
            {
              "userId": "user-123",
              "email": "ana@example.test",
              "fullname": "Ana Pérez",
              "birthDate": "1999-12-31T00:00:00.000Z",
              "theme": "adventure",
              "character": "mage"
            }
            """;

        var message = JsonSerializer.Deserialize<NotifyHistoryMessage>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(message);
        Assert.Equal("user-123", message.UserId);
        Assert.Equal("Ana Pérez", message.Fullname);
        Assert.Equal("adventure", message.Theme);
    }

    [Fact]
    public async Task User_result_is_published_with_scalar_id_and_camel_case_data()
    {
        var publishBus = new RecordingPublishBus();
        var publisher = CreatePublisher(publishBus);

        var result = await publisher.PublishUserResultAsync(
            new UserNotificationResult("user-123", "NOTIFIED", new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var message = Assert.IsType<UpdateUserNotificationMessage>(publishBus.Message);
        Assert.Equal("user-123", message.Id);
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"id\":\"user-123\"", json);
        Assert.Contains("\"notificationStatus\":\"NOTIFIED\"", json);
        Assert.DoesNotContain("ids", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Outbox_result_retries_and_returns_failure_when_kafka_cannot_confirm_delivery()
    {
        var publishBus = new RecordingPublishBus(new InvalidOperationException("broker unavailable"));
        var publisher = CreatePublisher(publishBus);

        var result = await publisher.PublishOutboxResultAsync(
            new OutboxNotificationResult("outbox-123", "ERROR", "GMAIL_SEND_FAILED"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("kafka.publish_failed", result.Error?.Code);
        Assert.Equal(3, publishBus.Attempts);
    }

    private static KafkaNotificationResultPublisher CreatePublisher(RecordingPublishBus publishBus) =>
        new(
            publishBus,
            Microsoft.Extensions.Options.Options.Create(new KafkaOptions
            {
                BootstrapServers = "localhost:9092",
                ClientId = "notification",
                GroupId = "notification",
                PublishMaxAttempts = 3,
                RetryDelayMilliseconds = 1
            }),
            NullLogger<KafkaNotificationResultPublisher>.Instance);

    private sealed class RecordingPublishBus(Exception? exception = null) : IPublishBus
    {
        public object? Message { get; private set; }
        public int Attempts { get; private set; }

        public Task Publish<TMessage>(TMessage message, string? path = null, IDictionary<string, object>? headers = null, CancellationToken cancellationToken = default)
        {
            Attempts++;
            Message = message;
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }
}
