# Certio Domain Model Reference

**Last Updated:** February 11, 2026  
**Version:** 5.0.0

---

## Overview

The Certio domain layer (`Certio.Domain`) contains 62+ entity classes organized by business domain. All entities support organization-scoped multi-tenancy. Key cross-cutting patterns include audit logging (`IAuditable`), soft deletes (`ISoftDeletable`), and AI content tracking (`IAIGenerated`).

---

## Table of Contents

1. [Base Classes & Interfaces](#base-classes--interfaces)
2. [Users & Organizations](#users--organizations)
3. [Matters](#matters)
4. [Tasks](#tasks)
5. [Documents](#documents)
6. [Communications](#communications)
7. [Email Integration](#email-integration)
8. [Calendar](#calendar)
9. [Billing](#billing)
10. [AI & Agents](#ai--agents)
11. [Unified Inbox](#unified-inbox)
12. [Change Control](#change-control)
13. [Notifications](#notifications)
14. [Workflows](#workflows)
15. [Audit](#audit)
16. [Entity Relationship Diagram](#entity-relationship-diagram)

---

## Base Classes & Interfaces

### AuditableEntity (`Certio.Domain.Audit`)
Abstract base class for entities needing audit and soft-delete support.

| Property | Type | Description |
|----------|------|-------------|
| CreatedAt | DateTime | Creation timestamp |
| CreatedById | int? | Creator user ID |
| ModifiedAt | DateTime? | Last modification timestamp |
| ModifiedById | int? | Last modifier user ID |
| IsDeleted | bool | Soft delete flag |
| DeletedAt | DateTime? | Deletion timestamp |
| DeletedById | int? | Deleter user ID |

**Implements:** `IAuditable`, `ISoftDeletable`

### IAIGenerated (`Certio.Domain.Audit`)
Interface for AI-created content.

| Property | Type | Description |
|----------|------|-------------|
| IsAIGenerated | bool | Whether created by AI |
| AIAgentType | string? | Agent type that created it |
| AIGenerationMetadata | string? | Generation metadata JSON |
| SourceConversationId | int? | Source conversation |
| SourceMessageId | int? | Source message |

### IApprovable (`Certio.Domain.Audit`)
Interface for content requiring approval.

| Property | Type | Description |
|----------|------|-------------|
| ApprovalStatus | string? | Approval status |
| ApprovedById | int? | Approver user ID |
| ApprovedAt | DateTime? | Approval timestamp |
| ApprovalNotes | string? | Approval notes |

---

## Users & Organizations

### User (`Certio.Domain.Users.User`)
Core user entity extending ASP.NET Core Identity.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| FirstName | string | First name |
| LastName | string | Last name |
| Email | string | Email address |
| PhoneNumber | string? | Phone number |
| Company | string? | Company name |
| JobTitle | string? | Job title |
| Department | string? | Department |
| Location | string? | Location |
| Avatar | string? | Avatar URL |
| Color | string? | Theme color |
| TimeZone | string? | User timezone |
| Language | string? | Preferred language |
| Theme | string? | UI theme |
| IsActive | bool | Active status |
| Enable2FA | bool | Two-factor auth enabled |
| IsDeleted | bool | Soft delete flag |
| CustomPermissions | string? | JSON custom permissions |

**Key Navigation Properties:** `UserOrganizations`, `TeamMemberships`, `MatterAssignments`, `ChatMessages`, `TrustedDevices`

**Key Methods:** `IsInOrganization()`, `IsOrganizationOwner()`, `IsOrganizationAdmin()`, `IsCertioStaff()`, `IsClient()`, `IsLawFirmMember()`, `IsLawFirmPartner()`, `CanAccessClientThroughFirm()`, `GetEffectivePermissions()`, `HasMatterAccess()`, `CanInviteUserType()`

### Organization (`Certio.Domain.Organizations.Organization`)
Multi-tenant organization entity.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| Name | string | Organization name |
| Description | string? | Description |
| OwnerId | int | Owner user ID |
| Type | OrganizationType | Organization type enum |
| IsPersonal | bool | Personal workspace flag |
| IsActive | bool | Active status |
| Color | string? | Brand color |
| Logo | string? | Logo URL |
| Settings | string? | JSON settings |

**OrganizationType enum:** `Client`, `LawFirm`, `EventPlanner`, `Government`, `NonProfit`

**Key Methods:** `HasUser()`, `GetUserMembership()`, `GetAIModelTier()`, `SetAIModelTier()`, `GetMatterTerminology()`, `SetMatterTerminology()`

### UserOrganization (`Certio.Domain.Users.UserOrganization`)
Junction entity for User-Organization membership.

| Property | Type | Description |
|----------|------|-------------|
| UserId | int | User foreign key |
| OrganizationId | int | Organization foreign key |
| Role | string | Organization role |
| UserType | string | User type within org |
| IsPrimary | bool | Primary organization |
| IsActive | bool | Active membership |
| JoinedAt | DateTime | Join date |
| LeftAt | DateTime? | Leave date |

**OrganizationRoles:** `Owner`, `Admin`, `Manager`, `Member`, `Lawyer`  
**UserTypes:** `Client`, `LawFirm`, `External`, `Certio`

### OrganizationRelationship (`Certio.Domain.Organizations.OrganizationRelationship`)
Relationship between organizations (e.g., law firm represents client).

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| SourceOrganizationId | int | Source org (e.g., law firm) |
| TargetOrganizationId | int | Target org (e.g., client) |
| RelationshipType | string | Type of relationship |
| IsActive | bool | Active status |
| ExpiresAt | DateTime? | Expiration date |

**RelationshipTypes:** `LawFirmClient`, `EventPlannerClient`, `Referral`, `CoCouncil`, `Consultant`

### Other User/Org Entities
- **OrganizationJoinCode** - Invitation codes for organizations
- **Team** - Teams within organizations
- **TeamMembership** - User-Team junction
- **TrustedDevice** - 2FA trusted devices
- **UserDeletionRequest** - GDPR deletion requests

---

## Matters

### Matter (`Certio.Domain.Matters.Matter`)
Core case/project entity.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| Title | string | Matter title |
| Description | string? | Description |
| Location | string? | Location |
| Status | string | Status (Planning, InProgress, Completed, etc.) |
| PracticeArea | string? | Legal practice area |
| AccessLevel | string | "Everyone" or "Specific" |
| OrganizationId | int | Owning organization |
| TeamId | int? | Assigned team |
| ClientId | int? | Client user |
| Budget | decimal? | Budget amount |
| StartDate | DateTime? | Start date |
| DueDate | DateTime? | Due date |
| CompletedDate | DateTime? | Completion date |
| ClientGoals | string? | Client objectives |
| LegalRequirements | string? | Legal requirements |

**Implements:** `IAIGenerated`, `IApprovable`

**Computed Properties:** `TasksCompleted`, `TotalTasks`, `Assignees`, `UniqueAssigneeCount`, `OriginatingAttorney`, `ResponsibleAttorney`, `ResponsibleStaff`

### MatterAssignment (`Certio.Domain.Matters.MatterAssignment`)
User assignment to a matter.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| MatterId | int | Matter foreign key |
| UserId | int | User foreign key |
| AssignmentType | string | Type of assignment |
| Role | string? | Role within matter |

**AssignmentTypes:** `OriginatingAttorney`, `ResponsibleAttorney`, `ResponsibleStaff`, `RelevantContact`

### MatterPermission (`Certio.Domain.Matters.MatterPermission`)
Fine-grained matter access control.

---

## Tasks

### TaskItem (`Certio.Domain.Tasks.TaskItem`)
Task entity with hierarchical support.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| OrgId | int | Organization ID |
| MatterId | int? | Matter foreign key |
| Title | string | Task title |
| Description | string? | Description |
| Status | string | Pending, InProgress, Review, Completed |
| Priority | string | Low, Medium, High, Critical |
| Order | int | Sort order |
| DueDate | DateTime? | Due date |
| ParentTaskItemId | int? | Parent task (for subtasks) |

**Implements:** `IAIGenerated`, `IApprovable`

**Relationships:** `SubTaskItems`, `SubTasks`, `TaskAssignments`, `Comments`, `Dependencies`, `DependentItems`

### SubTaskItem (`Certio.Domain.Tasks.SubTaskItem`)
Subtask under a task.

### TaskAssignment (`Certio.Domain.Tasks.TaskAssignment`)
User assignment to a task.

**AssignmentTypes:** `Assignee`, `Reviewer`, `Observer`, `Contributor`

### TaskItemComment (`Certio.Domain.Tasks.TaskItemComment`)
Comment on a task with threading, mentions, and reactions.

### TaskItemDependency (`Certio.Domain.Tasks.TaskItemDependency`)
Task-to-task dependency (FinishToStart, etc.).

---

## Documents

### Document (`Certio.Domain.Documents.Document`)
Document entity with vector search support.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| OrgId | Guid | Organization ID |
| MatterId | Guid? | Matter foreign key |
| SourceType | enum | GoogleDrive, OneDrive, InternalUpload |
| Title | string | Document title |
| FileType | string? | File extension/type |
| FileSizeBytes | long? | File size |
| Status | enum | Draft, Review, Final, Published |
| VectorId | string? | Vector store reference |
| Tags | List\<string\> | Document tags |
| Metadata | Dictionary | Key-value metadata |

### DocumentVersion (`Certio.Domain.Documents.DocumentVersion`)
Document version tracking.

### DocumentVector (`Certio.Domain.Documents.DocumentVector`)
Vector embeddings for RAG search.

| Property | Type | Description |
|----------|------|-------------|
| Id | Guid | Primary key |
| DocumentId | Guid | Document foreign key |
| ChunkIndex | int | Chunk position |
| ContentChunk | string | Text content |
| Embedding | float[] | Vector embedding |

### DocumentPermission (`Certio.Domain.Documents.DocumentPermission`)
Document-level permissions (Read, Comment, Edit, Owner).

### ExternalConnection (`Certio.Domain.Documents.ExternalConnection`)
Google Drive / OneDrive OAuth connections.

### RagQuery / RagCacheEntry
RAG query tracking and caching.

---

## Communications

### Conversation (`Certio.Domain.Services.Conversation`)
Team chat conversation/channel.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| Title | string | Conversation title |
| ConversationType | string | Type (team, AI, etc.) |
| ChannelType | string? | Channel type |
| IsChannel | bool | Is a channel |
| IsPrivateChannel | bool | Private channel flag |
| OrganizationId | int | Organization scope |
| MatterId | int? | Optional matter scope |

### ChatMessage (`Certio.Domain.Services.ChatMessage`)
Message within a conversation.

### DirectThread (`Certio.Domain.Services.DirectThread`)
1-on-1 direct message thread.

### DirectMessage (`Certio.Domain.Services.DirectMessage`)
Message within a direct thread.

### DirectParticipant (`Certio.Domain.Services.DirectParticipant`)
Participant in a direct thread with read tracking.

---

## Email Integration

### EmailAccount (`Certio.Domain.Services.EmailAccount`)
Connected email account (Gmail/Outlook).

### EmailMessage (`Certio.Domain.Services.EmailMessage`)
Synced email message.

---

## Calendar

### CalendarEvent (`Certio.Domain.Calendar.CalendarEvent`)
Calendar event with external sync support.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| OrgId | int | Organization scope |
| MatterId | int? | Optional matter link |
| Title | string | Event title |
| StartDateTime | DateTime | Start time |
| EndDateTime | DateTime | End time |
| IsAllDayEvent | bool | All-day flag |
| EventType | string | Event type |
| ExternalCalendarId | string? | External calendar ID |
| ExternalCalendarSource | string? | Google/Outlook |
| IsRecurring | bool | Recurring flag |
| RecurrenceRule | string? | RRULE string |

### CalendarEventAttendee (`Certio.Domain.Calendar.CalendarEventAttendee`)
Event attendee with response status.

### CalendarIntegration (`Certio.Domain.Calendar.CalendarIntegration`)
Google Calendar / Outlook Calendar OAuth connection.

---

## Billing

### TimeEntry (`Certio.Domain.Billing.TimeEntry`)
Billable time tracking. **Inherits:** `AuditableEntity`

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| OrganizationId | int | Organization scope |
| MatterId | int? | Matter link |
| ClientId | int? | Client organization |
| AssigneeId | int? | Assigned user |
| Date | DateTime | Entry date |
| Description | string | Work description |
| Hours | decimal | Hours worked |
| Rate | decimal | Hourly rate |
| Amount | decimal | Calculated amount |
| Status | enum | NeedsReview, Approved, Billed, Paid |
| IsBillable | bool | Billable flag |

### Expense (`Certio.Domain.Billing.Expense`)
Expense tracking. **Inherits:** `AuditableEntity`

### Invoice (`Certio.Domain.Billing.Invoice`)
Client invoice. **Inherits:** `AuditableEntity`

### InvoiceLineItem (`Certio.Domain.Billing.InvoiceLineItem`)
Invoice line items linked to time entries or expenses.

### Retainer (`Certio.Domain.Billing.Retainer`)
Trust/retainer account. **Inherits:** `AuditableEntity`

### RetainerTransaction (`Certio.Domain.Billing.RetainerTransaction`)
Deposit/withdrawal transactions on retainers.

---

## AI & Agents

### AIAgent (`Certio.Domain.AIAgents.AIAgent`)
AI agent configuration.

### AIAgentExecution (`Certio.Domain.AIAgents.AIAgentExecution`)
Execution record for an AI agent run.

### AIAgentResult (`Certio.Domain.Services.AIAgentResult`)
Result of an AI agent execution (summary, goal, suggestion, etc.).

### AIUsage / AIUsageDaily (`Certio.Domain.AIAgents`)
Token usage and cost tracking per user/organization.

### ChatSummary (`Certio.Domain.AIAgents.ChatSummary`)
AI-generated conversation summary.

### ClientGoal (`Certio.Domain.AIAgents.ClientGoal`)
AI-extracted client goals from conversations.

### ReplySuggestion (`Certio.Domain.AIAgents.ReplySuggestion`)
AI-generated reply suggestions.

### ClarityExplanation (`Certio.Domain.AIAgents.ClarityExplanation`)
AI-generated legal clarity explanations.

### AgentAction (`Certio.Domain.AgentActions.AgentAction`)
AI-proposed action with approval workflow.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| RunId | string | Execution run ID |
| ActionType | string | Type of action |
| Status | string | Proposed, Approved, Rejected, Completed, Failed, RolledBack |
| ActionPayload | string | JSON action data |
| ResultPayload | string? | JSON result data |
| BeforeState | string? | State before execution |
| AfterState | string? | State after execution |
| IsReversible | bool | Can be rolled back |

**Key Methods:** `CanBeApproved()`, `CanBeRejected()`, `CanBeExecuted()`, `CanBeRolledBack()`, `GetPayload<T>()`, `SetPayload<T>()`

---

## Unified Inbox

### InboxItem (`Certio.Domain.UnifiedInbox.InboxItem`)
Aggregated inbox item from email, DM, or chat.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| Source | enum | Email, DirectMessage, Chat |
| Subject | string | Item subject |
| Status | string | Status |
| Priority | string | Priority level |
| Labels | List\<string\> | User labels |
| UnreadCount | int | Unread messages |

### InboxMessage (`Certio.Domain.UnifiedInbox.InboxMessage`)
Individual message within an inbox item.

---

## Change Control

### ChangeNotice (`Certio.Domain.ChangeControl.ChangeNotice`)
Change notice with acknowledgement tracking.

### ChangeNoticeRecipient (`Certio.Domain.ChangeControl.ChangeNoticeRecipient`)
Recipient with acknowledgement status.

---

## Notifications

### Notification (`Certio.Domain.Notifications.Notification`)
In-app notification.

### NotificationTemplate (`Certio.Domain.Notifications.NotificationTemplate`)
Notification template.

### NotificationPreference (`Certio.Domain.Notifications.NotificationPreference`)
Per-user notification preferences (email, push, in-app).

---

## Workflows

### Workflow (`Certio.Domain.Workflows.Workflow`)
Workflow definition with JSON-based step configuration.

### WorkflowInstance (`Certio.Domain.Workflows.WorkflowInstance`)
Running workflow instance.

---

## Audit

### AuditLog (`Certio.Domain.Audit.AuditLog`)
Comprehensive audit log entry.

| Property | Type | Description |
|----------|------|-------------|
| Id | int | Primary key |
| EntityType | string | Entity type name |
| EntityId | string | Entity identifier |
| Action | string | Action performed |
| Result | string | SUCCESS or FAILURE |
| UserId | string | Acting user ID |
| UserName | string | Acting user name |
| IPAddress | string? | Client IP |
| UserAgent | string? | Browser user agent |
| Description | string? | Human-readable description |
| Timestamp | DateTime | Event timestamp |
| OrganizationId | int? | Organization scope |
| MatterId | int? | Matter scope |
| ResponseCode | int? | HTTP response code |
| DurationMs | long? | Request duration |

---

## Entity Relationship Diagram

```
User ←──→ UserOrganization ←──→ Organization
  │                                    │
  ├── TeamMembership ←──→ Team ────────┘
  ├── MatterAssignment ←──→ Matter ←──→ Organization
  │                          │
  │                          ├── TaskItem ←──→ TaskAssignment ←──→ User
  │                          │     │
  │                          │     ├── SubTaskItem
  │                          │     ├── TaskItemComment (threaded, mentions, reactions)
  │                          │     └── TaskItemDependency
  │                          │
  │                          ├── Document ←──→ DocumentVersion
  │                          │     └── DocumentVector (RAG embeddings)
  │                          │
  │                          ├── CalendarEvent ←──→ CalendarEventAttendee
  │                          │
  │                          ├── TimeEntry, Expense, Invoice, Retainer
  │                          │
  │                          ├── ChangeNotice ←──→ ChangeNoticeRecipient
  │                          │
  │                          └── AgentAction (AI proposals)
  │
  ├── Conversation ←──→ ChatMessage
  ├── DirectThread ←──→ DirectMessage
  ├── EmailAccount ←──→ EmailMessage
  ├── InboxItem ←──→ InboxMessage
  ├── Notification
  └── TrustedDevice

Organization ←──→ OrganizationRelationship ←──→ Organization
                        │
                        └── AssignedUsers (junction)
```
