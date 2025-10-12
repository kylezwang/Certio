namespace Certio.Domain.Exceptions
{
    /// <summary>
    /// Base exception for all domain-specific exceptions
    /// These exceptions represent business rule violations, not HTTP errors
    /// </summary>
    public abstract class DomainException : Exception
    {
        public string ErrorCode { get; }

        protected DomainException(string message, string errorCode) 
            : base(message)
        {
            ErrorCode = errorCode;
        }

        protected DomainException(string message, string errorCode, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Thrown when a requested resource is not found
    /// </summary>
    public class ResourceNotFoundException : DomainException
    {
        public string ResourceType { get; }
        public int ResourceId { get; }

        public ResourceNotFoundException(string resourceType, int resourceId) 
            : base($"{resourceType} with ID {resourceId} not found.", "RESOURCE_NOT_FOUND")
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    /// <summary>
    /// Thrown when user lacks permission for an operation
    /// </summary>
    public class UnauthorizedOperationException : DomainException
    {
        public int UserId { get; }
        public string Operation { get; }
        public string ResourceType { get; }

        public UnauthorizedOperationException(int userId, string operation, string resourceType, string reason = "") 
            : base($"User {userId} is not authorized to {operation} {resourceType}. {reason}".Trim(), "UNAUTHORIZED_OPERATION")
        {
            UserId = userId;
            Operation = operation;
            ResourceType = resourceType;
        }
    }

    /// <summary>
    /// Thrown when resource belongs to a different organization
    /// </summary>
    public class OrganizationMismatchException : DomainException
    {
        public int UserId { get; }
        public int OrganizationId { get; }
        public string ResourceType { get; }
        public int ResourceId { get; }

        public OrganizationMismatchException(int userId, int organizationId, string resourceType, int resourceId) 
            : base($"{resourceType} {resourceId} does not belong to organization {organizationId}.", "ORGANIZATION_MISMATCH")
        {
            UserId = userId;
            OrganizationId = organizationId;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    /// <summary>
    /// Thrown when business rule validation fails
    /// </summary>
    public class BusinessRuleViolationException : DomainException
    {
        public string RuleName { get; }

        public BusinessRuleViolationException(string ruleName, string message) 
            : base(message, "BUSINESS_RULE_VIOLATION")
        {
            RuleName = ruleName;
        }
    }

    /// <summary>
    /// Thrown when an invalid status transition is attempted
    /// </summary>
    public class InvalidStatusTransitionException : DomainException
    {
        public string FromStatus { get; }
        public string ToStatus { get; }
        public string ResourceType { get; }

        public InvalidStatusTransitionException(string resourceType, string fromStatus, string toStatus) 
            : base($"Cannot transition {resourceType} from '{fromStatus}' to '{toStatus}'.", "INVALID_STATUS_TRANSITION")
        {
            FromStatus = fromStatus;
            ToStatus = toStatus;
            ResourceType = resourceType;
        }
    }

    /// <summary>
    /// Thrown when required validation fails
    /// </summary>
    public class ValidationException : DomainException
    {
        public Dictionary<string, string[]> Errors { get; }

        public ValidationException(Dictionary<string, string[]> errors) 
            : base("One or more validation errors occurred.", "VALIDATION_ERROR")
        {
            Errors = errors;
        }

        public ValidationException(string field, string error) 
            : base($"Validation error: {error}", "VALIDATION_ERROR")
        {
            Errors = new Dictionary<string, string[]>
            {
                [field] = new[] { error }
            };
        }
    }
}

