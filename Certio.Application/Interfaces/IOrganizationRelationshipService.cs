using Certio.Application.DTOs;
using Certio.Domain.Organizations;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing law firm-client relationships and user assignments
    /// </summary>
    public interface IOrganizationRelationshipService
    {
        /// <summary>
        /// Gets relationship details between a law firm and client organization
        /// </summary>
        Task<ServiceResult<RelationshipDto>> GetRelationshipDetailsAsync(int lawFirmOrgId, int clientOrgId, int userId);

        /// <summary>
        /// Gets users from a law firm who can be assigned to a client relationship
        /// Excludes already assigned users
        /// </summary>
        Task<ServiceResult<List<AssignableUserDto>>> GetRelationshipAssignableUsersAsync(
            int relationshipId, 
            int userId);

        /// <summary>
        /// Assigns law firm users to a client relationship
        /// Handles business logic: validation, duplicate checking, audit logging
        /// </summary>
        Task<ServiceResult<AssignmentResultDto>> AssignUsersToRelationshipAsync(
            int relationshipId, 
            int userId, 
            List<int> userIdsToAssign,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Checks if a law firm has a relationship with a client organization
        /// Returns null if no relationship exists
        /// </summary>
        Task<ServiceResult<OrganizationRelationship?>> GetLawFirmRelationshipForClientAsync(
            int lawFirmOrgId, 
            int clientOrgId, 
            int userId);
    }
}

