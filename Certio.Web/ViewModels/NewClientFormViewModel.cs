using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    public class NewClientFormViewModel
    {
        [Required(ErrorMessage = "Client Organization Name is required.")]
        [StringLength(200, ErrorMessage = "Organization name cannot exceed 200 characters.")]
        public string ClientOrganizationName { get; set; } = string.Empty;

        // Selected external user ID to assign as primary client (Owner)
        [Required(ErrorMessage = "Please select a primary client.")]
        public int? SelectedExternalUserId { get; set; }

        // Available external users from external organization relationships
        public List<ExternalUserDto> AvailableExternalUsers { get; set; } = new List<ExternalUserDto>();

        // Context information
        public int LawFirmOrganizationId { get; set; }
        public string LawFirmOrganizationName { get; set; } = string.Empty;
    }

    public class ExternalUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public int ExternalOrganizationId { get; set; }
        public string ExternalOrganizationName { get; set; } = string.Empty;
    }
}

