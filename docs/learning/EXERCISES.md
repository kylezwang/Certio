# Exercises and labs

Labs are grouped by module. Each states a goal, a definition of done, and the trap to avoid — because in
most of these the obvious fix is subtly wrong, and noticing that is the point.

Difficulty: **[R]** reading and tracing, no code. **[S]** small, under an hour. **[M]** medium, a few
hours. **[L]** large, a day or more.

Nothing here requires a running database except where noted.

---

<a id="module-00"></a>

## Module 00 — Orientation

**0.1 [S] Get the stack running.** Start Docker, run `start_services.bat`, load the app, log in, open the
AI panel. **Done when** you have sent one AI message and seen a response.

**0.2 [R] Settle the naming question.** Find every place `Notal` appears and every place `Certio` appears
in infrastructure versus source. **Done when** you can state which name to use in a commit message, a
deploy conversation, and a class name.

**0.3 [R] Map the pivot.** Build a three-column table: concept, what the database stores, what an
`EventPlanner` organization's user sees. Cover at least `Matter`, `ManagingPartner`, `Partner`,
`Associate`, `Paralegal`, and "Legal Team". **Done when** you can predict the on-screen label for any
stored role without opening `RoleDisplayHelper`.

**0.4 [S] Find the terminology conflict.** `Organization.GetMatterTerminology()` and
`RoleDisplayHelper.GetMatterTerminology(OrganizationType)` disagree for at least one organization type.
Construct the case, then find a view that calls each overload. **Done when** you can name an organization
configuration where two pages in the same app show different words for the same entity. **The trap:**
assuming the `Organization` overload is always the one in use — check the call sites, several views take
the enum.

**0.5 [M] Write the pivot inventory.** Produce a list of every place the legal domain is encoded as
*behavior* rather than as a display string: keyword lists, permission grants, enum members that gate
branches. Start from module 14's root cause 5 table and extend it. **Done when** you have found at least
two items not already documented. **The trap:** grepping for `legal` finds the pivoted Python and misses
the unpivoted C#; search for domain nouns instead.

---

<a id="module-01"></a>

## Module 01 — Architecture and layer boundaries

**1.1 [R] Prove the layering violation.** Without opening a `.csproj`, find a piece of *code* that could
only compile if `Certio.Application` referenced `Certio.Infrastructure`. **Done when** you can cite a file
and line and explain what it imports.

**1.2 [R] Inventory the misplaced services.** List every service in `Certio.Web/Services/` whose interface
lives in `Certio.Application/Interfaces/`. **Done when** you can explain what that split costs
`MatterService` specifically, citing the nullable dependency and the log line that fires when it is
absent.

**1.3 [M] Design the inversion.** Sketch the repository interfaces you would add to
`Certio.Application` to invert the dependency for `MatterService` alone. **Done when** you can name the
interfaces, say which project each lives in, and estimate how many call sites change. **The trap:** a
generic `IRepository<T>` looks tidy and will not survive contact with the query filters in module 05 —
decide whether you are hiding EF or just relocating it.

**1.4 [R] Audit `ServiceResult<T>` discipline.** Find three services that return `ServiceResult<T>` and
three that throw or return raw types. **Done when** you can state the actual convention as practiced,
rather than as documented.

---

<a id="module-02"></a>

## Module 02 — The request lifecycle

**2.1 [R] Order the pipeline from source.** Reconstruct the middleware order from `Program.cs` alone,
then predict what breaks if `UseAuthentication` and `UseAuthorization` are swapped. **Done when** your
list matches the file and you can name the failure mode.

**2.2 [R] Trace the four authorization layers.** For one request to a matter details page, list every
place authorization is evaluated, in order, with file and line. **Done when** you can say which two
layers are the same check performed twice.

**2.3 [S] Break the tenancy resolution deliberately.** Find how `orgId` is resolved when the route has no
organization segment. **Done when** you can describe a request where `ClientContext` resolves to an
organization the user did not intend, and say whether any layer catches it.

**2.4 [M] Collapse the duplicate check.** Propose removing one of the two redundant authorization layers.
**Done when** you can list every endpoint that would lose protection and explain why that is or is not
acceptable. **The trap:** the service-level check protects non-HTTP callers — hubs, background work, the
AI executor — so it is not the one to remove.

