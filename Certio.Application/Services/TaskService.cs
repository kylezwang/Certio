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
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly IAuditService _auditService;
        private readonly ILogger<TaskService> _logger;

        public TaskService(
            ApplicationDbContext context,
            IPermissionService permissionService,
            IAuditService auditService,
            ILogger<TaskService> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<ServiceResult<TaskDto>> CreateTaskAsync(
            int userId, 
            int matterId, 
            CreateTaskDto createDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                // Verify matter exists and user has access
                var matter = await _context.Matters
                    .FirstOrDefaultAsync(m => m.Id == matterId);

                if (matter == null)
                {
                    throw new ResourceNotFoundException("Matter", matterId);
                }

                // Check matter access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "create", "Task", "No access to parent matter");
                }

                // Validate DTO
                ValidateCreateTaskDto(createDto);

                var task = new TaskItem
                {
                    OrgId = matter.OrganizationId,
                    MatterId = matterId,
                    Title = createDto.Title,
                    Description = createDto.Description,
                    Status = createDto.Status,
                    Priority = createDto.Priority,
                    Location = createDto.Location,
                    DueDate = createDto.DueDate,
                    Order = createDto.Order,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TaskItems.Add(task);
                await _context.SaveChangesAsync();

                // Handle assignments
                if (createDto.AssignedUserIds != null && createDto.AssignedUserIds.Any())
                {
                    foreach (var assigneeId in createDto.AssignedUserIds)
                    {
                        var assignment = new TaskAssignment
                        {
                            TaskItemId = task.Id,
                            UserId = assigneeId,
                            AssignmentType = "Assignee",
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.TaskAssignments.Add(assignment);
                    }
                    await _context.SaveChangesAsync();
                }

                // Audit log
                await _auditService.LogCreateAsync(userId, matter.OrganizationId, "Task", task.Id, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} created task {TaskId} in matter {MatterId}", userId, task.Id, matterId);

                // Reload with includes
                task = await _context.TaskItems
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .Include(t => t.Matter)
                    .FirstAsync(t => t.Id == task.Id);

                return ServiceResult<TaskDto>.SuccessResult(MapToTaskDto(task));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception creating task for user {UserId} in matter {MatterId}", userId, matterId);
                return ServiceResult<TaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task for user {UserId} in matter {MatterId}", userId, matterId);
                return ServiceResult<TaskDto>.FailureResult("An error occurred while creating the task", "ERROR");
            }
        }

        public async Task<ServiceResult<TaskDto>> UpdateTaskAsync(
            int userId, 
            int taskId, 
            UpdateTaskDto updateDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "update", "Task", "No access to task");
                }

                // Apply updates
                if (updateDto.Title != null) task.Title = updateDto.Title;
                if (updateDto.Description != null) task.Description = updateDto.Description;
                if (updateDto.Status != null) task.Status = updateDto.Status;
                if (updateDto.Priority != null) task.Priority = updateDto.Priority;
                if (updateDto.Location != null) task.Location = updateDto.Location;
                if (updateDto.DueDate.HasValue) task.DueDate = updateDto.DueDate;
                if (updateDto.Order.HasValue) task.Order = updateDto.Order.Value;
                if (updateDto.CompletedAt.HasValue) task.CompletedAt = updateDto.CompletedAt;

                task.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogUpdateAsync(userId, task.OrgId, "Task", task.Id, null, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} updated task {TaskId}", userId, taskId);

                return ServiceResult<TaskDto>.SuccessResult(MapToTaskDto(task));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception updating task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult<TaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult<TaskDto>.FailureResult("An error occurred while updating the task", "ERROR");
            }
        }

        public async Task<ServiceResult> DeleteTaskAsync(
            int userId, 
            int taskId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "delete", "Task", "No access to task");
                }

                _context.TaskItems.Remove(task);
                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogDeleteAsync(userId, task.OrgId, "Task", task.Id, ipAddress, userAgent);

                _logger.LogInformation("User {UserId} deleted task {TaskId}", userId, taskId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception deleting task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult.FailureResult("An error occurred while deleting the task", "ERROR");
            }
        }

        public async Task<ServiceResult<TaskDto>> GetTaskAsync(int userId, int taskId)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .Include(t => t.Comments)
                        .ThenInclude(c => c.User)
                    .Include(t => t.Comments)
                        .ThenInclude(c => c.Mentions)
                            .ThenInclude(m => m.MentionedUser)
                    .Include(t => t.Comments)
                        .ThenInclude(c => c.Reactions)
                            .ThenInclude(r => r.User)
                    .Include(t => t.SubTasks)
                        .ThenInclude(st => st.Assignments)
                            .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "view", "Task", "No access to task");
                }

                return ServiceResult<TaskDto>.SuccessResult(MapToTaskDto(task));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult<TaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task {TaskId} for user {UserId}", taskId, userId);
                return ServiceResult<TaskDto>.FailureResult("An error occurred while retrieving the task", "ERROR");
            }
        }

        public async Task<ServiceResult<List<TaskDto>>> ListTasksForMatterAsync(int userId, int matterId)
        {
            try
            {
                // Check matter access
                if (!await _permissionService.CanAccessMatterAsync(userId, matterId))
                {
                    throw new UnauthorizedOperationException(userId, "list", "Task", "No access to parent matter");
                }

                var tasks = await _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .Include(t => t.Comments)
                    .Include(t => t.SubTasks)
                    .Where(t => t.MatterId == matterId)
                    .OrderBy(t => t.Order)
                    .ThenBy(t => t.CreatedAt)
                    .ToListAsync();

                var taskDtos = tasks.Select(MapToTaskDto).ToList();

                return ServiceResult<List<TaskDto>>.SuccessResult(taskDtos);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception listing tasks for matter {MatterId}", matterId);
                return ServiceResult<List<TaskDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tasks for matter {MatterId}", matterId);
                return ServiceResult<List<TaskDto>>.FailureResult("An error occurred while listing tasks", "ERROR");
            }
        }

        public async Task<ServiceResult<List<TaskDto>>> ListTasksAsync(
            int userId, 
            int organizationId, 
            TaskFilterDto? filter = null)
        {
            try
            {
                // Validate user is in organization
                if (!await _permissionService.IsOrganizationMemberAsync(userId, organizationId))
                {
                    if (!await _permissionService.HasFirmBasedAccessAsync(userId, organizationId))
                    {
                        throw new UnauthorizedOperationException(userId, "list", "Task", "Not a member of organization");
                    }
                }

                var query = _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .Include(t => t.Comments)
                    .Include(t => t.SubTasks)
                    .Where(t => t.OrgId == organizationId);

                // Apply matter-level access filtering
                query = query.Where(t => 
                    t.Matter.AccessLevel == "Everyone" || 
                    t.Matter.Permissions.Any(p => p.UserId == userId && p.RevokedAt == null) ||
                    t.Matter.Assignments.Any(a => a.UserId == userId && a.RemovedAt == null) ||
                    t.TaskAssignments.Any(a => a.UserId == userId && a.RemovedAt == null));

                // Apply filters
                if (filter != null)
                {
                    if (filter.MatterId.HasValue)
                    {
                        query = query.Where(t => t.MatterId == filter.MatterId.Value);
                    }

                    if (!string.IsNullOrEmpty(filter.Status))
                    {
                        query = query.Where(t => t.Status == filter.Status);
                    }

                    if (!string.IsNullOrEmpty(filter.Priority))
                    {
                        query = query.Where(t => t.Priority == filter.Priority);
                    }

                    if (filter.AssignedUserId.HasValue)
                    {
                        query = query.Where(t => t.TaskAssignments.Any(a => 
                            a.UserId == filter.AssignedUserId.Value && 
                            a.RemovedAt == null));
                    }

                    if (filter.DueDateFrom.HasValue)
                    {
                        query = query.Where(t => t.DueDate >= filter.DueDateFrom.Value);
                    }

                    if (filter.DueDateTo.HasValue)
                    {
                        query = query.Where(t => t.DueDate <= filter.DueDateTo.Value);
                    }

                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        var searchTerm = filter.SearchTerm.ToLower();
                        query = query.Where(t => 
                            t.Title.ToLower().Contains(searchTerm) ||
                            (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
                    }
                }

                var tasks = await query.ToListAsync();
                var taskDtos = tasks.Select(MapToTaskDto).ToList();

                return ServiceResult<List<TaskDto>>.SuccessResult(taskDtos);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception listing tasks for org {OrgId}", organizationId);
                return ServiceResult<List<TaskDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tasks for org {OrgId}", organizationId);
                return ServiceResult<List<TaskDto>>.FailureResult("An error occurred while listing tasks", "ERROR");
            }
        }

        public async Task<ServiceResult<TaskAssignmentDto>> AssignTaskAsync(
            int userId, 
            int taskId, 
            AssignTaskDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.TaskAssignments)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "assign", "Task", "No access to task");
                }

                // Validate assignee has access to the task's organization
                if (!await _permissionService.IsOrganizationMemberAsync(assignmentDto.UserId, task.OrgId))
                {
                    throw new BusinessRuleViolationException("UserMembership", "User must be a member of the organization");
                }

                // Check if already assigned
                var existingAssignment = task.TaskAssignments
                    .FirstOrDefault(a => a.UserId == assignmentDto.UserId && a.RemovedAt == null);

                if (existingAssignment != null)
                {
                    throw new BusinessRuleViolationException("DuplicateAssignment", "User is already assigned to this task");
                }

                var assignment = new TaskAssignment
                {
                    TaskItemId = taskId,
                    UserId = assignmentDto.UserId,
                    AssignmentType = assignmentDto.AssignmentType,
                    Role = assignmentDto.Role,
                    IsNotifyRecipient = assignmentDto.IsNotifyRecipient,
                    AssignedAt = DateTime.UtcNow
                };

                _context.TaskAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                // Reload to get user info
                await _context.Entry(assignment).Reference(a => a.User).LoadAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, task.OrgId, "ASSIGN", "Task", taskId, 
                    ipAddress, userAgent, $"Assigned user {assignmentDto.UserId}");

                _logger.LogInformation("User {UserId} assigned user {AssigneeId} to task {TaskId}", 
                    userId, assignmentDto.UserId, taskId);

                return ServiceResult<TaskAssignmentDto>.SuccessResult(MapToTaskAssignmentDto(assignment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception assigning user to task {TaskId}", taskId);
                return ServiceResult<TaskAssignmentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning user to task {TaskId}", taskId);
                return ServiceResult<TaskAssignmentDto>.FailureResult("An error occurred while assigning user to task", "ERROR");
            }
        }

        public async Task<ServiceResult> RemoveTaskAssignmentAsync(
            int userId, 
            int taskId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.TaskAssignments)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "unassign", "Task", "No access to task");
                }

                var assignment = task.TaskAssignments
                    .FirstOrDefault(a => a.UserId == assigneeId && a.RemovedAt == null);

                if (assignment == null)
                {
                    throw new ResourceNotFoundException("TaskAssignment", assigneeId);
                }

                assignment.RemovedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, task.OrgId, "UNASSIGN", "Task", taskId, 
                    ipAddress, userAgent, $"Removed user {assigneeId}");

                _logger.LogInformation("User {UserId} removed user {AssigneeId} from task {TaskId}", 
                    userId, assigneeId, taskId);

                return ServiceResult.SuccessResult();
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception removing user from task {TaskId}", taskId);
                return ServiceResult.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user from task {TaskId}", taskId);
                return ServiceResult.FailureResult("An error occurred while removing user from task", "ERROR");
            }
        }

        public async Task<ServiceResult<TaskDto>> CompleteTaskAsync(
            int userId, 
            int taskId,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .Include(t => t.Matter)
                    .Include(t => t.TaskAssignments)
                        .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "complete", "Task", "No access to task");
                }

                task.Status = "Completed";
                task.CompletedAt = DateTime.UtcNow;
                task.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Audit log
                await _auditService.LogOperationAsync(userId, task.OrgId, "COMPLETE", "Task", taskId, 
                    ipAddress, userAgent, "Marked task as completed");

                _logger.LogInformation("User {UserId} completed task {TaskId}", userId, taskId);

                return ServiceResult<TaskDto>.SuccessResult(MapToTaskDto(task));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception completing task {TaskId}", taskId);
                return ServiceResult<TaskDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task {TaskId}", taskId);
                return ServiceResult<TaskDto>.FailureResult("An error occurred while completing the task", "ERROR");
            }
        }

        public async Task<ServiceResult<TaskCommentDto>> AddTaskCommentAsync(
            int userId, 
            int taskId, 
            AddTaskCommentDto commentDto,
            string? ipAddress = null,
            string? userAgent = null)
        {
            try
            {
                var task = await _context.TaskItems
                    .FirstOrDefaultAsync(t => t.Id == taskId);

                if (task == null)
                {
                    throw new ResourceNotFoundException("Task", taskId);
                }

                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "comment", "Task", "No access to task");
                }

                var comment = new TaskItemComment
                {
                    TaskItemId = taskId,
                    UserId = userId,
                    Content = commentDto.Content,
                    ParentCommentId = commentDto.ParentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TaskItemComments.Add(comment);
                await _context.SaveChangesAsync();

                // Handle mentions
                if (commentDto.MentionedUserIds != null && commentDto.MentionedUserIds.Any())
                {
                    foreach (var mentionedUserId in commentDto.MentionedUserIds)
                    {
                        var mention = new TaskCommentMention
                        {
                            CommentId = comment.Id,
                            MentionedUserId = mentionedUserId
                        };
                        _context.TaskCommentMentions.Add(mention);
                    }
                    await _context.SaveChangesAsync();
                }

                // Reload with includes
                comment = await _context.TaskItemComments
                    .Include(c => c.User)
                    .Include(c => c.Mentions)
                        .ThenInclude(m => m.MentionedUser)
                    .FirstAsync(c => c.Id == comment.Id);

                // Audit log
                await _auditService.LogOperationAsync(userId, task.OrgId, "COMMENT", "Task", taskId, 
                    ipAddress, userAgent, "Added comment");

                _logger.LogInformation("User {UserId} added comment to task {TaskId}", userId, taskId);

                return ServiceResult<TaskCommentDto>.SuccessResult(MapToTaskCommentDto(comment));
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception adding comment to task {TaskId}", taskId);
                return ServiceResult<TaskCommentDto>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to task {TaskId}", taskId);
                return ServiceResult<TaskCommentDto>.FailureResult("An error occurred while adding comment", "ERROR");
            }
        }

        public async Task<ServiceResult<List<TaskCommentDto>>> GetTaskCommentsAsync(int userId, int taskId)
        {
            try
            {
                // Check access
                if (!await _permissionService.CanAccessTaskAsync(userId, taskId))
                {
                    throw new UnauthorizedOperationException(userId, "view comments", "Task", "No access to task");
                }

            var comments = await _context.TaskItemComments
                .Include(c => c.User)
                .Include(c => c.Mentions)
                    .ThenInclude(m => m.MentionedUser)
                .Include(c => c.Reactions)
                    .ThenInclude(r => r.User)
                .Where(c => c.TaskItemId == taskId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

                var commentDtos = comments.Select(MapToTaskCommentDto).ToList();

                return ServiceResult<List<TaskCommentDto>>.SuccessResult(commentDtos);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Domain exception getting comments for task {TaskId}", taskId);
                return ServiceResult<List<TaskCommentDto>>.FailureResult(ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments for task {TaskId}", taskId);
                return ServiceResult<List<TaskCommentDto>>.FailureResult("An error occurred while retrieving comments", "ERROR");
            }
        }

        // Helper methods
        private void ValidateCreateTaskDto(CreateTaskDto dto)
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

        private TaskDto MapToTaskDto(TaskItem task)
        {
            return new TaskDto
            {
                Id = task.Id,
                OrgId = task.OrgId,
                MatterId = task.MatterId,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Priority = task.Priority,
                Location = task.Location,
                Order = task.Order,
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                LastModifiedAt = task.LastModifiedAt,
                DueDate = task.DueDate,
                CompletedAt = task.CompletedAt,
                Assignments = task.TaskAssignments.Select(MapToTaskAssignmentDto).ToList(),
                Comments = task.Comments.Select(MapToTaskCommentDto).ToList(),
                SubTasks = task.SubTasks.Select(MapToSubTaskDto).ToList(),
                MatterTitle = task.Matter?.Title
            };
        }

        private TaskAssignmentDto MapToTaskAssignmentDto(TaskAssignment assignment)
        {
            return new TaskAssignmentDto
            {
                Id = assignment.Id,
                TaskItemId = assignment.TaskItemId,
                UserId = assignment.UserId,
                AssignmentType = assignment.AssignmentType,
                Role = assignment.Role,
                IsNotifyRecipient = assignment.IsNotifyRecipient,
                AssignedAt = assignment.AssignedAt,
                CompletedAt = assignment.CompletedAt,
                User = assignment.User != null ? MapToUserSummaryDto(assignment.User) : null
            };
        }

        private TaskCommentDto MapToTaskCommentDto(TaskItemComment comment)
        {
            return new TaskCommentDto
            {
                Id = comment.Id,
                TaskItemId = comment.TaskItemId,
                UserId = comment.UserId,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt,
                LastModifiedAt = comment.LastModifiedAt,
                User = comment.User != null ? MapToUserSummaryDto(comment.User) : null,
                Mentions = comment.Mentions.Select(m => new CommentMentionDto
                {
                    Id = m.Id,
                    UserId = m.MentionedUserId,
                    User = m.MentionedUser != null ? MapToUserSummaryDto(m.MentionedUser) : null
                }).ToList(),
                Reactions = comment.Reactions.Select(r => new CommentReactionDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Emoji = r.ReactionType,
                    User = r.User != null ? MapToUserSummaryDto(r.User) : null
                }).ToList()
            };
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
                Assignments = subTask.Assignments.Select(a => new SubTaskAssignmentDto
                {
                    Id = a.Id,
                    SubTaskItemId = a.SubTaskItemId,
                    UserId = a.UserId,
                    AssignmentType = a.AssignmentType,
                    Role = a.Role,
                    IsNotifyRecipient = a.IsNotifyRecipient,
                    AssignedAt = a.AssignedAt,
                    User = a.User != null ? MapToUserSummaryDto(a.User) : null
                }).ToList()
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

