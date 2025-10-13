using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<TeamService> _logger;

        public TeamService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            ILogger<TeamService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _logger = logger;
        }

        public async Task<ServiceResult<List<TeamMemberDto>>> GetTeamMembersAsync(
            int organizationId, 
            int userId)
        {
            try
            {
                // Validate user has access to organization (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "TeamMembers", 
                        "User does not have access to this organization");
                }

                var teamMembers = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
                    .Select(uo => new TeamMemberDto
                    {
                        UserId = uo.UserId,
                        FirstName = uo.User.FirstName,
                        LastName = uo.User.LastName,
                        Email = uo.User.Email,
                        Role = uo.Role,
                        Department = !string.IsNullOrEmpty(uo.Department) ? uo.Department : uo.User.Department,
                        Location = uo.User.Location,
                        UserType = uo.UserType,
                        IsActive = uo.IsActive,
                        Avatar = uo.User.Avatar,
                        Color = uo.User.Color,
                        JoinedAt = uo.JoinedAt
                    })
                    .ToListAsync();

                _logger.LogInformation(
                    "Retrieved {Count} team members for organization {OrgId}", 
                    teamMembers.Count, organizationId);

                return ServiceResult<List<TeamMemberDto>>.SuccessResult(teamMembers);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting team members for org {OrgId}", organizationId);
                return ServiceResult<List<TeamMemberDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team members for organization {OrgId}", organizationId);
                return ServiceResult<List<TeamMemberDto>>.FailureResult(
                    "An error occurred while retrieving team members", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<int>> GetTeamMembersCountAsync(
            int organizationId, 
            int userId)
        {
            try
            {
                // Validate user has access to organization (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "TeamMembers", 
                        "User does not have access to this organization");
                }

                var count = await _context.UserOrganizations
                    .Where(uo => uo.OrganizationId == organizationId && uo.IsActive)
                    .CountAsync();

                return ServiceResult<int>.SuccessResult(count);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting team member count for org {OrgId}", organizationId);
                return ServiceResult<int>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team member count for organization {OrgId}", organizationId);
                return ServiceResult<int>.FailureResult(
                    "An error occurred while retrieving team member count", 
                    "ERROR");
            }
        }
    }
}

