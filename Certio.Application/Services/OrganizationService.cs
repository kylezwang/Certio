using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Certio.Domain.Organizations;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Certio.Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            ILogger<OrganizationService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task<ServiceResult<List<OrganizationDto>>> GetAccessibleOrganizationsAsync(int userId)
        {
            try
            {
                // Get direct organization memberships
                var directOrgs = await _context.UserOrganizations
                    .Where(uo => uo.UserId == userId && uo.IsActive)
                    .Select(uo => new OrganizationDto
                    {
                        Id = uo.Organization!.Id,
                        Name = uo.Organization!.Name,
                        Description = uo.Organization.Description,
                        OwnerId = uo.Organization.OwnerId,
                        OwnerFirstName = uo.Organization.Owner.FirstName,
                        OwnerLastName = uo.Organization.Owner.LastName,
                        Type = uo.Organization.Type,
                        IsPersonal = uo.Organization.IsPersonal,
                        IsPrimary = uo.IsPrimary,
                        IsActive = uo.Organization.IsActive,
                        Color = uo.Organization.Color,
                        Logo = uo.Organization.Logo,
                        CreatedAt = uo.Organization.CreatedAt
                    })
                    .ToListAsync();

                // Get organizations accessible via law firm relationships where the user is assigned or a direct member
                var lawFirmOrgIds = await _context.UserOrganizations
                    .Where(uo =>
                        uo.UserId == userId &&
                        uo.IsActive &&
                        uo.UserType == UserTypes.LawFirm)
                    .Select(uo => uo.OrganizationId)
                    .ToListAsync();

                var firmOrgs = new List<OrganizationDto>();

                if (lawFirmOrgIds.Any())
                {
                    var relationships = await _context.OrganizationRelationships
                        .Where(rel => lawFirmOrgIds.Contains(rel.SourceOrganizationId) &&
                                      rel.IsActive &&
                                      !rel.IsDeleted &&
                                      rel.RelationshipType == RelationshipTypes.LawFirmClient &&
                                      (!rel.ExpiresAt.HasValue || rel.ExpiresAt.Value > DateTime.UtcNow))
                        .Include(rel => rel.TargetOrganization)
                            .ThenInclude(org => org.Owner)
                        .ToListAsync();

                    if (relationships.Any())
                    {
                        var relationshipIds = relationships.Select(rel => rel.Id).ToList();
                        var assignedRelationshipIds = await _context.OrganizationRelationshipAssignedUsers
                            .Where(a => a.UserId == userId && relationshipIds.Contains(a.RelationshipId))
                            .Select(a => a.RelationshipId)
                            .ToListAsync();
                        var assignedSet = assignedRelationshipIds.ToHashSet();

                        var directOrgIds = directOrgs.Select(o => o.Id).ToHashSet();

                        foreach (var rel in relationships)
                        {
                            if (rel.TargetOrganization == null)
                            {
                                continue;
                            }

                            var hasDirectMembership = directOrgIds.Contains(rel.TargetOrganizationId);
                            var isAssigned = assignedSet.Contains(rel.Id);
                            var isOwner = rel.TargetOrganization.OwnerId == userId;

                            if (!hasDirectMembership && !isAssigned && !isOwner)
                            {
                                continue;
                            }

                            firmOrgs.Add(new OrganizationDto
                            {
                                Id = rel.TargetOrganization.Id,
                                Name = rel.TargetOrganization.Name,
                                Description = rel.TargetOrganization.Description,
                                OwnerId = rel.TargetOrganization.OwnerId,
                                OwnerFirstName = rel.TargetOrganization.Owner?.FirstName ?? string.Empty,
                                OwnerLastName = rel.TargetOrganization.Owner?.LastName ?? string.Empty,
                                Type = rel.TargetOrganization.Type,
                                IsPersonal = rel.TargetOrganization.IsPersonal,
                                IsPrimary = false,
                                IsActive = rel.TargetOrganization.IsActive,
                                Color = rel.TargetOrganization.Color,
                                Logo = rel.TargetOrganization.Logo,
                                CreatedAt = rel.TargetOrganization.CreatedAt
                            });
                        }
                    }
                }

                // Combine and deduplicate by organization ID
                var allOrgs = directOrgs
                    .Concat(firmOrgs)
                    .GroupBy(o => o.Id)
                    .Select(g => g.First()) // Take first occurrence (direct membership if exists)
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.Name)
                    .ToList();

                _logger.LogInformation(
                    "Retrieved {Count} accessible organizations for user {UserId}", 
                    allOrgs.Count, userId);

                return ServiceResult<List<OrganizationDto>>.SuccessResult(allOrgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible organizations for user {UserId}", userId);
                return ServiceResult<List<OrganizationDto>>.FailureResult(
                    "An error occurred while retrieving organizations", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<OrganizationDto>> GetOrganizationAsync(int orgId, int userId)
        {
            try
            {
                // Validate user has access to organization
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, orgId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, orgId);

                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "Organization", 
                        "User does not have access to this organization");
                }

                var org = await _context.Organizations
                    .Where(o => o.Id == orgId)
                    .Select(o => new OrganizationDto
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Description = o.Description,
                        OwnerId = o.OwnerId,
                        OwnerFirstName = o.Owner.FirstName,
                        OwnerLastName = o.Owner.LastName,
                        Type = o.Type,
                        IsPersonal = o.IsPersonal,
                        IsPrimary = false, // Set to false by default, caller can determine
                        IsActive = o.IsActive,
                        Color = o.Color,
                        Logo = o.Logo,
                        CreatedAt = o.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (org == null)
                {
                    throw new ResourceNotFoundException("Organization", orgId);
                }

                return ServiceResult<OrganizationDto>.SuccessResult(org);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting organization {OrgId}", orgId);
                return ServiceResult<OrganizationDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting organization {OrgId}", orgId);
                return ServiceResult<OrganizationDto>.FailureResult(
                    "An error occurred while retrieving organization", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<OrganizationBasicInfoDto>> GetOrganizationBasicInfoAsync(
            int orgId, 
            int userId)
        {
            try
            {
                // Validate user has access to organization
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, orgId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, orgId);

                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "Organization", 
                        "User does not have access to this organization");
                }

                var orgInfo = await _context.Organizations
                    .Where(o => o.Id == orgId)
                    .Select(o => new OrganizationBasicInfoDto
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Type = o.Type
                    })
                    .FirstOrDefaultAsync();

                if (orgInfo == null)
                {
                    throw new ResourceNotFoundException("Organization", orgId);
                }

                return ServiceResult<OrganizationBasicInfoDto>.SuccessResult(orgInfo);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting basic organization info for {OrgId}", orgId);
                return ServiceResult<OrganizationBasicInfoDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting basic organization info for {OrgId}", orgId);
                return ServiceResult<OrganizationBasicInfoDto>.FailureResult(
                    "An error occurred while retrieving organization info", 
                    "ERROR");
            }
        }
    }
}

