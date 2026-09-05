# Module 14 — Design critique and a prioritized roadmap

**Time:** 2 hours. **Prerequisite:** everything.

This module is the synthesis. It answers three questions: what did this codebase get right, what are its
real problems, and what would you actually do about them, in order.

A note on tone. Nearly everything criticized here was a reasonable decision at the time it was made. The
purpose of a critique is not to assign blame but to separate **decisions that are still serving the
project** from **decisions that have been outgrown** — because those need different responses.

---

## What this codebase gets right

Start here, and mean it. A curriculum that only lists problems teaches you to be cynical rather than
useful, and it would misrepresent the code.

**The security model is unusually thorough for a project this age.** Four independent authorization
layers (module 02), 23 permissions across 6 categories, imperative checks inside services rather than
only at the edge, and hub methods that derive identity server-side rather than trusting client
parameters (module 07). Over 90 percent of test lines target security (module 13). Cookie hardening is
stricter than most production apps ship (module 11). Whoever set the direction understood that a
multi-tenant platform's primary risk is cross-tenant disclosure — one client's budget, guest list, or
vendor pricing reaching a competitor.

**The audit trail is real.** `AuditInterceptor` (module 05) captures old and new values as JSON at the
`SaveChanges` boundary, converts hard deletes to soft deletes so records are never destroyed, and uses
`AsyncLocal` to hold per-request state in a singleton — a genuinely sophisticated technique that most
developers never reach for.

**The OAuth implementation is textbook.** Data Protection-signed `state` carrying identity across a
`SameSite=Strict` boundary, purpose-scoped protectors separating state from tokens, and an honest comment
explaining why `[AllowAnonymous]` is correct (module 10). Someone reasoned carefully about a subtle
problem and got it right.

**`ServiceResult<T>` is a well-chosen pattern** (module 01). Expected failures are return values,
exceptional failures are exceptions, and controllers stay thin. Applied consistently across 28 services.

**The AI integration is architecturally sound.** Putting the LLM work in a Python service is right — the
ecosystem is there, and it isolates a fast-moving dependency from the transactional core. The
propose/approve/execute state machine for agent actions (module 09) means the LLM never writes directly
to the database. A human approves every mutation. That is the correct design for AI in a product that
moves client money and commits vendors,
and plenty of teams have shipped worse.

**Local development actually works.** Docker Compose, smart connection-string fallback, optional Redis,
and `start_services.bat` mean you can run the whole stack offline. That is real engineering effort, and
it is why module 00 is short.

**Deployment has real gates.** Tests block production (module 13). Migrations are reviewed by a human
before they touch client records. The pipeline shape is correct.

---

## The five root causes

Nearly every problem in modules 01 through 13 traces to one of five decisions. Fixing symptoms one at a
time is how a codebase stays broken; the leverage is at the root.

### Root cause 1: single-instance assumptions, documented but not enforced

**Where it shows up:** modules 02, 04, 07, 08, 10, 12.

Thirteen files hold mutable `static` state (module 12's table). SignalR presence, WOPI tokens, chat group
tracking, the embedding queue, rate-limit gates, and sync throttles are all process-local. Redis is
optional, and when absent `AddDistributedMemoryCache` gives you an `IDistributedCache` that is not
distributed — silently breaking 2FA login and permission caching on a second instance.

**Why it happened:** every one of these was locally correct. A `ConcurrentDictionary` is the obvious way
to track typing indicators when you have one server. No individual decision was wrong.

**What makes it systemic:** *not* a lack of awareness.
`docs/BACKLOG/SCALABILITY_AND_ARCHITECTURE.md` names this as P0, states the single-instance limitation
outright, and sets the right trigger for pulling the work. The problems are narrower than "nobody knew":
the documented inventory covers **4 of the 13** components, missing `WopiAccessTokenService` and
`DocumentContentService`; and **nothing in the code or the pipeline enforces the constraint**, so the
instance count in the Azure portal is one click away from breaking login. Thirteen developers-worth of
locally-correct decisions accumulated because the constraint lived in a backlog file instead of a startup
check.

**Why it is dangerous:** these bugs are invisible to tests, code review, and a single-instance staging
environment. They appear the first time someone scales out, all at once, as unrelated-looking symptoms:
users show offline, some logins fail, Office editing breaks intermittently. Diagnosing that as one cause
is genuinely hard.

**The fix is cheap and mostly non-code:** write the constraint down, add a startup check that fails when
`Production` + multiple instances + no Redis, and migrate the three highest-impact stores (WOPI tokens,
presence, 2FA sessions) to `IDistributedCache` and the SignalR Redis backplane that is **already
referenced in the `.csproj`**.

### Root cause 2: two identity schemes that never converged

**Where it shows up:** modules 03, 05, 08, 10.

Users exist twice: ASP.NET Identity's `AspNetUsers` (string keys) and the domain `Users` table (int
keys), reconciled by `UserSyncMiddleware` on every request. Separately, the documents and RAG subsystem
uses `Guid` keys for organizations, users, and matters while the core uses `int` — with no foreign keys
between them, bridged by `CreateDeterministicGuid`, **copy-pasted into six files** (module 10).

