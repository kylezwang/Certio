# Phase 3: Permission System - Quick Reference

## 🚀 Quick Start

### 1. Protect a Controller Action

```csharp
using Certio.Web.Security;
using Certio.Domain.Users;

// Require permission
[RequirePermission(Permission.EditMatters)]
public async Task<IActionResult> EditMatter(int id)

// Require matter access
[RequireMatterAccess("matterId")]
public async Task<IActionResult> ViewMatter(int matterId)

// Combined: access + permission
[RequireMatterOperation(Permission.DeleteMatters, "matterId")]
public async Task<IActionResult> DeleteMatter(int matterId)

// Require task access
[RequireTaskAccess("taskId")]
public async Task<IActionResult> GetTask(int taskId)
```

### 2. Check Permission in Service

```csharp
// Validate (throws if denied)
await _permissionService.ValidatePermissionOrThrowAsync(
    userId, orgId, Permission.EditMatters, "EditMatter");

// Check (returns bool)
var canEdit = await _permissionService.HasPermissionAsync(
    userId, orgId, Permission.EditMatters);

// Check matter access
var canAccess = await _permissionService.CanAccessMatterAsync(userId, matterId);
```

### 3. Check in Razor View

```cshtml
@inject IPermissionService PermissionService

@{
    var canEdit = await PermissionService.HasPermissionAsync(
        User.GetUserId(), ViewBag.OrganizationId, Permission.EditMatters);
}

@if (canEdit)
{
    <button>Edit</button>
}
```

---

## 📋 Available Permissions

### Documents
- `ViewDocuments` - View document list
- `DownloadDocuments` - Download files
- `UploadDocuments` - Upload new files
- `DeleteDocuments` - Delete files
- `CommentOnDocuments` - Add comments

### Matters
- `ViewMatters` - View matter list
- `CreateMatters` - Create new matters
- `EditMatters` - Edit existing matters
- `DeleteMatters` - Delete matters
- `ManageMatterSettings` - Configure matter settings

### Users
- `InviteUsers` - Invite new users
- `RemoveUsers` - Remove users
- `ManageUserPermissions` - Change user roles

### Communications
- `ViewMessages` - View messages
- `SendMessages` - Send messages
- `DeleteMessages` - Delete messages
- `ManageThreads` - Manage threads

### System
- `ViewAuditLogs` - View audit logs
- `ManageSystemSettings` - System configuration
- `AccessAdminPanel` - Admin panel access

---

## 🎯 Common Scenarios

### Scenario 1: New Controller Action

**Question:** I'm adding a new action to delete documents. What permissions do I need?

**Answer:**
```csharp
[Authorize(Policy = "OrgMember")]
[RequirePermission(Permission.DeleteDocuments)]
public async Task<IActionResult> DeleteDocument(int documentId)
{
    // Permission is guaranteed by attribute
    await _documentService.DeleteAsync(documentId);
    return Ok();
}
```

### Scenario 2: Matter-Specific Operation

**Question:** How do I ensure user can edit a specific matter?

**Answer:**
```csharp
[Authorize(Policy = "OrgMember")]
[RequireMatterOperation(Permission.EditMatters, "matterId")]
public async Task<IActionResult> UpdateMatter(int matterId, [FromBody] UpdateMatterDto dto)
{
    // User has BOTH:
    // 1. Access to this matter
    // 2. EditMatters permission
    
    var result = await _matterService.UpdateAsync(matterId, dto);
    return Ok(result);
}
```

### Scenario 3: Service Layer Check

**Question:** How do I check permissions in a service method?

**Answer:**
```csharp
public async Task<ServiceResult> UpdateMatterAsync(int matterId, UpdateMatterDto dto, int userId)
{
    // Step 1: Check matter access
    await _permissionService.ValidateMatterAccessOrThrowAsync(userId, matterId, "UpdateMatter");
    
    // Step 2: Get matter to find organization
    var matter = await _context.Matters.FindAsync(matterId);
    
    // Step 3: Check edit permission
    await _permissionService.ValidatePermissionOrThrowAsync(
        userId, matter.OrganizationId, Permission.EditMatters, "UpdateMatter");
    
    // Step 4: Perform update
    // ...
    
    return ServiceResult.Success();
}
```

### Scenario 4: Conditional UI

**Question:** How do I show/hide buttons based on permissions?

