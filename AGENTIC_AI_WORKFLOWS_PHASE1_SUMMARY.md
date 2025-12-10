# Agentic AI Workflows - Phase 1 Implementation Summary

## Overview
This document summarizes the complete Phase 1 implementation of the Agent Action foundation and unified inbox system for Certio. The implementation establishes the infrastructure for agent-proposed actions with approval workflows, secure execution, and comprehensive auditing.

---

## Architecture & Design

### Core Principles
1. **RBAC-First**: Leverages existing Permission enum and PermissionSets
2. **Approval Workflow**: PENDING → APPROVED → RUNNING → DONE/FAILED lifecycle
3. **Idempotency**: Every action carries `x-run-id` header for safe retries
4. **Audit Everything**: All agent actions logged via existing AuditLog infrastructure
5. **Read-Only Inbox**: Phase 1 limited to ingestion and display (sending in Phase 2)

---

## Domain Entities

### AgentAction
**Location**: `Certio.Domain/AgentActions/AgentAction.cs`

**Lifecycle States**:
- `Pending`: Awaiting approval
- `Approved`: Approved by manager, ready for execution
- `Rejected`: Rejected by reviewer
- `Running`: Currently executing
- `Done`: Successfully completed
- `Failed`: Execution failed after max retries
- `RolledBack`: Action undone (if reversible)

**Key Fields**:
- `RunId` (string, 100 chars): Idempotency key from `x-run-id` header
- `CorrelationId` (string): For tracing related actions
- `ActionType` (enum): CreateTask, AttachFile, AddNote, StartTimer, SendTemplateMessage
- `ActionPayload` (JSON): Type-specific parameters
- `ResultPayload` (JSON): Execution result
- `BeforeState/AfterState` (JSON): For audit and rollback
- `Priority` (int): For queue processing
- `IsReversible` (bool): Can this action be rolled back?
- `AIAgentType` (string): Which AI agent proposed this?
- `SourceConversationId/SourceMessageId` (int): Provenance from chat

**Relationships**:
- Organization (required, NoAction delete)
- Matter (optional)
- ProposedBy/ApprovedBy/RejectedBy/RolledBackBy (User)

### ActionPayloads
**Location**: `Certio.Domain/AgentActions/ActionPayloads.cs`

Supported action types with typed payloads:
1. **CreateTaskPayload**: Title, Description, Priority, DueDate, AssigneeIds
2. **AttachFilePayload**: SourceDocumentId or StagedFilePath, TargetEntity
3. **AddNotePayload**: Content, IsInternal flag, TargetEntity
4. **StartTimerPayload**: Description, BillingCode, IsBillable
5. **SendTemplateMessagePayload**: TemplateId, RecipientIds, Channel (stub for Phase 2)

Each payload includes:
- `GetSummary()`: Human-readable description
- `GetRedactedSummary()`: Audit-safe version (no sensitive data)
- JSON serialization helpers

### InboxItem & InboxMessage
**Location**: `Certio.Domain/UnifiedInbox/InboxItem.cs`

**InboxItem** (thread-level):
- `Source`: Email, DirectMessage, Chat, External, Calendar
- `ThreadId`: For grouping related messages
- `Status`: Unread, Read, Archived, Starred, Snoozed
- `Labels`: List of user-defined tags
- `MessageCount` / `UnreadCount`: Thread metrics
- `HasAttachments`: Quick flag for filtering
- `SenderName/SenderId`: Thread initiator

**InboxMessage** (individual message):
- `Direction`: Inbound or Outbound
- `Content`: HTML or plain text
- `SenderUser`: Optional User navigation
- `ToRecipients/CcRecipients/BccRecipients`: Email recipients
- `HasAttachments` / `AttachmentsJson`: File metadata
- `Importance`: High, Normal, Low
- `HeadersJson`: Email headers for compliance

**Inbox Status Actions** (read-only in Phase 1):
- Mark as read/unread
- Archive/unarchive
- Toggle flag
- Add/remove labels
- Snooze until date
- Link/unlink to matter

---

## Service Layer

### IAgentActionService
**Location**: `Certio.Application/Interfaces/IAgentActionService.cs`

**Key Operations**:

#### Proposal
```csharp
Task<AgentActionResult> ProposeActionAsync(
    int organizationId,
    string actionType,
    object payload,
    int proposedByUserId,
    string runId,  // x-run-id header
    string? correlationId = null,
    ...
)
```

