# Duplicate Assignment Fix

## Issue

Users could not be assigned to multiple roles on the same matter. For example, an attorney could not be both "Originating Attorney" and "Responsible Attorney" on the same matter.

## Root Cause

The duplicate check in `AssignUserToMatterAsync` was too strict:

```csharp
// OLD CODE - Prevented ANY duplicate userId
var existingAssignment = matter.Assignments
    .FirstOrDefault(a => a.UserId == assignmentDto.UserId && a.RemovedAt == null);

if (existingAssignment != null)
{
    throw new BusinessRuleViolationException("DuplicateAssignment", "User is already assigned to this matter");
}
```

This prevented the same user from having multiple assignment types on the same matter.

## Solution

Updated the duplicate check to consider both `UserId` AND `AssignmentType`:

```csharp
// NEW CODE - Allows same user with different assignment types
var existingAssignment = matter.Assignments
    .FirstOrDefault(a => 
        a.UserId == assignmentDto.UserId && 
        a.AssignmentType == assignmentDto.AssignmentType && 
        a.RemovedAt == null);

if (existingAssignment != null)
{
    throw new BusinessRuleViolationException("DuplicateAssignment", 
        $"User is already assigned as {assignmentDto.AssignmentType} to this matter");
}
```

## Result

Now allows:
- ✅ User 1 as Originating Attorney
- ✅ User 1 as Responsible Attorney  
- ✅ User 1 as Billing Attorney
- ❌ User 1 as Responsible Attorney (duplicate - already assigned)

## UI Compatibility

The UI was already designed to handle this:
- `Index.cshtml` line 171: `var uniqueAssignees = matter.Assignments.GroupBy(a => a.UserId).Select(g => g.First()).ToList();`
- Avatars are deduplicated by UserId before display
- Same user with 3 roles still shows as 1 avatar in the "Team" section

## Scalability

This approach is scalable because:
- Normalized database structure (one row per assignment)
- UI deduplication happens in-memory (acceptable for typical matter counts)
- Can be optimized with computed properties if needed at scale
- Standard law firm practice: attorneys commonly serve multiple roles on matters

## Testing

After restart, test by:
1. Create a new matter
2. Assign the same user to multiple roles:
   - Originating Attorney: User 1
   - Responsible Attorney: User 1
   - Responsible Staff: User 5
3. Verify all 3 assignments are saved
4. Check UI shows User 1 avatar once but has 2 assignments in database

## Database Check

```powershell
# Check multiple assignments for same user
docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P $env:SQL_PASSWORD -C -N -W -s',' -Q "SELECT ma.MatterId, m.Title, ma.UserId, u.FirstName + ' ' + u.LastName AS UserName, ma.AssignmentType, ma.Role FROM CertioLocal.dbo.MatterAssignments ma INNER JOIN CertioLocal.dbo.Users u ON ma.UserId = u.Id INNER JOIN CertioLocal.dbo.Matters m ON ma.MatterId = m.Id WHERE ma.RemovedAt IS NULL ORDER BY ma.MatterId, ma.UserId, ma.AssignmentType"
```

