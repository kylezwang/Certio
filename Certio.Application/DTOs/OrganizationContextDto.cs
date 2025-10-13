namespace Certio.Application.DTOs
{
    /// <summary>
    /// Lightweight DTO for organization context operations
    /// Used by OrganizationContextService
    /// </summary>
    public class OrganizationContextDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string OrganizationType { get; set; } = "";
        public bool IsActive { get; set; }
    }
}

