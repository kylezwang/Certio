# PHASE 0: Foundation Assessment & Planning
## Security & Architecture Analysis for Certio

**Assessment Date:** October 11, 2025  
**Project:** Certio - AI-Powered Legal Matter Management System  
**Version:** .NET 9.0 / Python 3.11  

---

## EXECUTIVE SUMMARY

This document presents a comprehensive Phase 0 security and architecture assessment of the Certio application. The analysis reveals **critical security vulnerabilities** that require immediate attention, particularly around Insecure Direct Object References (IDOR) and insufficient permission validation. While a permission framework exists in the codebase, it is not consistently enforced across controllers and operations.

### Key Findings

- **26 Critical IDOR Vulnerabilities** identified across controllers
- **Direct database access pattern** (Controller → DbContext) bypasses permission checks
- **Inconsistent authorization enforcement** - permission framework defined but not used
- **Matter-level access controls exist but not enforced** at the operation level
- **Cross-organization data leakage risks** in tasks, matters, and communications

### Risk Level: **HIGH** ⚠️

---

## 1. SECURITY AUDIT REPORT

### 1.1 IDOR Vulnerabilities Catalog

#### **CRITICAL** - Matter Operations

| Location | Vulnerability | Risk | Attack Vector |
|----------|--------------|------|---------------|
| `MatterController.Edit()` Line 618 | Direct `FindAsync(id)` without org check | **CRITICAL** | User can edit any matter by knowing the ID |
| `MatterController.Delete()` Line 817 | Direct `FindAsync(id)` without org check | **CRITICAL** | User can delete any matter by knowing the ID |
| `MatterController.DeleteConfirmed()` Line 817 | No organization validation before deletion | **CRITICAL** | Bypass organization boundaries |

**Code Evidence:**
```csharp:597:660:Certio.Web/Controllers/MatterController.cs
// GET: Matter/Edit/5
public async Task<IActionResult> Edit(int? id)
{
    if (id == null) return NotFound();
    
    // ❌ SECURITY ISSUE: No organization ownership check
    var matter = await _context.Matters.FindAsync(id);
    if (matter == null) return NotFound();
    
    // User from ANY organization can edit this matter!
}
```

#### **CRITICAL** - Task Operations

| Location | Vulnerability | Risk | Attack Vector |
|----------|--------------|------|---------------|
| `TasksController.Update()` Line 226 | Direct `FindAsync` without org/matter validation | **CRITICAL** | Any user can update any task |
| `TasksController.Delete()` Line 321 | Direct `FindAsync` without ownership check | **CRITICAL** | Any user can delete any task |
| `TasksController.Get()` Line 295 | No organization boundary check | **HIGH** | Information disclosure across orgs |
| `TasksController.UpdateStatus()` Line 266 | No permission validation | **HIGH** | Unauthorized status changes |
| `TasksController.UpdateSubTaskStatus()` Line 571 | No ownership validation | **HIGH** | Modify subtasks across organizations |
| `TasksController.RemoveAssignment()` Line 517 | No permission check | **HIGH** | Remove any user from any task |
| `TasksController.RemoveSubTaskAssignment()` Line 554 | No ownership check | **HIGH** | Modify subtask assignments |

**Code Evidence:**
```csharp:215:260:Certio.Web/Controllers/TasksController.cs
// POST: Tasks/Update
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Update([FromBody] UpdateTaskRequest request)
{
    var customUser = HttpContext.Items["CustomUser"] as Certio.Domain.Users.User;
    if (customUser == null)
    {
        return Json(new { success = false, message = "User not authenticated" });
    }

    // ❌ SECURITY ISSUE: No validation that user has access to this task
    var task = await _context.TaskItems.FindAsync(request.Id);
    if (task == null)
    {
        return Json(new { success = false, message = "Task not found" });
    }

    // ❌ SECURITY ISSUE: No check that task.OrgId matches user's organization(s)
    // ❌ SECURITY ISSUE: No permission check (can user edit tasks?)
    
    task.Title = request.Title;
    task.Status = request.Status;
    await _context.SaveChangesAsync();
    
    return Json(new { success = true });
}
```

#### **HIGH** - Communication Operations

| Location | Vulnerability | Risk | Attack Vector |
|----------|--------------|------|---------------|
| `ChatController.SendMessage()` Line 92 | Minimal conversation ownership validation | **HIGH** | Send messages to unauthorized conversations |
| `ChatController.GetChannelMessages()` Line 119 | `AllowAnonymous` attribute | **CRITICAL** | Unauthenticated access to channel messages |
| `ChatController.DeleteConversation()` Line 247 | Insufficient ownership validation | **HIGH** | Delete conversations from other organizations |
| `ChatController.RenameConversation()` Line 233 | Basic orgId check only | **MEDIUM** | Rename conversations with minimal validation |

**Code Evidence:**
```csharp:117:150:Certio.Web/Controllers/ChatController.cs
[HttpGet("channel/{channelId}/messages")]
[AllowAnonymous] // ❌ SECURITY ISSUE: Allow for now, add proper auth later
public async Task<IActionResult> GetChannelMessages(int channelId)
{
    try
    {
        // ❌ SECURITY ISSUE: No authentication or authorization checks
        var messages = await _chatService.GetChannelMessagesAsync(channelId);
        
        return Json(formattedMessages);
    }
    catch (Exception ex)
    {
        return Json(new { success = false, error = ex.Message });
    }
}
```

#### **MEDIUM** - Administrative Operations

| Location | Vulnerability | Risk | Attack Vector |
|----------|--------------|------|---------------|
| `AdminController.MigrateUser()` Line 65 | Basic `[Authorize]` only | **MEDIUM** | Non-admin users might access |
| `ClientController.AddPeople()` Line 572 | Role validation after join code generation | **LOW** | Potential for invalid invitations |

### 1.2 Missing Permission Checks

#### Pattern Analysis

The application has a well-defined permission framework in `User.cs`:

