# Two-layer permission cache

Permission checks happen on almost every page. Hitting SQL for each one showed up in traces, so `CachedPermissionService` stores boolean results.

L1 is in-process memory. L2 is Redis when configured, and an in-memory distributed cache when it is not. TTLs are short (5 to 15 minutes) because a wrong "allowed" is worse than a slow check.

The tradeoff is invalidation. Role changes must delete the keys for that user and org. If you add a new permission check, use the cached service so the TTL and the key shape stay in one place (`Certio.Web/Services/CachedPermissionService.cs`).
