# Notification architecture implementation notes

The [root README](../README.md) is the service entry point. This note keeps details that are useful when tracing provider behavior.

## Dependency direction

`Worker` references `Application` and `Infrastructure`. `Infrastructure` references `Application` and `Domain`; `Application` references `Domain`. The Domain project remains independent of Kafka, HTTP, Gmail, Fluid, and PostgreSQL.

## Kafka handling

SlimMessageBus consumes `notify-history-topic` and `notify-outbox-topic` in the `vault-history-notification` group. The producer awaits broker confirmation, keys messages by user or outbox ID, and retries result publication according to `Kafka__PublishMaxAttempts` and `Kafka__RetryDelayMilliseconds`.

Consumers use `Kafka__ProcessingTimeoutSeconds`. If an exception remains after `Kafka__ConsumerMaxAttempts`, the custom error handler stops the host rather than committing a notification whose result was not confirmed.

## Provider boundaries

The History client posts to `History__SubscriptionPath` with the configured authorization token and maps HTTP, timeout, network, and malformed-response failures into workflow errors. Fluid loads and caches the three Liquid templates from the Worker content root, HTML-encodes dynamic values, and formats sign-in timestamps in `Templates__TimeZoneId`.

The Gmail adapter uses a refresh token with the minimum `gmail.send` scope. Gmail accepting an API request does not prove inbox delivery. PostgreSQL checkpoints apply only to the History flow; Outbox notifications deliberately have no checkpoint state.
