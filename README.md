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

The transport awaits broker confirmation when publishing. A consumer retries a failed message in-process and stops the worker after its bounded attempts, before the default SlimMessageBus handling can advance a failed notification silently. The history workflow generates the subscription history, renders it, delivers it and then publishes `NOTIFIED`. A History, template or Gmail failure publishes `ERROR` with a null notification date. The outbox workflow renders `UserSignedInEvent` messages with the sign-in template and `CreateUserEvent` messages with the welcome template, then publishes `PROCESSED` or an `ERROR` tied to the original outbox ID.

The typed History client sends a POST to `History__SubscriptionPath` with the literal `History__AuthorizationToken` header and reads `{ "history": "..." }`. It does not retry the POST automatically: a successful request persists generated content in History, and a retry without an idempotency key could create a duplicate story.

Gmail delivery is configured through OAuth refresh-token secrets and sends only with the `gmail.send` scope. The worker does not prompt for OAuth at startup. See [Gmail OAuth setup](docs/gmail-oauth.md) before adding credentials.

Email templates use Fluid/Liquid and live in `src/VaultHistory.Notification.Worker/Templates`. They are copied when the worker is built or published. `Templates__TimeZoneId` controls the explicit time zone shown in sign-in messages (default: `America/Guayaquil`). Rendering templates does not require Google credentials; credentials are only needed when the Gmail sender delivers the rendered HTML.

## Docker

La imagen y el entorno conjunto se administran desde [Vault.History.System](https://github.com/CarlosSV923/Vault.History.System). El Compose central conecta este worker con Kafka y History, incluye las plantillas publicadas y permite iniciar el contenedor con credenciales Google placeholder mientras no se procesen mensajes reales.

Después de clonar el repositorio de orquestación con sus submódulos:

```bash
docker compose up --build -d
```

Notification no publica puertos HTTP. Su estado se inspecciona mediante `docker compose ps` y `docker compose logs notification`.

## Commands

```powershell
dotnet restore VaultHistory.Notification.slnx
dotnet build VaultHistory.Notification.slnx --no-restore
dotnet test VaultHistory.Notification.slnx --no-build
```

## Releases

Release Please runs when changes reach `main` and can also be started manually from GitHub Actions. It uses Conventional Commits to prepare a release pull request that keeps `CHANGELOG.md`, `version.txt` and the GitHub release tag aligned.

The `simple` release strategy treats `version.txt` as the service version. `Directory.Build.props` reads that file for every project, so assemblies and published artifacts receive the same version. The initial baseline is `0.1.0`.

Use commit prefixes such as `feat:`, `fix:` and `feat!:` (or a `BREAKING CHANGE` footer) to request minor, patch and major increments. After merging a generated release pull request, Release Please creates the corresponding GitHub release and `vX.Y.Z` tag. No registry publication or CI validation is part of this workflow.

For the first release:

1. Merge changes that use Conventional Commits into `develop`, then promote `develop` to `main`.
2. Wait for the Release Please workflow to create or update its release pull request against `main`. It can be started with `workflow_dispatch` if a manual run is needed.
3. Review the proposed `CHANGELOG.md` and `version.txt` changes in that pull request.
4. Merge the release pull request. The next workflow run creates the GitHub release and matching version tag.
