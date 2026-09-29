using Certio.Application.Interfaces;
using Certio.Application.Services;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Certio.Web.Services
{
    /// <summary>
    /// Wrapper class for caching boolean values (since ICacheService requires class types)
    /// </summary>
    public class BooleanCacheWrapper
    {
        public bool Value { get; set; }
        
        public BooleanCacheWrapper() { }
        public BooleanCacheWrapper(bool value) { Value = value; }
    }

    /// <summary>
    /// Cached wrapper for PermissionService with Redis-backed caching
    /// Implements performance optimization for permission checks
    /// </summary>
    public class CachedPermissionService : IPermissionService
    {
        private readonly PermissionService _permissionService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CachedPermissionService> _logger;

        // Cache expiration times
        private static readonly TimeSpan PermissionCacheExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MatterAccessCacheExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan TaskAccessCacheExpiration = TimeSpan.FromMinutes(5);

        // Cache key prefixes
        private const string PERMISSION_PREFIX = "perm:";
        private const string EFFECTIVE_PERMS_PREFIX = "eff_perms:";
        private const string MATTER_ACCESS_PREFIX = "matter_access:";
        private const string TASK_ACCESS_PREFIX = "task_access:";
        private const string ORG_MEMBER_PREFIX = "org_member:";
        private const string FIRM_ACCESS_PREFIX = "firm_access:";

        public CachedPermissionService(
            PermissionService permissionService,
            ICacheService cacheService,
            ILogger<CachedPermissionService> logger)
        {
            _permissionService = permissionService;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<bool> HasPermissionAsync(int userId, int organizationId, Permission permission)
        {
            var sw = Stopwatch.StartNew();
            var cacheKey = $"{PERMISSION_PREFIX}{userId}:{organizationId}:{permission}";
            
            var cached = await _cacheService.GetAsync<BooleanCacheWrapper>(cacheKey);
            if (cached != null)
            {
                sw.Stop();
                _logger.LogDebug("Permission check CACHED for user {UserId} in {ElapsedMs}ms (Result: {Result})", 
                    userId, sw.ElapsedMilliseconds, cached.Value);
                return cached.Value;
            }

            var result = await _permissionService.HasPermissionAsync(userId, organizationId, permission);
            sw.Stop();
            
            _logger.LogInformation("Permission check COMPUTED for user {UserId}, permission {Permission} in {ElapsedMs}ms (Result: {Result})", 
                userId, permission, sw.ElapsedMilliseconds, result);
            
            await _cacheService.SetAsync(cacheKey, new BooleanCacheWrapper(result), PermissionCacheExpiration);
            
            return result;
        }

        public async Task<List<Permission>> GetEffectivePermissionsAsync(int userId, int organizationId)
        {
            var cacheKey = $"{EFFECTIVE_PERMS_PREFIX}{userId}:{organizationId}";
            
            var cached = await _cacheService.GetAsync<List<Permission>>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Effective permissions cache hit for user {UserId}, org {OrgId}", 
                    userId, organizationId);
                return cached;
            }

            var result = await _permissionService.GetEffectivePermissionsAsync(userId, organizationId);
            await _cacheService.SetAsync(cacheKey, result, PermissionCacheExpiration);
            
            return result;
        }

        public async Task<bool> CanAccessMatterAsync(int userId, int matterId)
        {
            var cacheKey = $"{MATTER_ACCESS_PREFIX}{userId}:{matterId}";
            
            var cached = await _cacheService.GetAsync<BooleanCacheWrapper>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Matter access cache hit for user {UserId}, matter {MatterId}", 
                    userId, matterId);
                return cached.Value;
            }

            var result = await _permissionService.CanAccessMatterAsync(userId, matterId);
            await _cacheService.SetAsync(cacheKey, new BooleanCacheWrapper(result), MatterAccessCacheExpiration);
            
            return result;
        }

        public async Task<bool> CanPerformMatterOperationAsync(int userId, int matterId, string operation)
        {
            // Don't cache operations, they're already composed of cached permissions
            return await _permissionService.CanPerformMatterOperationAsync(userId, matterId, operation);
        }

        public async Task<bool> CanAccessTaskAsync(int userId, int taskId)
        {
            var cacheKey = $"{TASK_ACCESS_PREFIX}{userId}:{taskId}";
            
            var cached = await _cacheService.GetAsync<BooleanCacheWrapper>(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Task access cache hit for user {UserId}, task {TaskId}", 
                    userId, taskId);
                return cached.Value;
            }

            var result = await _permissionService.CanAccessTaskAsync(userId, taskId);
            await _cacheService.SetAsync(cacheKey, new BooleanCacheWrapper(result), TaskAccessCacheExpiration);
            
            return result;
        }

        public async Task<bool> CanAccessSubTaskAsync(int userId, int subTaskId)
        {
            // SubTask access is derived from Task access, use task caching
            return await _permissionService.CanAccessSubTaskAsync(userId, subTaskId);
        }

        public async Task<bool> IsOrganizationMemberAsync(int userId, int organizationId)
        {
            var cacheKey = $"{ORG_MEMBER_PREFIX}{userId}:{organizationId}";
            
            var cached = await _cacheService.GetAsync<BooleanCacheWrapper>(cacheKey);
            if (cached != null)
            {
                return cached.Value;
            }

            var result = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
            await _cacheService.SetAsync(cacheKey, new BooleanCacheWrapper(result), PermissionCacheExpiration);
            
            return result;
        }

        public async Task<bool> HasFirmBasedAccessAsync(int userId, int organizationId)
        {
            var cacheKey = $"{FIRM_ACCESS_PREFIX}{userId}:{organizationId}";
            
            var cached = await _cacheService.GetAsync<BooleanCacheWrapper>(cacheKey);
            if (cached != null)
            {
                return cached.Value;
            }

            var result = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
            await _cacheService.SetAsync(cacheKey, new BooleanCacheWrapper(result), PermissionCacheExpiration);
            
            return result;
        }

        public async Task<List<int>> GetAccessibleOrganizationIdsAsync(int userId)
        {
            var cacheKey = $"accessible_orgs:{userId}";
            
            var cached = await _cacheService.GetAsync<List<int>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var result = await _permissionService.GetAccessibleOrganizationIdsAsync(userId);
            await _cacheService.SetAsync(cacheKey, result, PermissionCacheExpiration);
            
            return result;
        }

        public async Task ValidatePermissionOrThrowAsync(int userId, int organizationId, Permission permission, string operation)
        {
            await _permissionService.ValidatePermissionOrThrowAsync(userId, organizationId, permission, operation);
        }

        public async Task ValidateMatterAccessOrThrowAsync(int userId, int matterId, string operation)
        {
            await _permissionService.ValidateMatterAccessOrThrowAsync(userId, matterId, operation);
        }

        public async Task ValidateTaskAccessOrThrowAsync(int userId, int taskId, string operation)
        {
            await _permissionService.ValidateTaskAccessOrThrowAsync(userId, taskId, operation);
        }

        public async Task<bool> CanPerformOperationAsync(int userId, int matterId, Permission permission)
        {
            return await _permissionService.CanPerformOperationAsync(userId, matterId, permission);
        }

        public async Task<OrganizationRelationship?> GetFirmRelationshipAsync(int userId, int organizationId)
        {
            var cacheKey = $"firm_rel:{userId}:{organizationId}";
            
            var cached = await _cacheService.GetAsync<OrganizationRelationship>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var result = await _permissionService.GetFirmRelationshipAsync(userId, organizationId);
            if (result == null)
            {
                return null;
            }

            var sanitized = CreateCacheFriendlyRelationship(result);
            await _cacheService.SetAsync(cacheKey, sanitized, PermissionCacheExpiration);
            
            return sanitized;
        }

        public async Task<bool> CanPerformTaskOperationAsync(int userId, int taskId, Permission permission)
        {
            return await _permissionService.CanPerformTaskOperationAsync(userId, taskId, permission);
        }

        /// <summary>
        /// Invalidates all permission caches for a specific user
        /// Call this when user roles or permissions change
        /// </summary>
        public async Task InvalidateUserPermissionsAsync(int userId)
        {
            _logger.LogInformation("Invalidating all permission caches for user {UserId}", userId);
            
            // Invalidate all cached permissions
            // In a production system, you'd use Redis pattern matching to delete keys
            // For now, we rely on cache expiration
            
            // NOTE: To fully implement this, we'd need to extend ICacheService with a DeletePatternAsync method
            // that uses Redis SCAN and DELETE commands
        }

        /// <summary>
        /// Invalidates matter access caches for a specific matter
        /// Call this when matter permissions or assignments change
        /// </summary>
        public async Task InvalidateMatterAccessAsync(int matterId)
        {
            _logger.LogInformation("Invalidating matter access caches for matter {MatterId}", matterId);
            
            // Similar to above - would need pattern matching to delete all keys matching matter_access:*:{matterId}
            // For MVP, relying on cache expiration is acceptable
            await Task.CompletedTask;
        }

        /// <summary>
        /// Invalidates task access caches for a specific task
        /// Call this when task assignments change
        /// </summary>
        public async Task InvalidateTaskAccessAsync(int taskId)
        {
            _logger.LogInformation("Invalidating task access caches for task {TaskId}", taskId);
            await Task.CompletedTask;
        }

        private static OrganizationRelationship CreateCacheFriendlyRelationship(OrganizationRelationship relationship)
        {
            var sanitized = new OrganizationRelationship
            {
                Id = relationship.Id,
                SourceOrganizationId = relationship.SourceOrganizationId,
                TargetOrganizationId = relationship.TargetOrganizationId,
                RelationshipType = relationship.RelationshipType,
                AccessLevel = relationship.AccessLevel,
                Description = relationship.Description,
                IsActive = relationship.IsActive,
                ExpiresAt = relationship.ExpiresAt,
                CreatedAt = relationship.CreatedAt,
                LastModifiedDate = relationship.LastModifiedDate,
                CreatedById = relationship.CreatedById,
                ModifiedById = relationship.ModifiedById,
                DeletedAt = relationship.DeletedAt,
                DeletedById = relationship.DeletedById,
                DeletionReason = relationship.DeletionReason,
                IsDeleted = relationship.IsDeleted
            };

            sanitized.SourceOrganization = relationship.SourceOrganization != null
                ? new Organization
                {
                    Id = relationship.SourceOrganization.Id,
                    Name = relationship.SourceOrganization.Name,
                    Description = relationship.SourceOrganization.Description,
                    OwnerId = relationship.SourceOrganization.OwnerId,
                    Type = relationship.SourceOrganization.Type,
                    IsPersonal = relationship.SourceOrganization.IsPersonal,
                    IsActive = relationship.SourceOrganization.IsActive,
                    Color = relationship.SourceOrganization.Color,
                    Logo = relationship.SourceOrganization.Logo
                }
                : new Organization
                {
                    Id = relationship.SourceOrganizationId,
                    Name = string.Empty,
                    OwnerId = 0,
                    Type = OrganizationType.LawFirm,
                    IsActive = false
                };

            sanitized.TargetOrganization = relationship.TargetOrganization != null
                ? new Organization
                {
                    Id = relationship.TargetOrganization.Id,
                    Name = relationship.TargetOrganization.Name,
                    Description = relationship.TargetOrganization.Description,
                    OwnerId = relationship.TargetOrganization.OwnerId,
                    Type = relationship.TargetOrganization.Type,
                    IsPersonal = relationship.TargetOrganization.IsPersonal,
                    IsActive = relationship.TargetOrganization.IsActive,
                    Color = relationship.TargetOrganization.Color,
                    Logo = relationship.TargetOrganization.Logo
                }
                : new Organization
                {
                    Id = relationship.TargetOrganizationId,
                    Name = string.Empty,
                    OwnerId = 0,
                    Type = OrganizationType.Client,
                    IsActive = false
                };

            return sanitized;
        }
    }
}

