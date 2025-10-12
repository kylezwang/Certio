namespace Certio.Application.Interfaces
{
    /// <summary>
    /// Service for logging security and audit events
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// Logs a successful operation to the audit trail
        /// </summary>
        Task LogOperationAsync(
            int userId, 
            int organizationId, 
            string action, 
            string entityType, 
            int entityId, 
            string? ipAddress = null,
            string? userAgent = null,
            string? details = null);

        /// <summary>
        /// Logs an authorization failure for security monitoring
        /// </summary>
        Task LogAuthorizationFailureAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId, 
            string reason,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Logs a create operation
        /// </summary>
        Task LogCreateAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Logs an update operation with before/after values
        /// </summary>
        Task LogUpdateAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? changes = null,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Logs a delete operation
        /// </summary>
        Task LogDeleteAsync(
            int userId, 
            int organizationId, 
            string entityType, 
            int entityId,
            string? ipAddress = null,
            string? userAgent = null);
    }
}

