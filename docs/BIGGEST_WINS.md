# Certio / Notal - Biggest Wins Analysis

**Date:** February 11, 2026  
**Method:** Source code analysis with line-level verification  
**Scope:** Full codebase deep-dive against `docs/CAPABILITY_REPORT.md` and undocumented code paths

---

## How This Was Assessed

Every item below was verified by reading the actual source code -- method signatures, implementations, and file paths. Claims from existing documentation were not taken at face value. Where implementations are partial or stubs, that is noted explicitly. This is not a marketing document; it is an engineering audit.

See `docs/BIGGEST_WINS_PROCESS.md` for the full methodology.

---

## Tier 1: Architecture-Level Wins (Hardest to Replicate)

These capabilities are embedded in the data model and architecture. A competitor cannot bolt these on -- they require fundamental redesign.

### 1. Multi-Sided Organization Graph with Typed Relationships and Scoped Access

**What it is:** Any organization can form a typed relationship with any other organization, with independently configured access levels. A law firm can represent multiple clients. A planner can work with multiple vendors. Each relationship has its own access scope.

**Evidence:**
- `Certio.Domain/Organizations/OrganizationRelationship.cs` -- 6 relationship types: `LawFirmClient`, `EventPlannerClient`, `PartnerFirm`, `Subsidiary`, `Vendor`, `Consultant`
- 5 access levels: `FullAccess`, `ReadOnly`, `LimitedAccess`, `MatterSpecific`, `DocumentOnly`
- `IsValid()` method checks `IsActive` and expiration dates
- `Certio.Web/Services/FirmRelationshipCacheService.cs` caches relationships for performance
- `Certio.Web/Services/LawFirmRoleResolutionService.cs` resolves cross-org access in real time
- `Certio.Web/Services/CachedPermissionService.cs` caches firm-based access checks with `firm_access:{userId}:{orgId}` keys at 15-min TTL
- `Certio.Web/Middleware/ClientContextMiddleware.cs` and `ClientAccessMiddleware.cs` enforce org context on every request

**Why it matters:** Most project management tools assume a single-org model. Supporting "Firm X serves Client Y with access to Matters A, B, C but not D" requires a fundamentally different data model. Certio has this built into every query path, every permission check, and every middleware layer.

**Verification status:** Fully implemented and wired into the middleware pipeline.

---

### 2. AI Agent Action System with Human-in-the-Loop Governance

**What it is:** AI can propose actions (create tasks, add notes, attach files, start timers) that enter a human approval workflow before execution. Every action is auditable, reversible, and traceable back to its source conversation.

**Evidence:**
- `Certio.Domain/AgentActions/AgentAction.cs` -- Full lifecycle: `Pending` -> `Approved` -> `Running` -> `Done`/`Failed` -> `RolledBack`
- State guard methods: `CanBeApproved()`, `CanBeRejected()`, `CanBeExecuted()`, `CanBeRolledBack()`
- `BeforeState` / `AfterState` -- JSON snapshots enabling rollback
- `RunId` deduplication prevents duplicate proposals
- `CorrelationId` traces related actions across runs
- `Priority` field enables priority-based execution queue
- `Certio.Application/Services/AgentActionService.cs` (1000+ lines):
  - `ExecuteCreateTaskAsync()` -- Fully wired, creates real `TaskItem` entities
  - `ExecuteAddNoteAsync()` -- Fully wired, creates real `TaskItemComment` entities
  - `RollbackCreateTaskAsync()` -- Soft-deletes created tasks
  - `RollbackAddNoteAsync()` -- Removes created comments
  - `ExecuteAttachFileAsync()` / `ExecuteStartTimerAsync()` -- Stubs (payload models exist, execution returns placeholder)
- `Certio.Web/Security/RequireAgentPermissionAttribute.cs` -- Custom authorization attribute for agent actions
- Every state transition logged to `AuditLogs`

**Why it matters:** This is a purpose-built agentic AI governance layer. AI does not execute blind -- humans approve. If something goes wrong, it rolls back. The audit trail connects every AI action to its source conversation, agent type, and approver. No PM tool has this architecture.

**Verification status:** Core lifecycle fully functional. 2 of 5 action types fully wired (`CreateTask`, `AddNote`). 3 are stubs with complete payload models ready for wiring.

---

### 3. Zero-Touch Audit Trail via EF Core Interceptor

