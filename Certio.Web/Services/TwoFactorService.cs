using System.Text;

namespace Certio.Web.Services
{
    /// <summary>
    /// Simple 2FA service for email/SMS verification
    /// In production, you'd integrate with real email/SMS providers
    /// </summary>
    public interface ITwoFactorService
    {
        Task<string> GenerateVerificationCodeAsync();
        Task<bool> SendEmailVerificationAsync(string email, string code);
        Task<bool> SendSmsVerificationAsync(string phoneNumber, string code);
        bool VerifyCode(string inputCode, string storedCode, DateTime expiry);
    }

    public class TwoFactorService : ITwoFactorService
    {
        private readonly ILogger<TwoFactorService> _logger;

        public TwoFactorService(ILogger<TwoFactorService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate a 6-digit verification code
        /// </summary>
        public async Task<string> GenerateVerificationCodeAsync()
        {
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();
            
            _logger.LogInformation("Generated verification code: {Code}", code);
            return await Task.FromResult(code);
        }

        /// <summary>
        /// Send verification code via email
        /// In production, integrate with SendGrid, AWS SES, etc.
        /// </summary>
        public async Task<bool> SendEmailVerificationAsync(string email, string code)
        {
            try
            {
                // For development, just log the code
                _logger.LogInformation("Sending email verification to {Email} with code: {Code}", email, code);
                
                // Also output to console for immediate visibility
                Console.WriteLine($"🔐 2FA CODE FOR {email}: {code}");
                Console.WriteLine($"📧 Email verification code: {code}");
                Console.WriteLine($"⏰ Code expires in 10 minutes");
                
                // In production, you would:
                // 1. Create an email template
                // 2. Send via email service (SendGrid, AWS SES, etc.)
                // 3. Handle delivery failures
                
                // Simulate email sending delay
                await Task.Delay(100);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email verification to {Email}", email);
                return false;
            }
        }

        /// <summary>
        /// Send verification code via SMS
        /// In production, integrate with Twilio, AWS SNS, etc.
        /// </summary>
        public async Task<bool> SendSmsVerificationAsync(string phoneNumber, string code)
        {
            try
            {
                // For development, just log the code
                _logger.LogInformation("Sending SMS verification to {PhoneNumber} with code: {Code}", phoneNumber, code);
                
                // In production, you would:
                // 1. Format phone number properly
                // 2. Send via SMS service (Twilio, AWS SNS, etc.)
                // 3. Handle delivery failures
                
                // Simulate SMS sending delay
                await Task.Delay(100);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS verification to {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        /// <summary>
        /// Verify the input code against stored code and expiry
        /// </summary>
        public bool VerifyCode(string inputCode, string storedCode, DateTime expiry)
        {
            if (string.IsNullOrEmpty(inputCode) || string.IsNullOrEmpty(storedCode))
                return false;

            if (DateTime.UtcNow > expiry)
            {
                _logger.LogWarning("Verification code expired");
                return false;
            }

            var isValid = inputCode.Trim() == storedCode.Trim();
            
            if (isValid)
            {
                _logger.LogInformation("Verification code verified successfully");
            }
            else
            {
                _logger.LogWarning("Invalid verification code provided");
            }

            return isValid;
        }
    }
}
