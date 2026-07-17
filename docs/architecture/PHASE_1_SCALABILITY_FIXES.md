# Phase 1 Scalability & Correctness Fixes

**Status:** Completed
**Scope:** P0 items from the July 2026 architecture review
**Related:** `docs/architecture/CERTIO_TECHNICAL_ARCHITECTURE_DOCUMENT.md`

## Why this phase exists

A code-level architecture review (not just documentation) found several issues
that get worse as tenants and data volume grow: soft-delete/tenant filtering
was entirely manual, one list endpoint had a redundant per-row database round
trip, several list endpoints had no upper bound on rows returned, one
notification path broadcast to every connected user regardless of
organization, and SignalR had no shared state across multiple app instances.

This phase fixes the highest-severity, lowest-risk items first: the ones that
cause silent data correctness problems or fall over first under load, without
requiring a UI/API contract redesign (that is Phase 2+).

## What changed

### 1. Global soft-delete query filter (safety net)

**File:** `Certio.Infrastructure/Data/ApplicationDbContext.cs`

**Before:** Every entity with an `IsDeleted` or `DeletedAt` column relied on
every single query, in every service, remembering to add
`.Where(x => !x.IsDeleted)` or `.Where(x => x.DeletedAt == null)`. There was no
`HasQueryFilter` anywhere in the model. Missing one of these filters in a new
or edited query would silently leak soft-deleted rows back into lists, search
results, exports, or AI context — with no compiler or test warning.

**After:** `ApplySoftDeleteQueryFilters()` runs once during `OnModelCreating`
and, via reflection, applies a query filter to every entity type that exposes:

- a `bool IsDeleted` property → filtered to `!IsDeleted`, or
- (if it has no `IsDeleted`) a `DateTime? DeletedAt` property → filtered to
  `DeletedAt == null`

This covers every affected entity (`Matter`, `TaskItem`, `CalendarEvent`,
`Document`, `User`, `Organization`, `TimeEntry`, `Invoice`, `ChangeNotice`,
chat/DM entities, etc.) without requiring each entity to be touched
individually, and without requiring every existing service query to change.

**What this does NOT change:** existing manual `!IsDeleted` filters in
services are now redundant but harmless — they were left in place rather than
removed, to keep this change additive and low-risk. A future cleanup pass can
remove the redundant clauses.

**Escape hatch:** any query that legitimately needs to see soft-deleted rows
(admin tooling, a future "restore" feature, audit exports) must call
`.IgnoreQueryFilters()` explicitly. No such call sites existed at the time of
this change (verified by search), so nothing needed to be updated.

**Verification:** full solution build (0 errors) and the existing 57-test
suite (which constructs `ApplicationDbContext` against the EF InMemory
provider, exercising `OnModelCreating`) both pass. This was not smoke-tested
against a live SQL Server instance in this session (no configured local DB in
the dev environment used); the InMemory provider does execute the same model-
building code path.

### 2. Matter list N+1 fix

**File:** `Certio.Application/Services/MatterService.cs`

**Before:** `ListMattersAsync` already issues `.Include(m => m.TaskItems)`,
but the per-row mapping method `MapToMatterDto` unconditionally called:

```csharp
await _context.Entry(matter).Collection(m => m.TaskItems).LoadAsync();
```

Explicit `Load`/`LoadAsync` calls always issue a query, even when the
navigation was already populated by `Include()` — so every matter on every
list page triggered one extra round trip to the database purely to reload
data it already had in memory.

**After:** the explicit load is now guarded:

```csharp
if (!_context.Entry(matter).Collection(m => m.TaskItems).IsLoaded)
{
    await _context.Entry(matter).Collection(m => m.TaskItems).LoadAsync();
}
```

The list path (which eager-loads `TaskItems`) now does zero extra queries.
The single-matter paths (`CreateMatterAsync`, `UpdateMatterAsync`,
`GetMatterByIdAsync`), which don't eager-load `TaskItems`, are unaffected and
still load it exactly once, same as before.

### 3. Bounded list queries (safety cap, not full pagination)

**Files:** `Certio.Application/Configuration/QueryLimits.cs` (new),
`MatterService.ListMattersAsync`, `BillingService.GetTimeEntriesAsync` /
`GetExpensesAsync` / `GetInvoicesAsync`, `AuditService.GetEntityHistoryAsync` /
`GetUserActivityAsync` / `GetAIGeneratedContentAsync` /
`GetOrganizationAuditLogsAsync` / `GetMatterAuditLogsAsync`.

**Before:** these endpoints ran `.ToListAsync()` with no `Take()` at all. A
tenant with a large matter/billing/audit history would load every matching
row — with nested `Include()`s in some cases — into memory in a single
request.

**After:** each of the above now orders results deterministically and applies
`.Take(QueryLimits.DefaultMaxResults)` (500). If the cap is hit, a warning is
logged with the org/context so it's visible in telemetry which tenants are
approaching the limit:

```csharp
if (matters.Count == QueryLimits.DefaultMaxResults)
{
    _logger.LogWarning("ListMattersAsync truncated results at {MaxResults} for org {OrgId} ...", ...);
}
```

