# Architecture

The service runs as a .NET Generic Host worker. It exposes no HTTP endpoints.

`Worker` depends on `Application` and `Infrastructure`; `Infrastructure` depends on `Application` and `Domain`; `Application` depends on `Domain`. The Domain project has no transport or provider dependency.

Kafka consumers, the History HTTP client, Gmail, Fluid templates and Docker delivery are implemented in their dedicated backlog stories.
