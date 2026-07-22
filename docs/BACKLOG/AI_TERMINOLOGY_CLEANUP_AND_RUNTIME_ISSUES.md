# AI Terminology Cleanup (Completed) and Runtime Issues (Backlog)

**Status:** Part completed, part deferred (engineering backlog)
**Origin:** July 2026 "Matter vs Event" rebrand cleanup + live server log review

This document has two parts:

1. A record of the "Matter"/"legal" terminology debt that has been fixed so this work isn't re-discovered from scratch.
2. A set of runtime issues (performance, reliability, data quality) observed directly in a live `dotnet run` + `ai_agents` server log while testing the fixes above. These are unrelated to terminology and are tracked here as backlog.

---

## Part 1 — Completed: Notal AI no longer speaks "legal"

Certio/Notal pivoted from a legal practice management platform to an event planning platform. The AI layer (system prompts, product/onboarding knowledge base, RAG context, dashboard prompts) was still deeply legal-flavored, causing the AI to call itself a "legal assistant," offer "legal guidance," and describe features using terms like "Legal Domain Expertise," "Attorney-client privilege," and "Litigation."

### Fixed in this pass

| Area | File(s) | What changed |
|------|---------|---------------|
| AI persona | `ai_agents/main.py` | Removed "legal assistant" persona (4 system prompts); ChatSummarizer, ClientGoalExtractor, ReplySuggester, ClarityAgent prompts reframed from legal-analyst to event-planning-analyst; `legal_areas` practice-area list replaced with event categories; topic-keyword dictionaries (Contract Law/Litigation/IP/Employment Law/etc.) replaced with event-planning topics (Vendor Contracts, Staffing, Venue and Logistics, Guest Experience, Compliance and Permits); `ClarityAgent.legal_glossary` trimmed to contract/vendor-relevant terms; hardcoded fallback strings ("I'm here to help with your **legal** questions") fixed |
| Product knowledge base | `ai_agents/certio_knowledge_base.py` | Full rewrite: was headed `# Notal Legal Services Platform` with a `## Legal Domain Expertise` section (Corporate Law, Contract Law, Employment Law, IP, Litigation, Compliance). Replaced with event categories (Weddings, Corporate Events, Galas and Fundraisers, Conferences and Conventions, Vendor and Venue Management, Compliance and Permits). `user_types` lists (`Lawyer/Partner/Associate/Paralegal`) replaced with `Director/Planner/Coordinator` to match `RoleDisplayHelper`'s existing EventPlanner role-display mapping. Field/method names (`legal_areas`, `legal_requirements`, `legal_domains`, `get_legal_domain_context`) were kept unchanged to avoid a cross-file refactor — only the *content and labels* changed. |
| Onboarding knowledge base | `ai_agents/certio_onboarding_knowledge.py` | Full rewrite. This was the direct source of the reported leak ("...ask questions, request **legal guidance**... explain **legal terms**..."). All guides, FAQs, tutorials, and scenarios rewritten for event planners/clients; `Lawyer/Partner/Associate/Paralegal` → `Director/Planner/Coordinator`. |
| RAG chunk generators | `ai_agents/certio_rag_system.py`, `certio_rag_system_fallback.py`, `certio_rag_system_enhanced.py` | Hardcoded chunks such as `"Legal scenario: Litigation support..."` and `"ClarityAgent simplifies legal language..."` rewritten to event-planning equivalents; `category="legal_domain"/"legal_scenario"` renamed to `"event_category"/"event_scenario"` (these are shown to the LLM via `chunk.category.title()`). |
| Dashboard/chat context (C#) | `Certio.Web/Services/ChatService.cs`, `Certio.Application/Services/UserDataContextService.cs`, `Certio.Web/Services/BriefingMessageService.cs` | `DashboardCardContext` JSON fields (`Matter` → `Event`, `StaleMatters` → `StaleEvents`, etc.), prompt instructions, "Related Matter"/"Legal Requirements" labels, and "active matters" chat text fixed. |
| Duplicate fallback response | `Certio.Application/Services/AIAgentService.cs` | Found a **second, previously-missed copy** of the "AI unavailable" fallback response builder (`GenerateFallbackResponse`) with `contextHint = "I see you need legal guidance. "` — same bug already fixed once in `ChatService.cs`, but this duplicate had been missed. Fixed to event-planning phrasing. |
| Chat UI strings | `Certio.Web/wwwroot/js/chat.js` | "Requires Legal Review" badge → "Requires Review"; "Processing legal inquiry..." thinking indicator → event-planning phrasing; source list ("Legal Documentation", etc.) → event planning equivalents. |
| UI-visible strings (earlier pass) | `MatterController.cs`, `TasksController.cs`, `MatterService.cs`, various `.cshtml`, `matter-form.js` | Hardcoded "Matter" toasts/messages/placeholders → "Event". |

### Explicitly left unchanged (by design)

- Domain/DB identifiers: `Matter` entity, `MatterId`, `Matters` table, EF migrations, `LegalArea` column on `Matter` — renaming these is a schema-level change, out of scope for a terminology/prompt fix.
- Internal Python dict/variable/method names (`legal_area`, `legal_topics`, `legal_requirements`, `requires_legal_review`, `_assess_legal_complexity`, etc.) — kept stable to avoid a wide, riskier refactor across multiple files/serialization boundaries. Only the *values* and *human-readable labels* were changed.
- `ChannelManagementService.cs` default channel `urgent-matters` — intentionally gated to `OrganizationType.LawFirm` only, so it does not apply to `EventPlanner` orgs. Not a bug.

### Verification performed

- `python -m py_compile` on all edited `ai_agents/*.py` files — passes.
- `dotnet build Certio.sln` — 0 errors (pre-existing warnings only).
- Grepped both knowledge-base files for `legal|Lawyer|Paralegal|law firm|litigation|attorney` post-edit — no matches.

### Still worth spot-checking

- `_extract_legal_topics` / `_quick_conversation_analysis` in `ai_agents/main.py` retain the internal key name `legal_topics` (values are now event-planning topics) and an XML-style output tag `<legal_topics>Detected topics</legal_topics>` used in one structured-output prompt. This tag lives inside a hidden `<analysis>` block that is (as far as could be confirmed from this code path) parsed out rather than shown to the end user, but it was not proven end-to-end with a live trace. If "legal" text is ever seen leaking again from a *conversational* AI response (not onboarding/dashboard), start here.
- `AIAgentService.ExplainLegalLanguageAsync` (C# method name) and `Matter.LegalArea` (DB column, still logged as `"Found N matters..."`) were left as-is; see Part 2 for the corresponding log-message cleanup that's still open.

---

## Part 2 — Backlog: Runtime issues observed in server logs

While testing the above fix, a live session log (`dotnet run` web app + `ai_agents` FastAPI service) surfaced several **unrelated runtime issues** worth tracking separately. Evidence is quoted from the log; line numbers refer to the terminal capture reviewed.

### P0 — `GenerateDashboardCardStream` is taking 2–8 minutes per call

```
⚠️ SLOW REQUEST: POST /Client/1/Chat/GenerateDashboardCardStream completed in 147991ms with status 200
⚠️ SLOW REQUEST: POST /Client/1/Chat/GenerateDashboardCardStream completed in 500229ms with status 200
```

500229ms is **8.3 minutes** for a single dashboard card (Daily Briefing / Next Suggestions) generation. This is the most severe issue in the log. It appears to fire on essentially every chat turn (see next item), which compounds the impact. Needs profiling: is this waiting on the Azure OpenAI call itself, on `UserDataContextService` rebuilding the full org context synchronously, or on the repeated Google Drive lookups (see below)?

### P0 — `HttpClient.Timeout` (30s) is shorter than real AI streaming latency, causing mid-stream cancellations

```
Error calling AI streaming service: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
⚠️ SLOW REQUEST: POST /Client/1/Chat/GenerateAIResponseStream completed in 40984ms with status 200
```

Several `GenerateAIResponseStream` calls in this log took 8–41 seconds, i.e. routinely exceeding a 30-second `HttpClient` timeout. When the .NET side cancels, the Python side keeps computing/streaming into a closed socket, producing the cascading failure below. Either raise the timeout for streaming calls specifically (SSE/streaming clients typically should use a much longer or no overall timeout, relying on cancellation tokens instead) or make the cancellation propagate cleanly to the Python process instead of silently letting it run to completion.

### P1 — Hundreds of `socket.send() raised exception` warnings per cancelled stream

```
WARNING:asyncio:socket.send() raised exception.
WARNING:asyncio:socket.send() raised exception.
... (300+ repeats in this log capture)
```

Directly downstream of the timeout issue above: once the .NET client disconnects, the Python streaming generator in `ai_agents/main.py` keeps trying to write SSE chunks to the dead connection on every yield, logging a warning each time instead of detecting the disconnect once and stopping. Needs a `try/except` (or `request.is_disconnected()` check for FastAPI) around the stream-write loop to break out on first failure rather than spamming.

### P1 — Google Drive document downloads fail repeatedly for the same permanently-missing files

```
Google Drive download failed for document 6310e969-c4a4-401c-8ad2-3cb4c96b3615: NotFound - File not found: 1crSxJ9JKsoyGn6fNHtKz8n4GTX0WusgjE9_en_XotLA.
Google Drive download failed for document bf72b6c2-d1ca-42bd-aad4-7ed921f1efc3: NotFound - File not found: 1Bx8wD1hRQcSaUde_53Rv0rmmEi285WLaXsyUvtjfNF4.
```

Both document IDs fail with a 404 **multiple times** across different requests in the same short window — the failure isn't cached, so every dashboard/context build re-attempts the same doomed Google Drive API call, adding latency (see P0 above) and burning API quota. `DocumentContentService` should mark a document as permanently failed (or back off with a TTL) after a `NotFound` response instead of retrying every time context is rebuilt.

### P1 — 96% of documents fail content extraction

```
BuildDocumentsContextAsync: Returning 50 document chunks, 2 with usable content, 48 with failed extraction
BuildDocumentsContextAsync: 48 documents have failed content extraction. Consider reconnecting external integrations.
```

Only 2 of 50 fetched documents have usable content. This significantly limits what the AI can actually ground responses in, and the repeated warning suggests it's a known/live condition rather than a one-off. Worth a dedicated investigation into whether this is mostly the Google Drive 404s above, or a broader extraction pipeline problem (unsupported file types, expired OAuth tokens, etc.).

### P2 — Redundant per-request re-querying of the client-organization list

```
Law firm (Org 1) - including 19 client organizations: 4, 8, 9, 14, 15, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30
Law firm (Org 1) - including 19 client organizations: 4, 8, 9, 14, 15, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30
Law firm (Org 1) - including 19 client organizations: 4, 8, 9, 14, 15, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30
Law firm (Org 1) - including 19 client organizations: 4, 8, 9, 14, 15, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30
```

This identical line is logged 4–5 times within a single `Building user data context for User 1 in Org 1` call — once per sub-context builder (matters, tasks, calendar, documents, ...). Each one appears to independently re-resolve the org's client list instead of computing it once and passing it down. Low-risk, high-value fix: compute once per request and reuse.

### P2 — EF Core multiple-collection-include warning (no `QuerySplittingBehavior` configured)

```
warn: Microsoft.EntityFrameworkCore.Query[20504]
      Compiling a query which loads related collections for more than one collection navigation, either via 'Include' or through projection, but no 'QuerySplittingBehavior' has been configured. By default, Entity Framework will use 'QuerySplittingBehavior.SingleQuery', which can potentially result in slow query performance.
```

This is the same class of issue Phase 1 already fixed for the matter list N+1 (see [`../architecture/PHASE_1_SCALABILITY_FIXES.md`](../architecture/PHASE_1_SCALABILITY_FIXES.md)); this instance wasn't identified/fixed in that pass. Needs the offending query identified (enable query tagging or `.TagWith()`) and either split into `AsSplitQuery()` or restructured to avoid multiple collection includes.

### P3 — GPT-5 tier requested but never configured, silently falls back every time

```
INFO:__main__:Model gpt-5 not configured (AZURE_OPENAI_DEPLOYMENT_GPT5 not set), using fallback: gpt-4o
```

Happens on every "GPT5 tier selected" request in this log. Either configure `AZURE_OPENAI_DEPLOYMENT_GPT5` or stop selecting/reporting a "GPT5" tier that can never actually be served, since it's currently just dead configuration plus a log line on every request.

### P3 — Leftover "Law firm" / "matters" wording in internal log messages

```
info: Certio.Application.Services.UserDataContextService[0]
      Law firm (Org 1) - including 19 client organizations: ...
info: Certio.Application.Services.UserDataContextService[0]
      Found 23 matters for User 1 across 20 organizations
```

Not user-facing, but notable given Part 1 of this doc: `UserDataContextService.cs` still hardcodes "Law firm" and "matters" in `_logger` calls regardless of the organization's actual type/terminology (this org's data is clearly event-planning content — e.g. "Review day-of timeline with bride & groom" — yet the log still says "Law firm"/"matters"). Low priority (logs only), but cheap to fix alongside any future pass through that file.

### Investigate — Is `GenerateDashboardCardStream` supposed to fire on every chat turn?

Every `conversational-response-stream` call in this log is immediately followed by a `GenerateDashboardCardStream` call, and the latter is consistently the slowest operation observed (see P0 above). Worth confirming whether dashboard-card regeneration is intentionally coupled to every chat message (e.g. to refresh Daily Briefing/Next Suggestions after each turn) or if this is an unintended duplicate trigger. If intentional, it should likely be debounced/cached rather than recomputed synchronously and inline with the user's chat request.

---

## Related docs

- [`../architecture/PHASE_1_SCALABILITY_FIXES.md`](../architecture/PHASE_1_SCALABILITY_FIXES.md) — prior N+1/query-splitting fixes (same class of issue as the P2 EF Core item above)
- [`SCALABILITY_AND_ARCHITECTURE.md`](SCALABILITY_AND_ARCHITECTURE.md) — broader scalability backlog
- [`../features/EVENTPLANNER_IMPLEMENTATION_REPORT.md`](../features/EVENTPLANNER_IMPLEMENTATION_REPORT.md) — original Matter→Event aliasing design
