using System.Security.Cryptography;
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
        string ProtectCode(string code);
        bool VerifyProtectedCode(string inputCode, string protectedCode, DateTime expiry);
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
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
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
                _logger.LogInformation("Sending email verification to {Email}", email);
                
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
                _logger.LogInformation("Sending SMS verification to {PhoneNumber}", phoneNumber);
                
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
        /// Protect a verification code using a random salt and SHA-256 hash.
        /// </summary>
        public string ProtectCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Verification code cannot be empty", nameof(code));
            }

            Span<byte> salt = stackalloc byte[16];
            RandomNumberGenerator.Fill(salt);

            var normalizedCode = NormalizeCode(code);
            var saltBytes = salt.ToArray();
            var hash = ComputeHash(saltBytes, normalizedCode.AsSpan());

            return string.Join('.', Convert.ToBase64String(saltBytes), Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Verify the input code against the protected code and expiry
        /// </summary>
        public bool VerifyProtectedCode(string inputCode, string protectedCode, DateTime expiry)
        {
            if (string.IsNullOrWhiteSpace(inputCode) || string.IsNullOrWhiteSpace(protectedCode))
            {
                return false;
            }

            if (DateTime.UtcNow > expiry)
            {
                _logger.LogWarning("Verification code expired");
                return false;
            }

            var normalizedCode = NormalizeCode(inputCode);
            var parts = protectedCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid protected code format");
                return false;
            }

            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[0]);
                expectedHash = Convert.FromBase64String(parts[1]);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Protected code decoding failed");
                return false;
            }

            var actualHash = ComputeHash(salt, normalizedCode.AsSpan());

            var isValid = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            if (!isValid)
            {
                _logger.LogWarning("Invalid verification code provided");
            }
            else
            {
                _logger.LogInformation("Verification code verified successfully");
            }

            return isValid;
        }

        private static string NormalizeCode(string code) => code.Trim();

        private static byte[] ComputeHash(ReadOnlySpan<byte> salt, ReadOnlySpan<char> code)
        {
            var byteCount = Encoding.UTF8.GetByteCount(code);
            var buffer = new byte[salt.Length + byteCount];
            salt.CopyTo(buffer);
            
            var bytesWritten = Encoding.UTF8.GetBytes(code, buffer.AsSpan(salt.Length));
            if (bytesWritten != byteCount)
            {
                throw new InvalidOperationException("Unexpected number of bytes written during encoding");
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(buffer);
        }
    }
}