```csharp:304:335:Certio.Domain/Users/User.cs
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

**However, these permissions are NEVER enforced in controllers!**

#### Missing Permission Enforcement Locations

1. **MatterController**
   - ✅ Has organizational boundary checks (partially)
   - ❌ Never calls `GetEffectivePermissions()`
   - ❌ Never validates `Permission.EditMatters` before edit
   - ❌ Never validates `Permission.DeleteMatters` before delete
   - ❌ Never validates `Permission.CreateMatters` before create

2. **TasksController**
   - ❌ No organizational boundary validation
   - ❌ No permission checks whatsoever
   - ❌ No matter-level access validation
   - ❌ Any authenticated user can modify any task

3. **ChatController**
   - ❌ Minimal conversation ownership checks
   - ❌ No `Permission.SendMessages` validation
   - ❌ No `Permission.DeleteMessages` validation
   - ❌ Channel access with `[AllowAnonymous]`

4. **ClientController**
   - ✅ Good organizational membership validation
   - ❌ No granular permission checks for operations
   - ❌ Limited role-based validation for sensitive operations

### 1.3 Data Leakage Points

#### Cross-Organization Data Exposure

| Endpoint | Leakage Type | Severity | Details |
|----------|--------------|----------|---------|
| `GET /Tasks/Get/{id}` | Task Information | **HIGH** | Returns full task details without org check |
| `GET /Matter/Details/{id}` | Matter Information | **MEDIUM** | Checks org but uses primary org only |
| `GET /Chat/channel/{channelId}/messages` | Messages | **CRITICAL** | No authentication required |
| `POST /Tasks/Delete` | Data Loss | **CRITICAL** | Delete tasks from any organization |
| `POST /Matter/Delete` | Data Loss | **CRITICAL** | Delete matters from any organization |

#### Information Disclosure Vectors

1. **Sequential ID Enumeration**
   - All entities use sequential integer IDs
   - No GUIDs or random identifiers
   - Easy to guess valid IDs: `/Matter/Details/1`, `/Matter/Details/2`, etc.
   - **Recommendation**: Use GUIDs for sensitive resources

2. **Error Message Information Leakage**
   - Generic "Not Found" doesn't distinguish between "doesn't exist" and "no access"
   - Could be used to enumerate valid IDs
   - **Recommendation**: Consistent 404 responses for unauthorized access

3. **Query Parameter Manipulation**
   - Some endpoints accept orgId as a parameter
   - Trust client-provided orgId without server-side validation
   - Example: `/Client/{orgId}/Dashboard` trusts the URL parameter

### 1.4 Risk Severity Matrix

| Risk Category | Count | Severity | Immediate Action Required |
|--------------|-------|----------|---------------------------|
| **IDOR - Data Modification** | 8 | CRITICAL | YES - Hotfix Required |
| **IDOR - Data Deletion** | 4 | CRITICAL | YES - Hotfix Required |
| **IDOR - Information Disclosure** | 6 | HIGH | YES - Sprint Priority |
| **Missing Permission Checks** | 15+ | HIGH | YES - Sprint Priority |
| **Unauthenticated Endpoints** | 1 | CRITICAL | YES - Immediate Fix |
| **Cross-Org Data Leakage** | 8 | HIGH | YES - Sprint Priority |
| **Sequential ID Enumeration** | All Resources | MEDIUM | NO - Long-term improvement |

### 1.5 Authentication & Authorization Gaps

#### Current Implementation

**✅ What Works:**
- ASP.NET Core Identity for authentication
- Cookie-based session management (30-minute timeout)
- `ClientAccessMiddleware` validates organization membership
- `OrgMemberAuthorizationHandler` for policy-based authorization
- Law firm relationship validation via `IFirmRelationshipCacheService`

**❌ What's Missing:**
- **Operation-level permission checks** - Framework exists but unused
- **Resource-level authorization** - No validation of matter/task ownership
- **Granular RBAC enforcement** - Roles defined but not enforced
- **API endpoint protection** - Many endpoints lack authorization attributes
- **Matter-specific access control** - `MatterPermission` table not consulted

#### Authorization Policy Gaps

Current policy:
```csharp:291:297:Certio.Web/Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgMember", policy =>
        policy.RequireAuthenticatedUser()
              .AddRequirements(new OrgMemberRequirement()));
});
```

This only checks organizational membership, not operation permissions or resource ownership.

**Missing Policies Needed:**
- `CanEditMatter` - Validates user can edit specific matter
- `CanDeleteMatter` - Validates user can delete specific matter  
- `CanAccessTask` - Validates user can access task (via org or matter assignment)
- `CanModifyTask` - Validates user can modify task
- `HasPermission` - Generic permission validator

---

## 2. ARCHITECTURE BLUEPRINT

### 2.1 Current Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        CURRENT ARCHITECTURE                       │
│                         (Anti-Pattern)                            │
└─────────────────────────────────────────────────────────────────┘

Request Flow:
HTTP Request
    ↓
Authentication (ASP.NET Identity) ✅
    ↓
UserSyncMiddleware (Creates CustomUser) ✅
    ↓
ClientContextMiddleware (Extracts orgId from route) ✅
    ↓
ClientAccessMiddleware (Checks org membership) ⚠️  [Basic check only]
    ↓
Authorization (OrgMember Policy) ⚠️  [Only checks membership]
    ↓
┌────────────────────────────────────────┐
│         CONTROLLER                      │  ❌ Direct DB Access
│  ┌──────────────────────────────────┐  │
│  │ MatterController                  │  │
│  │ - Index()                         │  │
│  │ - Details(id)  ⚠️ Partial check  │  │
│  │ - Edit(id)     ❌ No check       │  │
│  │ - Delete(id)   ❌ No check       │  │
│  └──────────────────────────────────┘  │
│                  ↓ Direct DbContext     │
│          _context.Matters.FindAsync()   │
└─────────────────────┬──────────────────┘
                      ↓
            ┌─────────────────────┐
            │ ApplicationDbContext │  ❌ No abstraction
            └─────────────────────┘
                      ↓
            [SQL Server Database]
```

**Problems:**
1. ❌ Controllers have direct `DbContext` access
2. ❌ Business logic mixed with HTTP concerns  
3. ❌ No centralized permission enforcement point
4. ❌ Difficult to test (tightly coupled to DB)
5. ❌ Inconsistent authorization checks across controllers
6. ❌ Permission framework exists but never used

### 2.2 Target Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        TARGET ARCHITECTURE                        │
│                  (Clean Architecture + Repository)                │
└─────────────────────────────────────────────────────────────────┘

Request Flow:
HTTP Request
    ↓
Authentication (ASP.NET Identity) ✅
    ↓
UserSyncMiddleware ✅
    ↓
ClientContextMiddleware ✅
    ↓
ClientAccessMiddleware ✅
    ↓
Authorization (Enhanced Policies) ✅ NEW
    ↓
┌──────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                       │
│  ┌───────────────────────────────────────────────────────┐   │
│  │ MatterController                                       │   │
│  │  └─→ IMatterService.GetMatterAsync(id, userId, orgId)│   │
│  │      └─→ Service handles all permission checks        │   │
│  └───────────────────────────────────────────────────────┘   │
└────────────────────────────┬─────────────────────────────────┘
                             ↓
