using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    public class ProjectFormViewModel
    {
        public int Id { get; set; }
        public int Step { get; set; } = 1;
        
        // Step 1: Basic Information
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = "";
        
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = "";
        
        public string Category { get; set; } = "";
        
        public string ProjectType { get; set; } = "";
        
        // Step 2: Project Settings
        public string Status { get; set; } = "";
        
        public string Priority { get; set; } = "";
        
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        
        // Step 3: Client & Team
        public int? ClientId { get; set; }
        
        public int? TeamId { get; set; }
        
        // Step 4: Review & Create
        public bool IsComplete { get; set; } = false;
        
        // Available options
        public List<string> Categories { get; set; } = new List<string>
        {
            "AI/ML",
            "Frontend", 
            "Backend",
            "Full Stack",
            "Mobile",
            "DevOps",
            "Data Science",
            "Compliance",
            "Legal Tech"
        };
        
        public List<string> ProjectTypes { get; set; } = new List<string>
        {
            "Contract Management",
            "Document Review",
            "Compliance Tracking",
            "Client Portal",
            "Legal Research",
            "Case Management",
            "Billing System",
            "Legal Analytics"
        };
        
        public List<string> Statuses { get; set; } = new List<string>
        {
            "Planning",
            "In Progress",
            "Review",
            "On Hold",
            "Completed"
        };
        
        public List<string> Priorities { get; set; } = new List<string>
        {
            "Low",
            "Medium",
            "High",
            "Critical"
        };
        
        // Available clients and teams for step 3
        public List<ClientOption> Clients { get; set; } = new List<ClientOption>();
        public List<TeamOption> Teams { get; set; } = new List<TeamOption>();
    }
    
    public class ClientOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Company { get; set; } = "";
        public string Email { get; set; } = "";
    }
    
    public class TeamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string TeamType { get; set; } = "";
    }
}