- Idempotency check: If `runId` exists, returns duplicate result
- Payload validation
- Audit logging
- Returns: `AgentActionResult` with success flag, error code

#### Approval
```csharp
Task<AgentActionResult> ApproveActionAsync(int actionId, int approvedByUserId, string? notes)
Task<AgentActionResult> RejectActionAsync(int actionId, int rejectedByUserId, string? reason)
Task<AgentActionBulkResult> BulkApproveActionsAsync(...)
```

- State validation (must be Pending)
- User/timestamp tracking
- Audit logging

#### Execution
```csharp
Task<AgentActionResult> ExecuteActionAsync(int actionId)
Task<AgentActionBulkResult> ExecutePendingActionsAsync(int orgId, int? limit)
Task<AgentActionResult> RollbackActionAsync(int actionId, int userId, string? reason)
```

- State machine enforcement
- Attempt tracking
- Before/after state capture
- Rollback hooks

#### Query
- `GetActionAsync()`: By ID
- `GetActionByRunIdAsync()`: Idempotency lookup
- `GetPendingActionsAsync()`: For approval queue
- `GetActionsByStatusAsync()`: Filter by status
- `GetActionHistoryAsync()`: Audit trail
- `GetMatterActionsAsync()`: Matter-scoped
- `GetActionStatsAsync()`: Analytics

### IUnifiedInboxService
**Location**: `Certio.Application/Interfaces/IUnifiedInboxService.cs`

**Query Operations**:
- `GetInboxAsync()`: Paginated, filterable query
- `SearchAsync()`: Full-text search
- `GetMatterInboxAsync()`: Filter by matter
- `GetUnreadCountAsync()`: Quick stat
- `GetStatsAsync()`: Aggregated metrics

**Status Operations**:
- `MarkAsReadAsync()`: Single and bulk
- `ArchiveAsync()` / `UnarchiveAsync()`
- `ToggleFlagAsync()`
- `AddLabelsAsync()` / `RemoveLabelsAsync()`
- `SnoozeAsync()`
- `LinkToMatterAsync()` / `UnlinkFromMatterAsync()`

**Ingestion** (for connectors, Phase 2):
- `IngestItemAsync()`: Create or update thread
- `IngestMessageAsync()`: Add message to thread
- `SyncFromExternalSourceAsync()`: Bulk sync with cursor

---

## API Controllers

### AgentActionsController
**Location**: `Certio.Web/Controllers/Api/AgentActionsController.cs`

**Endpoints**:

**Query**:
- `GET /api/agent-actions/pending` - Approval queue
- `GET /api/agent-actions/history` - Audit trail
- `GET /api/agent-actions/by-status/{status}` - Filter by status
- `GET /api/agent-actions/{id}` - Get action
- `GET /api/agent-actions/by-run-id/{runId}` - Idempotency lookup
- `GET /api/agent-actions/matter/{matterId}` - Matter-scoped
- `GET /api/agent-actions/stats` - Analytics

**Propose**:
- `POST /api/agent-actions/propose` - Generic with RunId from header
- `POST /api/agent-actions/propose/create-task`
- `POST /api/agent-actions/propose/attach-file`
- `POST /api/agent-actions/propose/add-note`
- `POST /api/agent-actions/propose/start-timer`

**Approve/Reject**:
- `POST /api/agent-actions/{id}/approve` - Single
- `POST /api/agent-actions/{id}/reject` - Single
- `POST /api/agent-actions/bulk-approve` - Multiple
- `POST /api/agent-actions/bulk-reject` - Multiple

**Execute/Rollback**:
- `POST /api/agent-actions/{id}/execute` - Single
- `POST /api/agent-actions/execute-pending` - Batch queue
- `POST /api/agent-actions/{id}/rollback` - Undo

**Headers Used**:
- `x-run-id`: Idempotency key (defaults to new GUID)
- `x-correlation-id`: Optional tracing ID

### UnifiedInboxController
**Location**: `Certio.Web/Controllers/Api/UnifiedInboxController.cs`

**Query Endpoints**:
- `GET /api/inbox` - Paginated, filterable
- `GET /api/inbox/{id}` - Single item
- `GET /api/inbox/{id}/messages` - Thread messages
- `GET /api/inbox/matter/{matterId}` - Matter inbox
- `GET /api/inbox/unread-count` - Quick stat
- `GET /api/inbox/search` - Full-text search
- `GET /api/inbox/stats` - Aggregated metrics