**Why it happened:** ASP.NET Identity's schema is opinionated and the team wanted domain control, so they
built alongside it rather than customizing it — a common and often correct choice. The `Guid` seam looks
like the documents subsystem was built later or by someone with different conventions, then integrated
rather than reconciled.

**What it costs, concretely:** no referential integrity on `Document.OrgId`; no way to join documents to
organizations in SQL; a *dropped audit-logging feature* with a comment explaining that the FK types did
not match (`DocumentIndexerService.cs:133`); a per-request sync middleware in the hot path; and six
copies of a hash function that must agree byte-for-byte forever, whose divergence would silently hide one
tenant's documents rather than throw.

**Why it is the hardest to fix:** it is a data migration on production client records, touching every
Guid-keyed table. That is exactly why the deterministic-GUID bridge was built. The honest assessment is
that this is a deliberate multi-week project, not a refactor — but the *first* step is free: extract
`CreateDeterministicGuid` into one shared class today, before a seventh copy appears.

### Root cause 3: security enforced at call sites instead of by invariants

**Where it shows up:** modules 04, 05, 06, 09.

Tenant isolation is achieved by every query remembering to filter on `OrgId`. There is **no global query
filter for organization**, even though the same `DbContext` uses reflection to apply global soft-delete
filters (module 05) — so the mechanism exists and was not used for tenancy.

**The consequence is arithmetic.** With N query sites, tenant isolation holds only if all N are correct.
Module 09's IDOR is exactly one of those N failing: `AgentActionService` loads an action by integer ID and
never checks the organization, so a user with `ApproveAgentActions` in their own org can approve an action
in someone else's by guessing a sequential ID. It is not a careless bug — it is the *expected* outcome of
a design where correctness must be re-established at every call site.

**Why it happened:** global query filters for tenancy need the current organization at `DbContext`
construction time, which means an `IOrganizationContext` injected into the context — real design work.
Filtering per query is the path of least resistance, and it works until it doesn't.

**Why this is the highest-value structural fix:** it converts a property that must be *verified N times*
into one *enforced once*. It also makes the next tenancy bug impossible rather than merely absent.

### Root cause 4: conventions without enforcement

**Where it shows up:** modules 04, 11, 12, 13.

The repository has a `.cursorrules` file specifying no emojis, thin controllers, type hints in Python, and
`ServiceResult<T>` everywhere. Actual state: emojis in `Program.cs` startup output and
`CachedPermissionService` logs; a 1,275-line `DocumentsApiController` and a 2,767-line
`HomeController`; no Python linter; coverage
collected and discarded; no `.editorconfig`; no `TreatWarningsAsErrors`; nullable enabled but violations
only warn.

**The pattern:** every rule that a machine checks is followed. Every rule that only a human checks has
drifted. That is not a statement about this team's discipline — it is how software works, and it is the
cheapest category of problem to fix. A linter in CI ends the entire class permanently.

### Root cause 5: a domain pivot treated as a rename

**Where it shows up:** modules 00, 03, 04, 05, 08.

The product is moving from legal practice management to the events industry, and the work done so far is
real: `RoleDisplayHelper` remaps every user-visible term across 109 call sites, organizations can
override the vocabulary per tenant, `OrganizationType.EventPlanner` and
`RelationshipTypes.EventPlannerClient` exist, and the Python service's category and keyword lists are
fully converted. Judged as a renaming effort it is nearly finished.

