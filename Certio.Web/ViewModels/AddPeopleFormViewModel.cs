using System.ComponentModel.DataAnnotations;
using Certio.Domain.Users;

namespace Certio.Web.ViewModels
{
    public class AddPeopleFormViewModel
    {
        // Current step in the wizard (1 = selection, 2 = form)
        public int Step { get; set; } = 1;

        // Selection from Step 1 (InternalTeam, Client, External)
        public string? SelectionType { get; set; }

        // Context information
        public bool IsLawFirmUser { get; set; }
        public bool IsClientOrganization { get; set; }
        public int? LawFirmOrganizationId { get; set; }
        public string? LawFirmOrganizationName { get; set; }

        // For join code invitations (Client and External in all contexts, InternalTeam in law firm context)
        [EmailAddress]
        public string? Email { get; set; }

        public string? UserType { get; set; }
        public string? Role { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }

        // For team member assignment (InternalTeam in client context)
        public List<int> SelectedUserIds { get; set; } = new List<int>();

        // Available team members for selection
        public List<OrgMemberDto> AvailableTeamMembers { get; set; } = new List<OrgMemberDto>();
        public List<int> AlreadyAssignedUserIds { get; set; } = new List<int>();
    }

    public class OrgMemberDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public bool IsCurrentUser { get; set; }
        public bool IsAlreadyAssigned { get; set; }
    }
}

