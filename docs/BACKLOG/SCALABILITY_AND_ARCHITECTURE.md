# Backlog: Scalability and Architecture

**Status:** Deferred (engineering backlog)  
**Origin:** July 2026 architecture review  
**Completed prerequisite:** [`../architecture/PHASE_1_SCALABILITY_FIXES.md`](../architecture/PHASE_1_SCALABILITY_FIXES.md)

Phase 1 shipped the highest-severity, lowest-risk fixes (global soft-delete filters, Matter N+1, list caps, org-scoped SignalR fan-out, optional SignalR Redis backplane). Everything below was intentionally left as backlog.

## Priority guide

| Priority | Meaning | When to pull |
|----------|---------|--------------|
| **P0 — Conditional required** | Required for correct multi-instance / HA behavior | Before running more than one app instance behind a load balancer |
| **P1 — Reliability** | Correctness/stability bugs that can bite under load | Next reliability pass; do not wait for multi-instance |
| **P2 — Scale for large tenants** | Needed when Phase 1 safety caps start truncating real data | When truncation warnings appear, or orgs grow past ~hundreds of list rows |
| **P3 — Structural debt** | Maintainability and long-term architecture | When it blocks velocity or quality goals; not required for correctness |

---

## P0 — Distribute process-local state (multi-instance)

Phase 1’s SignalR Redis backplane only shares **hub message delivery**. These still hold state in one process and will silently diverge or break across instances:

| Component | Current behavior | Target |
|-----------|------------------|--------|
| `UserPresenceService` | Static `ConcurrentDictionary` | Redis (or equivalent) presence store |
| `ChatHub._typingUsers` | Static `Dictionary` | Redis-backed typing state (or drop if unused) |
| `FirmRelationshipCacheService` | `IMemoryCache` only | Shared cache via existing `ICacheService` / Redis L2 |
| `EmbeddingJobQueue` | In-process `Channel(200)` | Durable queue (Redis list, Azure Queue, or Service Bus) + worker |

**Known limitation until done:** single-instance (or sticky sessions with degraded presence/typing) only.

---

## P1 — Remove sync-over-async

Blocking async with `.Result` / `.GetAwaiter().GetResult()` can deadlock or starve the thread pool:

- `AccountController`
- `DriveOAuthController`
- `CalendarOAuthController`

Replace with proper `async`/`await` end-to-end.

---

## P2 — Real pagination (replace Phase 1 safety caps)

Phase 1 applied `.Take(QueryLimits.DefaultMaxResults)` (500) on matter, billing, and audit list endpoints. That prevents unbounded loads but **silently truncates** once hit.

Backlog work:

- Service contracts with `Skip`/`Take` or cursor pagination
- Controller query params
- UI page / load-more controls for matter, billing, and audit lists
- Optional: streaming / background job for `AuditService.ExportAuditLogsAsync` and full `GetAuditSummaryAsync` (left unbounded on purpose in Phase 1)

**Trigger:** log warnings when results hit the cap, or tenant volume makes truncation user-visible.

---

## P3 — Structural cleanup (technical debt)

Not required for production correctness at current scale; improves testability and long-term change cost.

| Item | Notes |
|------|--------|
| Invert Application → Infrastructure dependency | Introduce persistence ports in Application; implement in Infrastructure |
| Split fat controllers/services | `HomeController`, `ClientController`, `EmailService`, `ChatService`, etc. |
| Unify `int` vs `Guid` org/document IDs | Documents use deterministic `Guid OrgId`; rest use `int` |
| Dedicated vector store | Move off SQL `DocumentVectors` + Python TF-IDF/sklearn for serious RAG scale |
| AI service packaging | Split `ai_agents/main.py` monolith; event-driven context sync vs bulk dump |
| Audit outbox / workers outside web host | Reduce nested `SaveChanges` and request-thread competition under heavy write load |
| Remove redundant manual `!IsDeleted` filters | Harmless after Phase 1 global filters; cleanup only |

---

## Explicit non-goals (for now)

Do **not** treat the following as open “phases” that must be finished next:

- Full Clean Architecture rewrite
- Microservices split
- Requiring Redis for local development (optional Redis remains valid for single-instance)

---

## Related docs

- [`../architecture/ARCHITECTURE_REVIEW_2026.md`](../architecture/ARCHITECTURE_REVIEW_2026.md) — verified findings
- [`../architecture/PHASE_1_SCALABILITY_FIXES.md`](../architecture/PHASE_1_SCALABILITY_FIXES.md) — what already shipped
- [`../setup/REDIS_SETUP.md`](../setup/REDIS_SETUP.md) — Redis configuration (relevant to P0)
