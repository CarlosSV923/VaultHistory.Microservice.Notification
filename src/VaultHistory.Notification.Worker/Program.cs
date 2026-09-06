using VaultHistory.Notification.Application;
using VaultHistory.Notification.Infrastructure;
using VaultHistory.Notification.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("Configurations/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"Configurations/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddHostedService<NotificationWorker>();

await builder.Build().RunAsync();
