# Notification service documentation

Notification is a .NET Generic Host worker with no HTTP endpoints. SlimMessageBus consumes work from Kafka, the application workflow invokes History when required, Fluid renders email templates, Gmail delivers messages and Kafka receives correlated results.

- Interactive diagram: [Notification architecture](architecture/notification-architecture.html)
- Editable diagram source: [notification-architecture.json](architecture/notification-architecture.json)
- Commands: `dotnet restore`, `dotnet build`, `dotnet test`
- OAuth guidance: [gmail-oauth.md](gmail-oauth.md)

The worker consumes `notify-history-topic` and `notify-outbox-topic`; it produces `update-users-topic` and `update-outbox-topic`. Real Gmail credentials are only necessary for delivery, not for rendering templates or compiling the service.

For history notifications, PostgreSQL stores a checkpoint keyed by `notificationId`: generated story, confirmed email delivery, and confirmed Kafka result. A redelivery resumes after the last saved stage; it is an at-least-once flow and cannot guarantee exactly-once delivery if the process stops between Gmail accepting the message and saving that checkpoint.
