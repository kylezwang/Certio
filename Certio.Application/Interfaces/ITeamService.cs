using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing team member queries and operations
    /// </summary>
    public interface ITeamService
    {
        /// <summary>
        /// Gets all active team members for an organization
        /// Includes user details, role, and status
        /// </summary>
        Task<ServiceResult<List<TeamMemberDto>>> GetTeamMembersAsync(int organizationId, int userId);

        /// <summary>
        /// Gets count of active team members in an organization
        /// </summary>
        Task<ServiceResult<int>> GetTeamMembersCountAsync(int organizationId, int userId);
    }
}

