using Certio.Application.DTOs;

namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service interface for all subtask operations
    /// Inherits access control from parent task
    /// </summary>
    public interface ISubTaskService
    {
        /// <summary>
        /// Creates a new subtask within a task
        /// </summary>
        Task<ServiceResult<SubTaskDto>> CreateSubTaskAsync(
            int userId, 
            int taskId, 
            CreateSubTaskDto createDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Updates an existing subtask
        /// </summary>
        Task<ServiceResult<SubTaskDto>> UpdateSubTaskAsync(
            int userId, 
            int subTaskId, 
            UpdateSubTaskDto updateDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Deletes a subtask
        /// </summary>
        Task<ServiceResult> DeleteSubTaskAsync(
            int userId, 
            int subTaskId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets a subtask by ID with permission validation
        /// </summary>
        Task<ServiceResult<SubTaskDto>> GetSubTaskAsync(
            int userId, 
            int subTaskId);

        /// <summary>
        /// Lists all subtasks for a task
        /// </summary>
        Task<ServiceResult<List<SubTaskDto>>> ListSubTasksAsync(
            int userId, 
            int taskId);

        /// <summary>
        /// Toggles subtask completion status
        /// </summary>
        Task<ServiceResult<SubTaskDto>> ToggleSubTaskCompletionAsync(
            int userId, 
            int subTaskId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Assigns a subtask to a user
        /// </summary>
        Task<ServiceResult<SubTaskAssignmentDto>> AssignSubTaskAsync(
            int userId, 
            int subTaskId, 
            AssignSubTaskDto assignmentDto,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Removes a user assignment from a subtask
        /// </summary>
        Task<ServiceResult> RemoveSubTaskAssignmentAsync(
            int userId, 
            int subTaskId, 
            int assigneeId,
            string? ipAddress = null,
            string? userAgent = null);
    }
}

