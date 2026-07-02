# Certio Documentation

**Last Updated:** July 1, 2026

This index covers all documentation organized into the following sections. All docs live under `docs/` with the exception of project-level READMEs that remain in their respective module folders.

---

## Structure

```
docs/
├── architecture/   — System design, domain model, data schemas, service contracts
├── features/       — Per-feature implementation guides and reference docs
├── operations/     — Deployment, testing, monitoring, cost management
├── security/       — Security assessments, permission system, vulnerability reports
├── setup/          — Local dev setup, database, OAuth, Azure, offline config
├── archive/        — Historical session notes, fix logs, phase completion reports
└── INDEX.md        — This file
```

---

## Architecture

| Document | Description |
|----------|-------------|
| [`architecture/CERTIO_TECHNICAL_ARCHITECTURE_DOCUMENT.md`](architecture/CERTIO_TECHNICAL_ARCHITECTURE_DOCUMENT.md) | Comprehensive system architecture reference (2,400+ lines) |
| [`architecture/API_REFERENCE.md`](architecture/API_REFERENCE.md) | All endpoints, routes, and HTTP methods |
| [`architecture/DOMAIN_MODEL_REFERENCE.md`](architecture/DOMAIN_MODEL_REFERENCE.md) | 62+ entity classes with properties and relationships |
| [`architecture/SERVICES_REFERENCE.md`](architecture/SERVICES_REFERENCE.md) | 50+ services, middleware, and permissions |
| [`architecture/DATABASE_SCHEMA_DIAGRAM.md`](architecture/DATABASE_SCHEMA_DIAGRAM.md) | Entity relationship diagram and schema overview |
| [`architecture/DATABASE_SCHEMA_PLAN.md`](architecture/DATABASE_SCHEMA_PLAN.md) | Database schema design decisions |
| [`architecture/DATA_SYNC_STRATEGY.md`](architecture/DATA_SYNC_STRATEGY.md) | Data synchronization approach |
| [`architecture/COMMUNICATIONS_CONTROLLER_ARCHITECTURE.md`](architecture/COMMUNICATIONS_CONTROLLER_ARCHITECTURE.md) | Communications layer design |
| [`architecture/ENTERPRISE_SECURITY_ARCHITECTURE.md`](architecture/ENTERPRISE_SECURITY_ARCHITECTURE.md) | Enterprise-grade security architecture |

---

## Setup

| Document | Description |
|----------|-------------|
| [`setup/WINDOWS_SETUP_GUIDE.md`](setup/WINDOWS_SETUP_GUIDE.md) | Windows development environment setup |
| [`setup/SMART_DATABASE_GUIDE.md`](setup/SMART_DATABASE_GUIDE.md) | Local vs Azure SQL smart detection |
| [`setup/smart_database_setup.md`](setup/smart_database_setup.md) | Smart database setup scripts |
| [`setup/database_setup.md`](setup/database_setup.md) | Database initialization steps |
| [`setup/REDIS_SETUP.md`](setup/REDIS_SETUP.md) | Redis caching setup and configuration |
| [`setup/DOCKER_SQL_COMMAND_UPDATE.md`](setup/DOCKER_SQL_COMMAND_UPDATE.md) | Docker SQL Server commands reference |
| [`setup/OAUTH_LOCALHOST_SETUP.md`](setup/OAUTH_LOCALHOST_SETUP.md) | OAuth configuration for local development |
| [`setup/OFFLINE_WORK_GUIDE.md`](setup/OFFLINE_WORK_GUIDE.md) | Developing without internet access |
| [`setup/offline_setup.md`](setup/offline_setup.md) | Offline environment setup scripts |
| [`setup/AZURE_GPT41_SETUP.md`](setup/AZURE_GPT41_SETUP.md) | Azure OpenAI GPT-4.1 configuration |
| [`setup/AZURE_EMAIL_MIGRATION_GUIDE.md`](setup/AZURE_EMAIL_MIGRATION_GUIDE.md) | Azure Communication Services email setup |
| [`setup/EMAIL_WEBHOOK_SETUP_GUIDE.md`](setup/EMAIL_WEBHOOK_SETUP_GUIDE.md) | Email webhook endpoint setup |