---

<a id="module-03"></a>

## Module 03 — Identity and multi-tenancy

**3.1 [R] Trace both access mechanisms.** For a planner reaching a client organization, trace direct
membership and provider-based access separately. **Done when** you can state, for each, which table is
consulted and which method produces the permission list.

**3.2 [S] Reproduce the `UserTypes` trap on paper.** Write out the `UserOrganization` rows you would
create for a new event planning company using `UserType = "EventPlanner"`, then walk
`GetFirmRelationshipAsync` line by line with those rows. **Done when** you can state the exact permission
list the planner ends up with and cite the line that produces it.

**3.3 [M] Fix the `UserTypes` gap.** Implement `UserTypes.ServiceProvider`, keeping `LawFirm` as a
deprecated alias so existing rows keep working. **Done when** all eleven call sites accept either value
and the existing tests pass. **The trap:** a straight rename orphans every production row — you need the
alias and a backfill plan, and this is exactly the migration that the strings-instead-of-enums decision
was supposed to make cheap. Verify that claim.

**3.4 [M] Fix the multi-provider bug.** `FirstOrDefaultAsync` on provider membership silently drops the
second one. **Done when** you have a failing test for a freelance planner at two companies and a fix that
passes it. **The trap:** returning a list changes the shape of permission resolution downstream —
decide whether you union the relationships or require the caller to disambiguate, and justify it.

**3.5 [R] Design the events role vocabulary.** The external roles are `OpposingCounsel`,
`ExpertWitness`, `CourtPersonnel`, `RegulatoryBody`, `Other`. Propose the events equivalents. **Done
when** every proposed role has an explicit permission set and you have said what happens to
`ViewAuditLogs`, currently held only by `ClientLawyer`.

---

<a id="module-04"></a>

## Module 04 — The permission system

**4.1 [R] Compute permissions by hand.** For a `LawFirm`/`Paralegal` in a planning company with a
`DocumentOnly` relationship to a client, produce the exact effective permission list. **Done when** your
list matches what the code would produce and you can cite both source lists you unioned.

**4.2 [R] Demonstrate the privilege inversion.** **Done when** you can name a permission that
`ClientMember` has and `ClientManager` does not, and explain why the default role assignment makes this
worse than it looks.

**4.3 [S] Prove the invalidation stub does nothing.** Read `InvalidateUserPermissionsAsync` and trace
what it actually removes from cache. **Done when** you can state the maximum time a revoked permission
remains usable, separately for the L1 and L2 caches.

**4.4 [L] Implement epoch-based invalidation.** Add a per-user version number to the cache key so a bump
invalidates every derived entry at once. **Done when** a permission change takes effect on the next
request and you can state the added cost per check. **The trap:** the L1 memory cache is per-process, so
the epoch must be read from a shared store or you have only fixed one instance.

---

<a id="module-05"></a>

## Module 05 — The data model

**5.1 [R] Find the unfiltered entities.** Determine which soft-deletable entities lack a global query
filter. **Done when** you can name at least one place where deleted rows would appear in a query result.

**5.2 [M] Add the tenancy invariant.** Write a test asserting that a query for org A's matters never
returns org B's, then find an entity where the invariant does not hold. **Done when** the test fails for
a real reason before you fix anything.

**5.3 [R] Audit the audit allowlist.** Compare `ShouldAudit`'s eleven types against the 68 `DbSet`s.
**Done when** you can name the three unaudited entities you would add first and defend the ordering
against a specific dispute scenario — a contested invoice, a disputed approval, a revoked access claim.

**5.4 [S] Trace a soft delete.** Follow one `Remove()` call through `AuditInterceptor` to the row that
ends up in the database. **Done when** you can explain how a hard delete becomes an update and what the
audit row contains.

---

<a id="module-06"></a>

## Module 06 — Workflow: the matter lifecycle

**6.1 [R] Count the `SaveChanges` calls in matter creation.** Trace `MatterService.CreateMatterAsync` end
to end. **Done when** you have an exact count and can say which entities are written by each.