**This is explicitly a safety net, not real pagination.** No API/UI contract
changed — every existing caller still gets a `List<T>`/`IEnumerable<T>` back,
just capped instead of unbounded. Real cursor- or offset-based pagination
(with `Skip`/`Take` exposed through the service interface, controller query
params, and the corresponding views) is a larger, UI-touching change and is
scoped to Phase 2 — see `docs/architecture/ARCHITECTURE_REVIEW_2026.md`.

**Deliberately left unbounded:** `AuditService.ExportAuditLogsAsync` (CSV
export) and `GetAuditSummaryAsync` (aggregate report) still return every
matching row — capping an export or an aggregate would silently produce
incomplete/incorrect output, which is worse than a slow request. These need
a different fix (e.g. streaming export, background job) in a later phase.

### 4. Fixed `Clients.All` fan-out in `NotificationService.NotifyEntityChangeAsync`

**Files:** `Certio.Application/Interfaces/INotificationService.cs`,
`Certio.Web/Services/NotificationService.cs`

**Before:** for `taskitem`/`task` entities, and for any entity type not
explicitly matched in the `switch`, the notification was sent via
`_hubContext.Clients.All`, which broadcasts to **every connected client on
every tenant**, not just users who should see that event.

**After:** the method now requires an `organizationId` parameter and every
branch is scoped to a SignalR group (`org_{organizationId}`, plus
`matter_{entityId}` for matter-specific events). `Clients.All` no longer
appears anywhere in `NotificationService`.

**Note:** at the time of this change, `NotifyEntityChangeAsync` had no active
callers anywhere in the codebase (verified by search) — it was reachable
through the public interface but unused. The fix was still made because it is
part of the public `INotificationService` contract and the architecture
review flagged it; fixing it now prevents the fan-out bug from being
reintroduced the first time someone wires this method up.

### 5. SignalR Redis backplane (multi-instance readiness)

**File:** `Certio.Web/Program.cs`, `Certio.Web/Certio.Web.csproj`
(added `Microsoft.AspNetCore.SignalR.StackExchangeRedis`)

**Before:** `builder.Services.AddSignalR()` had no backplane. Behind a load
balancer with more than one app instance, each hub (`ChatHub`, `DirectHub`,
`NotificationHub`, `UpdatesHub`) only knew about connections on its own
process — a message sent from the instance handling the API request would
never reach a client connected to a different instance's hub. This fails
silently: no exception, just a notification/chat message that never arrives
for some fraction of users depending on load-balancer routing.

**After:** the existing Redis connection-string detection (already used for
distributed caching) is reused to conditionally enable the SignalR Redis
backplane:

```csharp
var signalRBuilder = builder.Services.AddSignalR();
if (redisEnabled)
{
    signalRBuilder.AddStackExchangeRedis(redisConnection!, options => { ... });
}
```

When no Redis connection string is configured (e.g. local development), the
app behaves exactly as before — single-instance, in-memory hub state, with a
log line noting the backplane is disabled. **This does not, by itself, fix
presence tracking (`UserPresenceService`), typing indicators
(`ChatHub._typingUsers`), or the in-process embedding queue
(`EmbeddingJobQueue`) — those still hold state in a static/local collection
per instance and are tracked as separate Phase 2 items.** The SignalR
backplane only fixes message delivery between hub connections; it does not
retroactively distribute application-level in-memory state.

## What Phase 1 deliberately did not touch

To keep this phase additive and independently verifiable, the following
known issues from the architecture review were **not** addressed here and
remain for Phase 2+:

- `UserPresenceService`, `ChatHub._typingUsers`, `FirmRelationshipCacheService`,
  and `EmbeddingJobQueue` are still process-local state (not distributed).
- Sync-over-async calls (`.Result`, `.GetAwaiter().GetResult()`) in
  `AccountController`, `DriveOAuthController`, `CalendarOAuthController`.
- No real pagination contract (API query params, UI page controls) — only the
  defensive cap described above.
- `Application` layer still depends on `Infrastructure`'s `ApplicationDbContext`
  directly (no repository/port abstraction).
- Fat controllers/services (`HomeController`, `ClientController`, `EmailService`,
  `ChatService`) were not refactored.
- Document vector search still lives in SQL Server via `sklearn`/TF-IDF on the
  Python side rather than a dedicated vector store.

## Verification performed

- `dotnet build Certio.sln` — succeeds, 0 errors (warnings are pre-existing).
- `dotnet test Certio.Tests/Certio.Tests.csproj` — 57/57 passing, no
  regressions from the changes in this phase.
- Manual code review of every entity with `IsDeleted`/`DeletedAt` to confirm
  no existing "restore"/admin query relies on seeing soft-deleted rows without
  `.IgnoreQueryFilters()` (none found).
- Confirmed `NotifyEntityChangeAsync` has no existing callers before changing
  its signature.
- Did **not** verify against a live SQL Server or Redis instance — the local
  dev environment used for this change did not have `SQL_PASSWORD` configured
  for the local SQL Server container. Anyone deploying this should smoke-test
  matter listing, billing lists, and a two-instance SignalR setup before
  relying on these fixes in production.
