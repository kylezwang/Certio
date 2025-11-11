using System.Security.Cryptography;
using System.Text;
using Certio.Application.Interfaces;

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
        private readonly IEmailSendingService _emailSendingService;

        public TwoFactorService(ILogger<TwoFactorService> logger, IEmailSendingService emailSendingService)
        {
            _logger = logger;
            _emailSendingService = emailSendingService;
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
        /// </summary>
        public async Task<bool> SendEmailVerificationAsync(string email, string code)
        {
            try
            {
                var subject = "Your Notal verification code";
                var htmlBody = BuildVerificationEmailBody(code);

                var sent = await _emailSendingService.SendSystemEmailAsync(email, subject, htmlBody);
                if (!sent)
                {
                    _logger.LogWarning("Failed to deliver verification email to {Email}", email);
                }

                return sent;
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

        private static string BuildVerificationEmailBody(string code)
        {
            var sanitizedCode = NormalizeCode(code);
            return $@"
<html>
  <body style=""font-family: Arial, sans-serif; color: #1f1f1f;"">
    <div style=""max-width: 480px; margin: 0 auto; padding: 24px;"">
      <h2 style=""color: #3d1019; margin-bottom: 16px;"">Verify your identity</h2>
      <p style=""margin-bottom: 16px;"">
        Use the verification code below to continue signing in to Notal.
      </p>
      <div style=""font-size: 28px; font-weight: 700; letter-spacing: 6px; color: #3d1019; margin: 24px 0;"">
        {sanitizedCode}
      </div>
      <p style=""margin-bottom: 8px;"">For security, this code expires in 10 minutes.</p>
      <p style=""margin-bottom: 0;"">If you did not request this code, please ignore this message.</p>
    </div>
  </body>
</html>";
        }
    }
}
