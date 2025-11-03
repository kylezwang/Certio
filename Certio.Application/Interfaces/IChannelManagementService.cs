using Certio.Domain.Services;

namespace Certio.Application.Interfaces;

public interface IChannelManagementService
{
    Task<List<Conversation>> GetOrganizationChannelsAsync(int organizationId);
    Task<Conversation> CreateDefaultChannelAsync(int organizationId, int createdById, string channelName, string description, string channelType = "Public");
    Task EnsureDefaultChannelsExistAsync(int organizationId, int createdById);
    Task<int> GetUnreadCountAsync(int conversationId, int userId);
    Task<List<int>> GetOnlineUserIdsAsync(int organizationId);
    Task<Conversation?> CreateMatterChannelAsync(int matterId, int organizationId, int createdById);
    Task<List<Conversation>> GetMatterChannelsForOrganizationAsync(int organizationId);
    Task<Dictionary<int, List<Conversation>>> GetClientOrganizationChannelsForLawFirmAsync(int lawFirmOrgId, int userId);
}