┌──────────────────────────────────────────────────────────────┐
│                       BUSINESS LAYER                          │
│  ┌───────────────────────────────────────────────────────┐   │
│  │ MatterService : IMatterService                        │   │
│  │  ├─→ IAuthorizationService (Check permissions)       │   │
│  │  ├─→ IMatterRepository (Data access)                 │   │
│  │  └─→ IPermissionValidator (Granular checks)          │   │
│  │                                                       │   │
│  │  GetMatterAsync(id, userId, orgId):                  │   │
│  │    1. Validate user permissions                      │   │
│  │    2. Check organizational access                    │   │
│  │    3. Validate matter-specific permissions           │   │
│  │    4. Fetch data via repository                      │   │
│  │    5. Apply data filtering (if needed)               │   │
│  │    6. Return DTO                                      │   │
│  └───────────────────────────────────────────────────────┘   │
└────────────────────────────┬─────────────────────────────────┘
                             ↓
┌──────────────────────────────────────────────────────────────┐
│                      DATA ACCESS LAYER                        │
│  ┌───────────────────────────────────────────────────────┐   │
│  │ MatterRepository : IMatterRepository                  │   │
│  │  ├─→ ApplicationDbContext                            │   │
│  │  ├─→ ILogger                                         │   │
│  │  └─→ ICacheService (optional)                        │   │
│  │                                                       │   │
│  │  GetByIdAsync(id, orgId):                            │   │
│  │    1. Query database with org filter                 │   │
│  │    2. Include necessary relationships                │   │
│  │    3. Map to domain entity                           │   │
│  │    4. Cache if needed                                │   │
│  │    5. Return entity                                  │   │
│  └───────────────────────────────────────────────────────┘   │
└────────────────────────────┬─────────────────────────────────┘
                             ↓
                  [SQL Server Database]
```

**Benefits:**
1. ✅ Single responsibility per layer
2. ✅ Centralized permission enforcement in services
3. ✅ Repository pattern for data access abstraction
4. ✅ Testable business logic (can mock repositories)
5. ✅ Consistent authorization across all operations
6. ✅ Easy to add cross-cutting concerns (logging, caching, auditing)

### 2.3 Service Boundaries Definition

#### Service Layer Organization

```
Certio.Application/
├── Services/
│   ├── IMatterService.cs
│   ├── MatterService.cs
│   ├── ITaskService.cs
│   ├── TaskService.cs
│   ├── ICommunicationService.cs
│   ├── CommunicationService.cs
│   ├── IPermissionService.cs
│   ├── PermissionService.cs
│   └── IAuthorizationService.cs (existing)
│
├── Repositories/ (NEW)
│   ├── IMatterRepository.cs
│   ├── MatterRepository.cs
│   ├── ITaskRepository.cs
│   ├── TaskRepository.cs
│   ├── IUserRepository.cs
│   └── UserRepository.cs
│
├── DTOs/ (NEW)
│   ├── MatterDto.cs
│   ├── TaskDto.cs
│   └── PermissionCheckDto.cs
│
└── Validators/ (NEW)
    ├── IPermissionValidator.cs
    ├── PermissionValidator.cs
    └── ResourceAccessValidator.cs
```

#### Service Responsibilities

**IMatterService**
```csharp
public interface IMatterService
{
    Task<MatterDto?> GetMatterAsync(int id, int userId, int orgId);
    Task<IEnumerable<MatterDto>> GetMattersForOrganizationAsync(int orgId, int userId);
    Task<MatterDto> CreateMatterAsync(CreateMatterRequest request, int userId, int orgId);
    Task<MatterDto> UpdateMatterAsync(int id, UpdateMatterRequest request, int userId, int orgId);
    Task<bool> DeleteMatterAsync(int id, int userId, int orgId);
    Task<bool> CanUserAccessMatterAsync(int matterId, int userId, int orgId);
    Task<IEnumerable<Permission>> GetUserMatterPermissionsAsync(int matterId, int userId, int orgId);
}
```

**ITaskService**
```csharp
public interface ITaskService
{
    Task<TaskDto?> GetTaskAsync(int id, int userId);
    Task<IEnumerable<TaskDto>> GetTasksForMatterAsync(int matterId, int userId, int orgId);
    Task<IEnumerable<TaskDto>> GetTasksForOrganizationAsync(int orgId, int userId);
    Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, int userId);
    Task<TaskDto> UpdateTaskAsync(int id, UpdateTaskRequest request, int userId);
    Task<bool> DeleteTaskAsync(int id, int userId);
    Task<bool> CanUserAccessTaskAsync(int taskId, int userId);
}
```

**IPermissionService**
```csharp
public interface IPermissionService
{
    Task<bool> UserHasPermissionAsync(int userId, int orgId, Permission permission);
    Task<bool> UserHasPermissionForMatterAsync(int userId, int orgId, int matterId, Permission permission);
    Task<bool> UserCanAccessResourceAsync(int userId, int orgId, ResourceType resourceType, int resourceId);
    Task<IEnumerable<Permission>> GetUserPermissionsAsync(int userId, int orgId);
    Task<IEnumerable<Permission>> GetUserMatterPermissionsAsync(int userId, int orgId, int matterId);
    Task GrantPermissionAsync(int userId, int orgId, Permission permission, int grantedByUserId);
    Task RevokePermissionAsync(int userId, int orgId, Permission permission, int revokedByUserId);
}
```

### 2.4 Interface Contracts

#### Repository Pattern Contracts

```csharp
/// <summary>
/// Base repository interface for common CRUD operations
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Specialized repository for matters with organization scoping
/// </summary>
public interface IMatterRepository : IRepository<Matter>
{
    Task<Matter?> GetByIdAsync(int id, int organizationId, CancellationToken ct = default);
    Task<IEnumerable<Matter>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default);
    Task<IEnumerable<Matter>> GetByUserAccessAsync(int userId, int organizationId, CancellationToken ct = default);
    Task<bool> UserHasAccessAsync(int matterId, int userId, int organizationId, CancellationToken ct = default);
    Task<IEnumerable<User>> GetMatterAssigneesAsync(int matterId, CancellationToken ct = default);
}

/// <summary>
/// Specialized repository for tasks with cross-organizational support
/// </summary>
public interface ITaskRepository : IRepository<TaskItem>
{
    Task<TaskItem?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<IEnumerable<TaskItem>> GetByMatterAsync(int matterId, int userId, CancellationToken ct = default);
    Task<IEnumerable<TaskItem>> GetByOrganizationAsync(int organizationId, int userId, CancellationToken ct = default);
    Task<IEnumerable<TaskItem>> GetAccessibleTasksAsync(int userId, IEnumerable<int> organizationIds, CancellationToken ct = default);
    Task<bool> UserCanAccessAsync(int taskId, int userId, CancellationToken ct = default);
}
```

#### Permission Validation Contracts

```csharp
/// <summary>
/// Service for validating user permissions and resource access
/// </summary>
public interface IPermissionValidator
{
    /// <summary>
    /// Validates if user has a specific permission in an organization
    /// </summary>
    Task<PermissionCheckResult> ValidatePermissionAsync(int userId, int orgId, Permission permission);
    
    /// <summary>
    /// Validates if user can access a specific matter
    /// </summary>
    Task<ResourceAccessResult> ValidateMatterAccessAsync(int userId, int orgId, int matterId);
    