But the old domain was not only encoded in *words*. It was also encoded in *behavior*, and behavior does
not turn up when you search for "legal":

| Where the old domain is encoded as behavior | Consequence |
|---|---|
| `UserTypes` has `LawFirm` but no `EventPlanner`, and eleven call sites gate cross-org access on it | Typing a planner's membership the obvious way grants zero client access, silently (module 03) |
| `HasLegalContent` requires one of 46 legal keywords | AI processing skips operational event conversations entirely (module 08) |
| `ClientLawyer` is the only client role holding `ViewAuditLogs` | The role cannot simply be dropped from the vocabulary (module 04) |
| External roles are `OpposingCounsel`, `ExpertWitness`, `CourtPersonnel`, `RegulatoryBody`, `Other` | Vendors and venues have only `Other` to occupy (module 03) |
| `AssignmentType` mixes `OriginatingAttorney` with `Vendor` and `Guest` | The assignment vocabulary is half-converted in one enum (module 05) |

Every one of these fails **silently**. Nothing throws, nothing logs an error, and the UI keeps showing
correct events terminology while the behavior underneath is still legal-shaped. That combination —
correct-looking surface, wrong-behaving core — is the most expensive kind of defect to diagnose, because
the evidence a user reports points away from the cause.

**The generalizable lesson:** when you pivot a domain, the rename is the easy half and you can tell when
it is done. The hard half is auditing every place the old domain is encoded as a *decision* — keyword
lists, permission grants, enum members that gate branches, seed data, prompts. Those have no shared
vocabulary to grep for. The way to find them is to trace the new domain's primary workflows end to end
and watch for branches that were written with the old domain's assumptions, which is precisely what
modules 03 and 08 do.

---

## The prioritized roadmap

Ordered by value divided by effort. The first tier is a few days total and eliminates the known
exploitable defects.

### Tier 0 — secrets and tracked artifacts

**Status (September 2026): history rewrite is done** for env files, secret appsettings / `bin` copies,
`Certio.Web/app.db` (+ `bin` copies), and `.vs/slnx.sqlite`. Paths were stripped with
`python -m git_filter_repo` and force-pushed to `main`, `production`, `mac-development`, and
`desktop-development` (see `docs/operations/MAKE_REPO_PUBLIC_SAFELY.md`). Rotation/revocation of keys
that had been in those files remains mandatory for anyone who cloned before the rewrite.

**Why this was Tier 0.** The files had been tracked despite `.gitignore` (ignore rules do not untrack
files already in the index). Publishing without a history purge would have made API keys and possible
Identity hashes permanently public.

Verify the purge yourself:

```powershell
git ls-files | Select-String "\.env$"
git ls-files | Select-String "\.(db|sqlite)$"
git log --all --oneline -- .env
git log --all --oneline -- ai_agents/.env
git log --all --oneline -- Certio.Web/app.db
```

Expect no hits. On Windows use `python -m git_filter_repo` (underscores); `git filter-repo` often is not
on PATH, and each run removes `origin` until you re-add it.

**Still open under Tier 0:**

1. Add secret scanning to CI / enable GitHub secret scanning so the next leak is caught automatically.
2. Only then change repository visibility to public (runbook step 5), after a final GitHub tree + code
   search check.

### Tier 1 — do this week

| # | Change | Why | Effort | Status |
|---|---|---|---|---|
| 1 | **Add org validation to `AgentActionService` mutations** | Known IDOR, module 09. Any authenticated user can approve another organization's action. | hours | done |
| 2 | **Add a test for #1**, then for tenant isolation generally | Proves the fix and catches the next one | hours | done |
| 3 | **Make `/healthz` probe SQL and Redis** via `AddHealthChecks` | Module 13: the final production deploy gate currently cannot fail | hours | done |
| 4 | **Add security headers middleware** (`nosniff`, `X-Frame-Options`, `Referrer-Policy`) | Module 11: five lines, zero risk, no refactor | hours | done |
| 5 | **Extract `CreateDeterministicGuid` to one shared class** | Module 10: six copies that must agree forever | hours | done |
| 6 | **Implement `CachedPermissionService` invalidation** | Module 04: it is a stub, so revoked permissions persist up to the TTL | day | open |
| 7 | **Add SRI to the SignalR CDN tag, or serve it locally** | Module 11: unauthenticated script execution on an authenticated page | hour | open |
| 8 | **Fix or delete `HasLegalContent`** | Module 08: a 46-keyword legal allowlist silently disables AI processing for event-planning conversations | hour | open |

