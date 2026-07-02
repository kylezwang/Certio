# Certio Services Reference

**Last Updated:** February 11, 2026  
**Version:** 5.0.0

---

## Overview

Certio uses a service layer architecture where business logic resides in services, keeping controllers thin. Services are organized across two layers:

- **Application Services** (`Certio.Application.Services`) - Core business logic with interfaces
- **Web Services** (`Certio.Web.Services`) - Presentation-layer services for web-specific concerns

All services use dependency injection and are registered in `Program.cs`.

---

## Application Services (`Certio.Application`)

### Core Business Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `MatterService` | `IMatterService` | Matter CRUD, assignments, permissions, access control |
| `TaskService` | `ITaskService` | Task management, comments, dependencies, reactions |
| `SubTaskService` | `ISubTaskService` | Subtask management within tasks |
| `CalendarService` | `ICalendarService` | Calendar event CRUD, attendees |
| `OrganizationService` | `IOrganizationService` | Organization management, settings |
| `TeamService` | `ITeamService` | Team management within organizations |
| `OrganizationRelationshipService` | `IOrganizationRelationshipService` | Org-to-org relationship management |
| `BillingService` | `IBillingService` | Time entries, expenses, invoices, retainers |
| `PermissionService` | `IPermissionService` | Permission checking, access validation |
| `OrganizationContextService` | `IOrganizationContextService` | Organization context per request |
| `AuditService` | `IAuditService` | Manual audit logging |
| `CalendarSyncService` | `ICalendarSyncService` | External calendar synchronization |

### AI & Agent Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `AIAgentService` | `IAIAgentService` | AI agent operations, execution |
| `AgentActionService` | `IAgentActionService` | Agent action propose/approve/execute/rollback |
| `UnifiedInboxService` | `IUnifiedInboxService` | Cross-channel inbox aggregation |
| `UserDataContextService` | `IUserDataContextService` | User data context for RAG queries |

### Document Services (`Certio.Application.Services.Documents`)

| Service | Interface | Description |
|---------|-----------|-------------|
| `DriveSyncService` | `IDriveSyncService` | Google Drive / OneDrive sync |
| `DocumentContentService` | `IDocumentContentService` | Document content retrieval |
| `FileStorageService` | - | Local file storage operations |
| `VectorStoreService` | `IVectorStoreService` | Vector storage for RAG embeddings |
| `DocumentIndexerService` | `IDocumentIndexerService` | Document indexing for search |
| `RagContextService` | `IRagContextService` | RAG context generation for AI |
| `WopiDiscoveryService` | - | WOPI discovery for Office Online |
| `WopiAccessTokenService` | - | WOPI access token management |
| `DocumentEmbedService` | `IDocumentEmbedService` | Document embed URL generation |
| `DocumentAuditService` | `IDocumentAuditService` | Document-specific audit logging |
| `WebhookHandlerService` | `IWebhookHandlerService` | External webhook processing |

---

## Web Services (`Certio.Web.Services`)

### Communication Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `ChatService` | `IChatService` | Team chat operations |
| `DirectMessageService` | `IDirectMessageService` | Direct messaging |
| `ChannelManagementService` | `IChannelManagementService` | Channel creation, management |
| `NotificationService` | `INotificationService` | Notification dispatch |
| `BriefingMessageService` | - | Daily briefing messages |

### Email Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `EmailService` | `IEmailService` | Email CRUD operations |
| `EmailSendingService` | `IEmailSendingService` | Email sending (MailKit) |
| `EmailSyncService` | - | Email sync (IHostedService) |
| `EmailToDmService` | `IEmailToDmService` | Email-to-DM conversion |

### Change Control Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `ChangeNoticeService` | `IChangeNoticeService` | Change notice management |
| `ChangeNoticeAutoReminderService` | - | Auto reminders (IHostedService) |

### Authentication & Security Services

| Service | Description |
|---------|-------------|
| `TwoFactorService` | Email-based 2FA code generation and validation |
| `TrustedDeviceService` | Trusted device management for 2FA |
| `UserSyncService` | Sync Identity users with custom User records |
| `UserDeletionService` | GDPR-compliant user deletion |
| `JoinCodeService` | Organization join code generation |
| `FirmRelationshipCacheService` | Cache law firm relationships |
| `LawFirmRoleResolutionService` | Resolve user roles within law firms |
| `FirmAccessAuditService` | Audit firm-based access patterns |