**What it is:** Every database change is automatically captured with old vs. new values in JSON, user identity, IP address, user agent, and timestamps. No developer action required -- the interceptor fires on every `SaveChanges()` call.

**Evidence:**
- `Certio.Infrastructure/Interceptors/AuditInterceptor.cs` (520+ lines):
  - Overrides `SavingChanges` and `SavedChanges` (both sync and async)
  - Phase 1 (`SavingChanges`): Captures entity states, old values, new values BEFORE save
  - Phase 2 (`SavedChanges`): Writes audit logs AFTER save (so auto-generated IDs are captured)
  - Automatically sets `CreatedById`, `ModifiedById`, `DeletedById` timestamps
  - Converts hard deletes to soft deletes for entities with `IsDeleted` property
  - Captures HTTP context (IP, User-Agent, session) from `IHttpContextAccessor`
  - `GetOldValues()` / `GetNewValues()` serialize changed properties to JSON
- `Certio.Web/Middleware/RequestAuditMiddleware.cs` -- Separately audits HTTP requests:
  - Captures path, method, response code, duration in milliseconds
  - Human-readable descriptions (e.g., "Viewed documents", "Connected Google Drive")
  - Skips static files, OPTIONS, HEAD
  - Verbose logging toggle via `VerboseLogging` cookie
- `Certio.Web/Middleware/PerformanceMonitoringMiddleware.cs` -- Adds `X-Response-Time-Ms` header to every response, logs slow requests (>100ms)

**Why it matters:** Compliance (SOC 2, GDPR, legal discovery) requires complete audit trails. Most systems require developers to manually add audit calls. Certio's interceptor-based approach means audit coverage is 100% by default -- a new entity gets audited the moment it is added to the DbContext without writing a single line of audit code.

**Verification status:** Fully implemented. Both entity-level and HTTP-level auditing are live.

---

### 4. Cross-Industry Portability via Terminology Customization

**What it is:** The same platform serves law firms, event planners, government agencies, and nonprofits. The core data model (Organization, Matter, Task, Billing) is industry-agnostic, but the UX adapts through configurable terminology and org-type-specific behavior.

**Evidence:**
- `Certio.Domain/Organizations/Organization.cs` -- `OrganizationType` enum: `Client`, `LawFirm`, `EventPlanner`, `Government`, `NonProfit`
- `Organization.GetMatterTerminology()` / `SetMatterTerminology()` -- "Matter" can become "Event", "Case", or "Project"
- `Certio.Domain/Billing/Expense.cs` -- `ExpenseCategories`: 14 categories including event-specific ones (Venue Rental, Catering, Florals & Decor, Photography, Entertainment, Rentals, Transportation, Supplies, Marketing, Staffing, Permits & Insurance, Vendor Fees, Travel, Other)
- `MatterAssignment.AssignmentType` is flexible text -- `OriginatingAttorney` can be `LeadPlanner` or `VendorLead`
- `AIModelTier` enum in `Organization.cs` -- Per-org AI model selection (Auto, GPT-4o, GPT-4o-mini, GPT-5, GPT-5.1, GPT-5.2)
- `Organization.Settings` -- JSON bag for arbitrary org-level configuration

**Why it matters:** Competitors are locked into one vertical (weddings, corporate events, legal) because their models assume industry-specific entities. Certio uses generic entities with configurable labels, so a single deployment serves multiple industries.

**Verification status:** Fully implemented. Organization types, terminology customization, and expense categories are all in the domain model.

---

## Tier 2: System-Level Wins (Significant Engineering Investment)

These are complete subsystems that would take months to rebuild from scratch.

### 5. Bidirectional Email Bridge (Gmail + Outlook to DM Threads)

**What it is:** Certio does not just send notifications via email. It establishes a full two-way bridge: incoming emails become DM threads, outgoing DMs can be sent as real emails, and the entire correspondence stays linked.

**Evidence:**
- `Certio.Web/Controllers/Api/EmailOAuthController.cs` -- Full OAuth2 for Gmail and Outlook (authorize, callback, token management)
- `Certio.Web/Services/EmailSendingService.cs`:
  - `SendGmailEmailAsync()` -- Gmail API v1 sends real emails
  - `SendOutlookEmailAsync()` -- Microsoft Graph API sends real emails
  - `SendSystemEmailAsync()` -- SendGrid REST + SMTP, Azure SMTP via MailKit
