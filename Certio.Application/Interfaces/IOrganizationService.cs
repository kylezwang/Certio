using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing organization queries and operations
    /// </summary>
    public interface IOrganizationService
    {
        /// <summary>
        /// Gets all organizations accessible to a user
        /// Includes direct memberships and firm-based access
        /// </summary>
        Task<ServiceResult<List<OrganizationDto>>> GetAccessibleOrganizationsAsync(int userId);

        /// <summary>
        /// Gets organization details by ID
        /// Validates user has access to organization
        /// </summary>
        Task<ServiceResult<OrganizationDto>> GetOrganizationAsync(int orgId, int userId);

        /// <summary>
        /// Gets basic organization info (name and type) for ViewBag/display purposes
        /// Optimized query for view-specific needs
        /// </summary>
        Task<ServiceResult<OrganizationBasicInfoDto>> GetOrganizationBasicInfoAsync(int orgId, int userId);
    }
}

