# Certio API Reference

**Last Updated:** February 11, 2026  
**Version:** 5.0.0

---

## Overview

Certio exposes both MVC controller endpoints (returning views) and REST API endpoints (returning JSON). All endpoints require authentication unless explicitly marked as `[AllowAnonymous]`.

**Base URL:** `http://localhost:5092` (development)

**Authentication:** Cookie-based (`CertioAuth`) via ASP.NET Core Identity  
**Authorization:** `OrgMember` policy requires organization membership

---

## Table of Contents

1. [MVC Controllers](#mvc-controllers)
2. [REST API Endpoints](#rest-api-endpoints)
3. [SignalR Hubs](#signalr-hubs)
4. [Webhook Endpoints](#webhook-endpoints)
5. [WOPI Endpoints](#wopi-endpoints)

---

## MVC Controllers

### HomeController
Public-facing pages and authentication.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | None | Landing page |
| GET | `/Home/Login` | None | Login page |
| GET | `/Home/Register` | None | Registration page |
| GET | `/Home/ForgotPassword` | None | Password reset |
| GET | `/Home/Privacy` | None | Privacy policy |
| GET | `/Home/Services` | None | Services page |

### ClientController
Organization dashboard and management.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client` | Authorize | Organization selection |
| GET | `/Client/{orgId}/Dashboard` | OrgMember | Organization dashboard |
| GET | `/Client/{orgId}/Teams` | OrgMember | Team management |
| GET | `/Client/{orgId}/Clients` | OrgMember | Client list |
| GET | `/Client/{orgId}/AccountSettings` | OrgMember | Account settings |
| POST | `/Client/{orgId}/AccountSettings/Save` | OrgMember | Save settings |
| POST | `/Client/{orgId}/AccountSettings/VerifyChanges` | OrgMember | Verify changes |
| GET/POST | `/Client/{orgId}/AddPeople` | OrgMember | Add team members |
| GET | `/Client/GetJoinCode` | Authorize | Generate join code |
| GET/POST | `/Client/{orgId}/NewClient` | OrgMember | Create new client |

### MatterController
Matter (case/project) management.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client/{orgId}/Matter` | OrgMember | Matter list |
| GET | `/Client/{orgId}/Matter/Details/{id}` | OrgMember | Matter details |
| GET | `/Client/{orgId}/Matter/Create` | OrgMember | Create form |
| POST | `/Client/{orgId}/Matter/Create` | OrgMember | Create matter |
| GET | `/Client/{orgId}/Matter/Edit/{id}` | OrgMember | Edit form |
| POST | `/Client/{orgId}/Matter/Edit` | OrgMember | Update matter |
| POST | `/Client/{orgId}/Matter/Delete/{id}` | OrgMember | Delete matter |
| PUT | `/Client/{orgId}/Matter/{matterId}` | OrgMember | Update matter (API) |
| GET | `/Client/{orgId}/Matter/{matterId}/Contacts` | OrgMember | Get contacts |
| GET | `/Client/{orgId}/Matter/{matterId}/Users` | OrgMember | Get users |
| POST | `/Client/{orgId}/Matter/{matterId}/Assignment` | OrgMember | Assign user |
| DELETE | `/Client/{orgId}/Matter/{matterId}/Assignment/{assignmentId}` | OrgMember | Remove assignment |

### TasksController
Task management.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client/{orgId}/Tasks` | OrgMember | Tasks page |
| GET | `/Client/{orgId}/Matter/{matterId}/Tasks` | OrgMember | Matter tasks |
| GET | `/Client/{orgId}/Matter/{matterId}/Timeline` | OrgMember | Task timeline |
| POST | `/Tasks/CreateTask` | OrgMember | Create task |
| POST | `/Tasks/UpdateTask` | OrgMember | Update task |
| POST | `/Tasks/DeleteTask` | OrgMember | Delete task |
| GET | `/Tasks/GetTask` | OrgMember | Get task details |
| POST | `/Tasks/SaveTask` | OrgMember | Save task |
| POST | `/Tasks/AddComment` | OrgMember | Add comment |
| POST | `/Tasks/UpdateComment` | OrgMember | Update comment |
| POST | `/Tasks/DeleteComment` | OrgMember | Delete comment |
| POST | `/Tasks/AddReaction` | OrgMember | Add reaction |
| POST | `/Tasks/RemoveReaction` | OrgMember | Remove reaction |
| POST | `/Tasks/AddDependency` | OrgMember | Add dependency |
| POST | `/Tasks/RemoveDependency` | OrgMember | Remove dependency |

### ChatController
AI-powered chat and conversations.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client/{orgId}/Chat` | OrgMember | Chat page |
| GET | `/Client/{orgId}/Chat/Conversations` | OrgMember | List conversations |
| GET | `/Client/{orgId}/Chat/AIConversations` | OrgMember | List AI conversations |
| GET | `/Client/{orgId}/Chat/Conversation/{id}` | OrgMember | View conversation |
| POST | `/Client/{orgId}/Chat/CreateConversation` | OrgMember | Create conversation |
| POST | `/Client/{orgId}/Chat/SendMessage` | OrgMember | Send message |
| POST | `/Client/{orgId}/Chat/UploadAttachment` | OrgMember | Upload attachment |
| POST | `/Client/{orgId}/Chat/RequestClarity` | OrgMember | Request AI clarity |
| GET | `/Client/{orgId}/Chat/Messages/{id}` | OrgMember | Get messages |
| POST | `/Client/{orgId}/Chat/GenerateAIResponse` | OrgMember | Generate AI response |
| POST | `/Client/{orgId}/Chat/GenerateAIResponseStream` | OrgMember | Stream AI response |

### BillingController
Billing, time tracking, expenses, invoices, and retainers.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client/{orgId}/Billing` | OrgMember | Billing dashboard |
| GET | `/Client/{orgId}/Billing/TimeEntries` | OrgMember | Time entries |
| GET | `/Client/{orgId}/Billing/Expenses` | OrgMember | Expenses |
| GET | `/Client/{orgId}/Billing/Overview` | OrgMember | Overview |
| GET | `/Client/{orgId}/Billing/Trusts` | OrgMember | Retainers/trusts |
| GET | `/Client/{orgId}/Billing/Invoices` | OrgMember | Invoices |

### CalendarController
Calendar management with Google/Outlook integration.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Client/{orgId}/Calendar` | OrgMember | Calendar page |
| GET | `/Client/{orgId}/Calendar/Events` | OrgMember | Get events |
| GET | `/Client/{orgId}/Calendar/Events/{eventId}` | OrgMember | Get event |
| POST | `/Client/{orgId}/Calendar/Events` | OrgMember | Create event |
| PUT | `/Client/{orgId}/Calendar/Events/{eventId}` | OrgMember | Update event |
| DELETE | `/Client/{orgId}/Calendar/Events/{eventId}` | OrgMember | Delete event |

### Other MVC Controllers

| Controller | Route | Description |
|------------|-------|-------------|
| CommunicationsController | `/Client/{orgId}/Communications` | Team communications |
| DocumentsController | `/Client/{orgId}/Documents` | Document management |
| HistoryController | `/Client/{orgId}/History` | Audit/activity history |
| SettingsController | `/Client/{orgId}/Settings` | Organization settings |
| ChangeNoticesController | `/Client/{orgId}/Matter/{matterId}/ChangeNotices` | Change control |
| PublicChangeNoticeController | `/public/change-notice/respond` | Public acknowledgement (AllowAnonymous) |
| AdminController | `/admin/*` | Admin functions |
| GlobalController | `/Global/Dashboard` | Global dashboard |

---

## REST API Endpoints

### Billing API (`/api/billing/{orgId}/`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/billing/{orgId}/time-entries` | Create time entry |
| PUT | `/api/billing/{orgId}/time-entries/{id}` | Update time entry |
| DELETE | `/api/billing/{orgId}/time-entries/{id}` | Delete time entry |
| GET | `/api/billing/{orgId}/time-entries` | List time entries |
| POST | `/api/billing/{orgId}/expenses` | Create expense |
| PUT | `/api/billing/{orgId}/expenses/{id}` | Update expense |
| DELETE | `/api/billing/{orgId}/expenses/{id}` | Delete expense |
| GET | `/api/billing/{orgId}/expenses` | List expenses |
| POST | `/api/billing/{orgId}/invoices` | Create invoice |
| PUT | `/api/billing/{orgId}/invoices/{id}` | Update invoice |
| DELETE | `/api/billing/{orgId}/invoices/{id}` | Delete invoice |
| GET | `/api/billing/{orgId}/invoices` | List invoices |
| POST | `/api/billing/{orgId}/retainers` | Create retainer |
| PUT | `/api/billing/{orgId}/retainers/{id}` | Update retainer |
| POST | `/api/billing/{orgId}/retainers/{id}/deposit` | Deposit to retainer |
| POST | `/api/billing/{orgId}/retainers/{id}/withdraw` | Withdraw from retainer |
| DELETE | `/api/billing/{orgId}/retainers/{id}` | Delete retainer |
| GET | `/api/billing/{orgId}/retainers` | List retainers |
| GET | `/api/billing/{orgId}/overview` | Billing overview |
| GET | `/api/billing/{orgId}/recent-activity` | Recent activity |
| GET | `/api/billing/{orgId}/expense-categories` | Expense categories |
| GET | `/api/billing/{orgId}/matters` | Matters for billing |
| GET | `/api/billing/{orgId}/clients` | Clients for billing |
| GET | `/api/billing/{orgId}/assignees` | Assignees for billing |

### Unified Inbox API (`/api/inbox`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/inbox` | List inbox items |
| GET | `/api/inbox/{id}` | Get inbox item |
| GET | `/api/inbox/{id}/messages` | Get messages for item |
| GET | `/api/inbox/matter/{matterId}` | Get matter inbox |
| GET | `/api/inbox/unread-count` | Get unread count |
| GET | `/api/inbox/search` | Search inbox |
| GET | `/api/inbox/stats` | Get inbox stats |
| POST | `/api/inbox/{id}/read` | Mark as read |
| POST | `/api/inbox/bulk-read` | Bulk mark as read |
| POST | `/api/inbox/{id}/unread` | Mark as unread |
| POST | `/api/inbox/{id}/archive` | Archive item |
| POST | `/api/inbox/{id}/unarchive` | Unarchive item |
| POST | `/api/inbox/{id}/toggle-flag` | Toggle flag |
| POST | `/api/inbox/{id}/labels/add` | Add labels |
| POST | `/api/inbox/{id}/labels/remove` | Remove labels |
| POST | `/api/inbox/{id}/snooze` | Snooze item |
| POST | `/api/inbox/{id}/link-matter/{matterId}` | Link to matter |
| POST | `/api/inbox/{id}/unlink-matter` | Unlink from matter |

### Agent Actions API (`/api/agent-actions`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/agent-actions/pending` | Get pending actions |
| GET | `/api/agent-actions/history` | Get action history |
| GET | `/api/agent-actions/by-status/{status}` | Get by status |
| GET | `/api/agent-actions/{id}` | Get action details |
| GET | `/api/agent-actions/by-run-id/{runId}` | Get by run ID |
| GET | `/api/agent-actions/matter/{matterId}` | Get by matter |
| GET | `/api/agent-actions/stats` | Get statistics |
| POST | `/api/agent-actions/propose` | Propose action |
| POST | `/api/agent-actions/propose/create-task` | Propose task creation |
| POST | `/api/agent-actions/propose/attach-file` | Propose file attachment |
| POST | `/api/agent-actions/propose/add-note` | Propose note |
| POST | `/api/agent-actions/propose/start-timer` | Propose timer |
| POST | `/api/agent-actions/{id}/approve` | Approve action |
| POST | `/api/agent-actions/{id}/reject` | Reject action |
| POST | `/api/agent-actions/bulk-approve` | Bulk approve |
| POST | `/api/agent-actions/bulk-reject` | Bulk reject |
| POST | `/api/agent-actions/{id}/execute` | Execute action |
| POST | `/api/agent-actions/execute-pending` | Execute all pending |
| POST | `/api/agent-actions/{id}/rollback` | Rollback action |

### Direct Messages API (`/api/dm`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/dm/threads` | Create thread |
| GET | `/api/dm/threads` | List threads |
| GET | `/api/dm/threads/{threadId}/messages` | Get messages |
| POST | `/api/dm/threads/{threadId}/messages` | Send message |
| POST | `/api/dm/threads/{threadId}/read` | Mark as read |
| POST | `/api/dm/notalize` | Notalize message |
| POST | `/api/dm/threads/{threadId}/send-email` | Send as email |
| POST | `/api/dm/notalize-join-code` | Notalize join code |
| POST | `/api/dm/settings/join-code-template` | Update template |
| GET | `/api/dm/settings/join-code-template` | Get template |

### Documents API (`/api/documents`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/documents/search` | Search documents |
| POST | `/api/documents/upload` | Upload document |
| GET | `/api/documents/{documentId}/download` | Download document |
| POST | `/api/documents/reindex/{orgId}` | Reindex documents |
| GET | `/api/documents/{documentId}/embed-url` | Get embed URL |
| GET | `/api/documents/list` | List documents |
| POST | `/api/documents/{documentId}/status` | Update status |
| POST | `/api/documents/{documentId}/move-to-matter` | Move to matter |

### Email OAuth API (`/api/email-oauth`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/email-oauth/gmail/authorize` | Authorize Gmail |
| GET | `/api/email-oauth/gmail/callback` | Gmail callback (AllowAnonymous) |
| GET | `/api/email-oauth/outlook/authorize` | Authorize Outlook |
| GET | `/api/email-oauth/outlook/callback` | Outlook callback (AllowAnonymous) |
| GET | `/api/email-oauth/status` | Get connection status |
| DELETE | `/api/email-oauth/{accountId}` | Delete email account |
| POST | `/api/email-oauth/sync` | Sync emails |
| GET | `/api/email-oauth/inbox` | Get inbox |
| GET | `/api/email-oauth/inbox/count` | Get inbox count |

### Drive OAuth API (`/api/drive-oauth`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/drive-oauth/google/authorize` | Authorize Google Drive |
| GET | `/api/drive-oauth/google/callback` | Google callback (AllowAnonymous) |
| GET | `/api/drive-oauth/onedrive/authorize` | Authorize OneDrive |
| GET | `/api/drive-oauth/onedrive/callback` | OneDrive callback (AllowAnonymous) |
| DELETE | `/api/drive-oauth/google` | Disconnect Google Drive |
| DELETE | `/api/drive-oauth/onedrive` | Disconnect OneDrive |
| POST | `/api/drive-oauth/sync` | Sync documents |
| GET | `/api/drive-oauth/status` | Get connection status |

### Calendar OAuth API (`/api/calendar-oauth`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar-oauth/google/authorize` | Authorize Google Calendar |
| GET | `/api/calendar-oauth/google/callback` | Google callback (AllowAnonymous) |
| GET | `/api/calendar-oauth/outlook/authorize` | Authorize Outlook Calendar |
| GET | `/api/calendar-oauth/outlook/callback` | Outlook callback (AllowAnonymous) |
| GET | `/api/calendar-oauth/status` | Get connection status |
| POST | `/api/calendar-oauth/disconnect` | Disconnect calendar |

### Calendar Sync API (`/api/calendar-sync`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/calendar-sync/unsynced-count` | Get unsynced count |
| POST | `/api/calendar-sync/sync` | Sync calendars |

### Other APIs

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/search/universal` | Universal search |
| GET | `/api/chat/channel/{channelId}/messages` | Get channel messages |
| GET | `/api/account-security/mfa/status` | Get MFA status |
| POST | `/api/account-security/mfa/toggle` | Toggle MFA |
| GET | `/api/account-security/sessions` | Get sessions |
| DELETE | `/api/account-security/sessions/{sessionId}` | Delete session |
| POST | `/api/account-security/sessions/signout-all` | Sign out all |
| GET | `/api/metrics/cache` | Get cache metrics |
| POST | `/api/metrics/reset` | Reset cache |
| POST | `/api/metrics/report` | Report metrics |
| GET | `/api/auth/providers` | Get auth providers |
| GET | `/api/auth/oauth-url` | Get OAuth URL |

---

## SignalR Hubs

### ChatHub (`/hubs/chat`)
**Auth:** `[Authorize]`  
Real-time team channel messaging.

### DirectHub (`/hubs/direct`)
**Auth:** `[Authorize]`  
Real-time direct messaging between users.

### NotificationHub (`/hubs/notifications`)
**Auth:** `[Authorize]`  
Real-time notification delivery.

### UpdatesHub (`/hubs/updates`)
Real-time data update notifications.

---

## Webhook Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/email-webhook/gmail` | None | Gmail push notifications |
| POST | `/api/email-webhook/outlook` | None | Outlook push notifications |
| POST | `/api/webhooks/google` | None | Google Drive webhooks |
| POST | `/api/webhooks/microsoft` | None | OneDrive webhooks |

---

## WOPI Endpoints (`/wopi/files/{fileId}`)

Web Application Open Platform Interface for Office Online integration.

**Auth:** `[AllowAnonymous]` (uses WOPI access tokens)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/wopi/files/{fileId}` | Check file info |
| GET | `/wopi/files/{fileId}/contents` | Get file contents |
| POST | `/wopi/files/{fileId}/contents` | Put file contents |
| PUT | `/wopi/files/{fileId}/contents` | Put file contents |
| POST | `/wopi/files/{fileId}/lock` | Lock file |
| POST | `/wopi/files/{fileId}/unlock` | Unlock file |
| POST | `/wopi/files/{fileId}/refreshlock` | Refresh lock |
| POST | `/wopi/files/{fileId}/getlock` | Get lock |

---

## Health Check

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/healthz` | Application health check |