**6.2 [M] Make it atomic.** Wrap matter creation in an explicit transaction so a failure midway leaves no
partial matter. **Done when** you can describe the failure you prevented. **The trap:** the audit
interceptor and the channel-creation side effect both participate — decide what happens to each on
rollback before you write the code.

**6.3 [R] Follow `AccessLevel` end to end.** From `CreateMatterDto.AccessLevel` to the enforcement check.
**Done when** you can state what each level means at the point of enforcement, not at the point of
assignment.

**6.4 [S] Localize the workflow.** List every user-visible string in the creation flow that says "matter"
and determine whether each passes through the terminology layer. **Done when** you can name at least one
string that would show the wrong word to an event planner.

---

<a id="module-07"></a>

## Module 07 — Workflow: real-time messaging

**7.1 [R] Map hub group membership.** For each hub, determine how a connection joins a group and where
that membership is stored. **Done when** you can explain what happens to group membership on reconnect.

**7.2 [R] Find the scale-out failures.** **Done when** you can name three pieces of hub state that are
process-local and describe the user-visible symptom of each on a two-instance deployment.

**7.3 [M] Enable the Redis backplane.** The package is already referenced. **Done when** presence and
message delivery work across two locally running instances. **The trap:** the backplane fixes message
fan-out but not the static dictionaries — verify which symptoms actually go away.

**7.4 [R] Audit hub authorization.** **Done when** you can state, for each hub method, whether it
validates that the caller belongs to the organization whose data it touches.

---

<a id="module-08"></a>

## Module 08 — Workflow: AI chat and the three retrieval systems

**8.1 [R] Prove the embeddings are not embeddings.** **Done when** you can show that
`DocumentVector.Embedding` is populated and never read during search, citing two files.

**8.2 [S] Reproduce the `HasLegalContent` gate.** Write out an eight-message event-planning conversation
that scores zero, and one that passes. **Done when** you can predict pass or fail for any conversation by
inspection, including at least one accidental pass through substring matching.

**8.3 [M] Fix the gate.** Decide between extending the keyword list and deleting the check. **Done when**
you have implemented one, justified it against the other two conditions in `ShouldProcessAIAgents`, and
said what your change does to AI processing volume. **The trap:** extending the list keeps a brittle
allowlist and you will be back here at the next pivot.

**8.4 [R] Map the trust boundary.** **Done when** you can enumerate exactly what a holder of `AI_API_KEY`
can read and write, and name the single change that removes most of that exposure.

**8.5 [L] Design real embeddings.** Which provider call, where in the pipeline, what schema change, what
happens to the existing column, and how existing documents get re-indexed. **Done when** you have a
migration plan that does not require downtime — and a measurement that would tell you whether it was
worth doing.

---

<a id="module-09"></a>

## Module 09 — Workflow: agent actions

**9.1 [R] Draw the state machine.** Every state and every valid transition, with the line that enforces
each. **Done when** your diagram matches `AgentActionService` exactly.

**9.2 [S] Demonstrate the IDOR.** Using only the code, construct the request a user in org A would send
to approve an action belonging to org B. **Done when** you can cite the four methods that fail to check
and explain why the route does not save you.

**9.3 [M] Fix it and prove the fix.** **Done when** the organization check is enforced in the service
layer and a test fails without it. **The trap:** adding the check in the controller leaves the hub and
background callers unprotected — module 02's lesson applies.

**9.4 [R] Trace the client-side parse.** Follow an `[ACTION:...]` block from the LLM response through
`agent-actions.js` to the rendered card. **Done when** you can state what a malicious model output could
cause and which validation is missing.

---

<a id="module-10"></a>

## Module 10 — Documents and external integrations

**10.1 [R] Follow the identity seam.** Trace how an `int` entity ID becomes a `Guid` for the document
subsystem. **Done when** you can explain what happens if two copies of `CreateDeterministicGuid` diverge.

**10.2 [S] Consolidate the duplication.** Extract the six copies into one shared class. **Done when** all
call sites compile against it and you can explain why the failure mode of divergence was silent rather
than loud.