**Status Endpoints** (read-only actions):
- `POST /api/inbox/{id}/read`
- `POST /api/inbox/{id}/unread`
- `POST /api/inbox/bulk-read` - Multiple items
- `POST /api/inbox/{id}/archive`
- `POST /api/inbox/{id}/unarchive`
- `POST /api/inbox/{id}/toggle-flag`
- `POST /api/inbox/{id}/labels/add`
- `POST /api/inbox/{id}/labels/remove`
- `POST /api/inbox/{id}/snooze` - Requires SnoozeUntil date
- `POST /api/inbox/{id}/link-matter/{matterId}`
- `POST /api/inbox/{id}/unlink-matter`

**All endpoints enforce**:
- `[Authorize(Policy = "OrgMember")]`
- `[RequireAgentPermission(Permission.ViewInbox)]` or `ManageInbox`

---

## Security & RBAC

### Permission Mappings
Added to existing `Permission` enum:
```csharp
ViewAgentActions,      // View-only access
ProposeAgentActions,   // Can propose actions
ApproveAgentActions,   // Can approve/reject/execute
RollbackAgentActions,  // Admin-only: can undo

ViewInbox,            // View-only access
ManageInbox           // Can modify status/labels
```

### Role-Based Grants

| Role | ViewAgentActions | ProposeAgentActions | ApproveAgentActions | RollbackAgentActions | ViewInbox | ManageInbox |
|------|:---:|:---:|:---:|:---:|:---:|:---:|
| Client Owner | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Client Manager | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ |
| Client Member | ✓ | ✓ | ✗ | ✗ | ✓ | ✗ |
| Law Firm ManagingPartner | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Law Firm Partner | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ |
| Law Firm Associate | ✓ | ✓ | ✗ | ✗ | ✓ | ✗ |
| Law Firm Paralegal | ✓ | ✓ | ✗ | ✗ | ✓ | ✗ |
| Law Firm Staff | ✓ | ✗ | ✗ | ✗ | ✓ | ✗ |
| Certio Admin | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

### Authorization Implementation
- `RequireAgentPermissionAttribute`: Filter-based permission check
- Uses existing `IPermissionService.HasPermissionAsync()`
- Returns 404 on deny (fail-closed)
- Validates `IClientContext.UserId` and `OrganizationId`

---

## Audit Logging

### New AuditLog Actions
```csharp
public const string AgentActionProposed = "AgentActionProposed";
public const string AgentActionApproved = "AgentActionApproved";
public const string AgentActionRejected = "AgentActionRejected";
public const string AgentActionStarted = "AgentActionStarted";
public const string AgentActionCompleted = "AgentActionCompleted";
public const string AgentActionFailed = "AgentActionFailed";
public const string AgentActionRolledBack = "AgentActionRolledBack";

public const string InboxItemCreated = "InboxItemCreated";
public const string InboxItemRead = "InboxItemRead";
public const string InboxItemArchived = "InboxItemArchived";
```

### Audit Entry Structure
```csharp
new AuditLog
{
    EntityType = "AgentAction",
    EntityId = action.Id,
    Action = auditAction,
    Result = AuditResults.Success,
    UserId = userId,
    OrganizationId = organizationId,
    MatterId = matterId,
    IsAIAction = !string.IsNullOrEmpty(aiAgentType),
    AIAgentType = aiAgentType,
    SourceConversationId = sourceConversationId,
    SourceMessageId = sourceMessageId,
    Description = $"{auditAction}: {actionType} - {summary}",
    NewValues = JSON { RunId, ActionType, Status, CorrelationId },
    Timestamp = DateTime.UtcNow
}
```

---

## Idempotency & Duplicates

### Implementation
1. **Header**: Client sends `x-run-id` (or defaults to new GUID)
2. **Lookup**: Service checks if `RunId` exists in database
3. **Response**:
   - If exists: HTTP 200 with existing action + `{"duplicate": true}`
   - If new: HTTP 201 (Created) with new action
4. **Safety**: Database unique index on `RunId` prevents race conditions

### Client Responsibility
- Retain `RunId` from response
- Retry with same `RunId` if request fails
- Safe to retry indefinitely

---

## Database Schema

### Tables Created

