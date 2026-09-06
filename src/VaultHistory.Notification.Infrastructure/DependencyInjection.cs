using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VaultHistory.Notification.Infrastructure.Options;

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

        return services;
    }
}