### Caching & Performance Services

| Service | Description |
|---------|-------------|
| `CachedPermissionService` | Redis-backed permission caching wrapper |
| `RedisCacheService` | Redis cache abstraction |
| `CacheMetricsService` | Cache hit/miss tracking |
| `MetricsReportingService` | Periodic metrics reporting (IHostedService) |

### AI Background Services

| Service | Description |
|---------|-------------|
| `AIBackgroundService` | Background AI processing (IHostedService) |
| `AIUsageService` | AI token usage and cost tracking |
| `UserPresenceService` | User online/offline presence tracking |

---

## Service Patterns

### ServiceResult\<T\> Pattern

All application services return `ServiceResult<T>` for standardized responses:

```csharp
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    
    public static ServiceResult<T> SuccessResult(T data);
    public static ServiceResult<T> Failure(string error);
}
```

### Standard Service Method Flow

1. Validate organization membership
2. Check permissions via `PermissionService`
3. Validate input DTO
4. Execute business logic
5. Save to database
6. Log audit event
7. Return `ServiceResult<T>`

### Hosted Services (Background Workers)

| Service | Trigger | Description |
|---------|---------|-------------|
| `EmailSyncService` | Timer-based | Periodically sync email |
| `ChangeNoticeAutoReminderService` | Timer-based | Send reminders before due dates |
| `MetricsReportingService` | Timer-based | Report cache and performance metrics |
| `AIBackgroundService` | Event-based | Process AI tasks in background |

---

## Middleware Pipeline

The middleware pipeline is configured in `Program.cs` in this order:

1. Exception Handler
2. HSTS (production)
3. HTTPS Redirection
4. Static Files
5. **PerformanceMonitoringMiddleware** - Request timing
6. Session
7. Routing
8. Authentication
9. **UserSyncMiddleware** - Sync Identity users with app users
10. **ClientContextMiddleware** - Extract `orgId` from routes, build context
11. **ChannelInitializationMiddleware** - Initialize chat channels
12. **ClientAccessMiddleware** - Verify client access rights
13. Authorization
14. **RequestAuditMiddleware** - Log HTTP requests to AuditLog

---

## Permission System

### Permission Categories (6)

1. **Document** (5): ViewDocuments, DownloadDocuments, UploadDocuments, DeleteDocuments, CommentOnDocuments
2. **Matter** (5): ViewMatters, CreateMatters, EditMatters, DeleteMatters, ManageMatterSettings
3. **User Management** (3): InviteUsers, RemoveUsers, ManageUserPermissions
4. **Communication** (4): ViewMessages, SendMessages, DeleteMessages, ManageThreads
5. **System** (3): ViewAuditLogs, ManageSystemSettings, AccessAdminPanel
6. **Agent & Inbox** (4+): ViewAgentActions, ProposeAgentActions, ApproveAgentActions, RollbackAgentActions, ViewInbox, ManageInbox

### Permission Sets (17 total)

| Type | Sets |
|------|------|
| Client | Owner, Manager, Member, Lawyer |
| Law Firm | Partner, Associate, Paralegal, Staff |
| External | OpposingCounsel, ExpertWitness, CourtPersonnel, RegulatoryBody, Other |
| Certio | Admin, MatterManager, Support, Legal |

### Permission Evaluation Order

1. Check direct organization membership
2. Check firm-based access (law firm users)
3. Evaluate base permissions from role + user type
4. Apply custom permission overlays
5. Filter by matter-specific access

### Caching Strategy

| Cache Key Pattern | TTL | Layer |
|-------------------|-----|-------|
| `perm:{userId}:{orgId}:{permission}` | 15 min | L1 Memory + L2 Redis |
| `eff_perms:{userId}:{orgId}` | 15 min | L1 Memory + L2 Redis |
| `matter_access:{userId}:{matterId}` | 10 min | L1 Memory + L2 Redis |
| `task_access:{userId}:{taskId}` | 5 min | L1 Memory + L2 Redis |
| `org_member:{userId}:{orgId}` | 15 min | L1 Memory + L2 Redis |
