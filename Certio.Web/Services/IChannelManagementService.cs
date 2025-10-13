using Certio.Domain.Services;
using Certio.Web.ViewModels;

namespace Certio.Web.Services;

public interface IChannelManagementService
{
    Task<List<Conversation>> GetOrganizationChannelsAsync(int organizationId);
    Task<Conversation> CreateDefaultChannelAsync(int organizationId, int createdById, string channelName, string description, string channelType = "Public");
    Task EnsureDefaultChannelsExistAsync(int organizationId, int createdById);
    Task<int> GetUnreadCountAsync(int conversationId, int userId);
    Task<List<int>> GetOnlineUserIdsAsync(int organizationId);
    Task<List<CommunicationsTeamMember>> GetOrganizationTeamMembersAsync(int organizationId);
}