**AgentActions**
- PK: `Id` (int)
- UK: `RunId` (nvarchar(100))
- FK: OrganizationId (NoAction), MatterId (SetNull), ProposedById/ApprovedById/RejectedById/RolledBackById (SetNull)
- Indexes: RunId, CorrelationId, (OrgId, Status, CreatedAt), Status, ActionType, MatterId

**InboxItems**
- PK: `Id` (int)
- FK: OrganizationId, MatterId (SetNull), UserId (SetNull)
- Indexes: (OrgId, Status, LastMessageAt), (OrgId, Source, LastMessageAt), ThreadId, ExternalId, MatterId, (IsDeleted, LastMessageAt)

**InboxMessages**
- PK: `Id` (int)
- FK: InboxItemId (Cascade), SenderUserId (SetNull)
- Indexes: (InboxItemId, CreatedAt), ExternalMessageId

---

## Service Registration

**Location**: `Certio.Web/Program.cs`

```csharp
// AGENT ACTIONS AND UNIFIED INBOX (Phase 1 Agentic AI Workflows)
builder.Services.AddScoped<Certio.Application.Interfaces.IAgentActionService, Certio.Application.Services.AgentActionService>();
builder.Services.AddScoped<Certio.Application.Interfaces.IUnifiedInboxService, Certio.Application.Services.UnifiedInboxService>();
```

---

## Phase 1 Limitations (By Design)

1. **Read-Only Inbox**: No sending capability (Phase 2)
2. **No Email/Calendar Sync**: Connectors in Phase 2
3. **Template Messages Stubbed**: SendTemplateMessage returns "not available in Phase 1"
4. **No Timer Implementation**: StartTimer is placeholder (billing module pending)
5. **Batch Execution**: Single-threaded queue (async batch in Phase 2)

---

## Testing Checklist

### Manual Tests

**Agent Action Proposal**:
- [ ] POST `/api/agent-actions/propose/create-task` with valid payload
- [ ] Verify 201 response with RunId in header
- [ ] Retry same RunId → expect 200 with `"duplicate": true`
- [ ] Verify audit log entry with `AgentActionProposed`

**Approval Workflow**:
- [ ] POST `/api/agent-actions/{id}/approve` as Manager role
- [ ] Verify status changes to `Approved`
- [ ] POST `/api/agent-actions/{id}/execute` 
- [ ] Verify status changes to `Done` or `Failed`
- [ ] Check audit trail for all transitions

**Permission Enforcement**:
- [ ] POST as Staff (no ProposeAgentActions) → expect 403
- [ ] GET as Viewer → expect 200
- [ ] Archive inbox item as Member (no ManageInbox) → expect 403

**Idempotency**:
- [ ] Send same `x-run-id` 3 times with identical payload
- [ ] Verify only 1 action created
- [ ] All responses should be identical except timestamp

---

## Future Phases (Phase 2+)

### Phase 2: Sending Capability
- Email sending integration
- Direct message sending
- Calendar event creation
- Template message implementation
- Inbox ingestion from external sources

### Phase 3: Advanced Workflows
- Agent-to-agent coordination
- Conditional action chains
- Batch processing & async execution
- Performance optimization

---

## References

- Domain Entities: `Certio.Domain/AgentActions/`, `Certio.Domain/UnifiedInbox/`
- Services: `Certio.Application/Services/AgentActionService.cs`, `Certio.Application/Services/UnifiedInboxService.cs`
- Controllers: `Certio.Web/Controllers/Api/AgentActionsController.cs`, `Certio.Web/Controllers/Api/UnifiedInboxController.cs`
- Database: `Certio.Infrastructure/Data/ApplicationDbContext.cs`
- Audit: `Certio.Domain/Audit/AuditLog.cs` (extended action types)
- RBAC: `Certio.Domain/Users/User.cs` (new permissions)

---

## Implementation Status

✅ **Complete**:
- Domain entities and relationships
- Service interfaces and implementations
- API controllers with proper authorization
- Database schema and migrations
- Audit logging integration
- RBAC permission mappings
- Idempotency implementation
- Security validation

⏳ **Phase 2 Backlog**:
- Email/Calendar/DM connectors
- Async batch execution
- Template message sending
- External webhook ingestion

---

**Last Updated**: December 9, 2024
**Version**: 1.0.0 (Phase 1)
**Status**: Ready for Migration & Testing

