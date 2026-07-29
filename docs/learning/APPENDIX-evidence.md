# Appendix — Evidence and documentation drift

This appendix exists because of the instruction that produced this curriculum: *use the docs to learn,
but do not take them at face value.* Every claim in modules 00–14 was verified against source. This file
records the places where the existing documentation and the code disagree, with a command you can run to
check each one yourself.

Two framing notes before the list.

**The documentation is large.** There are **200 markdown files** under `docs/` — 107 of them in
`docs/archive/`, 31 in `docs/features/`, 11 in `docs/architecture/`. That volume is itself the root cause
of most drift below: 200 documents cannot be kept current by hand, and a stale document is worse than a
missing one because it is trusted.

**Several existing documents are excellent and were used as sources.** Where they are accurate, this
curriculum cites them rather than repeating them:

| Document | Assessment |
|---|---|
| `docs/BACKLOG/SCALABILITY_AND_ARCHITECTURE.md` | Accurate and well-prioritized. Correctly identifies static state as P0, the `int`/`Guid` seam, the `Application → Infrastructure` inversion, sync-over-async in exactly the three files where it exists, and TF-IDF as a scale limit. The P0–P3 framing is genuinely good. |
| `docs/operations/MAKE_REPO_PUBLIC_SAFELY.md` | Correct runbook for the tracked-secrets problem, naming `ai_agents/.env` specifically. Not yet executed. |
| `docs/archive/AJAX_NAVIGATION_STATUS.md` | Accurately documents that AJAX navigation is disabled and why — **more accurate than the code's own comment.** |
| `docs/security/SECURITY_REMAINING_ISSUES.md` | Correctly flags the AI service perimeter, tracked SQLite databases, and webhook secret validation. |

---

## Live issues that documentation flagged and are still open

These are not drift — the docs are right, the work is outstanding. Listed first because they matter most.

<a id="e1"></a>

### E1. `ai_agents/.env` is tracked in git with live API keys

```powershell
git ls-files | Select-String "\.env$"
git log --oneline --diff-filter=A -- ai_agents/.env
```

Tracked since the initial commit. Contains `OPENAI_API_KEY` (164 chars, `sk-proj-` prefix) and
`AZURE_OPENAI_API_KEY` (84 chars). `.gitignore` lists the path, which has no effect on an already-tracked
file. Repository is currently private, so exposure is contained but permanent in history.
`MAKE_REPO_PUBLIC_SAFELY.md` documents the fix. See module 14, Tier 0.

### E2. Three SQLite databases are tracked

```powershell
git ls-files | Select-String "\.(db|sqlite)$"
```

`Certio.Web/app.db` plus two copies under `bin/Debug/`. Flagged as CRITICAL in
`SECURITY_REMAINING_ISSUES.md`. May contain ASP.NET Identity password hashes.

### E3. Static process-local state is broader than documented

The backlog names 4 components; module 12 finds **13**. Missing from the backlog:
`WopiAccessTokenService` (breaks Office Online editing on scale-out) and `DocumentContentService`
(multiplies Azure Document Intelligence concurrency and fragments the 429 circuit breaker).

---

## Documentation drift

### D1. Migration location and count

**`README.md` claims:** 42 migrations in `Certio.Web/Migrations`.

**Reality:** **72** migrations, in `Certio.Infrastructure/Migrations`. `Certio.Web/Migrations` exists and
is **empty**.

```powershell
(Get-ChildItem Certio.Infrastructure/Migrations -Filter *.cs |
  Where-Object { $_.Name -notmatch "Designer|ModelSnapshot" }).Count
Get-ChildItem Certio.Web/Migrations
```

Both numbers matter: someone following the README runs `dotnet ef` against the wrong project and gets a
confusing failure. The correct invocation is in `pr-validation.yml:79`.

### D2. "Vector search" and "semantic search"

**Docs claim:** vector search over document embeddings.

