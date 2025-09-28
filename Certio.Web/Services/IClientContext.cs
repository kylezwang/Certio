using Certio.Domain.Users;

namespace Certio.Web.Services
{
    public interface IClientContext
    {
        int? OrganizationId { get; }
        string? OrganizationName { get; }
        UserOrganization? Membership { get; }
        bool IsValid { get; }
    }
}


