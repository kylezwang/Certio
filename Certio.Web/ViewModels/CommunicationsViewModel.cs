using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    public class CommunicationsViewModel
    {
        public List<ChannelCategory> ChannelCategories { get; set; } = new List<ChannelCategory>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<CommunicationsTeamMember> TeamMembers { get; set; } = new List<CommunicationsTeamMember>();
        public string ActiveChannel { get; set; } = "general-client-chat";
        public int OnlineMembersCount { get; set; }
    }

    public class ChannelCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<Channel> Channels { get; set; } = new List<Channel>();
        public List<ChannelSubcategory> Subcategories { get; set; } = new List<ChannelSubcategory>();
    }

    public class ChannelSubcategory
    {
        public string Name { get; set; } = string.Empty;
        public List<Channel> Channels { get; set; } = new List<Channel>();
        public int? OrganizationId { get; set; }
        public int? OrganizationRelationshipId { get; set; }
    }

    public class Channel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Unread { get; set; }
        public ChannelType Type { get; set; }
        public bool IsPrivate { get; set; }
        public List<string> Users { get; set; } = new List<string>();
        public int? MatterId { get; set; }
        public string? MatterTitle { get; set; }
    }

    public enum ChannelType
    {
        Text,
        Voice
    }

    public class Message
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string User { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text"; // Text, Email, File, System
        public List<Reaction> Reactions { get; set; } = new List<Reaction>();
        public DateTime? CreatedAt { get; set; }
    }

    public class Reaction
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CommunicationsTeamMember
    {
        public int UserId { get; set; }
        public int OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string RoleColor { get; set; } = string.Empty;
        public string RoleIcon { get; set; } = string.Empty;
        public bool CanDirectMessage { get; set; } = true;
        public string? OrganizationName { get; set; }
        public bool IsExternalContacts { get; set; } = false;
        public string Color { get; set; } = "#3d1019"; // Default to maroon, will be overridden by User.Color
        public string? Email { get; set; }
    }
}

