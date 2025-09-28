using Certio.Domain.Users;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services
{
    public sealed class ClientContext : IClientContext
    {
        public int? OrganizationId { get; private set; }
        public string? OrganizationName { get; private set; }
        public UserOrganization? Membership { get; private set; }
        public bool IsValid => OrganizationId.HasValue && Membership != null;

        public static async Task<ClientContext> CreateAsync(HttpContext httpContext, ApplicationDbContext db, CancellationToken ct)
        {
            var context = new ClientContext();
            if (!httpContext.Items.TryGetValue("CurrentOrganizationId", out var orgObj) || orgObj is not int orgId)
            {
                return context; // invalid; fail-closed by auth policy
            }

            var customUser = httpContext.Items["CustomUser"] as Certio.Domain.Users.User;
            if (customUser == null)
            {
                return context;
            }

            var membership = await db.UserOrganizations
                .Include(uo => uo.Organization)
                .FirstOrDefaultAsync(uo => uo.UserId == customUser.Id && uo.OrganizationId == orgId && uo.IsActive, ct);

            if (membership == null)
            {
                return context;
            }

            context.OrganizationId = orgId;
            context.OrganizationName = membership.Organization.Name;
            context.Membership = membership;
            return context;
        }
    }
}


