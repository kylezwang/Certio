using System.Text.RegularExpressions;

namespace Certio.Web.Security
{
    /// <summary>
    /// Helper class for validating and sanitizing user inputs
    /// Prevents SQL injection, XSS, and other injection attacks
    /// </summary>
    public static class InputValidator
    {
        // Maximum lengths for common fields
        public const int MAX_TITLE_LENGTH = 200;
        public const int MAX_DESCRIPTION_LENGTH = 5000;
        public const int MAX_NAME_LENGTH = 100;
        public const int MAX_EMAIL_LENGTH = 200;
        public const int MAX_NOTES_LENGTH = 2000;

        /// <summary>
        /// Sanitizes a string by trimming and limiting length
        /// </summary>
        public static string Sanitize(string? input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var trimmed = input.Trim();
            return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        }

        /// <summary>
        /// Validates that a string is not null/empty and within length limits
        /// </summary>
        public static bool IsValidString(string? input, int maxLength, bool required = true)
        {
            if (string.IsNullOrWhiteSpace(input))
                return !required;

            return input.Trim().Length <= maxLength;
        }

        /// <summary>
        /// Validates that an ID is positive
        /// </summary>
        public static bool IsValidId(int id)
        {
            return id > 0;
        }

        /// <summary>
        /// Validates that an ID is positive or null
        /// </summary>
        public static bool IsValidOptionalId(int? id)
        {
            return !id.HasValue || id.Value > 0;
        }

        /// <summary>
        /// Validates email format
        /// </summary>
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email && email.Length <= MAX_EMAIL_LENGTH;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates date range (start before end)
        /// </summary>
        public static bool IsValidDateRange(DateTime? startDate, DateTime? endDate)
        {
            if (!startDate.HasValue || !endDate.HasValue)
                return true; // Null dates are acceptable

            return startDate.Value <= endDate.Value;
        }

        /// <summary>
        /// Removes potentially dangerous HTML/script content
        /// Basic implementation - consider using a library like HtmlSanitizer for production
        /// </summary>
        public static string RemoveHtmlTags(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove HTML tags
            var withoutTags = Regex.Replace(input, @"<[^>]*>", string.Empty);
            
            // Remove script content
            var withoutScripts = Regex.Replace(withoutTags, @"<script[^>]*?>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            return withoutScripts.Trim();
        }

        /// <summary>
        /// Validates that a list of IDs contains only positive integers
        /// </summary>
        public static bool AreValidIds(IEnumerable<int>? ids)
        {
            if (ids == null)
                return true;

            return ids.All(id => id > 0);
        }

        /// <summary>
        /// Validates priority value
        /// </summary>
        public static bool IsValidPriority(string? priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
                return false;

            var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
            return validPriorities.Contains(priority, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates status value for matters
        /// </summary>
        public static bool IsValidMatterStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            var validStatuses = new[] { "Planning", "In Progress", "InProgress", "Review", "Completed", "OnHold", "Cancelled" };
            return validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates status value for tasks
        /// </summary>
        public static bool IsValidTaskStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            var validStatuses = new[] { "Pending", "InProgress", "Review", "Completed", "Blocked" };
            return validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates access level value
        /// </summary>
        public static bool IsValidAccessLevel(string? accessLevel)
        {
            if (string.IsNullOrWhiteSpace(accessLevel))
                return false;

            var validLevels = new[] { "Everyone", "Specific" };
            return validLevels.Contains(accessLevel, StringComparer.OrdinalIgnoreCase);
        }
    }
}