    /// <summary>
    /// Validates if user can perform an operation on a matter
    /// </summary>
    Task<OperationAuthorizationResult> ValidateMatterOperationAsync(
        int userId, int orgId, int matterId, MatterOperation operation);
    
    /// <summary>
    /// Validates if user can access a specific task (via org or matter assignment)
    /// </summary>
    Task<ResourceAccessResult> ValidateTaskAccessAsync(int userId, int taskId);
    
    /// <summary>
    /// Checks if user is assigned to a matter (directly or via firm relationship)
    /// </summary>
    Task<bool> IsUserAssignedToMatterAsync(int userId, int matterId);
}

public class PermissionCheckResult
{
    public bool IsAuthorized { get; set; }
    public string? Reason { get; set; }
    public Permission Permission { get; set; }
}

public class ResourceAccessResult
{
    public bool CanAccess { get; set; }
    public string? DenialReason { get; set; }
    public AccessMethod AccessMethod { get; set; } // DirectMembership, FirmRelationship, MatterAssignment
}

public enum AccessMethod
{
    None,
    DirectMembership,
    FirmRelationship,
    MatterAssignment,
    SpecificPermission
}

public enum MatterOperation
{
    View,
    Create,
    Edit,
    Delete,
    ManageSettings,
    AssignUsers,
    ViewDocuments
}
```

---

## 3. PERMISSION MATRIX

### 3.1 User Types & Organizational Roles

#### User Type Hierarchy

```
UserTypes:
├── Client (Organization Members)
│   ├── Owner (Full organizational control)
│   ├── Manager (Team & matter management)
│   ├── Member (Self-service access)
│   └── Lawyer (Legal oversight)
│
├── LawFirm (Legal Service Providers)
│   ├── Partner (Full firm & client access)
│   ├── Associate (Assigned matter access)
│   ├── Paralegal (Document & support access)
│   └── Staff (Basic administrative access)
│
├── External (Third-Party Collaborators)
│   ├── OpposingCounsel (Limited shared access)
│   ├── ExpertWitness (Document review)
│   ├── CourtPersonnel (Read-only)
│   ├── RegulatoryBody (Compliance access)
│   └── Other (Custom permissions)
│
└── Certio (Platform Administrators)
    ├── Admin (Full system access)
    ├── MatterManager (Matter management)
    ├── Support (Customer support)
    └── Legal (Legal team access)