Items 1–5 have been implemented; see "What has changed since this critique was written" at the end of this
module. The findings above are kept in their original form so the reasoning stays legible, and because the
root causes they came from are still present.

### Tier 2 — this quarter

| # | Change | Why | Effort |
|---|---|---|---|
| 9 | **Global query filter for `OrganizationId`** | Root cause 3. Converts N call-site checks into one invariant. | week |
| 10 | **Rename `UserTypes.LawFirm` to `ServiceProvider`**, keeping the old value as a deprecated alias | Module 03: `UserTypes` has no `EventPlanner`, so the pivot's most likely onboarding mistake silently grants zero cross-org access | days |
| 11 | **Write down the deployment topology**, add a startup check for multi-instance without Redis | Root cause 1. Turns a mysterious bug class into a startup error. | days |
| 12 | **Move WOPI tokens, presence, and hub groups to Redis**; enable the SignalR backplane already in the `.csproj` | Root cause 1's three highest-impact instances | week |
| 13 | **Durable embedding queue** (a table is enough) | Module 10: restarts silently drop indexing jobs | days |
| 14 | **Add linters to CI** — `ruff` for Python, `TreatWarningsAsErrors`, an `.editorconfig`, a coverage floor | Root cause 4. Ends the class permanently. | days |
| 15 | **Add observability** — App Insights or OpenTelemetry, correlation IDs across the .NET/Python boundary | Module 12: no telemetry at all today | week |
| 16 | **Server-side validation of LLM action payloads** | Module 09: currently parsed and rendered client-side | days |
| 17 | **Reconcile the two terminology sources** | Module 00: `Organization.GetMatterTerminology()` defaults to "Event" for all orgs while `RoleDisplayHelper`'s enum overload defaults to "Matter", so wording depends on which overload a view calls | days |
| 18 | **Delete dead code** — `page-navigation.js`, `UpdatesHub`, `Dashboard_Old.cshtml`, `UnitTest1.cs`, `SemanticKernel`, `DotNetEnv` | Modules 11–13. Reduces surface and reader confusion. | day |

### Tier 3 — deliberate projects

| # | Change | Why | Effort |
|---|---|---|---|
| 19 | **Real embeddings for document RAG** | Module 08: retrieval is lexical, so it cannot match paraphrase — the core AI value proposition | weeks |
| 20 | **Unify the `int`/`Guid` identity seam** | Root cause 2. A data migration on production records. | weeks |
| 21 | **Fix the `Application → Infrastructure` reference** | Module 01: the Clean Architecture violation that makes services untestable without EF | weeks |
| 22 | **Split the largest files** — a 6,450-line Razor partial, a 4,970-line layout, a 2,767-line controller, a 969-line `Program.cs`, a 4,283-line `main.py` | Reviewability; mechanical but large | ongoing |
| 23 | **Extract inline scripts, then enforce CSP** | Module 11: the prerequisite for a meaningful CSP | weeks |
| 24 | **Decide the events role vocabulary and back it with permission sets** | Modules 03–04: `ClientLawyer` is the only client role with `ViewAuditLogs`, and external roles are still `OpposingCounsel`/`CourtPersonnel`/`ExpertWitness` | weeks |

**On items 19 and 21: consider not doing them.** Lexical retrieval may be adequate if users search for
terms that appear in their documents — measure before you rewrite. And the `Application → Infrastructure`
reference is a textbook violation that has cost this project relatively little in practice; the strongest
argument for fixing it is testability, which you could also buy with integration tests against an
in-memory provider. **Recognizing which textbook violations are actually hurting you is a more valuable
skill than recognizing violations.**

---

## Three lessons worth generalizing

**1. Optional infrastructure becomes load-bearing without telling you.** Redis was made optional for a
good reason. Then 2FA sessions, permission caching, and presence were built assuming a shared cache.
Nobody decided Redis was required; it became required. The lesson: when you make a dependency optional,
you owe the codebase a *test or check* that the degraded path still works — otherwise "optional" quietly
becomes "broken in one configuration nobody exercises."

