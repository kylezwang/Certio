# Tenancy and permissions

Every user-facing query should be scoped to the organization in the route. The `OrgMember` authorization policy is the first check. Services then check a finer permission.

`Permission` in `Certio.Domain/Users/User.cs` is the list of capabilities (create matters, manage billing, and so on). A user's effective set comes from their `UserOrganization` role. `PermissionService` loads that set from the database. `CachedPermissionService` sits in front of it.

## Cache

`RedisCacheService` is two layers, even when Redis is off:

1. `IMemoryCache` (L1), checked first.
2. `IDistributedCache` (L2). This is StackExchange Redis when `USE_REDIS=true` and a Redis connection string is set. Otherwise it is an in-memory distributed cache, so a second web instance would not see the first instance's entries.

Permission results are cached for 15 minutes, matter access for 10, task access for 5. The cache stores a small wrapper object because the cache interface is generic over a class, and a raw `bool` is not a class.

If you change a role or an assignment, the matching cache key has to be dropped. A stale "allowed" entry is the bug to look for when a permission change does not show up.

## IDOR

Item endpoints (open, edit, delete) should call `AuthorizationHelper` or a service method that checks the matter or task access rules, not only the org membership policy. List pages filter in the controller or service. Those two filters have to agree. If the list hides a row but the get action still returns it, that is a hole.