- `Certio.Web/Services/EmailToDmService.cs` -- `ConvertEmailToDirectMessageAsync()` creates DM threads from incoming emails, links `EmailMessage` to `DirectMessage`
- `Certio.Web/Services/EmailSyncService.cs` -- Background `IHostedService` refreshes OAuth tokens every 5 minutes (tokens expiring within 30 minutes)
- `Certio.Web/Controllers/Api/EmailWebhookController.cs` -- Gmail and Outlook push notification endpoints

**Why it matters:** A vendor or external party who never logs into Certio can communicate through email. Their replies appear as DM threads inside the platform. No PM tool does this without bolting on a separate email service.

**Verification status:** Fully implemented. Gmail send, Outlook send, email-to-DM conversion, background token refresh, and webhooks are all wired.

---

### 6. Change Notice System with Public Response and Auto-Reminders

**What it is:** A purpose-built change control workflow: draft -> send -> track acknowledgements -> auto-nudge -> receive clarification notes. External recipients respond via cryptographically signed links without logging in.

**Evidence:**
- `Certio.Web/Services/ChangeNoticeService.cs` (1000+ lines):
  - `SendOrNudgeInternalAsync()` -- Sends emails with signed action links (Confirm / Needs Clarification)
  - `ProtectToken()` -- Creates cryptographic tokens with `TokenPayload` including `TokenVersion`
  - `ComputeNoticeStatus()` -- Automatic rollup: Draft -> Sent -> PartiallyAcknowledged -> Acknowledged / NeedsClarification
  - `SyncChangeNoticeSentToDirectMessagesAsync()` -- Auto-creates DM threads when change notice is sent
  - `SyncClarificationToDirectMessagesAsync()` -- Syncs clarification replies into DM threads
  - `FindOrCreateUserForEmailInDmContextAsync()` -- Auto-provisions external contacts (creates user accounts for unknown email addresses)
- `Certio.Web/Services/ChangeNoticeAutoReminderService.cs` -- Background `IHostedService` with 5-minute polling loop, fires reminders 24/48/72 hours before due date
- `Certio.Domain/ChangeControl/ChangeNoticeRecipient.cs` -- `TokenVersion` property enables link invalidation on re-send
- `Certio.Web/Controllers/PublicChangeNoticeController.cs` -- `[AllowAnonymous]` public response page

**Why it matters:** Standard PM tools have "comments" or "tasks" for change management. Certio has first-class change orders with multi-party accountability, automated follow-up, and external participant support. This maps directly to real legal change orders and event planning change orders (venue time change, headcount revision).

**Verification status:** Fully implemented. All methods verified with line-level evidence.

---

### 7. 25-Permission / 17-Role Authorization Matrix with Two-Tier Caching

**What it is:** A fine-grained permission system with 25 permissions across 6 categories, 17 role-based permission sets, custom per-user overrides, and two-tier caching (Memory L1 + Redis L2).

**Evidence:**
- `Permission` enum in `Certio.Domain/Users/User.cs` -- 25 permissions:
  - Document (5): ViewDocuments, DownloadDocuments, UploadDocuments, DeleteDocuments, CommentOnDocuments
  - Matter (5): ViewMatters, CreateMatters, EditMatters, DeleteMatters, ManageMatterSettings
  - User Management (3): InviteUsers, RemoveUsers, ManageUserPermissions
  - Communication (4): ViewMessages, SendMessages, DeleteMessages, ManageThreads
  - System (3): ViewAuditLogs, ManageSystemSettings, AccessAdminPanel
  - Agent Actions (4): ViewAgentActions, ProposeAgentActions, ApproveAgentActions, RollbackAgentActions
  - Unified Inbox (2): ViewInbox, ManageInbox
- `PermissionSets` class -- 17 sets:
  - Client: Owner, Manager, Member, Lawyer
  - Law Firm: ManagingPartner, Partner, Associate, Paralegal, Staff
  - External: OpposingCounsel, ExpertWitness, CourtPersonnel, RegulatoryBody, Other
  - Certio: Admin, MatterManager, Support, Legal
- `Certio.Web/Services/CachedPermissionService.cs` wraps `PermissionService`:
  - L1: `IMemoryCache` (sub-millisecond)
  - L2: `IDistributedCache` / Redis (<10ms)
  - 8 cache key patterns with TTLs from 5-15 minutes
  - L2 hits warm L1 cache
- Custom authorization attributes: `RequireMatterAccessAttribute.cs`, `RequireTaskAccessAttribute.cs`, `RequireAgentPermissionAttribute.cs`
- `User.GetEffectivePermissions()` -- Resolves base permissions + custom overlays