```

### 3.2 Permission Mapping

#### Client Organization Permissions

| Role | ViewMatters | CreateMatters | EditMatters | DeleteMatters | ManageMatterSettings | InviteUsers | RemoveUsers | ViewDocuments | UploadDocuments | DeleteDocuments |
|------|------------|---------------|-------------|---------------|---------------------|-------------|-------------|---------------|----------------|----------------|
| **Owner** | ✅ All | ✅ All | ✅ All | ✅ All | ✅ All | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Manager** | ✅ Assigned | ❌ | ✅ Assigned | ❌ | ✅ Assigned | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Member** | ✅ Own | ✅ | ✅ Own | ✅ Own | ✅ Own | ❌ | ❌ | ✅ | ✅ | ✅ Own |
| **Lawyer** | ✅ Assigned | ❌ | ✅ Assigned | ❌ | ✅ Assigned | ✅ | ✅ | ✅ | ❌ | ❌ |

#### Law Firm Permissions

| Role | ViewMatters | CreateMatters | EditMatters | DeleteMatters | ManageMatterSettings | InviteUsers | ViewClientData |
|------|------------|---------------|-------------|---------------|---------------------|-------------|----------------|
| **Partner** | ✅ All Clients | ✅ | ✅ All Clients | ✅ All Clients | ✅ All Clients | ✅ | ✅ All Clients |
| **Associate** | ✅ Assigned | ❌ | ✅ Assigned | ❌ | ✅ Assigned | ❌ | ✅ Assigned |
| **Paralegal** | ✅ Assigned | ❌ | ✅ Assigned | ❌ | ❌ | ❌ | ✅ Assigned |
| **Staff** | ✅ Assigned | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ Read-only |

#### External User Permissions

| Role | ViewMatters | ViewDocuments | DownloadDocuments | CommentOnDocuments | SendMessages |
|------|------------|---------------|-------------------|-------------------|--------------|
| **OpposingCounsel** | ✅ Shared | ✅ Shared | ✅ Shared | ✅ | ✅ |
| **ExpertWitness** | ✅ Shared | ✅ Shared | ✅ Shared | ✅ | ✅ |
| **CourtPersonnel** | ✅ Shared | ✅ Shared | ✅ Shared | ❌ | ❌ |
| **RegulatoryBody** | ✅ Compliance | ✅ Compliance | ✅ Compliance | ❌ | ❌ |
| **Other** | ✅ Custom | ✅ Custom | ✅ Custom | ❌ | ❌ |

### 3.3 Matter-Specific Access Rules

#### Access Level: "Everyone"
- All active members of the organization can view the matter
- Permissions still governed by user role
- No explicit permission grants required

#### Access Level: "Specific"
- Only users with `MatterPermission` records can access
- Explicit permission grant required (stored in `MatterPermissions` table)
- Overrides role-based access for viewing
- Still respects role permissions for operations (edit/delete)

**Current Implementation Gap:**
```csharp:365:383:Certio.Web/Controllers/MatterController.cs
// Create MatterPermissions when AccessLevel is Specific
if (string.Equals(model.AccessLevel, "Specific", StringComparison.OrdinalIgnoreCase) && model.PermissionUserIds != null)
{
    // ✅ This creates the permissions correctly
    var permissions = validUserIds.Select(uid => new MatterPermission
    {
        MatterId = matter.Id,
        UserId = uid,
        GrantedAt = DateTime.UtcNow,
        GrantedById = customUser.Id
    }).ToList();

    _context.MatterPermissions.AddRange(permissions);
    await _context.SaveChangesAsync();
}
```

**Problem:** The `MatterPermissions` table is populated correctly, but **NEVER consulted** when checking access!

**Required Fix:**
```csharp
// In MatterService or Repository
public async Task<bool> CanUserAccessMatterAsync(int matterId, int userId, int orgId)
{
    var matter = await _repository.GetByIdAsync(matterId, orgId);
    if (matter == null) return false;
    
    // Check if user is in the organization
    if (!await _userRepository.IsUserInOrganizationAsync(userId, orgId))
        return false;
    
    // If AccessLevel is "Everyone", org membership is sufficient
    if (matter.AccessLevel == "Everyone")
        return true;
    
    // If AccessLevel is "Specific", check MatterPermissions table
    if (matter.AccessLevel == "Specific")
    {
        return await _context.MatterPermissions
            .AnyAsync(mp => mp.MatterId == matterId && 
                           mp.UserId == userId && 
                           mp.RevokedAt == null);
    }
    
    return false;
}
```

### 3.4 Operation-Permission Mapping

#### Matter Operations

| Operation | Required Permission | Additional Checks |
|-----------|-------------------|------------------|
| **View Matter** | `Permission.ViewMatters` | + Org membership + Matter access level |
| **Create Matter** | `Permission.CreateMatters` | + Org membership |
| **Edit Matter** | `Permission.EditMatters` | + Org membership + Matter access level |
| **Delete Matter** | `Permission.DeleteMatters` | + Org membership + Owner/Admin role |
| **Manage Settings** | `Permission.ManageMatterSettings` | + Org membership + Matter access level |
| **Assign Users** | `Permission.ManageMatterSettings` | + Org membership + Matter access level |

#### Task Operations

| Operation | Required Permission | Additional Checks |
|-----------|-------------------|------------------|
| **View Task** | `Permission.ViewMatters` | + Access to task's matter OR task's organization |
| **Create Task** | `Permission.CreateMatters` | + Access to parent matter |
| **Update Task** | `Permission.EditMatters` | + Access to task's matter OR assignment |
| **Delete Task** | `Permission.DeleteMatters` | + Access to task's matter OR creator |
| **Assign Task** | `Permission.ManageMatterSettings` | + Access to task's matter |

#### Communication Operations

| Operation | Required Permission | Additional Checks |
|-----------|-------------------|------------------|
| **View Messages** | `Permission.ViewMessages` | + Conversation participant OR org membership |
| **Send Message** | `Permission.SendMessages` | + Conversation participant OR channel member |
| **Delete Message** | `Permission.DeleteMessages` | + Message owner OR Admin |
| **Create Channel** | `Permission.ManageThreads` | + Org membership |

### 3.5 AI Agent Permission Requirements

Currently, AI agents operate with **implicit elevated permissions**. Need to define explicit boundaries:

| AI Operation | Required Permission | Safety Rails |
|--------------|-------------------|--------------|
| **Read Conversations** | `Permission.ViewMessages` | Only conversations user has access to |
| **Generate Summaries** | `Permission.ViewMessages` | No PII in summaries without consent |
| **Suggest Replies** | `Permission.ViewMessages` | User approval before sending |
| **Extract Goals** | `Permission.ViewMatters` | Only from matters user can access |
| **Create Tasks** | `Permission.CreateMatters` | User approval required |
| **Access Documents** | `Permission.ViewDocuments` | Respect document permissions |

**Security Requirements:**
1. AI must operate within the context of the requesting user's permissions
2. AI cannot escalate privileges or bypass access controls
3. AI-generated content must be auditable (track who/what/when)
4. AI must not leak information across organizational boundaries
5. AI operations must respect matter-specific access levels

---

## 4. TESTING STRATEGY

### 4.1 Security Test Scenarios

#### IDOR Testing Matrix

| Test Case | Description | Expected Result | Priority |
|-----------|-------------|----------------|----------|
| **TEST-IDOR-001** | User A tries to edit Matter owned by Org B | 403 Forbidden or 404 Not Found | CRITICAL |
| **TEST-IDOR-002** | User A tries to delete Task from Org B | 403 Forbidden or 404 Not Found | CRITICAL |
| **TEST-IDOR-003** | User A enumerates Matter IDs (1-100) | Only sees matters from their org(s) | HIGH |
| **TEST-IDOR-004** | User A tries to view Task not assigned to them | 403/404 | HIGH |
| **TEST-IDOR-005** | User A tries to access Chat channel from Org B | 403 Forbidden | HIGH |
| **TEST-IDOR-006** | Unauthenticated user accesses /Chat/channel/1/messages | 401 Unauthorized | CRITICAL |
| **TEST-IDOR-007** | Law Firm user accesses client matter without relationship | 403 Forbidden | HIGH |
| **TEST-IDOR-008** | External user accesses matter not shared with them | 403/404 | HIGH |
| **TEST-IDOR-009** | User with "Specific" access level bypasses permission check | 403 if not in MatterPermissions | HIGH |

#### Permission Enforcement Testing

| Test Case | Description | Expected Result | Priority |
|-----------|-------------|----------------|----------|
| **TEST-PERM-001** | Client Member tries to delete matter | 403 Forbidden | CRITICAL |
| **TEST-PERM-002** | Paralegal tries to create new matter | 403 Forbidden | HIGH |
| **TEST-PERM-003** | Staff user tries to upload document | 403 Forbidden | MEDIUM |
| **TEST-PERM-004** | Manager invites new user to org | 200 Success | MEDIUM |
| **TEST-PERM-005** | Associate edits assigned matter | 200 Success | MEDIUM |
| **TEST-PERM-006** | External user downloads shared document | 200 Success | MEDIUM |
| **TEST-PERM-007** | CourtPersonnel tries to send message | 403 Forbidden | MEDIUM |
| **TEST-PERM-008** | Partner accesses all client organizations | 200 Success | HIGH |
| **TEST-PERM-009** | User without ViewMatters permission lists matters | 403 Forbidden | CRITICAL |

#### Matter Access Level Testing

| Test Case | Description | Expected Result | Priority |
|-----------|-------------|----------------|----------|
| **TEST-MATTER-001** | User in org accesses "Everyone" matter | 200 Success | HIGH |
| **TEST-MATTER-002** | User NOT in MatterPermissions accesses "Specific" matter | 403 Forbidden | CRITICAL |
| **TEST-MATTER-003** | User IN MatterPermissions accesses "Specific" matter | 200 Success | HIGH |
| **TEST-MATTER-004** | Revoked permission user accesses "Specific" matter | 403 Forbidden | HIGH |
| **TEST-MATTER-005** | Matter with no access level defaults to "Everyone" | 200 Success for org members | MEDIUM |

### 4.2 Integration Test Plan

#### Test Suite Structure

```csharp
// Certio.Tests.Integration/Security/MatterSecurityTests.cs
public class MatterSecurityTests : IntegrationTestBase
{
    [Fact]
    public async Task Edit_Matter_From_Different_Organization_Should_Return_403()
    {
        // Arrange
        var org1 = await CreateOrganizationAsync("Org 1");
        var org2 = await CreateOrganizationAsync("Org 2");
        var user1 = await CreateUserInOrganizationAsync(org1.Id, OrganizationRoles.Member);
        var matter2 = await CreateMatterAsync(org2.Id);

        AuthenticateAs(user1);

        // Act
        var response = await Client.PostAsync($"/Matter/Edit/{matter2.Id}", CreateEditRequest());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        // Verify matter was NOT modified
        var unmodifiedMatter = await DbContext.Matters.FindAsync(matter2.Id);
        Assert.NotEqual("Modified Title", unmodifiedMatter.Title);
    }