**Answer:**
```cshtml
@inject IPermissionService PermissionService

@{
    var userId = User.GetUserId();
    var orgId = ViewBag.OrganizationId;
    
    var canCreate = await PermissionService.HasPermissionAsync(userId, orgId, Permission.CreateMatters);
    var canEdit = await PermissionService.HasPermissionAsync(userId, orgId, Permission.EditMatters);
    var canDelete = await PermissionService.HasPermissionAsync(userId, orgId, Permission.DeleteMatters);
}

@if (canCreate)
{
    <button onclick="createMatter()">New Matter</button>
}

@if (canEdit)
{
    <button onclick="editMatter()">Edit</button>
}

@if (canDelete)
{
    <button class="btn-danger" onclick="deleteMatter()">Delete</button>
}
```

---

## 🔍 Troubleshooting

### "Permission Denied" but user should have access

1. **Check user's role:**
   ```csharp
   var userOrg = await _context.UserOrganizations
       .FirstAsync(uo => uo.UserId == userId && uo.OrganizationId == orgId);
   Console.WriteLine($"Role: {userOrg.Role}, UserType: {userOrg.UserType}");
   ```

2. **Check effective permissions:**
   ```csharp
   var permissions = await _permissionService.GetEffectivePermissionsAsync(userId, orgId);
   Console.WriteLine($"Permissions: {string.Join(", ", permissions)}");
   ```

3. **Check cache:**
   - Wait 15 minutes for cache to expire
   - Or restart application to clear memory cache

### "Matter Access Denied"

1. **Check matter access level:**
   ```csharp
   var matter = await _context.Matters
       .Include(m => m.Permissions)
       .Include(m => m.Assignments)
       .FirstAsync(m => m.Id == matterId);
   
   Console.WriteLine($"AccessLevel: {matter.AccessLevel}");
   Console.WriteLine($"Permissions: {matter.Permissions.Count}");
   Console.WriteLine($"Assignments: {matter.Assignments.Count}");
   ```

2. **For "Specific" access level:**
   - Check if `MatterPermission` exists for user
   - Check if `MatterAssignment` exists for user
   - Verify `RevokedAt == null` and `RemovedAt == null`

### Law Firm Access Issues

1. **Check relationship:**
   ```csharp
   var relationship = await _permissionService.GetFirmRelationshipAsync(userId, clientOrgId);
   if (relationship != null)
   {
       Console.WriteLine($"Active: {relationship.IsActive}");
       Console.WriteLine($"Deleted: {relationship.IsDeleted}");
       Console.WriteLine($"Expires: {relationship.ExpiresAt}");
   }
   ```

2. **Common issues:**
   - Relationship is inactive
   - Relationship has expired
   - User is not in law firm organization

---

## 📊 Performance Tips

### ✅ Do This (Cache-Friendly)

```csharp
// Check once, use many times
var canEdit = await _permissionService.HasPermissionAsync(userId, orgId, Permission.EditMatters);

foreach (var matter in matters)
{
    matter.IsEditable = canEdit;
}
```

### ❌ Avoid This (Cache-Unfriendly)

```csharp
// Checking per item (many cache misses)
foreach (var matter in matters)
{
    matter.IsEditable = await _permissionService.CanAccessMatterAsync(userId, matter.Id);
}
```

---

## 🧪 Testing

### Unit Test Example

```csharp
[Fact]
public async Task UserWithPermission_CanPerformOperation()
{
    // Arrange
    var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Owner);
    var matter = await CreateMatterAsync(org.Id, "Everyone");
    
    // Act
    var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);
    var hasPermission = await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters);
    
    // Assert
    Assert.True(canAccess);
    Assert.True(hasPermission);
}
```

### Integration Test Example

```csharp
[Fact]
public async Task Controller_DeniesAccessWithoutPermission()
{
    // Arrange
    var client = _factory.CreateClient();
    await LoginAsUserAsync(client, userWithoutPermission);
    
    // Act
    var response = await client.PostAsync("/Matter/Delete/1", null);
    
    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

---

## 📚 More Information

- **Complete Guide:** `PHASE_3_PERMISSION_SYSTEM_GUIDE.md`
- **Security Audit:** `PHASE_3_PERMISSION_AUDIT_REPORT.md`
- **Implementation Summary:** `PHASE_3_IMPLEMENTATION_SUMMARY.md`
- **Unit Tests:** `Certio.Tests/Services/PermissionServiceTests.cs`

---

## 🆘 Need Help?

1. Check this quick reference
2. Review the complete guide
3. Look at unit tests for examples
4. Check troubleshooting section

**Common Gotchas:**
- ⚠️ Remember to use `[Authorize(Policy = "OrgMember")]` before permission attributes
- ⚠️ Parameter names in attributes must match action parameter names
- ⚠️ Cached permissions expire after 15 minutes
- ⚠️ Service layer should also validate (defense in depth)

