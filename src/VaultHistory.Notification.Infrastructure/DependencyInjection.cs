using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using System.Text;
using System.Text.Json;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.Contracts;
using VaultHistory.Notification.Infrastructure.Messaging.Kafka;
using VaultHistory.Notification.Infrastructure.History;
using VaultHistory.Notification.Infrastructure.Gmail;
using VaultHistory.Notification.Infrastructure.Options;
using VaultHistory.Notification.Infrastructure.Templates;
using VaultHistory.Notification.Infrastructure.Checkpoints;

namespace VaultHistory.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HistoryOptions>()
            .Bind(configuration.GetSection(HistoryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GmailOptions>()
            .Bind(configuration.GetSection(GmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TemplatesOptions>()
            .Bind(configuration.GetSection(TemplatesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IHistoryClient, HistoryClient>((serviceProvider, client) =>
        {
            var history = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HistoryOptions>>().Value;
            client.BaseAddress = new Uri(history.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(history.TimeoutSeconds);
        });

        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();

        services.AddSlimMessageBus(mbb =>
        {
            mbb.AddJsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            });

            mbb.Produce<UpdateUserNotificationMessage>(x => x
                .DefaultTopic(kafka.Topics.UpdateUsers)
                .KeyProvider((message, _) => Encoding.UTF8.GetBytes(message.Id)));

            mbb.Produce<UpdateOutboxStatusMessage>(x => x
                .DefaultTopic(kafka.Topics.UpdateOutbox)
                .KeyProvider((message, _) => Encoding.UTF8.GetBytes(message.Id)));

            mbb.Consume<NotifyHistoryMessage>(x => x
                .Topic(kafka.Topics.NotifyHistory)
                .WithConsumer<NotifyHistoryConsumer>()
                .KafkaGroup(kafka.GroupId)
                .Instances(1)
                .CheckpointEvery(1)
                .CheckpointAfter(TimeSpan.FromSeconds(5)));

            mbb.Consume<NotifyOutboxMessage>(x => x
                .Topic(kafka.Topics.NotifyOutbox)
                .WithConsumer<NotifyOutboxConsumer>()
                .KafkaGroup(kafka.GroupId)
                .Instances(1)
                .CheckpointEvery(1)
                .CheckpointAfter(TimeSpan.FromSeconds(5)));

            mbb.WithProviderKafka(settings =>
            {
                settings.BrokerList = kafka.BootstrapServers;
                settings.ProducerConfig = producer => producer.ClientId = kafka.ClientId;
                settings.ConsumerConfig = consumer => consumer.ClientId = kafka.ClientId;
            });
        });

        services.AddTransient<INotificationResultPublisher, KafkaNotificationResultPublisher>();
        services.AddSingleton<INotificationCheckpointStore, PostgresNotificationCheckpointStore>();
        services.AddTransient<IEmailSender, GmailEmailSender>();
        services.AddTransient<IGmailMessageClient, GmailApiMessageClient>();
        services.AddSingleton<ITemplateRenderer, FluidTemplateRenderer>();
        services.AddTransient(typeof(IKafkaConsumerErrorHandler<>), typeof(NotificationKafkaConsumerErrorHandler<>));

        return services;
    }
}
