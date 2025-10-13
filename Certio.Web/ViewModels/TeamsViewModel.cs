using System.ComponentModel.DataAnnotations;
using Certio.Domain.Matters;

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
    }

    public class TeamMember
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Location { get; set; }
        public TeamType Team { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public enum TeamType
    {
        Client,
        Legal,
        External
    }
}
