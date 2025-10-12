using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service interface for all matter operations
    /// Enforces organization isolation and permission checks
    /// </summary>
    public interface IMatterService
    {
        /// <summary>
        /// Creates a new matter in the specified organization
        /// </summary>
        Task<ServiceResult<MatterDto>> CreateMatterAsync(
            int userId, 
            int organizationId, 
            CreateMatterDto createDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Updates an existing matter
        /// </summary>
        Task<ServiceResult<MatterDto>> UpdateMatterAsync(
            int userId, 
            int matterId, 
            UpdateMatterDto updateDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Soft deletes a matter (marks as deleted)
        /// </summary>
        Task<ServiceResult> DeleteMatterAsync(
            int userId, 
            int matterId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets a matter by ID with permission validation
        /// </summary>
        Task<ServiceResult<MatterDto>> GetMatterAsync(
            int userId, 
            int matterId);

        /// <summary>
        /// Lists all matters accessible to the user in an organization
        /// </summary>
        Task<ServiceResult<List<MatterDto>>> ListMattersAsync(
            int userId, 
            int organizationId, 
            MatterFilterDto? filter = null);

        /// <summary>
        /// Assigns a user to a matter with a specific role
        /// </summary>
        Task<ServiceResult<MatterAssignmentDto>> AssignUserToMatterAsync(
            int userId, 
            int matterId, 
            AssignUserToMatterDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Removes a user assignment from a matter
        /// </summary>
        Task<ServiceResult> RemoveUserFromMatterAsync(
            int userId, 
            int matterId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Grants specific matter access to a user (for AccessLevel = "Specific")
        /// </summary>
        Task<ServiceResult> GrantMatterAccessAsync(
            int userId, 
            int matterId, 
            int granteeId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Revokes specific matter access from a user
        /// </summary>
        Task<ServiceResult> RevokeMatterAccessAsync(
            int userId, 
            int matterId, 
            int granteeId,
            string? ipAddress = null,
            string? userAgent = null);
    }
}