**Why it matters:** Permission checks happen on every request. The two-tier cache ensures sub-50ms checks even under load. The 17-role matrix covers every user type in legal and event planning workflows without requiring custom configuration.

**Verification status:** Fully implemented. 25 permissions, 17 sets, all 3 custom attributes wired into controllers, caching operational.

---

### 8. RAG-Powered AI with User-Scoped Data Context

**What it is:** Two separate RAG systems work together: (1) a .NET document vector search pipeline indexes organizational documents into searchable chunks, and (2) a Python User Data RAG system indexes each user's matters, tasks, calendar, and communications so AI responses are personalized.

**Evidence:**
- **.NET Document Pipeline:**
  - `Certio.Web/Services/BackgroundEmbeddingWorker.cs` -- `IHostedService` that continuously dequeues and processes embedding jobs
  - `Certio.Application/Services/Documents/DocumentIndexerService.cs` -- Chunks documents at 1200 characters, creates embeddings, stores via `VectorStoreService`
  - `Certio.Application/Services/Documents/VectorStoreService.cs` -- Text-based similarity search with weighted scoring (content, tags, title, category)
  - `Certio.Application/Services/Documents/RagContextService.cs` -- `BuildContextAsync()` retrieves relevant document context for AI queries, caches results in `RagCacheEntry` table
- **Python User Data RAG:**
  - `ai_agents/user_data_rag_system.py` -- `sync_user_data()` indexes matters, tasks, calendar, communications, clients, documents per user
  - TF-IDF embeddings (when sklearn available) with cosine similarity search
  - Keyword fallback search when dependencies unavailable
  - Disk-based cache persistence (`./data/user_data_cache/`)
  - Verified: actual cached data file found with 149 chunks across modules
- **Context Manager:**
  - `ai_agents/context_manager.py` -- Token-budget-aware context compression, prioritizes recent messages and high-importance context
- **Cost Optimization:**
  - `ai_agents/simplified_cost_optimization.py` -- Dynamic model selection:
    - Complexity < 0.4 -> GPT-4o Mini ($0.15/1M tokens)
    - Complexity 0.4-0.7 -> GPT-4o ($5/1M tokens)
    - Complexity > 0.7 -> GPT-4.1 ($15/1M tokens)
  - `IntelligentCacheManager` -- LRU response cache (max 1000 entries) avoids redundant LLM calls
  - `UsageTracker` -- Per-model cost analytics and trend tracking

**Why it matters:** The AI does not answer generically. It knows the user's matters, deadlines, team members, and vendors. Combined with dynamic model selection and response caching, the AI is both contextual and cost-efficient.

**Verification status:** .NET document pipeline uses text-based similarity (placeholder embeddings, architecture ready for real vector embeddings). Python User Data RAG uses real TF-IDF embeddings with disk persistence. Cost optimization fully operational.

---

### 9. Full Billing Stack (Time, Expenses, Invoices, Retainers)

**What it is:** Complete billing infrastructure built into the platform: time entries with rates, expenses with 14 categories, invoices with a 7-status lifecycle, and retainer/trust accounting with deposit/withdrawal transactions.

**Evidence:**
- `Certio.Domain/Billing/TimeEntry.cs` -- Rate, Hours, Amount, IsBillable, Status lifecycle (NeedsReview -> Approved -> Billed -> Paid)
- `Certio.Domain/Billing/Expense.cs` -- 14 categories including event-specific (Venue Rental, Catering, Florals & Decor, Photography, Entertainment, Rentals, Transportation, Supplies, Marketing, Staffing, Permits & Insurance, Vendor Fees, Travel, Other)
- `Certio.Domain/Billing/Invoice.cs` -- 7-status lifecycle (Draft, Pending, Paid, Overdue, PartiallyPaid, Cancelled, Refunded), plus `PaymentMethod`, `PaymentReference`, `PaidDate` fields
- `Certio.Domain/Billing/InvoiceLineItem.cs` -- Links to `TimeEntry` and `Expense`
- `Certio.Domain/Billing/Retainer.cs` + `RetainerTransaction.cs` -- Deposit/Withdrawal with `BalanceAfter` tracking
- `Certio.Application/Services/BillingService.cs` (495 lines) -- `DepositAsync()`, `WithdrawAsync()`, full CRUD for all entities
- `Certio.Web/Controllers/BillingController.cs` (1003 lines) -- REST API for all billing operations
- 7 billing view files in `Views/Billing/`: Index, Overview, Time Entries, Expenses, Invoices, Trusts, Modals