**Reality:** `DocumentIndexerService.GeneratePlaceholderEmbedding` produces a deterministic hash-derived
vector, and `VectorStoreService.ComputeScore` (`:143-240`) is **lexical term matching** — it never
computes cosine similarity against those vectors. See module 08.

```powershell
Select-String -Path Certio.Application/Services/Documents/DocumentIndexerService.cs -Pattern "Placeholder"
```

The Python side does use real TF-IDF via scikit-learn for the product knowledge base, which is genuine
lexical retrieval — but TF-IDF is not semantic either. **No component in the system computes semantic
similarity.** This is the most consequential drift in the repository, because "semantic search over your
documents" is a product claim, and a user searching "can I terminate early?" will not match a document
saying "grounds for cancellation."

### D3. "4 SignalR hubs"

**`README.md` claims:** 4 SignalR hubs.

**Reality:** 4 hubs are mapped, but `UpdatesHub` is `public class UpdatesHub : Hub { }` — an empty body —
and no `IHubContext<UpdatesHub>` is injected anywhere. It is a live WebSocket endpoint at `/hubs/updates`
that does nothing.

```powershell
Select-String -Path Certio.Web/Hubs/UpdatesHub.cs -Pattern "class UpdatesHub"
Get-ChildItem -Recurse -Filter *.cs | Select-String "IHubContext<UpdatesHub>"
```

### D4. `.cursorrules` conventions are not followed

**Claims:** no emojis anywhere; thin controllers under 50 lines per action; type hints and PEP 8 in Python.

**Reality:**

| Rule | Violation |
|---|---|
| No emojis | `Program.cs:39,50,86-90`; `CachedPermissionService.cs:63`; `RequirePermissionAttribute.cs` throughout |
| Thin controllers | `DocumentsApiController` 1,275 lines; `HomeController` 2,767 lines |
| Python style | No linter in CI — `pr-validation.yml` runs `py_compile` only, which is a syntax check |

```powershell
Select-String -Path Certio.Web/Program.cs -Pattern "🔧|❌|🚨"
```

Module 14 treats this as root cause 4: mechanically-checked rules are followed, human-checked rules drift.

### D5. Clean Architecture layering

**Docs claim:** `Domain → Application → Infrastructure → Web`.

**Reality:** `Certio.Application` references `Certio.Infrastructure`, so the dependency runs the wrong
way and Application services take `ApplicationDbContext` directly.

```powershell
Select-String -Path Certio.Application/Certio.Application.csproj -Pattern "ProjectReference"
```

The backlog acknowledges this as P3. Module 01 covers it; module 14 argues about whether to fix it.

### D6. The code comment on AJAX navigation is misleading — and the doc is right

`docs/archive/AJAX_NAVIGATION_STATUS.md` quotes the config as:

```javascript
enabled: false, // DISABLED UNTIL PAGES ARE AJAX-READY
```

The current code (`page-navigation.js:5-9`) reads:

```javascript
// Configuration - can be disabled if needed
const config = {
    enabled: false,
```

The explanatory comment was removed, leaving one that implies the feature is normally *on*. **This is
drift in the opposite direction from usual: the archived document is more accurate than the source.** A
developer reading only the code would reasonably conclude AJAX navigation is active. See module 11.

### D7. "Optional Redis" understates the dependency

**Docs claim:** Redis is optional; in-memory caching is a valid fallback. The backlog reinforces this as
an explicit non-goal: *"Requiring Redis for local development (optional Redis remains valid for
single-instance)."*

**That is correct for one instance and wrong for two**, and the documentation does not distinguish them
in the place a reader would look. With `AddDistributedMemoryCache`, `DistributedTwoFactorSessionStore`
becomes process-local, so **2FA login fails intermittently** behind a load balancer. Module 12 covers the
mechanism.

### D8. Documentation volume and archive hygiene

107 of 200 markdown files are in `docs/archive/`, with names like `DM_FIX_APPLIED.md`,
`DM_FINAL_STATUS.md`, `DM_WORKING_STATUS.md`, and `SESSION_COMPLETE.md`. These are development session
notes, not documentation. They are indexed by search, so a developer looking for how direct messaging
works will find several contradictory historical accounts before finding the code.