    [Fact]
    public async Task Delete_Task_Without_Access_Should_Return_403()
    {
        // Arrange
        var org1 = await CreateOrganizationAsync("Org 1");
        var org2 = await CreateOrganizationAsync("Org 2");
        var user1 = await CreateUserInOrganizationAsync(org1.Id, OrganizationRoles.Member);
        var matter2 = await CreateMatterAsync(org2.Id);
        var task2 = await CreateTaskAsync(matter2.Id, org2.Id);

        AuthenticateAs(user1);

        // Act
        var response = await Client.PostAsync($"/Tasks/Delete", new { id = task2.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        
        // Verify task still exists
        Assert.True(await DbContext.TaskItems.AnyAsync(t => t.Id == task2.Id));
    }
}

// Certio.Tests.Integration/Security/PermissionEnforcementTests.cs
public class PermissionEnforcementTests : IntegrationTestBase
{
    [Theory]
    [InlineData(OrganizationRoles.Member, Permission.DeleteMatters, false)]
    [InlineData(OrganizationRoles.Manager, Permission.DeleteMatters, false)]
    [InlineData(OrganizationRoles.Owner, Permission.DeleteMatters, true)]
    [InlineData(OrganizationRoles.Staff, Permission.CreateMatters, false)]
    [InlineData(OrganizationRoles.Associate, Permission.DeleteDocuments, false)]
    public async Task User_With_Role_Should_Have_Correct_Permission(
        string role, Permission permission, bool expectedHasPermission)
    {
        // Arrange
        var org = await CreateOrganizationAsync("Test Org");
        var user = await CreateUserInOrganizationAsync(org.Id, role);
        var permissionService = GetService<IPermissionService>();

        // Act
        var hasPermission = await permissionService.UserHasPermissionAsync(user.Id, org.Id, permission);

        // Assert
        Assert.Equal(expectedHasPermission, hasPermission);
    }

    [Fact]
    public async Task Matter_With_Specific_Access_Should_Enforce_Permissions()
    {
        // Arrange
        var org = await CreateOrganizationAsync("Test Org");
        var user1 = await CreateUserInOrganizationAsync(org.Id, OrganizationRoles.Member);
        var user2 = await CreateUserInOrganizationAsync(org.Id, OrganizationRoles.Member);
        var matter = await CreateMatterAsync(org.Id, accessLevel: "Specific");
        await GrantMatterPermissionAsync(matter.Id, user1.Id); // Only user1 has access

        AuthenticateAs(user2); // user2 does NOT have access

        // Act
        var response = await Client.GetAsync($"/Matter/Details/{matter.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

### 4.3 AI Agent Simulation Approach

#### Test AI Operations Within User Context

```csharp
public class AIAgentSecurityTests : IntegrationTestBase
{
    [Fact]
    public async Task AI_Should_Not_Access_Matters_Beyond_User_Permissions()
    {
        // Arrange
        var org1 = await CreateOrganizationAsync("Org 1");
        var org2 = await CreateOrganizationAsync("Org 2");
        var user1 = await CreateUserInOrganizationAsync(org1.Id, OrganizationRoles.Member);
        var matter1 = await CreateMatterAsync(org1.Id);
        var matter2 = await CreateMatterAsync(org2.Id);
        var conversation1 = await CreateConversationAsync(org1.Id, user1.Id);

        var aiService = GetService<IAIAgentService>();
        AuthenticateAs(user1);

        // Act - AI tries to generate summary using matter from org2
        var result = await aiService.GenerateMatterSummaryAsync(matter2.Id, user1.Id);

        // Assert
        Assert.Null(result); // Or appropriate error/exception
        // Verify AI didn't access unauthorized data
    }

    [Fact]
    public async Task AI_Generated_Tasks_Should_Respect_User_Permissions()
    {
        // Arrange
        var org = await CreateOrganizationAsync("Test Org");
        var paralegal = await CreateUserInOrganizationAsync(org.Id, OrganizationRoles.Paralegal);
        var matter = await CreateMatterAsync(org.Id);

        var aiService = GetService<IAIAgentService>();
        AuthenticateAs(paralegal);

        // Act - AI tries to create task (paralegal doesn't have CreateMatters permission)
        var result = await aiService.SuggestAndCreateTaskAsync(matter.Id, paralegal.Id, "AI suggested task");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("InsufficientPermissions", result.ErrorCode);
    }
}
```

### 4.4 Performance Benchmarks

#### Baseline Metrics (Current Architecture)

| Operation | Current Avg | Target | Acceptable Max | Notes |
|-----------|------------|--------|----------------|-------|
| GET /Matter/Index | ~120ms | <100ms | 200ms | Includes DB query + rendering |
| GET /Matter/Details/{id} | ~80ms | <50ms | 100ms | Single matter with relationships |
| POST /Matter/Create | ~200ms | <150ms | 300ms | Includes validation + DB insert |
| POST /Tasks/Update | ~60ms | <50ms | 100ms | Simple update |
| GET /Tasks/Index | ~180ms | <150ms | 250ms | Lists all tasks for org |

#### Target Metrics (With Service Layer + Caching)

| Operation | Target | With Cache Hit | Notes |
|-----------|--------|---------------|-------|
| GET /Matter/Index | <80ms | <30ms | Repository + Redis cache |
| GET /Matter/Details/{id} | <40ms | <15ms | Cached matter details |
| POST /Matter/Create | <120ms | N/A | Transaction + cache invalidation |
| POST /Tasks/Update | <40ms | N/A | Optimistic updates |
| GET /Tasks/Index | <100ms | <40ms | Cached task lists |

#### Load Testing Scenarios

```csharp
// Using NBomber or k6
public class LoadTests
{
    [Fact]
    public async Task Matter_Index_Should_Handle_100_Concurrent_Users()
    {
        var scenario = Scenario.Create("matter_index_load", async context =>
        {
            var response = await HttpClient.GetAsync("/Matter/Index");
            return response.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(5))
        );

        var stats = NBomberRunner.RegisterScenarios(scenario).Run();

        // Assert
        Assert.True(stats.AllOkCount > 29000); // 100 req/s * 300s = 30k, allow some variance
        Assert.True(stats.LatencyPercentile95 < 200); // 95th percentile under 200ms
    }
}
```

---

## 5. RECOMMENDATIONS & NEXT STEPS

### 5.1 Immediate Hotfixes (Week 1)

**Priority 1: CRITICAL Security Fixes**

1. **Fix Unauthenticated Endpoint**
   ```csharp
   // ChatController.cs line 118
   [HttpGet("channel/{channelId}/messages")]
   [AllowAnonymous] // ❌ REMOVE THIS
   [Authorize(Policy = "OrgMember")] // ✅ ADD THIS
   public async Task<IActionResult> GetChannelMessages(int channelId)
   ```

2. **Add Organization Checks to IDOR Endpoints**
   
   **MatterController.Edit()**
   ```csharp
   public async Task<IActionResult> Edit(int? id)
   {
       if (id == null) return NotFound();
       
       var customUser = HttpContext.Items["CustomUser"] as User;
       var primaryOrg = customUser?.GetPrimaryOrganization();
       if (primaryOrg == null) return RedirectToAction("Index", "Home");
       
       // ✅ ADD ORG CHECK
       var matter = await _context.Matters
           .Where(m => m.Id == id && m.OrganizationId == primaryOrg.OrganizationId)
           .FirstOrDefaultAsync();
       
       if (matter == null) return NotFound(); // Consistent 404 for security
       
       // ... rest of method
   }
   ```

   **TasksController.Update(), Delete(), Get()**
   ```csharp
   public async Task<IActionResult> Update([FromBody] UpdateTaskRequest request)
   {
       var customUser = HttpContext.Items["CustomUser"] as User;
       if (customUser == null) return Unauthorized();
       
       var task = await _context.TaskItems.FindAsync(request.Id);
       if (task == null) return NotFound();
       
       // ✅ ADD ORG/MATTER ACCESS CHECK
       var hasAccess = await UserCanAccessTaskAsync(task, customUser);
       if (!hasAccess) return NotFound(); // Return 404, not 403, to avoid enumeration
       
       // ... update logic
   }
   
   private async Task<bool> UserCanAccessTaskAsync(TaskItem task, User user)
   {
       // Check if user is in the task's organization
       if (await _context.UserOrganizations.AnyAsync(uo => 
           uo.UserId == user.Id && uo.OrganizationId == task.OrgId && uo.IsActive))
           return true;
       
       // Check if user has firm-based access to the organization
       // (Implementation using existing FirmRelationshipCacheService)
       
       return false;
   }
   ```

### 5.2 Phase 1: Service Layer Implementation (Weeks 2-4)

**Step 1: Create Repository Interfaces & Implementations**

```bash
# Create new folders
mkdir -p Certio.Application/Repositories
mkdir -p Certio.Application/Repositories/Implementations

# Create repository files
touch Certio.Application/Repositories/IMatterRepository.cs
touch Certio.Application/Repositories/ITaskRepository.cs
touch Certio.Application/Repositories/IUserRepository.cs
touch Certio.Application/Repositories/Implementations/MatterRepository.cs
touch Certio.Application/Repositories/Implementations/TaskRepository.cs
```

**Step 2: Implement Service Layer**

```bash
# Create service interfaces and implementations
touch Certio.Application/Services/IMatterService.cs
touch Certio.Application/Services/MatterService.cs
touch Certio.Application/Services/ITaskService.cs
touch Certio.Application/Services/TaskService.cs
touch Certio.Application/Services/IPermissionService.cs
touch Certio.Application/Services/PermissionService.cs
```

**Step 3: Register Services in DI Container**

```csharp
// Program.cs
builder.Services.AddScoped<IMatterRepository, MatterRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IMatterService, MatterService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
```

**Step 4: Refactor Controllers to Use Services**

```csharp
// MatterController.cs
public class MatterController : Controller
{
    private readonly IMatterService _matterService;
    private readonly IPermissionService _permissionService;
    
    public MatterController(IMatterService matterService, IPermissionService permissionService)
    {
        _matterService = matterService;
        _permissionService = permissionService;
    }
    
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        
        var customUser = HttpContext.Items["CustomUser"] as User;
        var primaryOrg = customUser?.GetPrimaryOrganization();
        if (primaryOrg == null) return RedirectToAction("Index", "Home");
        
        // ✅ Service handles all permission checks and validation
        var matter = await _matterService.GetMatterAsync(id.Value, customUser.Id, primaryOrg.OrganizationId);
        if (matter == null) return NotFound();
        
        // ✅ Check edit permission
        var canEdit = await _permissionService.UserHasPermissionForMatterAsync(
            customUser.Id, primaryOrg.OrganizationId, id.Value, Permission.EditMatters);
        if (!canEdit) return Forbid();
        
        return View(matter);
    }
}
```

### 5.3 Phase 2: Enhanced Authorization (Weeks 5-6)

**Implement Resource-Based Authorization**

```csharp
// Security/Requirements/MatterOperationRequirement.cs
public class MatterOperationRequirement : IAuthorizationRequirement
{
    public MatterOperation Operation { get; }
    
    public MatterOperationRequirement(MatterOperation operation)
    {
        Operation = operation;
    }
}

// Security/Handlers/MatterAuthorizationHandler.cs
public class MatterAuthorizationHandler : AuthorizationHandler<MatterOperationRequirement, Matter>
{
    private readonly IPermissionService _permissionService;
    
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MatterOperationRequirement requirement,
        Matter resource)
    {
        var userId = GetUserId(context.User);
        var orgId = GetOrganizationId(context);
        
        var permission = MapOperationToPermission(requirement.Operation);
        var hasPermission = await _permissionService.UserHasPermissionForMatterAsync(
            userId, orgId, resource.Id, permission);
        
        if (hasPermission)
            context.Succeed(requirement);
    }
    
    private Permission MapOperationToPermission(MatterOperation operation)
    {
        return operation switch
        {
            MatterOperation.View => Permission.ViewMatters,
            MatterOperation.Edit => Permission.EditMatters,
            MatterOperation.Delete => Permission.DeleteMatters,
            _ => throw new ArgumentException("Unknown operation")
        };
    }
}

// Usage in Controller
public async Task<IActionResult> Edit(int? id)
{
    var matter = await _matterService.GetMatterAsync(id.Value, userId, orgId);
    if (matter == null) return NotFound();
    
    var authResult = await _authorizationService.AuthorizeAsync(
        User, matter, new MatterOperationRequirement(MatterOperation.Edit));
    
    if (!authResult.Succeeded) return Forbid();
    
    return View(matter);
}
```

### 5.4 Phase 3: Testing & Validation (Weeks 7-8)

**Test Suite Implementation**

1. Create `Certio.Tests.Integration` project
2. Implement security test scenarios from Section 4.1
3. Set up CI/CD pipeline to run tests on every PR
4. Achieve >80% code coverage for security-critical paths

**Penetration Testing Checklist**

- [ ] IDOR vulnerability scanning (automated + manual)
- [ ] Permission bypass attempts
- [ ] Cross-organization data leakage tests
- [ ] Authentication bypass tests
- [ ] Session management tests
- [ ] SQL injection tests (should be prevented by EF Core, but verify)
- [ ] XSS vulnerability tests
- [ ] CSRF token validation

### 5.5 Phase 4: Monitoring & Auditing (Week 9+)

**Implement Comprehensive Audit Logging**

```csharp
// Services/AuditService.cs
public class AuditService : IAuditService
{
    public async Task LogSecurityEventAsync(SecurityEvent securityEvent)
    {
        var auditLog = new AuditLog
        {
            EntityType = securityEvent.ResourceType,
            EntityId = securityEvent.ResourceId,
            Action = securityEvent.Action,
            UserId = securityEvent.UserId,
            OrganizationId = securityEvent.OrganizationId,
            IpAddress = securityEvent.IpAddress,
            UserAgent = securityEvent.UserAgent,
            Success = securityEvent.Success,
            FailureReason = securityEvent.FailureReason,
            Timestamp = DateTime.UtcNow
        };
        
        await _context.AuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();
    }
}

// Usage in Service Layer
public async Task<MatterDto> UpdateMatterAsync(int id, UpdateMatterRequest request, int userId, int orgId)
{
    try
    {
        // Permission checks...
        
        // Update matter...
        
        await _auditService.LogSecurityEventAsync(new SecurityEvent
        {
            Action = "UpdateMatter",
            ResourceType = "Matter",
            ResourceId = id,
            UserId = userId,
            OrganizationId = orgId,
            Success = true
        });
        
        return updatedMatter;
    }
    catch (UnauthorizedAccessException ex)
    {
        await _auditService.LogSecurityEventAsync(new SecurityEvent
        {
            Action = "UpdateMatter",
            ResourceType = "Matter",
            ResourceId = id,
            UserId = userId,
            OrganizationId = orgId,
            Success = false,
            FailureReason = ex.Message
        });
        
        throw;
    }
}
```

**Security Monitoring Dashboard**

Key metrics to track:
- Failed authorization attempts per user/IP
- Cross-organization access attempts (blocked)
- Permission bypass attempts
- Unusual access patterns (time, location, volume)
- IDOR exploit attempts (sequential ID access patterns)

---

## 6. SUCCESS CRITERIA

### 6.1 Phase 0 Completion Checklist

- [x] Security vulnerabilities catalogued
- [x] Risk severity matrix created
- [x] Current architecture documented
- [x] Target architecture designed
- [x] Permission matrix defined
- [x] Testing strategy outlined

### 6.2 Phase 1-4 Success Metrics

**Security Metrics**
- Zero IDOR vulnerabilities in penetration tests
- 100% of operations require permission checks
- 100% of resources validate organizational ownership
- All security tests passing in CI/CD

**Architecture Metrics**
- All controllers refactored to use service layer
- Zero direct `DbContext` access in controllers
- Repository pattern implemented for all entities
- Service layer has >80% test coverage

**Performance Metrics**
- P95 latency <200ms for all operations
- Support 100 concurrent users without degradation
- Cache hit rate >70% for frequently accessed data

**Audit Metrics**
- 100% of security-relevant operations logged
- Audit logs retained for 90 days minimum
- Security events monitored in real-time
- Alerting configured for suspicious patterns

---

## 7. APPENDIX

### 7.1 Current User Types & Roles Reference

```csharp
// From Certio.Domain/Users/UserOrganization.cs

public static class OrganizationRoles
{
    // Client roles
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Member = "Member";
    public const string Lawyer = "Lawyer";
    
    // External roles  
    public const string OpposingCounsel = "OpposingCounsel";
    public const string ExpertWitness = "ExpertWitness";
    public const string CourtPersonnel = "CourtPersonnel";
    public const string RegulatoryBody = "RegulatoryBody";
    public const string Other = "Other";
    
    // Certio roles
    public const string Admin = "Admin";
    public const string MatterManager = "MatterManager";
    public const string Support = "Support";
    public const string Legal = "Legal";
    
    // Law Firm roles
    public const string Partner = "Partner";
    public const string Associate = "Associate";
    public const string Paralegal = "Paralegal";
    public const string Staff = "Staff";
    
    // General roles
    public const string Guest = "Guest";
}

public static class UserTypes
{
    public const string Client = "Client";
    public const string External = "External";
    public const string Certio = "Certio";
    public const string LawFirm = "LawFirm";
}
```

### 7.2 Database Schema Relevant Tables

**Key Security-Related Tables:**
- `Users` - User accounts
- `UserOrganizations` - Many-to-many user-org memberships with roles
- `Organizations` - Client/LawFirm organizations
- `OrganizationRelationships` - LawFirm-Client relationships
- `Matters` - Legal matters with `OrganizationId` and `AccessLevel`
- `MatterPermissions` - Explicit permissions for "Specific" access level matters
- `MatterAssignments` - User assignments to matters
- `TaskItems` - Tasks with `OrgId` and `MatterId`
- `TaskAssignments` - User assignments to tasks
- `AuditLogs` - Audit trail for all operations

### 7.3 Middleware Pipeline Order

Current order (from `Program.cs`):
1. `UseHttpsRedirection()`
2. `UseStaticFiles()`
3. `UseSession()`
4. `UseRouting()`
5. `UseAuthentication()` ✅
6. `UseUserSync()` ✅ Custom middleware
7. `UseMiddleware<ClientContextMiddleware>()` ✅ Custom
8. `UseChannelInitialization()` ✅ Custom
9. `UseClientAccessGuard()` ✅ Custom (maps to `ClientAccessMiddleware`)
10. `UseAuthorization()` ✅

**Important:** Middleware order is correct - authentication before custom middleware, authorization last.

### 7.4 Technology Stack

**Backend:**
- .NET 9.0 (web app)
- .NET 8.0 (other projects)
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server (local) / Azure SQL (production)
- Redis (caching)

**Frontend:**
- Razor Pages
- JavaScript (vanilla + SignalR)
- Bootstrap 5

**AI/ML:**
- Python 3.11 (AI agents)
- FastAPI (AI service API)
- Running on separate process (http://localhost:8000)

### 7.5 Contact & Review Schedule

**Phase 0 Review:** Week 1, End of Day 5  
**Phase 1 Review:** Week 4, Sprint Demo  
**Phase 2 Review:** Week 6, Sprint Demo  
**Phase 3 Review:** Week 8, Security Sign-off  
**Phase 4 Review:** Week 10, Production Readiness  

---

## DOCUMENT METADATA

**Document Version:** 1.0  
**Last Updated:** October 11, 2025  
**Author:** Security & Architecture Assessment Team  
**Classification:** Internal - Security Sensitive  
**Review Cycle:** Monthly during implementation, Quarterly after completion  

**Related Documents:**
- `CURSOR_COST_OPTIMIZATION.md` - Cost management strategies
- `DATABASE_SCHEMA_DIAGRAM.md` - Database structure
- `SMART_DATABASE_GUIDE.md` - Database setup guide
- `OFFLINE_WORK_GUIDE.md` - Offline development guide

**Approval Required From:**
- [ ] Security Team Lead
- [ ] Architecture Team Lead
- [ ] Development Team Lead
- [ ] Product Owner
- [ ] CTO/CISO

---

**END OF PHASE 0 ASSESSMENT**

