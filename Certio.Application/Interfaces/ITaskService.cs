using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service interface for all task operations
    /// Enforces matter-level access and organization isolation
    /// </summary>
    public interface ITaskService
    {
        /// <summary>
        /// Creates a new task within a matter
        /// </summary>
        Task<ServiceResult<TaskDto>> CreateTaskAsync(
            int userId, 
            int matterId, 
            CreateTaskDto createDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Updates an existing task
        /// </summary>
        Task<ServiceResult<TaskDto>> UpdateTaskAsync(
            int userId, 
            int taskId, 
            UpdateTaskDto updateDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Deletes a task
        /// </summary>
        Task<ServiceResult> DeleteTaskAsync(
            int userId, 
            int taskId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets a task by ID with permission validation
        /// </summary>
        Task<ServiceResult<TaskDto>> GetTaskAsync(
            int userId, 
            int taskId);

        /// <summary>
        /// Lists all tasks for a matter
        /// </summary>
        Task<ServiceResult<List<TaskDto>>> ListTasksForMatterAsync(
            int userId, 
            int matterId);

        /// <summary>
        /// Lists all tasks accessible to user in an organization
        /// </summary>
        Task<ServiceResult<List<TaskDto>>> ListTasksAsync(
            int userId, 
            int organizationId, 
            TaskFilterDto? filter = null);

        /// <summary>
        /// Assigns a task to a user
        /// </summary>
        Task<ServiceResult<TaskAssignmentDto>> AssignTaskAsync(
            int userId, 
            int taskId, 
            AssignTaskDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Removes a user assignment from a task
        /// </summary>
        Task<ServiceResult> RemoveTaskAssignmentAsync(
            int userId, 
            int taskId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Marks a task as complete
        /// </summary>
        Task<ServiceResult<TaskDto>> CompleteTaskAsync(
            int userId, 
            int taskId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Adds a comment to a task with optional mentions
        /// </summary>
        Task<ServiceResult<TaskCommentDto>> AddTaskCommentAsync(
            int userId, 
            int taskId, 
            AddTaskCommentDto commentDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets all comments for a task
        /// </summary>
        Task<ServiceResult<List<TaskCommentDto>>> GetTaskCommentsAsync(
            int userId, 
            int taskId);
    }
}