---

## Features

### AI & RAG

| Document | Description |
|----------|-------------|
| [`features/AI_CHAT_SYSTEM.md`](features/AI_CHAT_SYSTEM.md) | AI chat system overview and design |
| [`features/AI_CHAT_STREAMING_IMPLEMENTATION.md`](features/AI_CHAT_STREAMING_IMPLEMENTATION.md) | Streaming response implementation |
| [`features/AI_CHAT_STICKY_MESSAGE_IMPLEMENTATION.md`](features/AI_CHAT_STICKY_MESSAGE_IMPLEMENTATION.md) | Sticky message feature |
| [`features/AI_CHAT_INPUT_ENHANCEMENT.md`](features/AI_CHAT_INPUT_ENHANCEMENT.md) | Chat input UX improvements |
| [`features/AI_IMAGE_AND_FILE_PROCESSING_IMPLEMENTATION.md`](features/AI_IMAGE_AND_FILE_PROCESSING_IMPLEMENTATION.md) | File and image processing in AI |
| [`features/AGENTIC_AI_WORKFLOWS_PHASE1_SUMMARY.md`](features/AGENTIC_AI_WORKFLOWS_PHASE1_SUMMARY.md) | Agentic workflow foundations |
| [`features/USER_DATA_RAG_IMPLEMENTATION.md`](features/USER_DATA_RAG_IMPLEMENTATION.md) | User data RAG pipeline |
| [`features/RAG_SYSTEM_ENHANCEMENT_SUMMARY.md`](features/RAG_SYSTEM_ENHANCEMENT_SUMMARY.md) | RAG system enhancements |

### Calendar

| Document | Description |
|----------|-------------|
| [`features/CALENDAR_MODULE_IMPLEMENTATION_COMPLETE.md`](features/CALENDAR_MODULE_IMPLEMENTATION_COMPLETE.md) | Calendar module implementation |
| [`features/CALENDAR_OAUTH_IMPLEMENTATION.md`](features/CALENDAR_OAUTH_IMPLEMENTATION.md) | Google/Microsoft OAuth calendar sync |

### Communications & Messaging

| Document | Description |
|----------|-------------|
| [`features/COMMUNICATIONS_SIDEBAR_IMPLEMENTATION.md`](features/COMMUNICATIONS_SIDEBAR_IMPLEMENTATION.md) | Communications sidebar feature |
| [`features/DIRECT_MESSAGING_IMPLEMENTATION_SUMMARY.md`](features/DIRECT_MESSAGING_IMPLEMENTATION_SUMMARY.md) | Direct messaging system |
| [`features/DM_RELATIONSHIP_USERS_IMPLEMENTATION.md`](features/DM_RELATIONSHIP_USERS_IMPLEMENTATION.md) | Cross-org DM relationships |
| [`features/DM_QUICK_REFERENCE.md`](features/DM_QUICK_REFERENCE.md) | Direct messaging quick reference |
| [`features/EMAIL_INTEGRATION_IMPLEMENTATION_SUMMARY.md`](features/EMAIL_INTEGRATION_IMPLEMENTATION_SUMMARY.md) | Email integration overview |

### Tasks & Matters

| Document | Description |
|----------|-------------|
| [`features/TASKS_IMPLEMENTATION_GUIDE.md`](features/TASKS_IMPLEMENTATION_GUIDE.md) | Task system implementation guide |
| [`features/MATTER_DETAILS_IMPLEMENTATION_SUMMARY.md`](features/MATTER_DETAILS_IMPLEMENTATION_SUMMARY.md) | Matter details page |
| [`features/MATTER_DETAILS_LAYOUT_UPDATE.md`](features/MATTER_DETAILS_LAYOUT_UPDATE.md) | Matter details UI layout |
| [`features/MATTER_DETAILS_QUICK_REFERENCE.md`](features/MATTER_DETAILS_QUICK_REFERENCE.md) | Matter details quick reference |
| [`features/MATTER_TASKS_QUICK_INTEGRATION.md`](features/MATTER_TASKS_QUICK_INTEGRATION.md) | Matter-tasks integration guide |
| [`features/EVENTPLANNER_IMPLEMENTATION_REPORT.md`](features/EVENTPLANNER_IMPLEMENTATION_REPORT.md) | Event planner module |

