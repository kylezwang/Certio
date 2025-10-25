using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class OrganizationContextService : IOrganizationContextService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrganizationContextService> _logger;

        public OrganizationContextService(
            ApplicationDbContext context,
            ILogger<OrganizationContextService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int?> GetPrimaryOrganizationIdAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return null;
            }

            var primaryOrg = user.GetPrimaryOrganization();
            return primaryOrg?.OrganizationId;
        }

        public async Task<string?> GetUserRoleInOrganizationAsync(int userId, int organizationId)
        {
            var membership = await _context.UserOrganizations
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == userId && 
                    uo.OrganizationId == organizationId && 
                    uo.IsActive);

            return membership?.Role;
        }

        public async Task<string?> GetUserTypeInOrganizationAsync(int userId, int organizationId)
        {
            var membership = await _context.UserOrganizations
                .FirstOrDefaultAsync(uo => 
                    uo.UserId == userId && 
                    uo.OrganizationId == organizationId && 
                    uo.IsActive);

            return membership?.UserType;
        }

        public async Task<bool> ValidateUserInOrganizationAsync(int userId, int organizationId)
        {
            // Check direct membership first
            var hasDirectAccess = await _context.UserOrganizations
                .AnyAsync(uo => 
                    uo.UserId == userId && 
                    uo.OrganizationId == organizationId && 
                    uo.IsActive);
            
            if (hasDirectAccess)
            {
                return true;
            }
            
            // Check if user has access through organization relationships (law firm accessing client org)
            // Get user's primary organization
            var userPrimaryOrg = await _context.UserOrganizations
                .Where(uo => uo.UserId == userId && uo.IsActive && uo.IsPrimary)
                .Select(uo => new { uo.OrganizationId, uo.UserType })
                .FirstOrDefaultAsync();
            
            if (userPrimaryOrg == null)
            {
                return false;
            }
            
            // Check if there's an active relationship between user's org and target org
            var hasRelationshipAccess = await _context.OrganizationRelationships
                .AnyAsync(r => 
                    r.IsActive &&
                    !r.IsDeleted &&
                    ((r.SourceOrganizationId == userPrimaryOrg.OrganizationId && r.TargetOrganizationId == organizationId) ||
                     (r.TargetOrganizationId == userPrimaryOrg.OrganizationId && r.SourceOrganizationId == organizationId)));
            
            if (hasRelationshipAccess)
            {
                return true;
            }
            
            return false;
        }

        public async Task<List<int>> GetUserOrganizationIdsAsync(int userId)
        {
            return await _context.UserOrganizations
                .Where(uo => uo.UserId == userId && uo.IsActive)
                .Select(uo => uo.OrganizationId)
                .ToListAsync();
        }

        public async Task<OrganizationContextDto?> GetOrganizationAsync(int organizationId)
        {
            var org = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == organizationId);

            if (org == null)
            {
                return null;
            }

            return new OrganizationContextDto
            {
                Id = org.Id,
                Name = org.Name,
                OrganizationType = org.Type.ToString(),
                IsActive = org.IsActive
            };
        }
    }
}

