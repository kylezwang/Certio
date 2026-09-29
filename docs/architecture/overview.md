# Architecture overview

Notal is an ASP.NET Core MVC app with a separate Python service for model calls. Data lives in SQL Server. Redis is optional and is used for cache and the SignalR backplane when it is configured.

```
Browser
  |
  v
Certio.Web            MVC, Razor, SignalR, auth, web services
  |
  v
Certio.Application    service interfaces, DTOs, application services
  |
  v
Certio.Domain         entities and permission enum
  |
  v
Certio.Infrastructure DbContext, migrations, audit interceptor
  |
  +--> SQL Server
  +--> Redis (optional)

Certio.Web --HTTP--> ai_agents (FastAPI)
```

Dependencies point inward. Domain does not reference EF Core or ASP.NET. Controllers are supposed to stay thin and call services. Some controllers, especially `HomeController`, `ClientController`, and `MatterController`, still do more than that. See [known limitations](../known-limitations.md).

## A normal request

1. The user is signed in with ASP.NET Core Identity. Organization routes look like `/Client/{orgId}/...`.
2. An organization-membership policy checks that the user belongs to `orgId` before the action runs.
3. The action calls a service. Services that need a permission check go through `CachedPermissionService`.
4. Writes go through `ApplicationDbContext`. `AuditInterceptor` fills audit fields and writes an `AuditLog` row in the same save.
5. Chat and notification pushes go out through SignalR. If Redis is enabled, a backplane fans those messages out to every instance.

## Projects

| Project | Role |
|---------|------|
| `Certio.Web` | Host, controllers, views, hubs, most services that need HTTP or SignalR |
| `Certio.Application` | Interfaces, DTOs, `ServiceResult<T>`, services that do not need the web host |
| `Certio.Domain` | Entities, enums, `Permission` |
| `Certio.Infrastructure` | `ApplicationDbContext`, EF migrations, `AuditInterceptor` |
| `Certio.Tests` | xUnit tests with EF Core InMemory and Moq |
| `ai_agents` | FastAPI process the web app calls for model work |

The web project targets .NET 9. The class libraries target .NET 8.
