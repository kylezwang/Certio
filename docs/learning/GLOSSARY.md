# Glossary

Terms as this codebase uses them. Where a name is misleading, that is noted — several are.

## Product and naming

**Certio** — the name in code: solution, namespaces, projects, database. Use it when talking about source.

**Notal** — the name in deployment: the Azure Web Apps are `Notal-app` and `Notal-ai`, and the workflow
files are `production_notal-app.yml` and `production_notal-ai.yml`. A rebrand that reached infrastructure
but not source. See module 00.

**The pivot** — the product is moving from legal practice management to the events industry, starting with
event planners and their clients. The display layer is converted; the domain model and parts of the AI
layer are not. Module 00 maps which is which; root cause 5 in module 14 explains why the leftovers are
hard to find.

**Matter** — the central work item, and the entity most of the app orbits. Belongs to one organization,
has an `AccessLevel`, assigned team members, tasks, documents, and communication channels. **Displayed as
"Event"** to event-planning organizations. The name is legal-era; the entity already carries
`GuestCount` and `Location`.

**Terminology layer** — `RoleDisplayHelper` plus `Organization.GetMatterTerminology()`, which translate
stored legal vocabulary into what the user sees (`Matter` to Event, `Paralegal` to Coordinator,
`Partner` to Director), with optional per-organization overrides. The reason **you cannot grep for the
words users see.**

**Change notice** — a client-facing notification of a change to a matter, with public (`[AllowAnonymous]`)
view links and an auto-reminder background service.

## Tenancy and identity

**Organization** — the tenant boundary. Both service providers (planning companies, law firms) and their
client companies are `Organization` rows, distinguished by `OrganizationType` rather than by separate
tables.

**`UserOrganization`** — the membership join between a user and an organization, carrying the user's
**role** and **`UserType`** in that organization. A user can belong to several.

**`UserTypes.LawFirm`** — despite the name, this now means **"the service-provider side of a
provider-client relationship."** Event planner staff use this value too, because `UserTypes` has no
`EventPlanner` member and eleven call sites gate cross-organization access on it. The single most
important piece of vocabulary in this glossary; see module 03.

**`OrganizationRelationship`** — a provider-to-client link (`LawFirmClient` or `EventPlannerClient`; both
satisfy `IsServiceProviderClient`). This is what makes provider-based access possible: a planner reaches a
client organization's data through the relationship rather than through membership.

**Provider-based access** (called *firm-based access* in the source) — authorization derived from an
`OrganizationRelationship` rather than a `UserOrganization`. The reason permission resolution unions two
sources (module 04) and the reason a single `OrgId` check is not sufficient to answer "can this user see
this?"

**Dual user model** — users exist twice: `AspNetUsers` (ASP.NET Identity, string keys) and `Users` (domain,
int keys), reconciled by `UserSyncMiddleware` on each request. Root cause 2 in module 14.

**`ClientContext`** — the per-request resolved tenancy object: which organization this request is acting
in, and by what right. Built by `ClientContextMiddleware`, consumed by `OrgMemberAuthorizationHandler`.

**`OrgMember`** — the authorization policy asserting the caller has a valid `ClientContext` for the
requested organization. The most common `[Authorize(Policy = ...)]` value in the codebase.

## Permissions

**`Permission`** — the enum of 23 discrete capabilities across 6 categories (`Certio.Domain/Users/User.cs`).

**`PermissionSets`** — static role-to-permission mappings. Where the privilege inversion in module 04
lives.

**Effective permissions** — the union of permissions from all of a user's memberships and firm
relationships, computed by `User.GetEffectivePermissions`.

**`CachedPermissionService`** — the caching decorator over permission resolution (memory L1, distributed
L2). Note that its invalidation methods are stubs (module 04, N6).

## Application patterns

**`ServiceResult<T>` / `ServiceResult`** — the standard return type for application services. Expected
failures become `Failure("message")`; unexpected ones stay exceptions. Module 01.

**Thin controller** — the convention that controllers validate input, call one service, and translate a
`ServiceResult` to an HTTP result. Followed in newer controllers, not in the 1,275-line
`DocumentsApiController`.

**Domain exception** — an exception type in `Certio.Domain` representing a business-rule violation, caught
at the service boundary and mapped to a `ServiceResult` failure.

## Data

**`ApplicationDbContext`** — the single EF Core context, 68 `DbSet`s. Applies soft-delete global query
filters by reflection; notably does **not** apply an organization filter (module 05, root cause 3).

**Soft delete** — deletion by setting `DeletedAt` rather than removing the row. Enforced two ways: global
query filters exclude deleted rows, and `AuditInterceptor` converts hard deletes into soft deletes.

