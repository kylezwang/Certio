# Critical Security Issues Remaining for Maximum Security

**Last Updated:** 2025-01-27

## 🚨 CRITICAL PRIORITY (Block Production Release)

### 1. AI Service Perimeter - Unauthenticated Endpoints (CRITICAL)

**Risk Level:** CRITICAL - 30+ endpoints exposed without authentication

**Current State:**
- Only 8 `/agents/*` endpoints have `Depends(authenticate_request)`
- 30+ endpoints are completely unprotected:
  - `/analytics/*` (usage, cost-optimization, task-complexity, background-agents, context-optimization, cost-dashboard, cost-report, routing-performance, optimization-summary)
  - `/background-agents/*` (submit-task, task-status)
  - `/context/optimize`
  - `/knowledge/*` (search, stats, add)
  - `/training/*` (add-conversations, update-performance, recommendations, stats, should-retrain)
  - `/routing/*` (optimize-rules, route-task)
  - `/health`, `/`

**Required Actions:**
1. Apply `Depends(authenticate_request)` to ALL endpoints in `ai_agents/main.py`
2. Implement JWT-based service-to-service authentication with expiration
3. Add mutual TLS for internal service communication
4. Deploy comprehensive audit logging with PII scrubbing
5. Implement anomaly detection for unauthorized access patterns
6. Add rate limiting per API key and per endpoint

**Impact:** Attackers can access internal analytics, manipulate training data, submit background tasks, and access knowledge base without authentication.

---

### 2. SQLite Databases in Git Repository — **RESOLVED (history, Sep 2026)**

**Risk Level:** Was CRITICAL — Potential data breach and credential exposure

**Status:** Purged from git history with `python -m git_filter_repo` and force-pushed (see
`docs/operations/MAKE_REPO_PUBLIC_SAFELY.md`). Paths removed included `Certio.Web/app.db`,
`bin/Debug/**/app.db`, and `.vs/slnx.sqlite`. Verify with `git ls-files | Select-String "\.(db|sqlite)$"`
(expect empty).

**Still recommended:** Keep `.gitignore` / pre-commit / CI guards so DBs are not re-added; treat any
pre-rewrite clone as having seen DB contents.

**Historical notes (for context):**
- Files had been tracked: `Certio.Web/app.db`, copies under `bin/Debug/`, etc.
- May have contained Identity password hashes or local data

**Impact if reintroduced:** Same as before — do not commit local databases.

---

### 3. Email Webhook Validation - Optional Secrets (CRITICAL)

**Risk Level:** CRITICAL - Unauthenticated webhook access

**Current State:**
- Webhook secrets are **optional** - if `EmailIntegration:GmailVerificationToken` or `EmailIntegration:WebhookSecret` are missing, endpoints accept unauthenticated traffic
- No replay protection
- No rate limiting per source IP
- No monitoring/alerts for unauthorized attempts

**Required Actions:**
1. **Make secrets mandatory for production:**
   - Fail fast on application startup if secrets are missing in production environment
   - Add configuration validation middleware
2. **Add replay protection:**
   - Validate timestamp (reject requests older than 5 minutes)
   - Implement nonce tracking to prevent replay attacks
   - Store seen nonces in Redis with TTL
3. **Implement rate limiting:**
   - Rate limit per source IP address
   - Rate limit per webhook secret
   - Add throttling for suspicious patterns
4. **Add monitoring and alerting:**
   - Log all webhook attempts (authorized and unauthorized)
   - Alert on repeated unauthorized attempts
   - Alert on unusual webhook patterns
5. **Enhanced validation:**
   - Validate webhook signatures where supported (Microsoft Graph validation tokens)
   - Add HMAC signature validation for custom webhooks

**Impact:** Attackers can send fake webhook notifications, trigger email sync operations, potentially access user emails, and cause data corruption or denial of service.

---

## ⚠️ HIGH PRIORITY (Before Production Launch)

### 4. MFA Production Delivery

