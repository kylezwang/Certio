using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    public class MatterFormViewModel
    {
        public int Id { get; set; }
        public int Step { get; set; } = 1;
        
        // Step 1: Basic Information
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = "";
        
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = "";
        
        [StringLength(100, ErrorMessage = "Practice Area cannot exceed 100 characters")]
        public string PracticeArea { get; set; } = "";
        
        // Step 2: Matter Settings
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
        public string Status { get; set; } = "";
        
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? StatuteOfLimitationsDate { get; set; }
        
        // Step 3: Firm Assignments
        public List<FirmAssignmentViewModel> FirmAssignments { get; set; } = new List<FirmAssignmentViewModel>();
        
        // Step 3: Relevant Contacts
        public List<RelevantContactViewModel> RelevantContacts { get; set; } = new List<RelevantContactViewModel>();
        
        // Step 4: Review & Create
        public bool IsComplete { get; set; } = false;
        [StringLength(20, ErrorMessage = "Access Level cannot exceed 20 characters")]
        public string AccessLevel { get; set; } = "";
        
        // When AccessLevel == "Specific", the selected user ids to grant permissions
        public List<int> PermissionUserIds { get; set; } = new List<int>();
        
        // Available options
        public List<string> PracticeAreas { get; set; } = new List<string>
        {
            "Business Formation / Compliance",
            "Commercial Litigation",
            "Construction",
            "Corporate Litigation",
            "Criminal",
            "Elder",
            "Employment / Labor",
            "Family",
            "Healthcare",
            "Intellectual Property",
            "Medical Malpractice",
            "Personal Injury",
            "Privacy / Information Security",
            "Product Liability",
            "Real Estate",
            "Securities / Mergers & Acquisitions",
            "Sports / Entertainment / Gaming",
            "Tax",
            "Trusts",
            "Wills & Estates",
            "Other"
        };
        
        public List<string> Statuses { get; set; } = new List<string>
        {
            "Planning",
            "In Progress",
            "Review",
            "On Hold",
            "Completed"
        };
        
        // Available org members for step 3
        public List<OrgMemberOption> OrgMembers { get; set; } = new List<OrgMemberOption>();
    }
    
    public class FirmAssignmentViewModel
    {
        public int? UserId { get; set; }
        [StringLength(20, ErrorMessage = "Assignment Type cannot exceed 20 characters")]
        public string AssignmentType { get; set; } = ""; // OriginatingAttorney, ResponsibleAttorney, ResponsibleStaff
        [StringLength(100, ErrorMessage = "Role cannot exceed 100 characters")]
        public string Role { get; set; } = "";
        public bool IsNotifyRecipient { get; set; } = true;
    }
    
    public class RelevantContactViewModel
    {
        public int? UserId { get; set; }
        [StringLength(100, ErrorMessage = "Involvement cannot exceed 100 characters")]
        public string Involvement { get; set; } = "";
        public bool IsNotifyRecipient { get; set; } = true;
    }
    
    public class OrgMemberOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Company { get; set; } = "";
    }
}