```powershell
(Get-ChildItem docs -Recurse -Filter *.md).Count
(Get-ChildItem docs/archive -Filter *.md).Count
```

---

## Defects found by independent analysis

Not present in any existing document, ordered by severity. Each is covered in the module named.

| # | Finding | Module |
|---|---|---|
| N1 | **`AgentActionService` cross-org IDOR.** `ApproveActionAsync`, `RejectActionAsync`, `ExecuteActionAsync`, `RollbackActionAsync` load by integer ID with no organization check. A user with `ApproveAgentActions` in their own org can act on another org's action. | 09 |
| N2 | **`/healthz` cannot fail, and it is the production deploy gate.** `production_notal-app.yml:166-181` verifies deploy success by checking `ok == true` from an endpoint that returns it unconditionally. | 12, 13 |
| N3 | **No security headers at all.** No CSP, `X-Frame-Options`, `nosniff`, or `Referrer-Policy`; no middleware sets any header. | 11 |
| N4 | **SignalR loaded from cdnjs with no SRI hash** on authenticated pages displaying client contracts and guest data. Also client 6.0.1 against an ASP.NET Core 9 server. | 11 |
| N5 | **`CreateDeterministicGuid` is copy-pasted into 6 files.** Must agree byte-for-byte forever; divergence silently hides one tenant's documents rather than throwing. | 10 |
| N6 | **Permission cache invalidation is a stub.** `CachedPermissionService.InvalidateUserPermissionsAsync` does not invalidate, so revoked permissions persist until TTL. | 04 |
| N7 | **Privilege inversion in client roles.** A lower-privileged role receives a permission a higher one does not. | 04 |
| N8 | **The Redis `try/catch` is resilience theater.** `AddStackExchangeRedisCache` only registers options, so the `catch` at `Program.cs:628` can never run and its in-memory fallback is unreachable. | 12 |
| N9 | **`AIBackgroundService` never runs.** A `BackgroundService` registered with `AddSingleton` instead of `AddHostedService`; its `ExecuteAsync` is an empty poll loop. | 12 |
| N10 | **OAuth `state` has no expiry check.** `IssuedAtUtc` is in the payload; `UnprotectState` validates only `OrgId != Guid.Empty`, so state is replayable until key rotation. | 10 |
| N11 | **WOPI tokens in a static dictionary, and `CleanupExpiredTokens()` has no callers** — a slow memory leak plus broken editing on scale-out. | 10 |
| N12 | **384 lines of Python tests never run in CI.** Three `test_*.py` files sit at `ai_agents/` root; CI runs `pytest tests/` only. | 13 |
| N13 | **Coverage is collected and discarded.** No threshold in either workflow, so coverage can fall to zero with a green build. | 13 |
| N14 | **"Migration Safety Check" only checks that migrations build**, not that they are non-destructive. | 13 |
| N15 | **Two dead dependencies.** `Microsoft.SemanticKernel` (+ OpenAI connector) and `DotNetEnv` have zero usages; the `.env` parser was hand-rolled instead. | 12 |
| N16 | **`page-navigation.js` is 466 lines of dead code** shipped to every user. | 11 |
| N17 | **`FileStorageService` has Azure Blob configuration but writes to a local path** — ephemeral on App Service. | 10 |
| N18 | **`Dashboard_Old.cshtml` (152 KB) and `UnitTest1.cs` are untracked/dead leftovers.** | 11, 13 |
| N19 | **`UserTypes` has no `EventPlanner` member** (`UserOrganization.cs:75-81`) while `OrganizationType.EventPlanner` and `RelationshipTypes.EventPlannerClient` both exist. Cross-org access is gated on `UserType == UserTypes.LawFirm` in 11 places across 6 files, so typing an event planner's membership the obvious way silently grants zero client access. | 00, 03 |
| N20 | **`HasLegalContent` gates AI processing on 46 legal keywords with no events terms** (`Certio.Web/Services/AIBackgroundService.cs:219-236`). Reached live via `ChatService.cs:306-308`. Event-planning conversations without a word like "contract" get no AI processing, silently. The Python equivalents were pivoted (`main.py:929-933`, `:976-979`); the C# one was missed. | 00, 08 |
| N21 | **Two terminology sources disagree.** `Organization.GetMatterTerminology()` defaults to "Event" for every organization (`Organization.cs:208-216`); `RoleDisplayHelper.GetMatterTerminology(OrganizationType)` returns "Matter" unless the type is `EventPlanner`. Wording depends on which overload a view calls. | 00 |
| N22 | **No external role fits an events deployment.** `OpposingCounsel`, `ExpertWitness`, `CourtPersonnel`, `RegulatoryBody`, `Other` (`UserOrganization.cs:52-56`) — vendors, venues, and photographers have only `Other`. | 03 |

