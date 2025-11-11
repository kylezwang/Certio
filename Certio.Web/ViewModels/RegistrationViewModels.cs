using System.ComponentModel.DataAnnotations;

namespace Certio.Web.ViewModels
{
    /// <summary>
    /// Step 1: Email input
    /// </summary>
    public class EmailInputViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Step 2: 2FA verification
    /// </summary>
    public class TwoFactorVerificationViewModel
    {
        [Required(ErrorMessage = "Verification code is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 digits")]
        public string VerificationCode { get; set; } = "";

        [Required]
        public string Token { get; set; } = "";
        
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string VerificationMethod { get; set; } = "email"; // email or sms
    }

    /// <summary>
    /// Step 3: Complete registration with personal details
    /// </summary>
    public class CompleteRegistrationViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
        public string FirstName { get; set; } = "";
        
        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
        public string LastName { get; set; } = "";
        
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = "";
        
        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = "";
        
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
    }

    /// <summary>
    /// Registration session data stored in TempData
    /// </summary>
    public class RegistrationSessionData
    {
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string VerificationCode { get; set; } = "";
        public string VerificationMethod { get; set; } = "email";
        public DateTime CodeExpiry { get; set; }
        public bool IsVerified { get; set; }
    }
}
