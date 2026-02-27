using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Certio.Domain.Matters;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class MatterService : IMatterService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly IOrganizationContextService _orgContextService;
        private readonly IAuditService _auditService;
        private readonly ILogger<MatterService> _logger;
        private readonly Certio.Application.Interfaces.IChannelManagementService? _channelManagementService;

        public MatterService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            IOrganizationContextService orgContextService,
            IAuditService auditService,
            ILogger<MatterService> logger,
            Certio.Application.Interfaces.IChannelManagementService? channelManagementService = null)
        {
            _context = context;
            _permissionService = permissionService;
            _orgContextService = orgContextService;
            _auditService = auditService;
            _logger = logger;
            _channelManagementService = channelManagementService;
        }

        public async Task<ServiceResult<MatterDto>> CreateMatterAsync(
            int userId, 
            int organizationId, 
            CreateMatterDto createDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // Validate user is in organization (direct membership or firm-based access)
                var isOrgMember = await _permissionService.IsOrganizationMemberAsync(userId, organizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
                
                _logger.LogInformation($"CreateMatter access check: UserId={userId}, OrgId={organizationId}, IsOrgMember={isOrgMember}, HasFirmAccess={hasFirmAccess}");
                
                if (!isOrgMember && !hasFirmAccess)
                {
                    throw new UnauthorizedOperationException(userId, "create", "Matter", "Not a member of organization");
                }

                // Check permission
                var hasPermission = await _permissionService.HasPermissionAsync(userId, organizationId, Permission.CreateMatters);
                _logger.LogInformation($"CreateMatter permission check: UserId={userId}, OrgId={organizationId}, HasPermission={hasPermission}");
                
                if (!hasPermission)
                {
                    var permissions = await _permissionService.GetEffectivePermissionsAsync(userId, organizationId);
                    _logger.LogWarning($"User {userId} lacks CreateMatters permission in org {organizationId}. Effective permissions: {string.Join(", ", permissions)}");
                    throw new UnauthorizedOperationException(userId, "create", "Matter", "Lacks CreateMatters permission");
                }

                // Validate DTO
                ValidateCreateMatterDto(createDto);

                // Create matter
                var matter = new Matter
                {
                    Title = createDto.Title,
                    Description = createDto.Description,
                    Location = createDto.Location,
                    Status = createDto.Status,
                    PracticeArea = createDto.PracticeArea,
                    GuestCount = createDto.GuestCount,
                    Budget = createDto.Budget,
                    AccessLevel = createDto.AccessLevel,
                    OrganizationId = organizationId,
                    TeamId = createDto.TeamId,
                    ClientId = createDto.ClientId,
                    StartDate = createDto.StartDate,
                    DueDate = createDto.DueDate,
                    PendingDate = createDto.PendingDate,
                    StatuteOfLimitationsDate = createDto.StatuteOfLimitationsDate,
                    ClientGoals = createDto.ClientGoals,
                    LegalRequirements = createDto.LegalRequirements,
                    Notes = createDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId
                };

                _context.Matters.Add(matter);
                await _context.SaveChangesAsync();

                // Handle specific permissions if AccessLevel is "Specific"
                if (matter.AccessLevel == "Specific" && createDto.PermissionUserIds != null && createDto.PermissionUserIds.Any())
                {
                    var permissions = createDto.PermissionUserIds.Select(uid => new MatterPermission
                    {
                        MatterId = matter.Id,
                        UserId = uid,
                        GrantedAt = DateTime.UtcNow,
                        GrantedById = userId
                    }).ToList();

                    _context.MatterPermissions.AddRange(permissions);
                    await _context.SaveChangesAsync();
                }

                // Auto-create channel for this matter
                if (_channelManagementService != null)
                {
                    try
                    {
                        _logger.LogInformation("Attempting to create channel for matter {MatterId} in organization {OrganizationId} by user {UserId}", matter.Id, organizationId, userId);
                        var channel = await _channelManagementService.CreateMatterChannelAsync(matter.Id, organizationId, userId);
                        if (channel != null)
                        {
                            _logger.LogInformation("Successfully created channel {ChannelId} for matter {MatterId}", channel.Id, matter.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Channel creation returned null for matter {MatterId}", matter.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create channel for matter {MatterId}, but matter was created successfully", matter.Id);
                        // Don't fail the matter creation if channel creation fails
                    }
                }
                else
                {
                    _logger.LogWarning("ChannelManagementService is null - cannot create channel for matter {MatterId}", matter.Id);
                }

                // Audit log

                _logger.LogInformation("User {UserId} created matter {MatterId} in org {OrgId}", userId, matter.Id, organizationId);

                return ServiceResult<MatterDto>.SuccessResult(await MapToMatterDto(matter));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception creating matter for user {UserId}", userId);
                return ServiceResult<MatterDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating matter for user {UserId} in org {OrgId}", userId, organizationId);
                return ServiceResult<MatterDto>.FailureResult("An error occurred while creating the matter", "ERROR");
            }
        }

        public async Task<ServiceResult<MatterDto>> UpdateMatterAsync(
            int userId, 
            int matterId, 
            UpdateMatterDto updateDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .Include(m => m.Permissions)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "update", "Matter", "No access to matter");
                }

                // Check permission
                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.EditMatters))
                {
                    throw new UnauthorizedOperationException(userId, "update", "Matter", "Lacks EditMatters permission");
                }

                // Apply updates
                if (updateDto.Title != null) matter.Title = updateDto.Title;
                if (updateDto.Description != null) matter.Description = updateDto.Description;
                if (updateDto.Location != null) matter.Location = updateDto.Location;
                if (updateDto.Status != null) matter.Status = updateDto.Status;
                if (updateDto.PracticeArea != null) matter.PracticeArea = updateDto.PracticeArea;
                if (updateDto.GuestCount.HasValue) matter.GuestCount = updateDto.GuestCount;
                if (updateDto.Budget.HasValue) matter.Budget = updateDto.Budget;
                if (updateDto.AccessLevel != null) matter.AccessLevel = updateDto.AccessLevel;
                if (updateDto.TeamId.HasValue) matter.TeamId = updateDto.TeamId;
                if (updateDto.ClientId.HasValue) matter.ClientId = updateDto.ClientId;
                if (updateDto.StartDate.HasValue) matter.StartDate = updateDto.StartDate;
                if (updateDto.DueDate.HasValue) matter.DueDate = updateDto.DueDate;
                if (updateDto.CompletedDate.HasValue) matter.CompletedDate = updateDto.CompletedDate;
                if (updateDto.PendingDate.HasValue) matter.PendingDate = updateDto.PendingDate;
                if (updateDto.StatuteOfLimitationsDate.HasValue) matter.StatuteOfLimitationsDate = updateDto.StatuteOfLimitationsDate;
                if (updateDto.StatuteOfLimitationsSatisfied.HasValue) matter.StatuteOfLimitationsSatisfied = updateDto.StatuteOfLimitationsSatisfied.Value;
                if (updateDto.ClientGoals != null) matter.ClientGoals = updateDto.ClientGoals;
                if (updateDto.LegalRequirements != null) matter.LegalRequirements = updateDto.LegalRequirements;
                if (updateDto.Notes != null) matter.Notes = updateDto.Notes;

                matter.LastModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Audit log

                _logger.LogInformation("User {UserId} updated matter {MatterId}", userId, matterId);

                return ServiceResult<MatterDto>.SuccessResult(await MapToMatterDto(matter));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception updating matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult<MatterDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult<MatterDto>.FailureResult("An error occurred while updating the matter", "ERROR");
            }
        }

        public async Task<ServiceResult> DeleteMatterAsync(
            int userId, 
            int matterId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "delete", "Matter", "No access to matter");
                }

                // Check permission
                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.DeleteMatters))
                {
                    throw new UnauthorizedOperationException(userId, "delete", "Matter", "Lacks DeleteMatters permission");
                }

                matter.IsDeleted = true;
                matter.DeletedAt = DateTime.UtcNow;
                matter.DeletedById = userId;
                await _context.SaveChangesAsync();

                // Audit log

                _logger.LogInformation("User {UserId} deleted matter {MatterId}", userId, matterId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception deleting matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult.FailureResult("An error occurred while deleting the matter", "ERROR");
            }
        }

        public async Task<ServiceResult<MatterDto>> GetMatterAsync(int userId, int matterId)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                        .ThenInclude(a => a.User)
                    .Include(m => m.Permissions)
                        .ThenInclude(p => p.User)
                    .Include(m => m.TaskItems)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "view", "Matter", "No access to matter");
                }

                return ServiceResult<MatterDto>.SuccessResult(await MapToMatterDto(matter));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult<MatterDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting matter {MatterId} for user {UserId}", matterId, userId);
                return ServiceResult<MatterDto>.FailureResult("An error occurred while retrieving the matter", "ERROR");
            }
        }

        public async Task<ServiceResult<List<MatterDto>>> ListMattersAsync(
            int userId, 
            int organizationId, 
            MatterFilterDto? filter = null)
        {
            try
            {
                // Validate user is in organization
                if (!await _permissionService.IsOrganizationMemberAsync(userId, organizationId))
                {
                    // Check firm-based access
                    if (!await _permissionService.HasFirmBasedAccessAsync(userId, organizationId))
                    {
                        throw new UnauthorizedOperationException(userId, "list", "Matter", "Not a member of organization");
                    }
                }

                var query = _context.Matters
                    .Include(m => m.Assignments)
                        .ThenInclude(a => a.User)
                    .Include(m => m.Permissions)
                        .ThenInclude(p => p.User)
                    .Include(m => m.TaskItems)
                    .Where(m => m.OrganizationId == organizationId && !m.IsDeleted);

                // Apply access filtering based on membership type
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(userId, organizationId);
                
                _logger.LogInformation("ListMatters: User {UserId} accessing Org {OrgId}, hasFirmAccess={HasFirmAccess}", 
                    userId, organizationId, hasFirmAccess);
                
                if (!hasFirmAccess)
                {
                    // Direct members: check their role in the organization
                    var userOrgMembership = await _context.UserOrganizations
                        .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationId == organizationId && uo.IsActive);
                    
                    _logger.LogInformation("ListMatters: User {UserId} direct member with Role={Role}", 
                        userId, userOrgMembership?.Role ?? "NULL");
                    
                    // Only Partners see all matters; others ONLY see assigned matters
                    if (userOrgMembership?.Role != Certio.Domain.Users.OrganizationRoles.Partner && 
                        userOrgMembership?.Role != Certio.Domain.Users.OrganizationRoles.ManagingPartner)
                    {
                        _logger.LogInformation("ListMatters: Applying non-Partner filtering for user {UserId}", userId);
                        
                        // Non-partners: only show matters they're explicitly assigned to or have permissions for
                        query = query.Where(m => 
                            m.Permissions.Any(p => p.UserId == userId && p.RevokedAt == null) ||
                            m.Assignments.Any(a => a.UserId == userId && a.RemovedAt == null));
                    }
                    else
                    {
                        _logger.LogInformation("ListMatters: User {UserId} is Partner, no filtering applied", userId);
                    }
                }
                else
                {
                    // Firm-based users: check their relationship AccessLevel AND their role in the law firm
                    var firmRelationship = await _permissionService.GetFirmRelationshipAsync(userId, organizationId);
                    
                    // Check if user is a Partner in their law firm
                    var lawFirmMembership = await _context.UserOrganizations
                        .FirstOrDefaultAsync(uo => uo.UserId == userId && 
                                                   uo.IsActive && 
                                                   uo.UserType == Certio.Domain.Users.UserTypes.LawFirm);
                    
                    var isPartner = lawFirmMembership?.Role == Certio.Domain.Users.OrganizationRoles.Partner || 
                                    lawFirmMembership?.Role == Certio.Domain.Users.OrganizationRoles.ManagingPartner;
                    
                    _logger.LogInformation("ListMatters: Firm user {UserId}, isPartner={IsPartner}, AccessLevel={AccessLevel}", 
                        userId, isPartner, firmRelationship?.AccessLevel ?? "NULL");
                    
                    // Partners with Full/Limited access see all matters
                    // Non-partners OR MatterSpecific/DocumentOnly access see only assigned matters
                    if (!isPartner || 
                        firmRelationship?.AccessLevel == "MatterSpecific" || 
                        firmRelationship?.AccessLevel == "DocumentOnly")
                    {
                        _logger.LogInformation("ListMatters: Applying matter-specific filtering for user {UserId}", userId);
                        
                        // Only show matters they're assigned to or have explicit permissions for
                        query = query.Where(m => 
                            m.AccessLevel == "Everyone" || 
                            m.Permissions.Any(p => p.UserId == userId && p.RevokedAt == null) ||
                            m.Assignments.Any(a => a.UserId == userId && a.RemovedAt == null));
                    }
                    else
                    {
                        _logger.LogInformation("ListMatters: Partner with Full/Limited access, no filtering applied for user {UserId}", userId);
                    }
                }

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Status))
                    {
                        query = query.Where(m => m.Status == filter.Status);
                    }

                    if (!string.IsNullOrEmpty(filter.PracticeArea))
                    {
                        query = query.Where(m => m.PracticeArea == filter.PracticeArea);
                    }

                    if (filter.AssignedUserId.HasValue)
                    {
                        query = query.Where(m => m.Assignments.Any(a => 
                            a.UserId == filter.AssignedUserId.Value && 
                            a.RemovedAt == null));
                    }

                    if (filter.StartDateFrom.HasValue)
                    {
                        query = query.Where(m => m.StartDate >= filter.StartDateFrom.Value);
                    }

                    if (filter.StartDateTo.HasValue)
                    {
                        query = query.Where(m => m.StartDate <= filter.StartDateTo.Value);
                    }

                    if (filter.DueDateFrom.HasValue)
                    {
                        query = query.Where(m => m.DueDate >= filter.DueDateFrom.Value);
                    }

                    if (filter.DueDateTo.HasValue)
                    {
                        query = query.Where(m => m.DueDate <= filter.DueDateTo.Value);
                    }

                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        var searchTerm = filter.SearchTerm.ToLower();
                        query = query.Where(m => 
                            m.Title.ToLower().Contains(searchTerm) ||
                            m.Description.ToLower().Contains(searchTerm));
                    }
                }

                var matters = await query.ToListAsync();
                var matterDtos = new List<MatterDto>();

                foreach (var matter in matters)
                {
                    matterDtos.Add(await MapToMatterDto(matter));
                }

                return ServiceResult<List<MatterDto>>.SuccessResult(matterDtos);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception listing matters for user {UserId} in org {OrgId}", userId, organizationId);
                return ServiceResult<List<MatterDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing matters for user {UserId} in org {OrgId}", userId, organizationId);
                return ServiceResult<List<MatterDto>>.FailureResult("An error occurred while listing matters", "ERROR");
            }
        }

        public async Task<ServiceResult<MatterAssignmentDto>> AssignUserToMatterAsync(
            int userId, 
            int matterId, 
            AssignUserToMatterDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "assign", "Matter", "No access to matter");
                }

                // Check permission to manage matter settings OR if user created the matter (allow assignment during creation)
                var hasManagePermission = await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings);
                var isCreator = matter.CreatedById == userId;
                
                if (!hasManagePermission && !isCreator)
                {
                    throw new UnauthorizedOperationException(userId, "assign", "Matter", "Lacks ManageMatterSettings permission and is not the matter creator");
                }

                // Validate assignee has access to organization (direct membership or firm-based access)
                var hasDirectMembership = await _permissionService.IsOrganizationMemberAsync(assignmentDto.UserId, matter.OrganizationId);
                var hasFirmAccess = await _permissionService.HasFirmBasedAccessAsync(assignmentDto.UserId, matter.OrganizationId);
                
                if (!hasDirectMembership && !hasFirmAccess)
                {
                    throw new BusinessRuleViolationException("UserMembership", "User must be a member of the organization or have firm-based access");
                }

                // Check if already assigned with the same AssignmentType
                // Allow same user to have multiple different assignment types (e.g., Originating Attorney + Responsible Attorney)
                var existingAssignment = matter.Assignments
                    .FirstOrDefault(a => 
                        a.UserId == assignmentDto.UserId && 
                        a.AssignmentType == assignmentDto.AssignmentType && 
                        a.RemovedAt == null);

                if (existingAssignment != null)
                {
                    throw new BusinessRuleViolationException("DuplicateAssignment", 
                        $"User is already assigned as {assignmentDto.AssignmentType} to this matter");
                }

                var assignment = new MatterAssignment
                {
                    MatterId = matterId,
                    UserId = assignmentDto.UserId,
                    AssignmentType = assignmentDto.AssignmentType,
                    Role = assignmentDto.Role,
                    IsNotifyRecipient = assignmentDto.IsNotifyRecipient,
                    AssignedAt = DateTime.UtcNow
                };

                _context.MatterAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                // Reload to get user info
                await _context.Entry(assignment).Reference(a => a.User).LoadAsync();

                // Audit logging now handled automatically by AuditInterceptor

                _logger.LogInformation("User {UserId} assigned user {AssigneeId} to matter {MatterId}", 
                    userId, assignmentDto.UserId, matterId);

                return ServiceResult<MatterAssignmentDto>.SuccessResult(MapToMatterAssignmentDto(assignment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception assigning user to matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning user to matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult("An error occurred while assigning user to matter", "ERROR");
            }
        }

        public async Task<ServiceResult> RemoveUserFromMatterAsync(
            int userId, 
            int matterId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "unassign", "Matter", "No access to matter");
                }

                // Check permission
                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                {
                    throw new UnauthorizedOperationException(userId, "unassign", "Matter", "Lacks ManageMatterSettings permission");
                }

                var assignment = matter.Assignments
                    .FirstOrDefault(a => a.UserId == assigneeId && a.RemovedAt == null);

                if (assignment == null)
                {
                    throw new ResourceNotFoundException("MatterAssignment", assigneeId);
                }

                assignment.RemovedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Audit logging now handled automatically by AuditInterceptor

                _logger.LogInformation("User {UserId} removed user {AssigneeId} from matter {MatterId}", 
                    userId, assigneeId, matterId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception removing user from matter {MatterId}", matterId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from matter {MatterId}", matterId);
                return ServiceResult.FailureResult("An error occurred while removing user from matter", "ERROR");
            }
        }

        public async Task<ServiceResult<MatterAssignmentDto>> AddContactToMatterAsync(
            int userId,
            int matterId,
            AddContactToMatterDto dto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                    throw new ResourceNotFoundException("Matter", matterId);

                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                    throw new UnauthorizedOperationException(userId, "add-contact", "Matter", "No access to matter");

                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                    throw new UnauthorizedOperationException(userId, "add-contact", "Matter", "Lacks ManageMatterSettings permission");

                var assignment = new MatterAssignment
                {
                    MatterId = matterId,
                    UserId = null,
                    AssignmentType = dto.AssignmentType,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumber = dto.PhoneNumber,
                    Role = dto.Role,
                    VendorCategory = dto.VendorCategory,
                    VendorStatus = dto.VendorStatus,
                    ContractAmount = dto.ContractAmount,
                    AmountPaid = dto.AmountPaid,
                    RsvpStatus = dto.RsvpStatus,
                    PartySize = dto.PartySize,
                    TableNumber = dto.TableNumber,
                    DietaryRestrictions = dto.DietaryRestrictions,
                    MealChoice = dto.MealChoice,
                    AssignedAt = DateTime.UtcNow
                };

                _context.MatterAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} added {Type} contact to matter {MatterId}",
                    userId, dto.AssignmentType, matterId);

                return ServiceResult<MatterAssignmentDto>.SuccessResult(MapToMatterAssignmentDto(assignment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception adding contact to matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding contact to matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult("An error occurred while adding contact to matter", "ERROR");
            }
        }

        public async Task<ServiceResult<MatterAssignmentDto>> UpdateContactOnMatterAsync(
            int userId,
            int matterId,
            int assignmentId,
            AddContactToMatterDto dto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                    throw new ResourceNotFoundException("Matter", matterId);

                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                    throw new UnauthorizedOperationException(userId, "update-contact", "Matter", "No access to matter");

                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                    throw new UnauthorizedOperationException(userId, "update-contact", "Matter", "Lacks ManageMatterSettings permission");

                var assignment = matter.Assignments.FirstOrDefault(a => a.Id == assignmentId && a.RemovedAt == null);
                if (assignment == null)
                    throw new ResourceNotFoundException("MatterAssignment", assignmentId);

                assignment.Email = dto.Email;
                assignment.FirstName = dto.FirstName;
                assignment.LastName = dto.LastName;
                assignment.PhoneNumber = dto.PhoneNumber;
                assignment.Role = dto.Role;
                assignment.VendorCategory = dto.VendorCategory;
                assignment.VendorStatus = dto.VendorStatus;
                assignment.ContractAmount = dto.ContractAmount;
                assignment.AmountPaid = dto.AmountPaid;
                assignment.RsvpStatus = dto.RsvpStatus;
                assignment.PartySize = dto.PartySize;
                assignment.TableNumber = dto.TableNumber;
                assignment.DietaryRestrictions = dto.DietaryRestrictions;
                assignment.MealChoice = dto.MealChoice;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated contact {AssignmentId} on matter {MatterId}",
                    userId, assignmentId, matterId);

                return ServiceResult<MatterAssignmentDto>.SuccessResult(MapToMatterAssignmentDto(assignment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception updating contact on matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact on matter {MatterId}", matterId);
                return ServiceResult<MatterAssignmentDto>.FailureResult("An error occurred while updating contact on matter", "ERROR");
            }
        }

        public async Task<ServiceResult> RemoveContactFromMatterAsync(
            int userId,
            int matterId,
            int assignmentId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Assignments)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                    throw new ResourceNotFoundException("Matter", matterId);

                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                    throw new UnauthorizedOperationException(userId, "remove-contact", "Matter", "No access to matter");

                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                    throw new UnauthorizedOperationException(userId, "remove-contact", "Matter", "Lacks ManageMatterSettings permission");

                var assignment = matter.Assignments.FirstOrDefault(a => a.Id == assignmentId && a.RemovedAt == null);
                if (assignment == null)
                    throw new ResourceNotFoundException("MatterAssignment", assignmentId);

                assignment.RemovedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} removed contact {AssignmentId} from matter {MatterId}",
                    userId, assignmentId, matterId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception removing contact from matter {MatterId}", matterId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing contact from matter {MatterId}", matterId);
                return ServiceResult.FailureResult("An error occurred while removing contact from matter", "ERROR");
            }
        }

        public async Task<ServiceResult> GrantMatterAccessAsync(
            int userId, 
            int matterId, 
            int granteeId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Permissions)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "grant_access", "Matter", "No access to matter");
                }

                // Check permission
                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                {
                    throw new UnauthorizedOperationException(userId, "grant_access", "Matter", "Lacks ManageMatterSettings permission");
                }

                // Validate grantee is in organization
                if (!await _permissionService.IsOrganizationMemberAsync(granteeId, matter.OrganizationId))
                {
                    throw new BusinessRuleViolationException("UserMembership", "User must be a member of the organization");
                }

                // Check if already has permission
                var existingPermission = matter.Permissions
                    .FirstOrDefault(p => p.UserId == granteeId && p.RevokedAt == null);

                if (existingPermission != null)
                {
                    throw new BusinessRuleViolationException("DuplicatePermission", "User already has access to this matter");
                }

                var permission = new MatterPermission
                {
                    MatterId = matterId,
                    UserId = granteeId,
                    GrantedAt = DateTime.UtcNow,
                    GrantedById = userId
                };

                _context.MatterPermissions.Add(permission);
                await _context.SaveChangesAsync();

                // Audit logging now handled automatically by AuditInterceptor

                _logger.LogInformation("User {UserId} granted matter access to user {GranteeId} for matter {MatterId}", 
                    userId, granteeId, matterId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception granting matter access for matter {MatterId}", matterId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting matter access for matter {MatterId}", matterId);
                return ServiceResult.FailureResult("An error occurred while granting matter access", "ERROR");
            }
        }

        public async Task<ServiceResult> RevokeMatterAccessAsync(
            int userId, 
            int matterId, 
            int granteeId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var matter = await _context.Matters
                    .Include(m => m.Permissions)
                    .FirstOrDefaultAsync(m => m.Id == matterId && !m.IsDeleted);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "revoke_access", "Matter", "No access to matter");
                }

                // Check permission
                if (!await _permissionService.HasPermissionAsync(userId, matter.OrganizationId, Permission.ManageMatterSettings))
                {
                    throw new UnauthorizedOperationException(userId, "revoke_access", "Matter", "Lacks ManageMatterSettings permission");
                }

                var permission = matter.Permissions
                    .FirstOrDefault(p => p.UserId == granteeId && p.RevokedAt == null);

                if (permission == null)
                {
                    throw new ResourceNotFoundException("MatterPermission", granteeId);
                }

                permission.RevokedAt = DateTime.UtcNow;
                permission.RevokedById = userId;
                await _context.SaveChangesAsync();

                // Audit logging now handled automatically by AuditInterceptor

                _logger.LogInformation("User {UserId} revoked matter access from user {GranteeId} for matter {MatterId}", 
                    userId, granteeId, matterId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception revoking matter access for matter {MatterId}", matterId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking matter access for matter {MatterId}", matterId);
                return ServiceResult.FailureResult("An error occurred while revoking matter access", "ERROR");
            }
        }

        // Helper methods
        private void ValidateCreateMatterDto(CreateMatterDto dto)
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                errors["Title"] = new[] { "Title is required" };
            }

            if (dto.Title?.Length > 200)
            {
                errors["Title"] = new[] { "Title cannot exceed 200 characters" };
            }

            if (dto.Description?.Length > 1000)
            {
                errors["Description"] = new[] { "Description cannot exceed 1000 characters" };
            }

            if (string.IsNullOrWhiteSpace(dto.Location))
            {
                errors["Location"] = new[] { "Location is required" };
            }

            if (dto.Location?.Length > 300)
            {
                errors["Location"] = new[] { "Location cannot exceed 300 characters" };
            }

            if (errors.Any())
            {
                throw new ValidationException(errors);
            }
        }

        private async Task<MatterDto> MapToMatterDto(Matter matter)
        {
            // Load task items for computed properties
            await _context.Entry(matter)
                .Collection(m => m.TaskItems)
                .LoadAsync();

            return new MatterDto
            {
                Id = matter.Id,
                Title = matter.Title,
                Description = matter.Description,
                Location = matter.Location,
                Status = matter.Status,
                PracticeArea = matter.PracticeArea,
                GuestCount = matter.GuestCount,
                Budget = matter.Budget,
                AccessLevel = matter.AccessLevel,
                OrganizationId = matter.OrganizationId,
                TeamId = matter.TeamId,
                ClientId = matter.ClientId,
                StartDate = matter.StartDate,
                DueDate = matter.DueDate,
                CompletedDate = matter.CompletedDate,
                PendingDate = matter.PendingDate,
                StatuteOfLimitationsDate = matter.StatuteOfLimitationsDate,
                StatuteOfLimitationsSatisfied = matter.StatuteOfLimitationsSatisfied,
                ClientGoals = matter.ClientGoals,
                LegalRequirements = matter.LegalRequirements,
                Notes = matter.Notes,
                CreatedAt = matter.CreatedAt,
                LastModifiedDate = matter.LastModifiedDate,
                TasksCompleted = matter.TasksCompleted,
                TotalTasks = matter.TotalTasks,
                Assignments = matter.Assignments.Where(a => a.RemovedAt == null).Select(MapToMatterAssignmentDto).ToList(),
                Permissions = matter.Permissions.Where(p => p.RevokedAt == null).Select(MapToMatterPermissionDto).ToList()
            };
        }

        private MatterAssignmentDto MapToMatterAssignmentDto(MatterAssignment assignment)
        {
            return new MatterAssignmentDto
            {
                Id = assignment.Id,
                MatterId = assignment.MatterId,
                UserId = assignment.UserId,
                Email = assignment.Email,
                FirstName = assignment.FirstName,
                LastName = assignment.LastName,
                PhoneNumber = assignment.PhoneNumber,
                AssignmentType = assignment.AssignmentType,
                Role = assignment.Role,
                VendorCategory = assignment.VendorCategory,
                VendorStatus = assignment.VendorStatus,
                ContractAmount = assignment.ContractAmount,
                AmountPaid = assignment.AmountPaid,
                RsvpStatus = assignment.RsvpStatus,
                PartySize = assignment.PartySize,
                TableNumber = assignment.TableNumber,
                DietaryRestrictions = assignment.DietaryRestrictions,
                MealChoice = assignment.MealChoice,
                IsNotifyRecipient = assignment.IsNotifyRecipient,
                AssignedAt = assignment.AssignedAt,
                User = assignment.User != null ? MapToUserSummaryDto(assignment.User) : null
            };
        }

        private MatterPermissionDto MapToMatterPermissionDto(MatterPermission permission)
        {
            return new MatterPermissionDto
            {
                Id = permission.Id,
                MatterId = permission.MatterId,
                UserId = permission.UserId,
                GrantedAt = permission.GrantedAt,
                GrantedById = permission.GrantedById,
                RevokedAt = permission.RevokedAt,
                User = permission.User != null ? MapToUserSummaryDto(permission.User) : null
            };
        }

        private UserSummaryDto MapToUserSummaryDto(User user)
        {
            return new UserSummaryDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber
            };
        }
    }
}