**10.3 [R] Audit the OAuth state.** **Done when** you can explain what `UnprotectState` validates, what
it does not, and how long a captured state value remains usable.

**10.4 [M] Make WOPI tokens survive scale-out.** **Done when** editing works with two instances behind a
load balancer, and you can say what happens to in-flight sessions during a deploy.

---

<a id="module-11"></a>

## Module 11 — The front end

**11.1 [R] Inventory the dead code.** **Done when** you can state how many kilobytes of JavaScript are
shipped to every user and never executed, with evidence for the "never executed" part.

**11.2 [S] Add security headers.** `nosniff`, `X-Frame-Options`, `Referrer-Policy`. **Done when** they
appear on every response and nothing breaks.

**11.3 [S] Fix the CDN risk.** Add SRI to the SignalR tag or serve it locally. **Done when** the page
works and a tampered CDN response would be rejected. Note the client-server version mismatch while you
are there.

**11.4 [L] Make a CSP possible.** Inventory the inline scripts, then propose the extraction order. **Done
when** you can estimate the work and name the first three files to change. **The trap:** a CSP with
`unsafe-inline` is not a CSP; if you cannot remove inline scripts, say so rather than shipping a header
that only looks like protection.

---

<a id="module-12"></a>

## Module 12 — Cross-cutting concerns

**12.1 [R] Inventory static mutable state.** **Done when** you have the file list and can classify each
entry as harmless, degraded, or broken under multi-instance deployment.

**12.2 [S] Prove the Redis fallback is unreachable.** **Done when** you can explain why the `catch` block
around `AddStackExchangeRedisCache` can never execute.

**12.3 [S] Fix the background service registration.** `AIBackgroundService` is a `BackgroundService`
registered with `AddSingleton`. **Done when** you can state what changes if you register it correctly —
and notice that one of its methods is already reachable and running.

**12.4 [M] Add a startup guard.** Fail fast when the app is configured for multiple instances without
Redis. **Done when** a misconfigured deployment produces a clear startup error rather than intermittent
bugs.

---

<a id="module-13"></a>

## Module 13 — Testing, CI, and quality gates

**13.1 [R] Assess the coverage honestly.** **Done when** you can state which subsystems have meaningful
tests and which have none, and defend the existing prioritization or argue against it.

**13.2 [S] Make `/healthz` able to fail.** **Done when** it probes SQL and Redis, and you have confirmed
that the deploy gate in the workflow would actually catch a broken deployment.

**13.3 [S] Run the orphaned Python tests.** Three `test_*.py` files at `ai_agents/` root never run in CI.
**Done when** they run and you have reported how many pass.

**13.4 [M] Add the quality gates.** `ruff`, `TreatWarningsAsErrors`, an `.editorconfig`, a coverage floor.
**Done when** CI fails on a deliberate violation of each. **The trap:** turning all warnings into errors
on a codebase this size is a large first commit — decide whether to ratchet or to fix.

---

<a id="module-14"></a>

## Module 14 — Design critique and a prioritized roadmap

**14.1 [R] Re-derive the root causes.** Without rereading module 14, group the defects from modules 01
through 13 into causes. **Done when** you can defend your grouping — and note where it differs from mine,
because the grouping is a judgment call.

**14.2 [M] Do the Tier 0 items.** Env / appsettings / SQLite history rewrite is already done (Sep 2026);
complete any remaining Tier 0 items (secret scanning, public-visibility checklist) and explain why
Tier 0 ranked above everything else. **Done when** you can verify `git ls-files` shows no `.env` or
`*.db` and you can defend the priority ordering.

**14.3 [R] Argue against three roadmap items.** Pick three and make the strongest case for not doing them.
**Done when** you have changed your mind about at least one. Recognizing which textbook violations are
actually hurting you is the skill being exercised.

**14.4 [L] Write the pivot completion plan.** Sequence the events-industry work: the `UserTypes` rename,
the role vocabulary, the AI keyword gate, the terminology reconciliation. **Done when** you have an
ordering with dependencies, an estimate, and an explicit statement of what you would ship to the first
event-planning customer without finishing. **The trap:** the display layer already looks finished, which
makes it tempting to call the pivot done — lead with the items that fail silently.
