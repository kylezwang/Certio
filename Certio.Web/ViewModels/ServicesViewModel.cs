using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    public class ServicesViewModel
    {
        public List<ChannelCategory> ChannelCategories { get; set; } = new List<ChannelCategory>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public List<ServiceTeamMember> TeamMembers { get; set; } = new List<ServiceTeamMember>();
        public string ActiveChannel { get; set; } = "general-client-chat";
        public int OnlineMembersCount { get; set; }
    }

    public class ChannelCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<Channel> Channels { get; set; } = new List<Channel>();
    }

    public class Channel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Unread { get; set; }
        public ChannelType Type { get; set; }
        public bool IsPrivate { get; set; }
        public List<string> Users { get; set; } = new List<string>();
    }

    public enum ChannelType
    {
        Text,
        Voice
    }

    public class Message
    {
        public int Id { get; set; }
        public string User { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<Reaction> Reactions { get; set; } = new List<Reaction>();
    }

    public class Reaction
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ServiceTeamMember
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string RoleColor { get; set; } = string.Empty;
        public string RoleIcon { get; set; } = string.Empty;
    }
}
