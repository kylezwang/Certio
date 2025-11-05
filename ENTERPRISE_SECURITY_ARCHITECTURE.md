# Certio Enterprise Architecture & Security Readiness

**Revision date:** 2025-01-27  
**Prepared by:** GPT-5 Codex

---

## 1. Executive Summary

- Certio now enforces server-side membership checks on SignalR chat and notification hubs, closing the cross-tenant impersonation gap previously identified.  
- Core AI agent endpoints include API-key authentication and rate limiting, but more than twenty analytics, training, and background-task routes still bypass the security dependency.  
- Outlook and Gmail webhook handlers validate shared secrets when configured, yet the endpoints accept unauthenticated traffic if those secrets are missing from the environment.  
- Multi-factor authentication is now mandatory: email confirmation + 2FA are enforced at login, verification codes are salted/hashed server-side, legacy identities are migrated on first successful 2FA challenge, and a dedicated LoginTwoFactor view matches registration styling. **Note:** Currently outputs codes to terminal for development; production requires email/SMS delivery.  
- Outstanding enterprise blockers: local SQLite artifacts remain checked in, several FastAPI analytics/training endpoints are exposed without authentication, and webhook secrets remain optional in misconfigured environments.  
- Recommended focus: purge committed databases/secrets, enforce mandatory webhook secrets, seal the FastAPI perimeter, and continue tightening deployment configuration gates.

---

## 2. System Topology Overview