**2. A workaround is a decision that needs a deadline.** `CreateDeterministicGuid` was correct damage
control. Left unowned, it spread to six files and silently deleted an audit-logging feature. Workarounds
do not stay contained; they become the pattern others copy. Write down what would need to be true to
remove one.

**3. The failure modes that survive are the silent ones.** Almost every defect in this curriculum fails
quietly: `typeof` checks swallow renamed functions; `AddDistributedMemoryCache` satisfies the interface
and loses the data; a `BackgroundService` registered as a singleton never starts; `/healthz` always says
yes; a missing tenancy check returns another firm's data with a 200. Loud failures get fixed in a day.
**When you are choosing between an error and a fallback, prefer the error** — and when you must have a
fallback, make it emit something a human will actually see.

---

## Check yourself

1. Pick the three changes you would make first and defend the ordering to a skeptical engineering
   manager who wants features instead. Use blast radius, not code cleanliness.
2. Argue *against* fixing the `Application → Infrastructure` reference. What is the strongest case for
   leaving it, and what evidence would change your mind?
3. Root cause 1 produced thirteen instances of the same mistake by thirteen locally-correct decisions.
   What artifact — not code — would have prevented it?
4. The AI system is architecturally sound but retrieval is lexical. Design the smallest experiment that
   tells you whether real embeddings are worth weeks of work.
5. Every mechanically-checked rule is followed; every human-checked rule has drifted. Which three checks
   would you add first, and which rule in `.cursorrules` is not worth enforcing at all?
6. You have one week and one engineer. Write the plan, and say explicitly what you are choosing not to do.

---

## What has changed since this critique was written

Tier 1 items 1–5 have been implemented. If you are reading a module that describes one of these as a live
defect, the module is describing the code as it was found, which is still the more useful thing to study —
the reasoning is the lesson, not the patch.

**1 and 2 — the `AgentActionService` IDOR.** Every mutating and reading method on the service now takes
`organizationId` as a required first parameter and filters on it, so a caller cannot express a
cross-organization request. Pushing the check into the service rather than the controller means the
compiler enforces it: a new controller cannot forget the parameter. Fixing it surfaced a second leak
that the original critique missed — `RunId` is globally unique, so the idempotency lookup in
`ProposeActionAsync` could confirm the existence of another tenant's action. It now looks up unscoped,
compares the organization, and returns `RUNID_CONFLICT` without echoing the other tenant's data. Tenant
isolation tests in `Certio.Tests` cover both.

**3 — health checks.** `/healthz` is now a readiness probe that verifies SQL connectivity and a
distributed-cache round trip; `/livez` is a separate liveness probe that only reports that the process is
up. Conflating the two is why a Redis outage would otherwise take instances out of rotation instead of
degrading them. The checks carry 5s and 3s timeouts because the deploy workflow gives up after 15s — an
unhealthy check that answers too slowly is reported as a timeout, which looks like a different problem.

**4 — security headers.** `SecurityHeadersMiddleware` sets `nosniff`, `X-Frame-Options: SAMEORIGIN`,
`Referrer-Policy: strict-origin-when-cross-origin`, and `X-Permitted-Cross-Domain-Policies: none`. CSP is
deliberately excluded: the inline scripts and CDN references in the views mean any useful policy would
either break pages or be permissive enough to be theater.

**5 — the deterministic GUID.** All six copies are gone, replaced by
`Certio.Domain.Identity.DeterministicGuid` with named helpers (`ForOrganization`, `ForUser`, `ForMatter`)
so the namespace prefix cannot be mistyped. The delicate part was proving equivalence: this function's
output is already persisted in the Documents tables, and a changed algorithm does not throw — it returns
GUIDs that match no rows, so documents quietly vanish. `DeterministicGuidTests` pins seven known outputs
whose expected values were derived independently of the C# implementation, so the test constrains the
storage format rather than restating the code. Note that this consolidation removes the *divergence*
risk, not root cause 2: the `int`/`Guid` seam itself is still there, and remains Tier 3 item 20.

---

## Where to go next

- **Sharpen your read of the evidence:** [`APPENDIX-evidence.md`](APPENDIX-evidence.md) lists every
  documentation claim that does not match the code, with the verification command for each.
- **Build something:** the labs in [`EXERCISES.md`](EXERCISES.md) implement most of Tier 1.
- **Reread module 01** now that you have finished. The architecture diagram means something different
  the second time.
