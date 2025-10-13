using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Certio.Domain.Organizations;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class OrganizationRelationshipService : IOrganizationRelationshipService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly IAuditService _auditService;
        private readonly ILogger<OrganizationRelationshipService> _logger;

        public OrganizationRelationshipService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            IAuditService auditService,
            ILogger<OrganizationRelationshipService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<ServiceResult<RelationshipDto>> GetRelationshipDetailsAsync(
            int lawFirmOrgId, 
            int clientOrgId, 
            int userId)
        {
            try
            {
                // Validate user has permission to view relationships (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, lawFirmOrgId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, lawFirmOrgId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "OrganizationRelationship", 
                        "Not a member of law firm organization");
                }

                var relationship = await _context.OrganizationRelationships
                    .Where(or => 
                        or.SourceOrganizationId == lawFirmOrgId &&
                        or.TargetOrganizationId == clientOrgId &&
                        or.IsActive &&
                        !or.IsDeleted)
                    .FirstOrDefaultAsync();

                if (relationship == null)
                {
                    return ServiceResult<RelationshipDto>.FailureResult(
                        "Relationship not found", 
                        "NOT_FOUND");
                }

                // Get assigned users
                var assignedUsers = await _context.Set<OrganizationRelationshipAssignedUser>()
                    .Where(orau => orau.RelationshipId == relationship.Id)
                    .Select(orau => new
                    {
                        orau.UserId,
                        orau.AssignedAt,
                        orau.AssignedById
                    })
                    .ToListAsync();

                // Get user details for assigned users
                var userIds = assignedUsers.Select(au => au.UserId).ToList();
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new
                    {
                        u.Id,
                        u.FirstName,
                        u.LastName,
                        u.Email
                    })
                    .ToListAsync();

                // Get roles from UserOrganization
                var userRoles = await _context.UserOrganizations
                    .Where(uo => userIds.Contains(uo.UserId) && uo.OrganizationId == lawFirmOrgId && uo.IsActive)
                    .Select(uo => new { uo.UserId, uo.Role })
                    .ToDictionaryAsync(x => x.UserId, x => x.Role);

                var dto = new RelationshipDto
                {
                    Id = relationship.Id,
                    SourceOrganizationId = relationship.SourceOrganizationId,
                    TargetOrganizationId = relationship.TargetOrganizationId,
                    RelationshipType = relationship.RelationshipType,
                    AccessLevel = relationship.AccessLevel,
                    Description = relationship.Description,
                    IsActive = relationship.IsActive,
                    ExpiresAt = relationship.ExpiresAt,
                    CreatedAt = relationship.CreatedAt,
                    AssignedUsers = assignedUsers.Select(au =>
                    {
                        var user = users.FirstOrDefault(u => u.Id == au.UserId);
                        return new AssignedUserDto
                        {
                            UserId = au.UserId,
                            Name = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Unknown",
                            Email = user?.Email ?? "",
                            Role = userRoles.ContainsKey(au.UserId) ? userRoles[au.UserId] : null,
                            AssignedAt = au.AssignedAt,
                            AssignedById = au.AssignedById
                        };
                    }).ToList()
                };

                return ServiceResult<RelationshipDto>.SuccessResult(dto);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting relationship details");
                return ServiceResult<RelationshipDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting relationship details between {LawFirmId} and {ClientId}", 
                    lawFirmOrgId, clientOrgId);
                return ServiceResult<RelationshipDto>.FailureResult(
                    "An error occurred while retrieving relationship details", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<List<AssignableUserDto>>> GetRelationshipAssignableUsersAsync(
            int relationshipId, 
            int userId)
        {
            try
            {
                var relationship = await _context.OrganizationRelationships
                    .Where(or => or.Id == relationshipId && or.IsActive && !or.IsDeleted)
                    .FirstOrDefaultAsync();

                if (relationship == null)
                {
                    throw new ResourceNotFoundException("OrganizationRelationship", relationshipId);
                }

                // Validate user has permission to manage assignments (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, relationship.SourceOrganizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, relationship.SourceOrganizationId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "manage", "OrganizationRelationship", 
                        "Not a member of law firm organization");
                }

                // Get law firm members
                var lawFirmMembers = await _context.UserOrganizations
                    .Where(uo => 
                        uo.OrganizationId == relationship.SourceOrganizationId &&
                        uo.UserType == UserTypes.LawFirm &&
                        uo.IsActive)
                    .Select(uo => new
                    {
                        uo.UserId,
                        uo.Role,
                        uo.User.FirstName,
                        uo.User.LastName,
                        uo.User.Email,
                        uo.UserType
                    })
                    .ToListAsync();

                // Get already assigned users
                var assignedUserIds = await _context.Set<OrganizationRelationshipAssignedUser>()
                    .Where(orau => orau.RelationshipId == relationshipId)
                    .Select(orau => orau.UserId)
                    .ToListAsync();

                var assignableUsers = lawFirmMembers.Select(member => new AssignableUserDto
                {
                    Id = member.UserId,
                    Name = $"{member.FirstName} {member.LastName}".Trim(),
                    Email = member.Email,
                    Role = member.Role,
                    UserType = member.UserType,
                    IsCurrentUser = member.UserId == userId,
                    IsAlreadyAssigned = assignedUserIds.Contains(member.UserId)
                }).ToList();

                return ServiceResult<List<AssignableUserDto>>.SuccessResult(assignableUsers);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting assignable users for relationship {RelationshipId}", relationshipId);
                return ServiceResult<List<AssignableUserDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assignable users for relationship {RelationshipId}", relationshipId);
                return ServiceResult<List<AssignableUserDto>>.FailureResult(
                    "An error occurred while retrieving assignable users", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<AssignmentResultDto>> AssignUsersToRelationshipAsync(
            int relationshipId, 
            int userId, 
            List<int> userIdsToAssign,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // Validate relationship exists and is active
                var relationship = await _context.OrganizationRelationships
                    .Where(or => or.Id == relationshipId && or.IsActive && !or.IsDeleted)
                    .FirstOrDefaultAsync();

                if (relationship == null)
                {
                    throw new ResourceNotFoundException("OrganizationRelationship", relationshipId);
                }

                // Validate user has permission to assign users (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, relationship.SourceOrganizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, relationship.SourceOrganizationId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "assign", "OrganizationRelationship", 
                        "Not a member of law firm organization");
                }

                // Validate all users to assign are law firm members
                var validUserIds = await _context.UserOrganizations
                    .Where(uo => 
                        userIdsToAssign.Contains(uo.UserId) &&
                        uo.OrganizationId == relationship.SourceOrganizationId &&
                        uo.UserType == UserTypes.LawFirm &&
                        uo.IsActive)
                    .Select(uo => uo.UserId)
                    .ToListAsync();

                var invalidUserIds = userIdsToAssign.Except(validUserIds).ToList();
                if (invalidUserIds.Any())
                {
                    throw new InvalidOperationException(
                        $"Users {string.Join(", ", invalidUserIds)} are not valid law firm members");
                }

                // Get already assigned users to prevent duplicates
                var existingAssignments = await _context.Set<OrganizationRelationshipAssignedUser>()
                    .Where(orau => orau.RelationshipId == relationshipId)
                    .Select(orau => orau.UserId)
                    .ToListAsync();

                // Create assignments for new users only
                var newUserIds = userIdsToAssign.Except(existingAssignments).ToList();
                var newAssignments = newUserIds.Select(newUserId => new OrganizationRelationshipAssignedUser
                {
                    RelationshipId = relationshipId,
                    UserId = newUserId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = userId
                }).ToList();

                if (newAssignments.Any())
                {
                    _context.Set<OrganizationRelationshipAssignedUser>().AddRange(newAssignments);
                    await _context.SaveChangesAsync();

                    // Audit log for each assignment
                    foreach (var assignment in newAssignments)
                    {
                        await _auditService.LogCreateAsync(
                            userId, 
                            relationship.SourceOrganizationId, 
                            "OrganizationRelationshipAssignment",
                            assignment.Id,
                            ipAddress,
                            userAgent);
                    }

                    _logger.LogInformation(
                        "User {UserId} assigned {Count} users to relationship {RelationshipId}", 
                        userId, newAssignments.Count, relationshipId);
                }

                var result = new AssignmentResultDto
                {
                    AssignedCount = newAssignments.Count,
                    AlreadyAssignedCount = userIdsToAssign.Count - newAssignments.Count,
                    Message = newAssignments.Any()
                        ? $"Successfully assigned {newAssignments.Count} team member(s)"
                        : "All selected members were already assigned"
                };

                return ServiceResult<AssignmentResultDto>.SuccessResult(result);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception assigning users to relationship {RelationshipId}", relationshipId);
                return ServiceResult<AssignmentResultDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning users to relationship {RelationshipId}", relationshipId);
                return ServiceResult<AssignmentResultDto>.FailureResult(
                    "An error occurred while assigning users", 
                    "ERROR");
            }
        }

        public async Task<ServiceResult<OrganizationRelationship?>> GetLawFirmRelationshipForClientAsync(
            int lawFirmOrgId, 
            int clientOrgId, 
            int userId)
        {
            try
            {
                // Validate user has permission (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, lawFirmOrgId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, lawFirmOrgId);
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "view", "OrganizationRelationship", 
                        "Not a member of law firm organization");
                }

                var relationship = await _context.OrganizationRelationships
                    .Where(or => 
                        or.SourceOrganizationId == lawFirmOrgId &&
                        or.TargetOrganizationId == clientOrgId &&
                        or.IsActive &&
                        !or.IsDeleted)
                    .FirstOrDefaultAsync();

                return ServiceResult<OrganizationRelationship?>.SuccessResult(relationship);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting law firm relationship");
                return ServiceResult<OrganizationRelationship?>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting law firm relationship between {LawFirmId} and {ClientId}", 
                    lawFirmOrgId, clientOrgId);
                return ServiceResult<OrganizationRelationship?>.FailureResult(
                    "An error occurred while retrieving relationship", 
                    "ERROR");
            }
        }
    }
}

