# Notal documentation

Notal is the product name. The C# projects and Python modules still use the `Certio` codename.

| Document | What it covers |
|----------|----------------|
| [Architecture overview](architecture/overview.md) | Layers, a request, and where code lives |
| [Domain model](architecture/domain-model.md) | Organizations, events, tasks, documents |
| [Tenancy and permissions](architecture/multi-tenancy-and-permissions.md) | Org scope, permission sets, cache |
| [Realtime](architecture/realtime.md) | SignalR hubs |
| [AI service](architecture/ai-service.md) | The Python service, RAG, and agent actions |
| [Data and auditing](architecture/data-and-auditing.md) | Soft delete and the audit interceptor |
| [Decisions](decisions/001-clean-architecture.md) | Short notes on why the big choices were made |
| [Local development](setup/local-development.md) | Run the stack on your machine |
| [Configuration](setup/configuration.md) | Environment variables |
| [Deployment](operations/deployment.md) | What the GitHub Actions workflows do |
| [Testing](operations/testing.md) | How to run the tests |
| [Known limitations](known-limitations.md) | What is still rough |
