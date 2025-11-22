using Certio.Domain.Organizations;
using Certio.Domain.Users;
using System.Text.RegularExpressions;

namespace Certio.Web.Helpers
{
    public static class RoleDisplayHelper
    {
        /// <summary>
        /// Gets the display name for a role based on the organization type.
        /// EventPlanner orgs show different role names than LawFirm orgs.
        /// </summary>
        public static string GetRoleDisplayName(string role, OrganizationType orgType)
        {
            if (orgType == OrganizationType.EventPlanner)
            {
                return role switch
                {
                    OrganizationRoles.ManagingPartner => "Managing Director",
                    OrganizationRoles.Partner => "Director",
                    OrganizationRoles.Associate => "Planner",
                    OrganizationRoles.Paralegal => "Coordinator",
                    OrganizationRoles.Staff => "Staff",
                    _ => SplitCamelCase(role)
                };
            }
            
            // LawFirm and others - use original role name with spacing
            return SplitCamelCase(role);
        }
        
        /// <summary>
        /// Gets a friendly display name for the organization type.
        /// </summary>
        public static string GetOrganizationTypeDisplayName(OrganizationType orgType)
        {
            return orgType switch
            {
                OrganizationType.Client => "Client",
                OrganizationType.LawFirm => "Law Firm",
                OrganizationType.EventPlanner => "Event Planning Company",
                OrganizationType.Government => "Government",
                OrganizationType.NonProfit => "Non-Profit",
                _ => orgType.ToString()
            };
        }
        
        /// <summary>
        /// Gets the icon class for an organization type.
        /// </summary>
        public static string GetOrganizationIconClass(OrganizationType orgType)
        {
            return orgType switch
            {
                OrganizationType.LawFirm => "fa-solid fa-building-columns",
                OrganizationType.EventPlanner => "fa-solid fa-clipboard-list",
                OrganizationType.Client => "fa-solid fa-user",
                OrganizationType.Government => "fa-solid fa-landmark",
                OrganizationType.NonProfit => "fa-solid fa-hand-holding-heart",
                _ => "fa-solid fa-building"
            };
        }
        
        /// <summary>
        /// Gets the label for "My Firm" / "My Org" based on organization type.
        /// </summary>
        public static string GetMyOrgLabel(OrganizationType orgType)
        {
            return orgType switch
            {
                OrganizationType.LawFirm => "My Firm",
                OrganizationType.EventPlanner => "My Company",
                _ => "My Org"
            };
        }
        
        /// <summary>
        /// Gets terminology for "Matter" based on organization type.
        /// EventPlanner orgs use "Event" instead of "Matter".
        /// </summary>
        public static string GetMatterTerminology(OrganizationType orgType)
        {
            return orgType == OrganizationType.EventPlanner ? "Event" : "Matter";
        }
        
        /// <summary>
        /// Gets plural terminology for "Matters" based on organization type.
        /// EventPlanner orgs use "Events" instead of "Matters".
        /// </summary>
        public static string GetMattersTerminology(OrganizationType orgType)
        {
            return orgType == OrganizationType.EventPlanner ? "Events" : "Matters";
        }
        
        /// <summary>
        /// Gets terminology for "Legal" based on organization type.
        /// EventPlanner orgs use "Events" instead of "Legal".
        /// </summary>
        public static string GetLegalTerminology(OrganizationType orgType)
        {
            return orgType == OrganizationType.EventPlanner ? "Events" : "Legal";
        }
        
        /// <summary>
        /// Gets terminology for "Legal Team" based on organization type.
        /// EventPlanner orgs use "Events Team" instead of "Legal Team".
        /// </summary>
        public static string GetLegalTeamTerminology(OrganizationType orgType)
        {
            return orgType == OrganizationType.EventPlanner ? "Events Team" : "Legal Team";
        }
        
        /// <summary>
        /// Gets icon for Legal/Events team based on organization type.
        /// EventPlanner orgs use clipboard-list icon instead of building-columns.
        /// </summary>
        public static string GetLegalTeamIcon(OrganizationType orgType)
        {
            return orgType == OrganizationType.EventPlanner ? "fa-solid fa-clipboard-list" : "fa-solid fa-building-columns";
        }
        
        /// <summary>
        /// Splits camel case strings into space-separated words.
        /// Example: "ManagingPartner" becomes "Managing Partner"
        /// </summary>
        private static string SplitCamelCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;
                
            return Regex.Replace(input, "(?<=[a-z])(?=[A-Z])", " ");
        }
    }
}