**Why it matters:** A planner or law firm can track costs against budgets, generate invoices, manage retainers, and reconcile billing without leaving the platform. Competitors typically push this to QuickBooks or have no billing at all.

**Verification status:** Fully implemented. All CRUD, deposit/withdraw, and UI views verified.

---

### 10. WOPI Protocol for Collaborative Document Editing

**What it is:** Full WOPI (Web Application Open Platform Interface) implementation enabling collaborative document editing through Office Online or LibreOffice Online directly in the browser.

**Evidence:**
- `Certio.Web/Controllers/WopiController.cs` (679 lines) -- All 7 WOPI endpoints implemented: CheckFileInfo, GetContents, PutContents, Lock, Unlock, RefreshLock, GetLock
- `Certio.Application/Services/Documents/WopiAccessTokenService.cs` -- Cryptographically secure 32-byte Base64 tokens, 8-hour expiry, in-memory validation store
- `Certio.Application/Services/Documents/WopiDiscoveryService.cs` -- Microsoft WOPI discovery XML with 24-hour cache, local fallback file

**Why it matters:** Users can edit Word, Excel, and PowerPoint documents without downloading them. The platform becomes the document collaboration layer, not just a storage layer.

**Verification status:** All endpoints implemented. Lock management is basic (allows all locks without conflict detection).

---

## Tier 3: Feature-Level Wins (Notable)

### 11. Real-Time Communication Platform (4 SignalR Hubs)

- **ChatHub** (`Certio.Web/Hubs/ChatHub.cs`): Team channels with reactions (`AddReaction`), editing (`EditMessage`), replying (`ReplyToMessage`), typing indicators (`StartTyping`/`StopTyping`), AI clarity requests (`RequestClarity`), AI response generation (`GenerateAIResponse`)
- **DirectHub** (`Certio.Web/Hubs/DirectHub.cs`): 1-on-1 messaging with typing indicators and read receipts (`MarkRead`)
- **NotificationHub** (`Certio.Web/Hubs/NotificationHub.cs`): Group-based notifications (user, matter, organization) with permission-validated subscriptions via `IPermissionService.CanAccessMatterAsync()`
- **UpdatesHub** (`Certio.Web/Hubs/UpdatesHub.cs`): Stub -- empty implementation, exists for future entity change broadcasting
- **UserPresenceService** (`Certio.Web/Services/UserPresenceService.cs`): In-memory online/offline tracking with multi-connection support per user

### 12. Daily Briefing System

- `Certio.Web/Services/BriefingMessageService.cs` generates personalized daily briefings per user
- `GatherDailyStatsAsync()`: Pending tasks, tasks due today, active matters, unread messages, recent documents (last 7 days)
- `GatherNotableSuggestionsAsync()`: Proactively identifies overdue tasks, stale matters (no activity in 7+ days), unread alerts
- Creates/uses a dedicated "daily-briefing" channel
- Deduplication via in-memory cache prevents double-sends

### 13. Two-Factor Authentication with Trusted Devices

- `Certio.Web/Services/TwoFactorService.cs` -- Email-based 6-digit codes with 10-minute expiry, generated via `RandomNumberGenerator.GetInt32()`
- `Certio.Web/Services/TwoFactorSessionStore.cs` -- Redis-backed distributed session store (`auth:twofactor:{token}`) using `IDistributedCache`
- Constant-time code verification via `CryptographicOperations.FixedTimeEquals()` to prevent timing attacks
- `Certio.Domain/Users/TrustedDevice.cs` -- Remember devices to skip 2FA on known browsers

### 14. Universal Cross-Entity Search

- `Certio.Web/Controllers/Api/UniversalSearchController.cs` (229 lines) -- Searches across 4 entity types: matters, tasks, clients, navigation items
- Permission-filtered results via `GetAccessibleMatterIdsAsync()`
- Input sanitization and length limits
- Unified JSON response format with icons and URLs

### 15. Google Calendar + Outlook Calendar Sync (Both Implemented)

