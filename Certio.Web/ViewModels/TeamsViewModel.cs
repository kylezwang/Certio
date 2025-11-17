using System.ComponentModel.DataAnnotations;
using Certio.Domain.Matters;
using System.Text.RegularExpressions;

namespace Certio.Web.ViewModels
{
    public class TeamsViewModel
    {
        public List<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
        public int ClientTeamCount { get; set; }
        public int LegalTeamCount { get; set; }
        public int ExternalTeamCount { get; set; }
        public int TotalMembersCount { get; set; }
        public List<Matter> Matters { get; set; } = new List<Matter>();
        public List<PendingInvitation> PendingInvitations { get; set; } = new List<PendingInvitation>();
    }

    public class TeamMember
    {
        private string _role = string.Empty;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Role
        {
            get => _role;
            set
            {
                _role = value ?? string.Empty;
                DisplayRole = FormatRole(_role);
            }
        }
        public string DisplayRole { get; private set; } = string.Empty;
        public string? Department { get; set; }
        public string? Location { get; set; }
        public TeamType Team { get; set; }
        public string Color { get; set; } = string.Empty;

        private static string FormatRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return string.Empty;
            }

            var withSpaces = Regex.Replace(role, "(?<=[a-z])(?=[A-Z])", " ");
            withSpaces = Regex.Replace(withSpaces, "(?<=[A-Z])(?=[A-Z][a-z])", " ");
            return withSpaces.Trim();
        }
    }

    public enum TeamType
    {
        Client,
        Legal,
        External
    }

    public class PendingInvitation
    {
        public string JoinCode { get; set; } = string.Empty;
        public string InvitedRole { get; set; } = string.Empty;
        public string InvitedUserType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