**Fixed since this appendix was written: N1, N2, N3, N5.** The entries are left as written, with their
original line references, so the verification commands below still make sense against the git history and
so the reasoning stays readable. What changed in each case is summarized in
[module 14](14-design-critique.md#what-has-changed-since-this-critique-was-written). Two notes worth
carrying forward:

- **N1 was incomplete as written.** Fixing it exposed a second cross-tenant leak in the same service:
  `RunId` is globally unique, so the idempotency check in `ProposeActionAsync` could confirm the existence
  of another organization's action. A finding that names four methods can still be understating the
  problem.
- **N5's fix carried the same risk as the defect.** These GUIDs are already persisted, so consolidating
  the copies could itself have orphaned documents silently. The golden-value tests in
  `Certio.Tests/Domain/DeterministicGuidTests.cs` are what make the extraction safe to repeat.

---

## How to re-verify everything

The commands used throughout this curriculum, for when you want to check a claim after the code changes:

```powershell
# Source file count, excluding build output
(git ls-files | Select-String -NotMatch "/(bin|obj)/").Count

# Largest files by layer
Get-ChildItem Certio.Web/Controllers -Recurse -Filter *.cs |
  Sort-Object Length -Descending | Select-Object -First 10 Name,Length

# Static mutable state inventory (root cause 1)
Get-ChildItem Certio.Application,Certio.Web -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String "static\s+(readonly\s+)?(ConcurrentDictionary|Dictionary|HashSet|List|DateTime|SemaphoreSlim)"

# DI lifetime census
Select-String -Path Certio.Web/Program.cs -Pattern "AddScoped|AddSingleton|AddTransient|AddHostedService"

# Tests
(Get-ChildItem Certio.Tests -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-String "\[Fact\]").Count

# Secrets and databases in git
git ls-files | Select-String "\.(env|db|sqlite|pfx|pem|key)$"

# Pivot: every place cross-org access is gated on the LawFirm user type (N19)
Get-ChildItem Certio.Web,Certio.Application,Certio.Domain -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String 'UserTypes\.LawFirm|"LawFirm"'

# Pivot: how far the events terminology layer reaches
Get-ChildItem Certio.Web -Recurse -Include *.cs,*.cshtml |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String "GetMatterTerminology|GetMattersTerminology|GetLegalTerminology|GetRoleDisplayName"

# Pivot: legal vocabulary still encoded as behavior rather than as display text (N20)
Select-String -Path Certio.Web/Services/AIBackgroundService.cs -Pattern "legalKeywords" -Context 0,20
```

---

## A closing note on method

The pattern in this appendix is worth generalizing beyond this codebase. The drift is not random: the
documentation is accurate about **intentions** and inaccurate about **details that changed after it was
written** — migration counts, file locations, whether a feature is enabled, whether a search is semantic.

Where the docs describe *what the team meant to build*, trust them; that intent is real and it explains
why the code looks the way it does. Where they describe *what the code currently does*, verify. And when
a document and the code disagree, the interesting question is not which one is wrong — it is **what
changed in between**, because that usually points at a decision nobody wrote down.
