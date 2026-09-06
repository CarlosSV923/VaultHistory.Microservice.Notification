using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VaultHistory.Notification.Infrastructure;
using VaultHistory.Notification.Infrastructure.Options;

namespace VaultHistory.Notification.Infrastructure.IntegrationTests;

public sealed class OptionsRegistrationTests
{
    [Fact]
    public void Infrastructure_options_bind_valid_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:ClientId"] = "notification",
                ["Kafka:GroupId"] = "notification",
                ["Kafka:Topics:NotifyHistory"] = "notify-history-topic",
                ["Kafka:Topics:NotifyOutbox"] = "notify-outbox-topic",
                ["Kafka:Topics:UpdateUsers"] = "update-users-topic",
                ["Kafka:Topics:UpdateOutbox"] = "update-outbox-topic",
                ["History:BaseUrl"] = "https://history.example.test",
                ["History:SubscriptionPath"] = "api/v1/history/generate/subscription",
                ["History:AuthorizationToken"] = "token",
                ["Gmail:SenderAddress"] = "notification@example.test",
                ["Gmail:SenderName"] = "Vault History",
                ["Gmail:ClientId"] = "client-id",
                ["Gmail:ClientSecret"] = "client-secret",
                ["Gmail:RefreshToken"] = "refresh-token",
                ["Templates:HistoryTemplatePath"] = "Templates/history.liquid",
                ["Templates:SignInTemplatePath"] = "Templates/sign-in.liquid",
                ["Templates:WelcomeTemplatePath"] = "Templates/welcome.liquid"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KafkaOptions>>().Value;

        Assert.Equal("notify-history-topic", options.Topics.NotifyHistory);
    }

    [Fact]
    public void Infrastructure_options_reject_incomplete_configuration()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(delegate
        {
            _ = provider.GetRequiredService<IOptions<KafkaOptions>>().Value;
        });
    }
}
