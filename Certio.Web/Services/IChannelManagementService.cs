using Certio.Domain.Services;
using Certio.Web.ViewModels;
using Certio.Application.Interfaces;

namespace Certio.Web.Services;

public interface IChannelManagementService : Certio.Application.Interfaces.IChannelManagementService
{
    Task<List<CommunicationsTeamMember>> GetOrganizationTeamMembersAsync(int organizationId);
}