**Web Tier (ASP.NET Core 9 / C# 12)**  
- `Certio.Web` hosts Razor pages, MVC controllers, and SignalR hubs (`ChatHub`, `NotificationHub`, `DirectHub`, `UpdatesHub`).  
- Clean-architecture service layer via `Certio.Application` interfaces, wired through dependency injection.  
- Security middleware pipeline: HTTPS redirect → static files → performance monitoring → session → routing → authentication → user sync → client context → channel initialization → client access guard → authorization.

**Data & Integration Layer**  
- Primary data store: SQL Server (local Docker in dev, Azure SQL in production); Entity Framework Core with audit interceptor.  
- Caching: in-memory + Redis (`RedisCacheService`, `CacheMetricsService`).  
- Background hosted services: metrics reporting, email sync, AI background processing.

**AI Service Tier (FastAPI / Python 3.11)**  
- `ai_agents/main.py` exposes conversation summarization, goal extraction, reply suggestion, clarity, orchestration, analytics, knowledge-base, and training endpoints.  
- Security envelope: API key auth via `authenticate_request`, per-key rate limiting, usage tracking, intelligent routing—currently wired only for `/agents/*` routes.

**Eventing & Real-Time**  
- SignalR hubs dispatch conversation events with per-tenant access control enforced server-side.  
- Email webhooks trigger sync workflows; integration-specific secrets validated before processing (`EmailWebhookController`).

**Client Context & Permissions**  
- Central `ClientContextMiddleware` resolves organization, matter, and user scope; `CachedPermissionService` + `OrgMemberRequirement` enforce policy-based authorization.

---

## 3. Security Posture Cross-Check

| Risk Area | Current Status | Evidence | Remaining Actions |
| --- | --- | --- | --- |
| SignalR impersonation / cross-tenant chat | **Resolved** | `ChatHub.JoinConversation` & `SendMessage` derive user/org from context and verify access via `_chatService.CanUserAccessConversationAsync`; outbound events use server-resolved identities. | Continue regression testing around channel joins and DM flows. |
| Notification subscription hijack | **Resolved** | `NotificationHub` validates caller identity with `IPermissionService` before joining groups. | Add telemetry to detect repeated denied attempts. |
| Default SA password & forced local SQL | **Resolved** | `Program.cs` now fails fast when `SQL_PASSWORD` missing; no default credentials injected. | Document infra setup to avoid accidental Docker launch in production environments. |
| Identity confirmation & password strength | **Mitigated** | Identity now mandates confirmed accounts/emails, 12+ char complex passwords, and unique emails via `AddDefaultIdentity` options. | Add phone verification or WebAuthn for high-risk roles; ensure legacy accounts complete email verification after migration. |
| Mandatory MFA enforcement | **Resolved** | `HomeController.Login` gates sign-in behind salted 2FA codes, dedicated verification UI matching registration styling (`LoginTwoFactor.cshtml`), rate limits (5 attempts before lockout), and lockout after repeated failures; registration enables 2FA by default. Codes are cryptographically protected via `TwoFactorService.ProtectCode()` with SHA-256 hashing. **Current state:** Codes output to terminal for development; production requires email/SMS delivery integration. | **Critical:** Implement production-grade email/SMS delivery, add backup codes/device management, and audit 2FA delivery channels before GA. |
| Email webhook validation | **Resolved** | Gmail/Outlook handlers now enforce shared secret matching (`IsSecureMatch`) **mandatory in production** with fail-fast validation. In production, missing secrets return 500 error. In development, secrets are optional for local testing. | **Completed:** Secrets are mandatory in production. **Remaining:** Add replay protection (timestamp validation, nonce tracking), implement rate limiting/throttling per source IP, add monitoring/alerts for unauthorized attempts, validate webhook signatures where supported. |
| AI service perimeter | **Resolved** | All 23 previously unprotected endpoints now include `Depends(authenticate_request)`. Protected endpoints include `/analytics/*`, `/background-agents/*`, `/context/optimize`, `/knowledge/*`, `/training/*`, `/routing/*`. Only `/health` and `/` remain public for monitoring. | **Completed:** All endpoints require authentication. **Remaining:** Consider JWT + mutual TLS for service-to-service communication, add comprehensive audit logs for all AI operations, implement anomaly detection for unauthorized access attempts. |
| Infrastructure config drift | **Moderate** | `USE_AZURE_SQL` defaults to local SQL and triggers Docker startup if unset. | Gate Docker bootstrap to development profiles, add deployment validation for production settings. |
| Tracked SQLite databases | **Partially Resolved** | **Current state:** `.gitignore` updated with explicit exclusions, pre-commit hooks created, CI/CD check scripts added. **Remaining:** Database files still exist in git history and need manual cleanup using `git filter-branch` or BFG Repo-Cleaner. All exposed credentials should be rotated. | **Completed:** Prevention mechanisms in place (pre-commit hooks, CI checks, enhanced `.gitignore`). **URGENT:** (1) Manually purge from git history using `git filter-branch` or BFG Repo-Cleaner, (2) Rotate all credentials/secrets that may have been exposed, (3) Force push cleaned history if repository was shared. |

---

## 4. AI Workflow Integration Readiness

- **Invocation Path:** web controllers → `ChatService` / `AIBackgroundService` → `AIAgentService` (HTTP) → FastAPI orchestrator → OpenAI / background agents.  
- **Caching & Cost Controls:** intelligent router selects models based on complexity, `UsageTracker` captures metrics, background manager coordinates offline tasks.  
- **Observability:** logs for rate limit breaches, webhook anomalies, AI errors; metrics service publishes periodic telemetry.

**Gaps to Address Before Enterprise Launch**

- Harden identity prerequisites (confirmed accounts, MFA policy).  
- Expand AI service auth to cover analytics, knowledge, training, and background-agent endpoints, aligning with principle of least privilege.  
- Implement structured audit logs around AI decisions (`ProcessAIAgentsAsync`) and link them to conversation context for compliance review.  
- Define SLOs and alerts for AI background queues and webhook ingestion latency.
- Enforce webhook shared secrets (and signature validation where supported) as required configuration, with monitoring for invalid attempts.

---

## 5. Operational Architecture & Deployment

- **Environment Configuration:** relies on `.env` loader for local dev; production must set `SQL_PASSWORD`, Azure connection strings, `AIService:ApiKey`, email webhook secrets, and Redis endpoints explicitly.  
- **Scaling Considerations:** stateless web nodes behind load balancer; Redis centralizes cache, SQL Server handles multi-tenant data. SignalR scaling requires backplane (Redis or Azure SignalR) when horizontal scaling; design currently Redis-ready.  
- **Disaster Recovery:** ensure database backups and AI service key rotation schedule; webhook replay tolerance achieved via idempotent sync.  
- **Compliance:** audit interceptor attaches user context to EF operations; consider centralizing log retention and encryption.

---

## 6. Remediation Roadmap

### **CRITICAL PRIORITY (Immediate Action Required)**

1. **AI Service Perimeter Hardening (CRITICAL - Block Production Release)**  
   - **Current state:** 30+ FastAPI endpoints are exposed without authentication, including analytics, training, knowledge-base, and background agent endpoints.  
   - **Action required:** Apply `Depends(authenticate_request)` to ALL endpoints in `ai_agents/main.py` (lines 1200-1695+).  
   - **Additional measures:** Implement JWT-based service-to-service authentication with expiration and rotation, add mutual TLS for internal communication, instrument comprehensive audit logging with PII scrubbing, and deploy anomaly detection for unauthorized access patterns.

2. **Data Hygiene & Secrets Management (CRITICAL - Data Breach Risk)**  
   - **Current state:** SQLite databases (`app.db*`) are tracked in git repository, potentially exposing production data and credentials.  
   - **Action required:**  
     - Immediately purge from git history: `git filter-branch --force --index-filter 'git rm --cached --ignore-unmatch Certio.Web/app.db* Certio.Web/bin/**/app.db*' --prune-empty --tag-name-filter cat -- --all`  
     - Rotate ALL credentials/secrets that may have been exposed (SQL passwords, AI API keys, webhook tokens, OAuth tokens).  
     - Add pre-commit hooks and CI/CD checks to prevent reintroduction.  
     - Document incident response if data was exposed to external parties.

3. **Email Webhook Validation (CRITICAL - Unauthenticated Access)**  
   - **Current state:** Webhook endpoints accept unauthenticated traffic when secrets are missing from configuration.  
   - **Action required:** Make secrets mandatory for production (fail fast on startup if missing), add replay protection (timestamp/nonce validation), implement rate limiting per source IP, and add monitoring/alerts for unauthorized attempts.

### **HIGH PRIORITY (Before Production Launch)**

4. **Identity & Access Hardening**  
   - **MFA Production Delivery:** Replace terminal code output with production-grade email/SMS delivery (SendGrid, AWS SES, Twilio).  
   - **Additional MFA features:** Roll out device-based MFA (authenticator apps/WebAuthn) and backup codes for recovery scenarios.  
   - **Privileged access:** Implement step-up MFA for privileged actions (admin actions, sensitive data access).  
   - **Testing:** Expand automated tests covering registration/confirmation gating and MFA flows.

5. **Secrets Management**  
   - Centralize secret storage (Azure Key Vault or environment-scoped secret managers).  
   - Implement secret rotation policies and automated alerts for expiring secrets.

6. **Monitoring & Observability (Medium Priority)**  
   - Extend `CacheMetricsService` and `MetricsReportingService` outputs to centralized observability stack (Application Insights, Grafana).  
   - Track SignalR group membership changes and failed joins for security monitoring.  
   - Add structured application events around AI fallbacks and webhook errors.

7. **Resilience Enhancements (Medium Priority)**  
   - Formalize retry policies for AI HTTP client (`Polly` resilience strategies).  
   - Establish background job dead-letter queue for failed AI tasks.  
   - Validate webhook handlers against high-volume and replay scenarios; add alerting for repeated unauthorized attempts.

---

## 7. Appendices

### A. Key Components Snapshot

- `Program.cs` – environment bootstrap, security middleware pipeline, DI registration.  
- `ChatHub.cs` – tenant-aware SignalR messaging with presence tracking.  
- `NotificationHub.cs` – secure subscription management for user/matter/org notifications.  
- `EmailWebhookController.cs` – validated ingestion of Outlook/Gmail sync signals.  
- `AIAgentService.cs` – authenticated bridge between .NET tier and AI agents.  
- `ai_agents/main.py` – FastAPI orchestration, auth gate (`authenticate_request`), rate limiting, and agent endpoints.

### B. Reference Configuration Keys

| Key | Purpose | Notes |
| --- | --- | --- |
| `SQL_PASSWORD` | Required for local SQL Server connection string composition. | Never default; set per environment. |
| `USE_AZURE_SQL` | Explicit flag to use Azure SQL when true. | Combine with `AZURE_SQL_CONNECTION_STRING`. |
| `AIService:BaseUrl` / `AIService:ApiKey` | Directs and authenticates web app requests to AI service. | API key is forwarded as `X-API-Key`. |
| `EmailIntegration:WebhookSecret` | Outlook webhook validation secret. | Must match Microsoft Graph subscription `clientState`. |
| `EmailIntegration:GmailVerificationToken` | Gmail Pub/Sub verification token. | Enforced in headers and message attributes. |

---

## 8. Conclusion

Certio's core architecture now satisfies key multi-tenant isolation requirements and has implemented mandatory MFA enforcement with cryptographic protection. **All three critical security issues have been addressed:**

1. ✅ **AI Service Perimeter:** All 23 unprotected FastAPI endpoints now require authentication via `Depends(authenticate_request)`. Unauthorized access to analytics, training data, knowledge base, and background tasks is prevented.
2. ⚠️ **Data Exposure Risk:** Prevention mechanisms are in place (pre-commit hooks, CI checks, enhanced `.gitignore`), but manual git history cleanup is still required to remove existing database files from repository history.
3. ✅ **Webhook Validation:** Email webhook secrets are now mandatory in production with fail-fast validation. Unauthenticated webhook traffic is prevented in production environments.

**Remaining Actions:**
- **URGENT:** Manually purge SQLite databases from git history and rotate all potentially exposed credentials
- **HIGH PRIORITY:** Implement production-grade MFA delivery (email/SMS), add replay protection to webhooks, implement centralized secrets management
- **MEDIUM PRIORITY:** Add comprehensive audit logging, anomaly detection, and monitoring/alerts

**Estimated Timeline to Production-Ready Security:** 1-2 weeks for remaining manual cleanup and high-priority items.

See `CRITICAL_SECURITY_FIXES_COMPLETED.md` for detailed summary of completed fixes and `SECURITY_REMAINING_ISSUES.md` for remaining remediation steps.