### Documents & Storage

| Document | Description |
|----------|-------------|
| [`features/WOPI_IMPLEMENTATION_GUIDE.md`](features/WOPI_IMPLEMENTATION_GUIDE.md) | WOPI/Office Online integration guide |
| [`features/WOPI_IMPLEMENTATION_SUMMARY.md`](features/WOPI_IMPLEMENTATION_SUMMARY.md) | WOPI implementation overview |
| [`features/SETUP_FILE_AND_IMAGE_PROCESSING.md`](features/SETUP_FILE_AND_IMAGE_PROCESSING.md) | File/image processing configuration |

### Other Features

| Document | Description |
|----------|-------------|
| [`features/SOFT_DELETE_IMPLEMENTATION_SUMMARY.md`](features/SOFT_DELETE_IMPLEMENTATION_SUMMARY.md) | Soft delete across entities |
| [`features/COMPREHENSIVE_AUDIT_LOGGING_IMPLEMENTATION.md`](features/COMPREHENSIVE_AUDIT_LOGGING_IMPLEMENTATION.md) | Audit logging system |
| [`features/PERFORMANCE_MONITORING_GUIDE.md`](features/PERFORMANCE_MONITORING_GUIDE.md) | Performance monitoring setup |
| [`features/ADDPEOPLE_REDESIGN_IMPLEMENTATION.md`](features/ADDPEOPLE_REDESIGN_IMPLEMENTATION.md) | Add People flow redesign |
| [`features/ADDPEOPLE_USER_FLOWS.md`](features/ADDPEOPLE_USER_FLOWS.md) | Add People UX flows |
| [`features/AJAX_NAVIGATION_IMPLEMENTATION.md`](features/AJAX_NAVIGATION_IMPLEMENTATION.md) | AJAX page navigation |
| [`features/MOBILE_STYLE_TEMPLATE.md`](features/MOBILE_STYLE_TEMPLATE.md) | Mobile UI style template |

---

## Security

| Document | Description |
|----------|-------------|
| [`security/ENTERPRISE_SECURITY_ARCHITECTURE.md`](../docs/architecture/ENTERPRISE_SECURITY_ARCHITECTURE.md) | See Architecture section |
| [`security/PHASE_0_SECURITY_ARCHITECTURE_ASSESSMENT.md`](security/PHASE_0_SECURITY_ARCHITECTURE_ASSESSMENT.md) | Pre-launch security assessment |
| [`security/PHASE_3_PERMISSION_SYSTEM_GUIDE.md`](security/PHASE_3_PERMISSION_SYSTEM_GUIDE.md) | Permission system implementation guide (800+ lines) |
| [`security/CRITICAL_SECURITY_FIXES_COMPLETED.md`](security/CRITICAL_SECURITY_FIXES_COMPLETED.md) | Security hardening completed |
| [`security/SECURITY_REMAINING_ISSUES.md`](security/SECURITY_REMAINING_ISSUES.md) | Outstanding security items |
| [`security/VULNERABILITY_CHECK_REPORT.md`](security/VULNERABILITY_CHECK_REPORT.md) | Vulnerability assessment report |
| [`security/CALENDAR_SECURITY_REVIEW.md`](security/CALENDAR_SECURITY_REVIEW.md) | Calendar module security review |

---

## Operations