**`AuditInterceptor`** — the EF `SaveChangesInterceptor` that stamps `CreatedById`/`ModifiedById`, converts
hard deletes, and writes `AuditLog` rows with before-and-after JSON. Uses `AsyncLocal` to hold per-request
state in a singleton — the correct pattern that module 12 contrasts against static dictionaries.

**`AuditLog`** — the application-level audit trail. Richer than most, and untested (module 13).

**Migration** — an EF Core schema change. **72 of them, in `Certio.Infrastructure/Migrations`**, despite
what the README says (appendix D1). Applied to production **manually** from a CI-generated
`migrations.sql`; nothing calls `Database.Migrate()`.

## Real-time

**Hub** — a SignalR endpoint. `ChatHub` (`/hubs/chat`), `DirectHub` (`/hubs/direct`), `NotificationHub`
(`/hubs/notifications`), and `UpdatesHub` (`/hubs/updates`) — the last being empty and unused
(appendix D3).

**Backplane** — the Redis-based mechanism that relays hub messages between server instances.
`Microsoft.AspNetCore.SignalR.StackExchangeRedis` is referenced and configurable. It shares *message
delivery* only, not the static presence and typing state (module 07).

**`UserPresenceService`** — online/offline tracking, held in static dictionaries. Process-local
(module 12).

## AI

**`ai_agents`** — the Python 3.11 FastAPI microservice holding all LLM work. Called by `AIAgentService`
over HTTP with a shared `X-API-Key`.

**RAG (Retrieval-Augmented Generation)** — retrieve relevant text, then include it in the LLM prompt.
This codebase has **three separate RAG systems**: product knowledge base, user data context, and
documents (module 08).

**"Embedding"** — in this codebase, usually **not** a semantic vector.
`DocumentIndexerService.GeneratePlaceholderEmbedding` produces a hash-derived vector that
`VectorStoreService.ComputeScore` never uses; scoring is lexical term matching. The Python product KB uses
real TF-IDF, which is lexical too. **Nothing here computes semantic similarity** (appendix D2).

**`DocumentVector`** — the table holding chunk text and its placeholder vector. Searched lexically.

**Chunk** — a fixed 1,200-character slice of document text, split without sentence awareness (module 10).

**Agent action** — a database-backed proposed mutation from the AI, moving through
propose → approve → execute → (rollback). The mechanism that keeps the LLM from writing directly to the
database (module 09).

**`[ACTION:...]` block** — the marker the LLM emits in its response text, parsed **client-side** by
`agent-actions.js` and rendered as an approval card. Module 09 discusses why client-side parsing of model
output is the weak point.

**`runId`** — the idempotency key on agent action execution, preventing double-execution on retry.

## Documents and integrations

**`ExternalConnection`** — a stored OAuth grant (Google or Microsoft) with an encrypted refresh token,
scoped to an organization.

**WOPI** — Web Application Open Platform Interface, the protocol letting Office Online edit a
document that Certio hosts. Certio is the **WOPI host**; Microsoft's servers call back into
`WopiController`, which is legitimately `[AllowAnonymous]` because the caller carries a WOPI access token
rather than a user cookie (module 10).

**`WopiAccessTokenService`** — mints and validates those tokens, in a **static** dictionary — so editing
breaks on scale-out and expired tokens leak (module 10, N11).

**Azure Document Intelligence** — the Azure service used to extract text from PDFs and images. Guarded by
a `SemaphoreSlim(2)` and a 429 back-off gate, both process-local.

**Deterministic GUID** — `CreateDeterministicGuid(namespace, intId)`, a UUIDv5-style SHA-256 mapping that
bridges the `int`-keyed core to the `Guid`-keyed documents subsystem. **Copy-pasted into six files**
(module 10, N5).

## Infrastructure

**`ICacheService` / `RedisCacheService`** — the application caching abstraction. Reads through memory L1
to distributed L2.

**`AddDistributedMemoryCache`** — the .NET fallback that implements `IDistributedCache` with a
**process-local** dictionary. Used whenever no Redis connection string is detected. The name is the trap:
consumers asking for a distributed cache silently get a local one (module 12).

**Smart database selection** — `GetConnectionStringAsync`, which picks Azure SQL or local SQL Server based
on `USE_AZURE_SQL` and connectivity. Why the project runs offline.

**`ValidateProductionConfiguration`** — the startup check that refuses to boot in Production without
required webhook secrets, collecting all errors before throwing. A pattern worth copying (module 12).

**`/healthz`** — the health endpoint. Returns `{ok: true}` unconditionally, and is the final gate on
production deploys (appendix N2).
