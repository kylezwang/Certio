using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Domain.Exceptions;
using Certio.Domain.Tasks;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services
{
    public class SubTaskService : ISubTaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly IAuditService _auditService;
        private readonly ILogger<SubTaskService> _logger;

        public SubTaskService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            IAuditService auditService,
            ILogger<SubTaskService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<ServiceResult<SubTaskDto>> CreateSubTaskAsync(
            int userId, 
            int taskId, 
            CreateSubTaskDto createDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // Verify parent task exists and user has access
                var task = await _context.TaskItems
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check task access (which validates matter access)
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "create", "SubTask", "No access to parent task");
                }

                // Validate DTO
                ValidateCreateSubTaskDto(createDto);

                var subTask = new SubTaskItem
                {
                    TaskId = taskId,
                    MatterId = task.MatterId,
                    OrgId = task.OrgId,
                    Title = createDto.Title,
                    DueDate = createDto.DueDate,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SubTaskItems.Add(subTask);
                await _context.SaveChangesAsync();

                // Handle assignments
                if (createDto.AssignedUserIds != null && createDto.AssignedUserIds.Any())
                {
                    foreach (var assigneeId in createDto.AssignedUserIds)
                    {
                        var assignment = new SubTaskAssignment
                        {
                            SubTaskItemId = subTask.Id,
                            UserId = assigneeId,
                            AssignmentType = "Assignee",
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.SubTaskAssignments.Add(assignment);
                    }
                    await _context.SaveChangesAsync();
                }

                // Audit log
                await _auditService.LogCreateAsync(userId, task.OrgId, "SubTask", subTask.Id, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} created subtask {SubTaskId} in task {TaskId}", userId, subTask.Id, taskId);

                // Reload with includes
                subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                        .ThenInclude(a => a.User)
                    .FirstAsync(st => st.Id == subTask.Id);

                return ServiceResult<SubTaskDto>.SuccessResult(MapToSubTaskDto(subTask));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception creating subtask for user {UserId} in task {TaskId}", userId, taskId);
                return ServiceResult<SubTaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subtask for user {UserId} in task {TaskId}", userId, taskId);
                return ServiceResult<SubTaskDto>.FailureResult("An error occurred while creating the subtask", "ERROR");
            }
        }

        public async Task<ServiceResult<SubTaskDto>> UpdateSubTaskAsync(
            int userId, 
            int subTaskId, 
            UpdateSubTaskDto updateDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access via parent task
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "update", "SubTask", "No access to subtask");
                }

                // Apply updates
                if (updateDto.Title != null) subTask.Title = updateDto.Title;
                if (updateDto.DueDate.HasValue) subTask.DueDate = updateDto.DueDate;
                if (updateDto.IsCompleted.HasValue)
                {
                    subTask.IsCompleted = updateDto.IsCompleted.Value;
                    if (updateDto.IsCompleted.Value && !subTask.CompletedAt.HasValue)
                    {
                        subTask.CompletedAt = DateTime.UtcNow;
                    }
                    else if (!updateDto.IsCompleted.Value)
                    {
                        subTask.CompletedAt = null;
                    }
                }

                subTask.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogUpdateAsync(userId, subTask.OrgId, "SubTask", subTask.Id, null, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} updated subtask {SubTaskId}", userId, subTaskId);

                return ServiceResult<SubTaskDto>.SuccessResult(MapToSubTaskDto(subTask));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception updating subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult<SubTaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult<SubTaskDto>.FailureResult("An error occurred while updating the subtask", "ERROR");
            }
        }

        public async Task<ServiceResult> DeleteSubTaskAsync(
            int userId, 
            int subTaskId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "delete", "SubTask", "No access to subtask");
                }

                _context.SubTaskItems.Remove(subTask);
                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogDeleteAsync(userId, subTask.OrgId, "SubTask", subTask.Id, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} deleted subtask {SubTaskId}", userId, subTaskId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception deleting subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult.FailureResult("An error occurred while deleting the subtask", "ERROR");
            }
        }

        public async Task<ServiceResult<SubTaskDto>> GetSubTaskAsync(int userId, int subTaskId)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "view", "SubTask", "No access to subtask");
                }

                return ServiceResult<SubTaskDto>.SuccessResult(MapToSubTaskDto(subTask));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult<SubTaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subtask {SubTaskId} for user {UserId}", subTaskId, userId);
                return ServiceResult<SubTaskDto>.FailureResult("An error occurred while retrieving the subtask", "ERROR");
            }
        }

        public async Task<ServiceResult<List<SubTaskDto>>> ListSubTasksAsync(int userId, int taskId)
        {
            try
            {
                // Check task access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "list", "SubTask", "No access to parent task");
                }

                var subTasks = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                        .ThenInclude(a => a.User)
                    .Where(st => st.TaskId == taskId)
                    .OrderBy(st => st.CreatedAt)
                    .ToListAsync();

                var subTaskDtos = subTasks.Select(MapToSubTaskDto).ToList();

                return ServiceResult<List<SubTaskDto>>.SuccessResult(subTaskDtos);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception listing subtasks for task {TaskId}", taskId);
                return ServiceResult<List<SubTaskDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing subtasks for task {TaskId}", taskId);
                return ServiceResult<List<SubTaskDto>>.FailureResult("An error occurred while listing subtasks", "ERROR");
            }
        }

        public async Task<ServiceResult<SubTaskDto>> ToggleSubTaskCompletionAsync(
            int userId, 
            int subTaskId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "toggle", "SubTask", "No access to subtask");
                }

                subTask.IsCompleted = !subTask.IsCompleted;
                subTask.CompletedAt = subTask.IsCompleted ? DateTime.UtcNow : null;
                subTask.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, subTask.OrgId, "TOGGLE", "SubTask", subTaskId, 
                    ipAddress, userAgent, $"Toggled completion to {subTask.IsCompleted}");

                _logger.LogInformation("User {UserId} toggled subtask {SubTaskId} completion to {IsCompleted}", 
                    userId, subTaskId, subTask.IsCompleted);

                return ServiceResult<SubTaskDto>.SuccessResult(MapToSubTaskDto(subTask));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception toggling subtask {SubTaskId} completion", subTaskId);
                return ServiceResult<SubTaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling subtask {SubTaskId} completion", subTaskId);
                return ServiceResult<SubTaskDto>.FailureResult("An error occurred while toggling subtask completion", "ERROR");
            }
        }

        public async Task<ServiceResult<SubTaskAssignmentDto>> AssignSubTaskAsync(
            int userId, 
            int subTaskId, 
            AssignSubTaskDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "assign", "SubTask", "No access to subtask");
                }

                // Validate assignee has access to the organization
                if (!await _permissionService.IsOrganizationMemberAsync(assignmentDto.UserId, subTask.OrgId))
                {
                    throw new BusinessRuleViolationException("UserMembership", "User must be a member of the organization");
                }

                // Check if already assigned
                var existingAssignment = subTask.Assignments
                    .FirstOrDefault(a => a.UserId == assignmentDto.UserId && a.RemovedAt == null);

                if (existingAssignment != null)
                {
                    throw new BusinessRuleViolationException("DuplicateAssignment", "User is already assigned to this subtask");
                }

                var assignment = new SubTaskAssignment
                {
                    SubTaskItemId = subTaskId,
                    UserId = assignmentDto.UserId,
                    AssignmentType = assignmentDto.AssignmentType,
                    Role = assignmentDto.Role,
                    IsNotifyRecipient = assignmentDto.IsNotifyRecipient,
                    AssignedAt = DateTime.UtcNow
                };

                _context.SubTaskAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                // Reload to get user info
                await _context.Entry(assignment).Reference(a => a.User).LoadAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, subTask.OrgId, "ASSIGN", "SubTask", subTaskId, 
                    ipAddress, userAgent, $"Assigned user {assignmentDto.UserId}");

                _logger.LogInformation("User {UserId} assigned user {AssigneeId} to subtask {SubTaskId}", 
                    userId, assignmentDto.UserId, subTaskId);

                return ServiceResult<SubTaskAssignmentDto>.SuccessResult(MapToSubTaskAssignmentDto(assignment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception assigning user to subtask {SubTaskId}", subTaskId);
                return ServiceResult<SubTaskAssignmentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning user to subtask {SubTaskId}", subTaskId);
                return ServiceResult<SubTaskAssignmentDto>.FailureResult("An error occurred while assigning user to subtask", "ERROR");
            }
        }

        public async Task<ServiceResult> RemoveSubTaskAssignmentAsync(
            int userId, 
            int subTaskId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var subTask = await _context.SubTaskItems
                    .Include(st => st.Assignments)
                    .FirstOrDefaultAsync(st => st.Id == subTaskId);

                if (subTask == null)
                {
                    throw new ResourceNotFoundException("SubTask", subTaskId);
                }

                // Check access
                if (!await _permissionService.CanAccessSubTaskAsync(userId, subTaskId))
                {
                    throw new UnauthorizedOperationException(userId, "unassign", "SubTask", "No access to subtask");
                }

                var assignment = subTask.Assignments
                    .FirstOrDefault(a => a.UserId == assigneeId && a.RemovedAt == null);

                if (assignment == null)
                {
                    throw new ResourceNotFoundException("SubTaskAssignment", assigneeId);
                }

                assignment.RemovedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, subTask.OrgId, "UNASSIGN", "SubTask", subTaskId, 
                    ipAddress, userAgent, $"Removed user {assigneeId}");

                _logger.LogInformation("User {UserId} removed user {AssigneeId} from subtask {SubTaskId}", 
                    userId, assigneeId, subTaskId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception removing user from subtask {SubTaskId}", subTaskId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from subtask {SubTaskId}", subTaskId);
                return ServiceResult.FailureResult("An error occurred while removing user from subtask", "ERROR");
            }
        }

        // Helper methods
        private void ValidateCreateSubTaskDto(CreateSubTaskDto dto)
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

            if (errors.Any())
            {
                throw new ValidationException(errors);
            }
        }

        private SubTaskDto MapToSubTaskDto(SubTaskItem subTask)
        {
            return new SubTaskDto
            {
                Id = subTask.Id,
                TaskId = subTask.TaskId,
                MatterId = subTask.MatterId,
                OrgId = subTask.OrgId,
                Title = subTask.Title,
                CreatedAt = subTask.CreatedAt,
                LastModifiedAt = subTask.LastModifiedAt,
                DueDate = subTask.DueDate,
                IsCompleted = subTask.IsCompleted,
                CompletedAt = subTask.CompletedAt,
                Assignments = subTask.Assignments.Select(MapToSubTaskAssignmentDto).ToList()
            };
        }

        private SubTaskAssignmentDto MapToSubTaskAssignmentDto(SubTaskAssignment assignment)
        {
            return new SubTaskAssignmentDto
            {
                Id = assignment.Id,
                SubTaskItemId = assignment.SubTaskItemId,
                UserId = assignment.UserId,
                AssignmentType = assignment.AssignmentType,
                Role = assignment.Role,
                IsNotifyRecipient = assignment.IsNotifyRecipient,
                AssignedAt = assignment.AssignedAt,
                User = assignment.User != null ? MapToUserSummaryDto(assignment.User) : null
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

