using Microsoft.Extensions.DependencyInjection;
using VaultHistory.Notification.Application.Abstractions;
using VaultHistory.Notification.Application.UseCases;

namespace VaultHistory.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<INotificationWorkflow, NotificationWorkflow>();

        return services;
    }
}
