using Certio.Domain.Organizations;

namespace Certio.Application.DTOs
{
    public class OrganizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int OwnerId { get; set; }
        public string OwnerFirstName { get; set; } = "";
        public string OwnerLastName { get; set; } = "";
        public OrganizationType Type { get; set; }
        public bool IsPersonal { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public string? Color { get; set; }
        public string? Logo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrganizationBasicInfoDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public OrganizationType Type { get; set; }
    }
}