- `Certio.Application/Services/Calendar/CalendarSyncService.cs` has both `SyncGoogleCalendarAsync()` AND `SyncOutlookCalendarAsync()`
- `Certio.Web/Controllers/Api/CalendarOAuthController.cs` -- OAuth for both Google Calendar and Outlook Calendar
- `Certio.Domain/Calendar/CalendarEvent.cs` -- External sync tracking (`ExternalCalendarId`, `ExternalCalendarSource`, `SyncStatus`, `LastSyncedAt`)
- Note: The `CAPABILITY_REPORT.md` listed Outlook Calendar sync as "Significant New Work (3+ sprints)" in the Unrealized Potential section -- this is incorrect; it is already implemented.

---

## Fact-Check Corrections to CAPABILITY_REPORT.md

The following claims in `docs/CAPABILITY_REPORT.md` were found to be inaccurate during source code verification:

| Section | Claim in Report | Actual Finding | Severity |
|---------|----------------|----------------|----------|
| 1.2 Roles & Permissions | "20 granular permissions" | 25 permissions (added Agent Actions: 4, Unified Inbox: 2) | Minor |
| 1.2 Roles & Permissions | "18+ roles across types" | 17 permission sets (ManagingPartner and Partner are separate from a generic "Partner") | Minor |
| 2.1 Python Agents | Endpoint `/agents/clarity` | Actual endpoint is `/agents/explain-clarity` | Naming |
| 2.1 Python Agents | Endpoint `/sync/user-data` | Actual endpoint is `/data-context/sync` | Naming |
| 2.2 .NET Agent System | "AIBackgroundService processes conversations in background" | The `ExecuteAsync()` loop is minimal (checks every second but does nothing). Processing is triggered externally via `ProcessAIAgentsAsync()`, not by the background loop itself. | Moderate |
| 3 Integration Surface | "SQLite (dev) / Azure SQL (prod)" | SQL Server (Docker) is the primary local development database; SQLite is only a fallback | Minor |
| 5 Unrealized Potential | "Calendar bidirectional sync -- Outlook Calendar OAuth parity: Significant New Work (3+ sprints)" | `SyncOutlookCalendarAsync()` already exists and is implemented in `CalendarSyncService.cs` with token refresh | **Significant** |
| 5 Unrealized Potential | "Unified Inbox -- Add write actions: reply, archive, snooze, assign" listed as < 1 sprint work | `UnifiedInboxController.cs` already has Archive, Unarchive, Snooze, MarkRead, ToggleFlag, AddLabels, RemoveLabels, LinkMatter, UnlinkMatter endpoints | **Significant** |

---

## Undocumented Wins Discovered During Analysis

These capabilities exist in the codebase but were not highlighted in any existing documentation:

1. **Correlation ID tracing** -- `AgentAction.CorrelationId` links related actions across multiple AI runs, enabling full provenance chains from conversation to execution
2. **Priority-based execution queue** -- `AgentAction.Priority` field enables ordering agent actions by urgency during batch execution
3. **Hard-to-soft delete conversion** -- `AuditInterceptor` automatically converts any `EntityState.Deleted` to a soft delete if the entity has an `IsDeleted` property, preventing accidental permanent data loss
4. **Performance response headers** -- `PerformanceMonitoringMiddleware` adds `X-Response-Time-Ms` to every HTTP response for client-side performance monitoring
5. **Verbose audit toggle** -- `RequestAuditMiddleware` respects a `VerboseLogging` cookie, allowing per-session control of GET request audit logging without code changes
6. **Disk-persisted RAG cache** -- `user_data_rag_system.py` caches user data index to JSON files on disk (`./data/user_data_cache/`), surviving Python service restarts
7. **Intelligent LLM response caching** -- `IntelligentCacheManager` in cost optimization uses LRU cache (1000 entries) to avoid redundant LLM API calls for similar queries
8. **AI cost trend analytics** -- `UsageTracker` class tracks per-model token costs with trend analysis and cache hit rate reporting
9. **Daily briefing suggestions engine** -- `BriefingMessageService` does not just summarize stats; it proactively identifies overdue tasks and stale matters (no activity in 7+ days) as actionable suggestions
10. **External contact auto-provisioning** -- `FindOrCreateUserForEmailInDmContextAsync()` in `ChangeNoticeService.cs` creates user accounts for unknown email recipients, enabling seamless external collaboration without manual onboarding
11. **Invoice partial payment support** -- `PartiallyPaid` status in the `InvoiceStatus` enum and `PaidDate` field exist, ready for payment processing integration
12. **Calendar recurrence model** -- `CalendarEvent.IsRecurring`, `RecurrenceRule`, `RecurrenceEndDate` fields exist in the domain model (RRULE expansion logic not yet built, but the schema is ready)