| Document | Description |
|----------|-------------|
| [`operations/FINAL_DEPLOYMENT_CHECKLIST.md`](operations/FINAL_DEPLOYMENT_CHECKLIST.md) | Production deployment checklist |
| [`operations/PHASE_1_DEPLOYMENT_CHECKLIST.md`](operations/PHASE_1_DEPLOYMENT_CHECKLIST.md) | Phase 1 deployment checklist |
| [`operations/DEPLOYMENT_READY_STATUS.md`](operations/DEPLOYMENT_READY_STATUS.md) | Deployment readiness status |
| [`operations/DEPLOYMENT_COMPLETE_SUMMARY.md`](operations/DEPLOYMENT_COMPLETE_SUMMARY.md) | Post-deployment summary |
| [`operations/TESTING_QUICK_START.md`](operations/TESTING_QUICK_START.md) | Security testing quick start |
| [`operations/AUDIT_LOGGING_TESTING_GUIDE.md`](operations/AUDIT_LOGGING_TESTING_GUIDE.md) | Audit log verification procedures |
| [`operations/PHASE_1_TESTING_GUIDE.md`](operations/PHASE_1_TESTING_GUIDE.md) | Phase 1 test procedures |
| [`operations/PERFORMANCE_MONITORING_IMPLEMENTATION_SUMMARY.md`](operations/PERFORMANCE_MONITORING_IMPLEMENTATION_SUMMARY.md) | Performance monitoring setup |
| [`operations/PERFORMANCE_METRICS_QUICK_REFERENCE.md`](operations/PERFORMANCE_METRICS_QUICK_REFERENCE.md) | Key performance metrics |
| [`operations/CURSOR_COST_OPTIMIZATION.md`](operations/CURSOR_COST_OPTIMIZATION.md) | AI cost optimization strategies |
| [`operations/MAKE_REPO_PUBLIC_SAFELY.md`](operations/MAKE_REPO_PUBLIC_SAFELY.md) | Steps to safely open-source the repo |

---

## Strategic

| Document | Description |
|----------|-------------|
| [`CAPABILITY_REPORT.md`](CAPABILITY_REPORT.md) | Strategic capability report — integration surface and competitive moat |
| [`BIGGEST_WINS.md`](BIGGEST_WINS.md) | 15 biggest codebase wins, fact-checked against source |

---

## AI Agents Module

The Python AI service has its own doc index at [`ai_agents/docs/INDEX.md`](../ai_agents/docs/INDEX.md).

| Document | Description |
|----------|-------------|
| [`ai_agents/README.md`](../ai_agents/README.md) | Service overview, dependencies, running locally |
| [`ai_agents/docs/QUICK_START.md`](../ai_agents/docs/QUICK_START.md) | Quick start guide |
| [`ai_agents/docs/LLM_TRAINING_GUIDE.md`](../ai_agents/docs/LLM_TRAINING_GUIDE.md) | LLM training procedures |
| [`ai_agents/docs/MIGRATION_GUIDE.md`](../ai_agents/docs/MIGRATION_GUIDE.md) | AI service migration guide |
| [`ai_agents/docs/AZURE_PRODUCTION_GUIDE.md`](../ai_agents/docs/AZURE_PRODUCTION_GUIDE.md) | Azure production deployment |
| [`ai_agents/docs/RAG_SYSTEM_IMPROVEMENTS.md`](../ai_agents/docs/RAG_SYSTEM_IMPROVEMENTS.md) | RAG system improvements |
| [`ai_agents/docs/ADVANCED_OPTIMIZATION_GUIDE.md`](../ai_agents/docs/ADVANCED_OPTIMIZATION_GUIDE.md) | Advanced performance optimization |

---

## Archive

The [`archive/`](archive/) folder contains historical documents that are no longer actively maintained:

- **Fix logs** — per-session bug fix summaries (e.g. `DM_FIX_APPLIED.md`, `CALENDAR_ATTENDEES_FIX.md`)
- **Phase reports** — `PHASE_1_IMPLEMENTATION_SUMMARY.md` through `PHASE_4_COMPLETION_SUMMARY.md`
- **Status snapshots** — `DM_WORKING_STATUS.md`, `FINAL_IMPLEMENTATION_STATUS.md`, etc.
- **Debug logs** — `DM_DEBUG_STEPS.md`, `DEBUGGING_MATTER_TASKS_TAB.md`, `STICKY_MESSAGE_DEBUG_GUIDE.md`
- **Refactoring notes** — service layer and controller refactoring session docs
- **Test scripts** — `test_registration_flow.md`, `test_smart_detection.md`

These are preserved for historical reference. Commits introducing them can be found via `git log --follow -- docs/archive/<filename>`.
