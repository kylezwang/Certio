# Certio Architecture Review (July 2026)

**Method:** direct code inspection (not documentation) — project files, DI
registration in `Program.cs`, service implementations, `ApplicationDbContext`,
SignalR hubs, and the `ai_agents` Python service. Every claim below was
verified against the actual source before being used to plan fixes; see
"Cross-check notes" for specifics.

## Summary

Certio is a pragmatic layered monolith, not a strict Clean Architecture
implementation. That trade-off bought delivery speed early on; the items
below are where it now creates real risk as tenants/data grow.

## Verified findings

| Area | Finding | Evidence |
|---|---|---|
| Layering | `Certio.Application` depends directly on `Certio.Infrastructure`'s `ApplicationDbContext` in 25+ services (no repository abstraction) | `MatterService`, `BillingService`, `AuditService`, etc. all inject `ApplicationDbContext` |
| Tenancy | No `HasQueryFilter` existed anywhere prior to this review; org/soft-delete scoping was 100% manual per-query | grep across `Certio.Infrastructure` and `Certio.Domain` returned zero matches |
| N+1 | `MatterService.MapToMatterDto` called `Entry().Collection().LoadAsync()` unconditionally even when `TaskItems` was already eager-loaded via `.Include()` in `ListMattersAsync` | confirmed by reading both methods; explicit `Load` always queries regardless of `IsLoaded` unless guarded |
| Unbounded reads | `MatterService.ListMattersAsync`, `BillingService` (time entries/expenses/invoices), `AuditService` list methods had `.ToListAsync()` with no `Take()` | confirmed via `Grep` for `ToListAsync()` without adjacent `Skip`/`Take` |
| Fan-out | `NotificationService.NotifyEntityChangeAsync` used `Clients.All` for `task`/`taskitem` and any unmatched entity type | confirmed by reading `NotificationService.cs`; no active callers existed at review time |
| Process-local state | `UserPresenceService` (static `ConcurrentDictionary`), `ChatHub._typingUsers` (static `Dictionary`), `FirmRelationshipCacheService` (`IMemoryCache` only, no Redis), `EmbeddingJobQueue` (in-process bounded `Channel(200)`) | confirmed by reading each file directly |
| SignalR | 4 hubs registered (`ChatHub`, `DirectHub`, `NotificationHub`, `UpdatesHub`), no Redis/Azure SignalR backplane configured | confirmed via `Program.cs`: `AddSignalR()` had no `.AddStackExchangeRedis(...)` call |
| Caching | Redis is optional — gated on `ConnectionStrings:Redis` + `USE_REDIS`/Azure host string; falls back to `AddDistributedMemoryCache()` | confirmed in `Program.cs` |
| Sync-over-async | `.Result`/`.GetAwaiter().GetResult()` calls in `AccountController`, `DriveOAuthController`, `CalendarOAuthController` | confirmed via `Grep` |
| Fat controllers/services | `HomeController` (2767 lines), `ClientController` (2058 lines), `EmailService` (1489 lines), `ChatService` (1287 lines) | measured directly from file sizes |
| Dual ID schemes | Most entities use `int` IDs; `Document`/`DocumentVersion`/`RagQuery`/etc. use `Guid OrgId` derived deterministically from the `int` org ID | confirmed in `Certio.Domain/Documents/Document.cs` and `UserDataContextService.CreateDeterministicGuid` |
| AI service | `ai_agents/main.py` is a ~4,300-line FastAPI monolith; document/user-data RAG assembled in .NET and POSTed to Python over HTTP with an API key | confirmed by reading `main.py` and `AIAgentService.cs` |
| Tests | `Certio.Tests` has focused coverage (permissions, hub security, auth attributes, email webhooks) — 57 tests total, not broad service/controller coverage relative to codebase size | `dotnet test` output |

## Phase roadmap

**Phase 1 — correctness & lowest-risk scale fixes (completed):**
see `docs/architecture/PHASE_1_SCALABILITY_FIXES.md`.
- Global soft-delete query filter safety net
- Matter list N+1 fix
- Bounded list queries (defensive cap, not full pagination)
- `Clients.All` fan-out fix in `NotificationService`
- SignalR Redis backplane (opt-in via existing Redis config)

**Phase 2 — distributed state & real pagination (proposed, not started):**
- Move `UserPresenceService`, `ChatHub._typingUsers`, `FirmRelationshipCacheService`,
  and `EmbeddingJobQueue` off process-local storage onto Redis (or equivalent)
  so they behave correctly with more than one app instance.
- Design and ship real pagination (API query params + UI controls) for matter,
  billing, and audit list endpoints, replacing the Phase 1 safety cap.
- Fix remaining sync-over-async call sites.

**Phase 3 — structural cleanup (proposed, not started):**
- Introduce a persistence port/interface in `Certio.Application` so it no
  longer depends on `Certio.Infrastructure` directly.
- Split the largest controllers/services (`HomeController`, `ClientController`,
  `EmailService`, `ChatService`) into focused, testable units.
- Unify the `int`/`Guid` ID split in the Documents subsystem.
- Move document vector search to a dedicated vector store.

## Cross-check notes

This review was produced by re-reading the source directly (not relying on
`.cursorrules` or existing `docs/` content, which describe intent rather than
verified behavior — e.g. `.cursorrules` claims "Thin Controllers... < 50 lines
per action", which the 2767-line `HomeController` contradicts). Every finding
in the table above was independently confirmed via `Read`/`Grep` against the
current source before Phase 1 work began, and the fixes in Phase 1 were
verified with a full solution build (`dotnet build`, 0 errors) and the
existing test suite (`dotnet test`, 57/57 passing).
