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
        
        [StringLength(100, ErrorMessage = "Matter Type cannot exceed 100 characters")]
        public string MatterType { get; set; } = "";
        
        // Step 2: Matter Settings
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters")]
        public string Status { get; set; } = "";
        
        [StringLength(20, ErrorMessage = "Priority cannot exceed 20 characters")]
        public string Priority { get; set; } = "";
        
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
        
        public List<string> MatterTypes { get; set; } = new List<string>
        {
            "Asset & Judgment Enforcement",
            "Billing, Timekeeping & Expense Tracking",
            "Case Management",
            "Client Intake, Conflict Checks & Engagement Letters",
            "Client Portal & Secure Messaging",
            "Closing/Escrow & Disbursement Management",
            "Compliance Management (Tracking, Audits & Policies)",
            "Contract Management",
            "Corporate Governance & Board Minutes",
            "Court Filings & Service",
            "Data Breach Response & Notification",
            "Discovery Drafting (Interrogatories, Requests, Admissions)",
            "Document Automation & E-Signature",
            "Document Review",
            "E-Discovery & Evidence Management",
            "Entity Formation & Filings",
            "Estate Planning & Probate Administration",
            "Expert & Witness Management",
            "Insurance Coverage Analysis & Tendering",
            "Investigations & Fact Development",
            "IP Licensing & Royalty Management",
            "IP Prosecution & Portfolio",
            "Legal Analytics",
            "Legal Holds & Records Retention",
            "Legal Research",
            "Lien & Subrogation Resolution",
            "Litigation Docketing & Deadlines",
            "M&A Deal Room & Checklists",
            "Medical Records Retrieval & Chronologies",
            "Negotiation & Settlement Management",
            "Real Estate Transactions",
            "Regulatory Filings & Reporting",
            "Rules-Based Calendaring (Court Rules Engines)",
            "Task Management & Workflow Automation",
            "Trademark/Copyright Monitoring & Takedowns",
            "Trust/IOLTA Accounting & Reconciliation",
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
        
        public List<string> Priorities { get; set; } = new List<string>
        {
            "Low",
            "Medium",
            "High",
            "Critical"
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
