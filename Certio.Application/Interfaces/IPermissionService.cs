using Certio.Domain.Users;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for evaluating user permissions and resource access
    /// Centralizes all permission logic
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Checks if user has a specific permission in an organization
        /// </summary>
        Task<bool> HasPermissionAsync(int userId, int organizationId, Permission permission);

        /// <summary>
        /// Gets all effective permissions for a user in an organization
        /// </summary>
        Task<List<Permission>> GetEffectivePermissionsAsync(int userId, int organizationId);

        /// <summary>
        /// Checks if user can access a specific matter
        /// </summary>
        Task<bool> CanAccessMatterAsync(int userId, int matterId);

        /// <summary>
        /// Checks if user can perform a specific operation on a matter
        /// </summary>
        Task<bool> CanPerformMatterOperationAsync(int userId, int matterId, string operation);

        /// <summary>
        /// Checks if user can access a specific task
        /// </summary>
        Task<bool> CanAccessTaskAsync(int userId, int taskId);

        /// <summary>
        /// Checks if user can access a specific subtask
        /// </summary>
        Task<bool> CanAccessSubTaskAsync(int userId, int subTaskId);

        /// <summary>
        /// Checks if user is a member of an organization (direct or via firm relationship)
        /// </summary>
        Task<bool> IsOrganizationMemberAsync(int userId, int organizationId);

        /// <summary>
        /// Checks if user has firm-based access to an organization
        /// </summary>
        Task<bool> HasFirmBasedAccessAsync(int userId, int organizationId);

        /// <summary>
        /// Gets all organization IDs accessible to a user
        /// </summary>
        Task<List<int>> GetAccessibleOrganizationIdsAsync(int userId);

        /// <summary>
        /// Validates operation and throws exception if not authorized
        /// </summary>
        Task ValidatePermissionOrThrowAsync(int userId, int organizationId, Permission permission, string operation);
    }
}

