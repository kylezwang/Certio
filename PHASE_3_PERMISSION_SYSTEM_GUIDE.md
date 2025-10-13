# Phase 3: Permission System - Complete Guide

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Permission Types](#permission-types)
4. [Usage Guide](#usage-guide)
5. [Caching Strategy](#caching-strategy)
6. [Best Practices](#best-practices)
7. [Troubleshooting](#troubleshooting)
8. [API Reference](#api-reference)

---

## Overview

The Certio permission system implements fine-grained, multi-level authorization with intelligent caching. It supports:

- **Organization-level permissions** (role-based)
- **Matter-specific access control** (resource-based)
- **Task-level permissions** (contextual)
- **Law firm relationship permissions** (inheritance-based)
- **Redis-backed caching** (performance optimization)

### Key Features

✅ **Multi-Level Permission Checking**
- Organization membership validation
- Role-based permissions
- Resource-specific access control
- Contextual permission evaluation

✅ **Intelligent Caching**
- Two-tier caching (Memory + Redis)
- Automatic cache warming
- Performance target: < 50ms per check

✅ **Developer-Friendly**
- Declarative attributes
- Strongly-typed permissions
- Comprehensive error messages
- Built-in audit logging

---

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Controller Layer                         │
│  [RequirePermission] [RequireMatterAccess] [RequireTaskAccess]│
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│              Authorization Filters                          │
│  RequirePermissionFilter | RequireMatterAccessFilter        │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│         CachedPermissionService (IPermissionService)        │
│                  [Redis L2 Cache]                           │
│                  [Memory L1 Cache]                          │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│              PermissionService (Core Logic)                 │
│  - GetEffectivePermissions()                                │
│  - CanAccessMatter()                                        │
│  - HasFirmBasedAccess()                                     │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                  Database Layer                             │
│  UserOrganizations | MatterPermissions | MatterAssignments  │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Request arrives** at controller with permission attribute
2. **Filter extracts** user ID and context (org/matter/task ID)
3. **Cache check** (L1 Memory → L2 Redis)
4. **Cache miss** → Query database and populate cache
5. **Permission evaluated** against user's role and assignments
6. **Result returned** (Allow/Deny)
7. **Audit logged** (if denied)

---

## Permission Types

### Enum Definition

```csharp
public enum Permission
{
    // Document permissions
    ViewDocuments,
    DownloadDocuments,
    UploadDocuments,
    DeleteDocuments,
    CommentOnDocuments,
    
    // Matter permissions
    ViewMatters,
    CreateMatters,
    EditMatters,
    DeleteMatters,
    ManageMatterSettings,
    
    // User management permissions
    InviteUsers,
    RemoveUsers,
    ManageUserPermissions,
    
    // Communication permissions
    ViewMessages,
    SendMessages,
    DeleteMessages,
    ManageThreads,
    
    // System permissions
    ViewAuditLogs,
    ManageSystemSettings,
    AccessAdminPanel
}
```

### Permission Sets by Role

#### Client Roles

**Owner** (Full access):
```csharp
PermissionSets.ClientOwner = [
    ViewDocuments, DownloadDocuments, UploadDocuments, DeleteDocuments,
    ViewMatters, CreateMatters, EditMatters, DeleteMatters,
    InviteUsers, RemoveUsers, ManageUserPermissions,
    ViewMessages, SendMessages, DeleteMessages
]
```

**Manager** (Management access):
```csharp
PermissionSets.ClientManager = [
    ViewDocuments, DownloadDocuments, UploadDocuments,
    ViewMatters, EditMatters, ManageMatterSettings,
    InviteUsers, RemoveUsers,
    ViewMessages, SendMessages
]
```

**Member** (Standard access):
```csharp
PermissionSets.ClientMember = [
    ViewDocuments, DownloadDocuments, UploadDocuments,
    ViewMatters, CreateMatters, EditMatters,
    ViewMessages, SendMessages
]
```

**Lawyer** (Legal oversight):
```csharp
PermissionSets.ClientLawyer = [
    ViewDocuments, DownloadDocuments,
    ViewMatters, EditMatters, ManageMatterSettings,
    InviteUsers, RemoveUsers,
    ViewMessages, SendMessages,
    ViewAuditLogs
]
```

#### Law Firm Roles

**Partner** (Full client access):
```csharp
PermissionSets.LawFirmPartner = [
    All permissions for assigned client organizations
]
```

**Associate** (Limited access):
```csharp
PermissionSets.LawFirmAssociate = [
    ViewDocuments, DownloadDocuments, UploadDocuments,
    ViewMatters, EditMatters,
    ViewMessages, SendMessages
]
```

**Paralegal** (Task-focused):
```csharp
PermissionSets.LawFirmParalegal = [
    ViewDocuments, DownloadDocuments, UploadDocuments,
    ViewMatters, EditMatters,
    ViewMessages, SendMessages
]
```

#### External Roles

**Opposing Counsel**:
```csharp
PermissionSets.OpposingCounsel = [
    ViewDocuments, DownloadDocuments, CommentOnDocuments,
    ViewMatters,
    ViewMessages, SendMessages
]
```

**Expert Witness**:
```csharp
PermissionSets.ExpertWitness = [
    ViewDocuments, DownloadDocuments, CommentOnDocuments,
    ViewMatters,
    ViewMessages, SendMessages
]
```

---

## Usage Guide

### 1. Controller-Level Protection

#### Basic Permission Check

```csharp
using Certio.Web.Security;
using Certio.Domain.Users;

[Authorize(Policy = "OrgMember")]
[RequirePermission(Permission.CreateMatters)]
public async Task<IActionResult> CreateMatter()
{
    // User is guaranteed to have CreateMatters permission
    return View();
}
```

#### Matter-Specific Access

```csharp
[Authorize(Policy = "OrgMember")]
[RequireMatterAccess("matterId")]
public async Task<IActionResult> ViewMatter(int matterId)
{
    // User is guaranteed to have access to this specific matter
    var matter = await _matterService.GetByIdAsync(matterId);
    return View(matter);
}
```

#### Combined Permission + Resource Check

```csharp
[Authorize(Policy = "OrgMember")]
[RequireMatterOperation(Permission.EditMatters, "matterId")]
public async Task<IActionResult> EditMatter(int matterId, [FromBody] UpdateMatterDto dto)
{
    // User has BOTH:
    // 1. Access to this matter
    // 2. EditMatters permission in the organization
    
    var result = await _matterService.UpdateAsync(matterId, dto);
    return Ok(result);
}
```

#### Task Access Control

```csharp
[Authorize(Policy = "OrgMember")]
[RequireTaskAccess("taskId")]
public async Task<IActionResult> GetTask(int taskId)
{
    // User can access this task through:
    // - Direct task assignment
    // - Matter assignment
    // - Matter has "Everyone" access level
    
    var task = await _taskService.GetByIdAsync(taskId);
    return Ok(task);
}
```

### 2. Service-Level Permission Checks

#### Validate Permission (Throws on Failure)

```csharp
public async Task<ServiceResult<Matter>> CreateMatterAsync(CreateMatterDto dto, int userId, int orgId)
{
    // Validate permission - throws UnauthorizedOperationException if user lacks permission
    await _permissionService.ValidatePermissionOrThrowAsync(
        userId, 
        orgId, 
        Permission.CreateMatters, 
        "CreateMatter");
    
    // Create matter
    var matter = new Matter { /* ... */ };
    await _context.Matters.AddAsync(matter);
    await _context.SaveChangesAsync();
    
    return ServiceResult<Matter>.Success(matter);
}
```

#### Check Permission (Returns Bool)

```csharp
public async Task<ServiceResult<List<Matter>>> GetAccessibleMattersAsync(int userId, int orgId)
{
    // Check permission without throwing
    var canView = await _permissionService.HasPermissionAsync(
        userId, 
        orgId, 
        Permission.ViewMatters);
    
    if (!canView)
    {
        return ServiceResult<List<Matter>>.Failure("Insufficient permissions");
    }
    
    var matters = await GetMattersForUserAsync(userId, orgId);
    return ServiceResult<List<Matter>>.Success(matters);
}
```

#### Matter Access Validation

```csharp
public async Task<ServiceResult> DeleteMatterAsync(int matterId, int userId)
{
    // Validate matter access
    await _permissionService.ValidateMatterAccessOrThrowAsync(userId, matterId, "DeleteMatter");
    
    // Get matter to check organization
    var matter = await _context.Matters.FindAsync(matterId);
    
    // Validate delete permission in the organization
    await _permissionService.ValidatePermissionOrThrowAsync(
        userId, 
        matter.OrganizationId, 
        Permission.DeleteMatters, 
        "DeleteMatter");
    
    // Perform soft delete
    matter.IsDeleted = true;
    matter.DeletedAt = DateTime.UtcNow;
    matter.DeletedById = userId;
    
    await _context.SaveChangesAsync();
    return ServiceResult.Success();
}
```

### 3. Manual Permission Checks

#### In Razor Views

```cshtml
@inject IPermissionService PermissionService

@{
    var userId = User.GetUserId();
    var orgId = ViewBag.OrganizationId;
    var canEdit = await PermissionService.HasPermissionAsync(userId, orgId, Permission.EditMatters);
}

@if (canEdit)
{
    <button class="btn btn-primary" onclick="editMatter()">Edit Matter</button>
}
else
{
    <span class="text-muted">View Only</span>
}
```

#### In JavaScript (via API)

```javascript
async function checkPermission(permission) {
    const response = await fetch(`/api/permissions/check?permission=${permission}`);
    const result = await response.json();
    return result.hasPermission;
}

// Usage
if (await checkPermission('EditMatters')) {
    showEditButton();
}
```

---

## Caching Strategy

### Cache Layers

#### L1: In-Memory Cache
- **TTL:** 5 minutes
- **Scope:** Per web server instance
- **Use Case:** Hot data (frequently accessed)
- **Hit Time:** < 1ms

#### L2: Redis Distributed Cache
- **TTL:** 15 minutes (permissions), 10 minutes (matter access), 5 minutes (task access)
- **Scope:** Shared across all instances
- **Use Case:** Warm data (semi-frequently accessed)
- **Hit Time:** < 10ms

### Cache Keys

```
Permission Check:       perm:{userId}:{orgId}:{permission}
Effective Permissions:  eff_perms:{userId}:{orgId}
Matter Access:          matter_access:{userId}:{matterId}
Task Access:            task_access:{userId}:{taskId}
Org Membership:         org_member:{userId}:{orgId}
Firm Access:            firm_access:{userId}:{orgId}
Accessible Orgs:        accessible_orgs:{userId}
Firm Relationship:      firm_rel:{userId}:{orgId}
```

### Cache Invalidation

#### Automatic Invalidation (TTL)
- Permissions expire after 15 minutes
- Matter access expires after 10 minutes
- Task access expires after 5 minutes

#### Manual Invalidation (Future Enhancement)

```csharp
// When user role changes
await cachedPermissionService.InvalidateUserPermissionsAsync(userId);

// When matter permissions change
await cachedPermissionService.InvalidateMatterAccessAsync(matterId);

// When task assignments change
await cachedPermissionService.InvalidateTaskAccessAsync(taskId);
```

### Cache Performance Metrics

**Target SLAs:**
- Cache hit rate: > 80%
- Permission check latency: < 50ms
- Cache invalidation latency: < 100ms

**Monitoring:**
```csharp
// Log cache performance
_logger.LogInformation("Permission cache hit for user {UserId}, org {OrgId}, permission {Permission}", 
    userId, organizationId, permission);
```

---

## Best Practices

### 1. Always Use Attributes for Controllers

❌ **Bad:**
```csharp
public async Task<IActionResult> EditMatter(int id)
{
    var canEdit = await _permissionService.HasPermissionAsync(...);
    if (!canEdit) return Forbid();
    // ...
}
```

✅ **Good:**
```csharp
[RequireMatterOperation(Permission.EditMatters)]
public async Task<IActionResult> EditMatter(int id)
{
    // Permission guaranteed by attribute
    // ...
}
```

### 2. Validate in Service Layer Too (Defense in Depth)

```csharp
// Controller
[RequireMatterOperation(Permission.EditMatters)]
public async Task<IActionResult> EditMatter(int id, [FromBody] UpdateMatterDto dto)
{
    return await _matterService.UpdateAsync(id, dto, User.GetUserId());
}

// Service
public async Task<ServiceResult> UpdateAsync(int id, UpdateMatterDto dto, int userId)
{
    // Double-check permission in service layer
    await _permissionService.ValidateMatterAccessOrThrowAsync(userId, id, "UpdateMatter");
    // ...
}
```

### 3. Use Specific Permissions

❌ **Bad:**
```csharp
[RequirePermission(Permission.ViewMatters)] // Too broad
public async Task<IActionResult> DeleteMatter(int id)
```

✅ **Good:**
```csharp
[RequireMatterOperation(Permission.DeleteMatters)] // Specific
public async Task<IActionResult> DeleteMatter(int id)
```

### 4. Log Permission Denials

```csharp
if (!await _permissionService.HasPermissionAsync(userId, orgId, permission))
{
    _logger.LogWarning(
        "Permission denied: User {UserId} attempted {Operation} in org {OrgId} without {Permission}",
        userId, operation, orgId, permission);
    
    await _auditService.LogUnauthorizedAccessAsync(userId, operation, permission.ToString());
    
    throw new UnauthorizedOperationException(...);
}
```

### 5. Cache-Friendly Permission Checks

✅ **Cache-Friendly:**
```csharp
// Check permission once, use many times
var canEdit = await _permissionService.HasPermissionAsync(userId, orgId, Permission.EditMatters);

foreach (var matter in matters)
{
    if (canEdit)
    {
        matter.IsEditable = true;
    }
}
```

❌ **Cache-Unfriendly:**
```csharp
// Checking permission per matter (cache miss for each)
foreach (var matter in matters)
{
    matter.IsEditable = await _permissionService.CanAccessMatterAsync(userId, matter.Id);
}
```

### 6. Handle Permission Exceptions Gracefully

```csharp
try
{
    await _permissionService.ValidatePermissionOrThrowAsync(...);
    // Operation
}
catch (UnauthorizedOperationException ex)
{
    _logger.LogWarning(ex, "Unauthorized operation attempt");
    return Forbid(); // Return 403 Forbidden
}
```

---

## Troubleshooting

### Common Issues

#### 1. "Permission denied but user should have access"

**Diagnosis:**
```csharp
// Check effective permissions
var permissions = await _permissionService.GetEffectivePermissionsAsync(userId, orgId);
_logger.LogInformation("User {UserId} has permissions: {Permissions}", 
    userId, string.Join(", ", permissions));

// Check user's role
var userOrg = await _context.UserOrganizations
    .FirstAsync(uo => uo.UserId == userId && uo.OrganizationId == orgId);
_logger.LogInformation("User role: {Role}, UserType: {UserType}", 
    userOrg.Role, userOrg.UserType);
```

**Common Causes:**
- User has wrong role assigned
- Permission not included in role's permission set
- Cached old permissions (wait for TTL or clear cache)

#### 2. "Matter access denied but user is assigned"

**Diagnosis:**
```csharp
var matter = await _context.Matters
    .Include(m => m.Assignments)
    .Include(m => m.Permissions)
    .FirstAsync(m => m.Id == matterId);

_logger.LogInformation(
    "Matter {MatterId} AccessLevel: {AccessLevel}, Assignments: {Assignments}, Permissions: {Permissions}",
    matterId, 
    matter.AccessLevel,
    matter.Assignments.Count,
    matter.Permissions.Count);
```

**Common Causes:**
- Matter has `AccessLevel = "Specific"` but no permission granted
- Assignment was removed (`RemovedAt != null`)
- Permission was revoked (`RevokedAt != null`)

#### 3. "Law firm partner cannot access client matters"

**Diagnosis:**
```csharp
var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, clientOrgId);
_logger.LogInformation("Firm access: {HasAccess}", hasFirmAccess);

var relationship = await _permissionService.GetFirmRelationshipAsync(userId, clientOrgId);
if (relationship != null)
{
    _logger.LogInformation(
        "Relationship: Active={IsActive}, Deleted={IsDeleted}, Expires={ExpiresAt}",
        relationship.IsActive, relationship.IsDeleted, relationship.ExpiresAt);
}
```

**Common Causes:**
- Relationship is inactive (`IsActive = false`)
- Relationship is deleted (`IsDeleted = true`)
- Relationship has expired (`ExpiresAt < DateTime.UtcNow`)
- Wrong relationship type (not `LawFirmClient`)

#### 4. "Permission check is slow"

**Diagnosis:**
```csharp
var stopwatch = Stopwatch.StartNew();
var hasPermission = await _permissionService.HasPermissionAsync(userId, orgId, permission);
stopwatch.Stop();

_logger.LogWarning("Permission check took {ElapsedMs}ms (target: < 50ms)", 
    stopwatch.ElapsedMilliseconds);
```

**Common Causes:**
- Redis is down (falling back to database)
- Cache is cold (first access after restart)
- Complex permission inheritance calculation
- Database query is slow (missing indexes)

**Solutions:**
- Ensure Redis is running: `docker ps | grep redis`
- Check Redis connectivity: `redis-cli ping`
- Review database indexes on `UserOrganizations`, `MatterPermissions`, `MatterAssignments`

---

## API Reference

### IPermissionService Interface

#### HasPermissionAsync
```csharp
Task<bool> HasPermissionAsync(int userId, int organizationId, Permission permission)
```
Checks if user has a specific permission in an organization.

**Parameters:**
- `userId`: The user ID to check
- `organizationId`: The organization context
- `permission`: The permission to check

**Returns:** `true` if user has permission, `false` otherwise

**Caches:** Yes (15 min TTL)

---

#### GetEffectivePermissionsAsync
```csharp
Task<List<Permission>> GetEffectivePermissionsAsync(int userId, int organizationId)
```
Gets all permissions a user has in an organization.

**Returns:** List of all effective permissions (direct + firm-based)

**Caches:** Yes (15 min TTL)

---

#### CanAccessMatterAsync
```csharp
Task<bool> CanAccessMatterAsync(int userId, int matterId)
```
Checks if user can access a specific matter.

**Access Rules:**
- Matter has `AccessLevel = "Everyone"` → Org member can access
- Matter has `AccessLevel = "Specific"` → Requires `MatterPermission` or `MatterAssignment`

**Caches:** Yes (10 min TTL)

---

#### CanAccessTaskAsync
```csharp
Task<bool> CanAccessTaskAsync(int userId, int taskId)
```
Checks if user can access a specific task.

**Access Rules:**
- User has `TaskAssignment` → Can access
- User has `MatterAssignment` for parent matter → Can access
- Parent matter is accessible → Can access

**Caches:** Yes (5 min TTL)

---

#### ValidatePermissionOrThrowAsync
```csharp
Task ValidatePermissionOrThrowAsync(int userId, int organizationId, Permission permission, string operation)
```
Validates permission or throws `UnauthorizedOperationException`.

**Use Case:** Service layer validation

**Throws:** `UnauthorizedOperationException` if user lacks permission

---

#### CanPerformOperationAsync
```csharp
Task<bool> CanPerformOperationAsync(int userId, int matterId, Permission permission)
```
Checks if user can perform operation on a matter (combines access + permission check).

**Use Case:** Complex permission scenarios

---

### Authorization Attributes

#### [RequirePermission(Permission)]
```csharp
[RequirePermission(Permission.EditMatters)]
public async Task<IActionResult> Action()
```
Requires user to have specific permission in current organization.

---

#### [RequireMatterAccess("parameterName")]
```csharp
[RequireMatterAccess("matterId")]
public async Task<IActionResult> Action(int matterId)
```
Requires user to have access to specified matter.

**Parameter Lookup:** Route data → Query string → Form data

---

#### [RequireTaskAccess("parameterName")]
```csharp
[RequireTaskAccess("taskId")]
public async Task<IActionResult> Action(int taskId)
```
Requires user to have access to specified task.

---

#### [RequireMatterOperation(Permission, "parameterName")]
```csharp
[RequireMatterOperation(Permission.EditMatters, "matterId")]
public async Task<IActionResult> Action(int matterId)
```
Requires BOTH matter access AND specific permission.

---

## Conclusion

The Phase 3 permission system provides enterprise-grade security with:
- ✅ Fine-grained permission control
- ✅ Multi-level authorization
- ✅ Intelligent caching
- ✅ Developer-friendly API
- ✅ Comprehensive testing
- ✅ Audit logging

**For Questions or Issues:**
- Review this guide first
- Check the troubleshooting section
- Review the audit report (PHASE_3_PERMISSION_AUDIT_REPORT.md)
- Check unit tests for examples

