# Architecture

The service runs as a .NET Generic Host worker. It exposes no HTTP endpoints.

`Worker` depends on `Application` and `Infrastructure`; `Infrastructure` depends on `Application` and `Domain`; `Application` depends on `Domain`. The Domain project has no transport or provider dependency.

Kafka consumers, the History HTTP client, Gmail, Fluid templates and Docker delivery are implemented in their dedicated backlog stories.

## Kafka transport

SlimMessageBus 3.5 with its Kafka provider owns two consumers in the `vault-history-notification` group. Messages are JSON camelCase and use explicit topics, so messages produced by KafkaJS do not need CLR type headers. The publisher awaits Kafka confirmation and uses user IDs or outbox IDs as Kafka keys to preserve ordering per entity.

Failures are retried with the configured bounded delay. When retries are exhausted, the error handler asks the host to stop and returns a failed processing result. This avoids treating a notification as complete when its result cannot be preserved. A Kafka broker was not available in the local environment during this change; the automated tests cover KafkaJS JSON fixtures and the C# result contracts. A broker-level integration run remains part of the full environment validation in HU-14.
