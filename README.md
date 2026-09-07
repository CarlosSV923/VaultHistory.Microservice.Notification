# VaultHistory.Microservice.Notification

Background worker responsible for Vault History notifications. This initial version establishes the .NET 10 DDD structure and a Generic Host only; it does not expose controllers, HTTP endpoints, Swagger or listening ports.

## Projects

- `Domain`: provider-independent results and notification types.
- `Application`: use-case boundaries and provider ports.
- `Infrastructure`: configuration and future provider adapters.
- `Worker`: Generic Host startup and future Kafka consumers.

## Configuration

`src/VaultHistory.Notification.Worker/Configurations/appsettings.json` declares the four Kafka topics. Supply Kafka, History and Gmail secrets through user secrets or environment variables, for example `Kafka__BootstrapServers`, `History__AuthorizationToken` and `Gmail__RefreshToken`. Startup validation deliberately fails with a clear options error when mandatory configuration is absent.

Kafka uses SlimMessageBus and the `vault-history-notification` consumer group. `NotifyHistory` and `NotifyOutbox` are consumed, while `UpdateUsers` and `UpdateOutbox` receive results. Every output has the contract `{ "id": "...", "data": { ... } }`: `id` is the user ID for user updates and the outbox ID for outbox updates. `Kafka__ProcessingTimeoutSeconds`, `Kafka__PublishMaxAttempts`, `Kafka__ConsumerMaxAttempts` and `Kafka__RetryDelayMilliseconds` control bounded processing and publishing retries.

The transport awaits broker confirmation when publishing. A consumer retries a failed message in-process and stops the worker after its bounded attempts, before the default SlimMessageBus handling can advance a failed notification silently. The notification workflows themselves are added in HU-07 and HU-08; until then the transport fails closed if it receives a message.

The typed History client sends a POST to `History__SubscriptionPath` with the literal `History__AuthorizationToken` header and reads `{ "history": "..." }`. It does not retry the POST automatically: a successful request persists generated content in History, and a retry without an idempotency key could create a duplicate story.

## Commands

```powershell
dotnet restore VaultHistory.Notification.slnx
dotnet build VaultHistory.Notification.slnx --no-restore
dotnet test VaultHistory.Notification.slnx --no-build
```