**Risk Level:** HIGH - Currently outputs codes to terminal (development only)

**Current State:**
- MFA is enforced at login with cryptographic protection
- Codes are output to terminal/console for development
- No production email/SMS delivery implemented

**Required Actions:**
1. **Implement production email delivery:**
   - Integrate SendGrid, AWS SES, or similar service
   - Add email templates with branding
   - Implement delivery retry logic
   - Add delivery status tracking
2. **Implement SMS delivery (optional but recommended):**
   - Integrate Twilio, AWS SNS, or similar service
   - Add phone number verification
   - Support SMS as alternative to email
3. **Add backup codes:**
   - Generate and store backup codes during registration
   - Allow users to regenerate backup codes
   - Track backup code usage
4. **Device management:**
   - Allow users to register trusted devices
   - Implement "remember this device" functionality
   - Add device revocation capability

**Impact:** Users cannot complete login in production without manual code delivery.

---

### 5. Secrets Management Centralization

**Risk Level:** HIGH - Secrets scattered across configuration files

**Current State:**
- Secrets stored in `appsettings.json`, `.env` files, environment variables
- No centralized secret management
- No secret rotation policies

**Required Actions:**
1. **Centralize secret storage:**
   - Migrate to Azure Key Vault or AWS Secrets Manager
   - Remove secrets from configuration files
   - Implement secret injection at runtime
2. **Implement secret rotation:**
   - Automated rotation policies
   - Version management for secrets
   - Rollback capabilities
3. **Add monitoring:**
   - Alert on secret expiration
   - Track secret access patterns
   - Audit all secret access

**Impact:** Secrets may be exposed in configuration files, version control, or environment leaks.

---

### 6. Infrastructure Configuration Hardening

**Risk Level:** HIGH - Docker bootstrap in production risk

**Current State:**
- `USE_AZURE_SQL` defaults to local SQL
- Docker startup triggered if `USE_AZURE_SQL` is unset
- No deployment validation for production settings

**Required Actions:**
1. **Gate Docker bootstrap:**
   - Only allow Docker SQL startup in Development environment
   - Fail fast in Production if `USE_AZURE_SQL` is not explicitly set to true
   - Add environment validation middleware
2. **Add deployment validation:**
   - Validate all required production configuration keys on startup
   - Fail fast with clear error messages if configuration is invalid
   - Add configuration health check endpoint
3. **Documentation:**
   - Document required configuration for each environment
   - Add setup guides for production deployment
   - Create configuration checklists

**Impact:** Production deployments may accidentally use local SQL or Docker, exposing data or causing service failures.

---

## 📋 MEDIUM PRIORITY (Security Enhancements)

### 7. Monitoring & Observability

**Required Actions:**
1. Extend metrics to centralized observability stack (Application Insights, Grafana)
2. Track SignalR group membership changes and failed joins
3. Add structured application events around AI fallbacks and webhook errors
4. Implement security event correlation and alerting

### 8. Resilience Enhancements

**Required Actions:**
1. Formalize retry policies for AI HTTP client (Polly resilience strategies)
2. Establish background job dead-letter queue for failed AI tasks
3. Validate webhook handlers against high-volume and replay scenarios
4. Add alerting for repeated unauthorized attempts

### 9. Advanced MFA Features

**Required Actions:**
1. Implement WebAuthn/FIDO2 for hardware security keys
2. Add step-up MFA for privileged actions
3. Implement risk-based authentication
4. Add MFA bypass recovery procedures

---

## Summary

**Critical blockers for production:**
1. ✅ MFA enforcement (completed, needs production delivery)
2. ❌ AI service perimeter (30+ unauthenticated endpoints)
3. ✅ SQLite databases purged from git history (Sep 2026)
4. ❌ Webhook validation (optional secrets)

**Estimated effort to reach production-ready security:**
- Critical items: 2-3 weeks
- High priority items: 1-2 weeks
- Medium priority items: 2-3 weeks

**Total: 5-8 weeks of focused security hardening**

