# Architecture

The service runs as a .NET Generic Host worker. It exposes no HTTP endpoints.

`Worker` depends on `Application` and `Infrastructure`; `Infrastructure` depends on `Application` and `Domain`; `Application` depends on `Domain`. The Domain project has no transport or provider dependency.

Kafka consumers, the History HTTP client, Gmail, Fluid templates and Docker delivery are implemented in their dedicated backlog stories.

## Kafka transport

SlimMessageBus 3.5 with its Kafka provider owns two consumers in the `vault-history-notification` group. Messages are JSON camelCase and use explicit topics, so messages produced by KafkaJS do not need CLR type headers. The publisher awaits Kafka confirmation and uses user IDs or outbox IDs as Kafka keys to preserve ordering per entity.

Failures are retried with the configured bounded delay. When retries are exhausted, the error handler asks the host to stop and returns a failed processing result. This avoids treating a notification as complete when its result cannot be preserved. A Kafka broker was not available in the local environment during this change; the automated tests cover KafkaJS JSON fixtures and the C# result contracts. A broker-level integration run remains part of the full environment validation in HU-14.

## History client

`IHistoryClient` is implemented by a typed `HttpClient` and calls `POST api/v1/history/generate/subscription`. Its `Authorization` header is the configured token value verbatim, matching the History job-token guard. It maps the notification birthday to a `yyyy-MM-dd` `date` and omits no user identifiers from the required `userId` field. The client does not automatically retry this POST because History persists each generated story and has no idempotency key. It distinguishes request validation, authorization, rate limit, server, network, timeout and invalid-response failures for the notification workflow.

## History notification workflow

`NotificationWorkflow` coordinates the history consumer's use case: validate the incoming data, request a subscription history, render the history template, deliver the HTML and publish `NOTIFIED` for the same `userId`. A failed History request, template render or Gmail delivery skips every later stage and publishes `ERROR` with a null notification date. Result publication has the bounded Kafka retry configured in HU-03; if it cannot be confirmed, the consumer reports a failure instead of acknowledging the incoming message. Unit tests use fakes for all ports, so this workflow neither needs Google credentials nor sends email.

## Outbox notification workflow

The same workflow handles outbox notifications without calling History. `UserSignedInEvent` selects the sign-in template and requires `occurredOn`; `CreateUserEvent` selects the welcome template. A successful delivery publishes `PROCESSED` with `error: null`, while a validation, template or Gmail failure publishes `ERROR` with the original `outboxId` and a sanitized error code. This keeps two events for the same user independently correlated and avoids exposing provider messages in Kafka.

## Email templates

`FluidTemplateRenderer` parses and caches the three Liquid templates copied with the Worker: generated history, sign-in and welcome. It HTML-encodes user data and generated history, turning each history paragraph into a safe HTML paragraph. Sign-in timestamps are formatted with the configured `Templates:TimeZoneId`, which defaults to `America/Guayaquil`. Template rendering is local and does not need Gmail OAuth credentials; those are only required by the delivery adapter.
