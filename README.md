# VaultHistory.Microservice.Notification

Background worker responsible for Vault History notifications. This initial version establishes the .NET 10 DDD structure and a Generic Host only; it does not expose controllers, HTTP endpoints, Swagger or listening ports.

## Projects

- `Domain`: provider-independent results and notification types.
- `Application`: use-case boundaries and provider ports.
- `Infrastructure`: configuration and future provider adapters.
- `Worker`: Generic Host startup and future Kafka consumers.

## Configuration

`src/VaultHistory.Notification.Worker/Configurations/appsettings.json` declares the four Kafka topics. Supply Kafka, History and Gmail secrets through user secrets or environment variables, for example `Kafka__BootstrapServers`, `History__AuthorizationToken` and `Gmail__RefreshToken`. Startup validation deliberately fails with a clear options error when mandatory configuration is absent.

## Commands

```powershell
dotnet restore VaultHistory.Notification.slnx
dotnet build VaultHistory.Notification.slnx --no-restore
dotnet test VaultHistory.Notification.slnx --no-build
```
