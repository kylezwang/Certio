using Certio.Domain.Users;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for managing organization context and membership
    /// </summary>
    public interface IOrganizationContextService
    {
        /// <summary>
        /// Gets the primary organization for a user
        /// </summary>
        Task<int?> GetPrimaryOrganizationIdAsync(int userId);

        /// <summary>
        /// Gets user's role in an organization
        /// </summary>
        Task<string?> GetUserRoleInOrganizationAsync(int userId, int organizationId);

        /// <summary>
        /// Gets user's type (LawFirm, Client, etc.) in an organization
        /// </summary>
        Task<string?> GetUserTypeInOrganizationAsync(int userId, int organizationId);

        /// <summary>
        /// Validates that user belongs to organization
        /// </summary>
        Task<bool> ValidateUserInOrganizationAsync(int userId, int organizationId);

        /// <summary>
        /// Gets all organizations a user is a member of
        /// </summary>
        Task<List<int>> GetUserOrganizationIdsAsync(int userId);

        /// <summary>
        /// Gets organization details
        /// </summary>
        Task<OrganizationDto?> GetOrganizationAsync(int organizationId);
    }

    public class OrganizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string OrganizationType { get; set; } = "";
        public bool IsActive { get; set; }
    }
}

