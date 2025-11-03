using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Certio.Application.Interfaces;
using Certio.Domain.Services;
using Certio.Infrastructure.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;

namespace Certio.Web.Services;

public class EmailService : IEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IEmailToDmService _emailToDmService;
    private readonly ILogger<EmailService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmailService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IEmailToDmService emailToDmService,
        ILogger<EmailService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _configuration = configuration;
        _dataProtectionProvider = dataProtectionProvider;
        _emailToDmService = emailToDmService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public string GetGmailAuthUrl(string redirectUri)
    {
        var clientId = _configuration["EmailIntegration:Gmail:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException("Gmail ClientId is not configured");
        }

        var scopes = new[] { 
            "https://www.googleapis.com/auth/gmail.readonly",
            "https://www.googleapis.com/auth/gmail.send",
            "https://www.googleapis.com/auth/gmail.modify"
        };

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}&" +
            $"access_type=offline&" +
            $"prompt=consent";

        return authUrl;
    }

    public string GetOutlookAuthUrl(string redirectUri)
    {
        var clientId = _configuration["EmailIntegration:Outlook:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException("Outlook ClientId is not configured");
        }

        var scopes = new[] {
            "https://graph.microsoft.com/Mail.Read",
            "https://graph.microsoft.com/Mail.Send",
            "https://graph.microsoft.com/Mail.ReadWrite"
        };

        var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}&" +
            $"response_mode=query";

        return authUrl;
    }

    public async Task<EmailAccount> ConnectGmailAccountAsync(int userId, string authCode, string redirectUri, CancellationToken ct = default)
    {
        var clientId = _configuration["EmailIntegration:Gmail:ClientId"];
        var clientSecret = _configuration["EmailIntegration:Gmail:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Gmail OAuth credentials are not configured");
        }

        // Use provided redirect URI (must match authorization URL)
        if (string.IsNullOrEmpty(redirectUri))
        {
            redirectUri = _configuration["EmailIntegration:Gmail:RedirectUri"];
            if (string.IsNullOrEmpty(redirectUri) || redirectUri.Contains("${"))
            {
                redirectUri = "http://localhost:5092/api/email-oauth/gmail/callback";
                _logger.LogWarning("Using fallback redirect URI: {RedirectUri}", redirectUri);
            }
        }

        _logger.LogInformation("Connecting Gmail account for user {UserId} with redirect URI: {RedirectUri}", userId, redirectUri);

        // Exchange authorization code for tokens
        var tokenResponse = await ExchangeGmailCodeForTokensAsync(authCode, clientId, clientSecret, redirectUri, ct);

        // Get user's email address
        var userEmail = await GetGmailUserEmailAsync(tokenResponse.AccessToken, ct);

        // Encrypt tokens
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var encryptedAccessToken = protector.Protect(tokenResponse.AccessToken);
        var encryptedRefreshToken = tokenResponse.RefreshToken != null 
            ? protector.Protect(tokenResponse.RefreshToken) 
            : null;

        // Check if there's an existing account for this email address (active or inactive)
        var existingAccount = await _context.EmailAccounts
            .FirstOrDefaultAsync(ea => ea.UserId == userId && 
                                      ea.Provider == "Gmail" && 
                                      ea.EmailAddress == userEmail, ct);

        EmailAccount emailAccount;
        if (existingAccount != null)
        {
            // Reuse existing account to preserve historical emails
            _logger.LogInformation("Reusing existing Gmail account {AccountId} for {Email}", existingAccount.Id, userEmail);
            existingAccount.AccessToken = encryptedAccessToken;
            existingAccount.RefreshToken = encryptedRefreshToken;
            existingAccount.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);
            existingAccount.IsActive = true;
            emailAccount = existingAccount;
        }
        else
        {
            // Deactivate any other email accounts for this user
            var otherAccounts = await _context.EmailAccounts
            .Where(ea => ea.UserId == userId && ea.IsActive)
            .ToListAsync(ct);
        
            foreach (var account in otherAccounts)
        {
            account.IsActive = false;
        }

        // Create new email account
            emailAccount = new EmailAccount
        {
            UserId = userId,
            Provider = "Gmail",
            EmailAddress = userEmail,
            AccessToken = encryptedAccessToken,
            RefreshToken = encryptedRefreshToken,
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailAccounts.Add(emailAccount);
        }
        
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Connected Gmail account {Email} for user {UserId}", userEmail, userId);

        // Trigger immediate sync in background using a new scope
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000); // Small delay to ensure account is saved
                
                // Create a new scope for the background task to get a fresh DbContext
                using var scope = _scopeFactory.CreateScope();
                var backgroundService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await backgroundService.SyncEmailsAsync(emailAccount.Id, CancellationToken.None);
                
                _logger.LogInformation("Initial sync completed for Gmail account {Email}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial sync for Gmail account {Email}", userEmail);
            }
        }, ct);

        return emailAccount;
    }

    public async Task<EmailAccount> ConnectOutlookAccountAsync(int userId, string authCode, string redirectUri, CancellationToken ct = default)
    {
        var clientId = _configuration["EmailIntegration:Outlook:ClientId"];
        var clientSecret = _configuration["EmailIntegration:Outlook:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("Outlook OAuth credentials are not configured");
        }

        // Use provided redirect URI (must match authorization URL)
        if (string.IsNullOrEmpty(redirectUri))
        {
            redirectUri = _configuration["EmailIntegration:Outlook:RedirectUri"];
            if (string.IsNullOrEmpty(redirectUri) || redirectUri.Contains("${"))
            {
                redirectUri = "http://localhost:5092/api/email-oauth/outlook/callback";
                _logger.LogWarning("Using fallback redirect URI: {RedirectUri}", redirectUri);
            }
        }

        _logger.LogInformation("Connecting Outlook account for user {UserId} with redirect URI: {RedirectUri}", userId, redirectUri);

        // Exchange authorization code for tokens
        var tokenResponse = await ExchangeOutlookCodeForTokensAsync(authCode, clientId, clientSecret, redirectUri, ct);

        // Get user's email address
        var userEmail = await GetOutlookUserEmailAsync(tokenResponse.AccessToken, ct);

        // Encrypt tokens
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var encryptedAccessToken = protector.Protect(tokenResponse.AccessToken);
        var encryptedRefreshToken = tokenResponse.RefreshToken != null 
            ? protector.Protect(tokenResponse.RefreshToken) 
            : null;

        // Check if there's an existing account for this email address (active or inactive)
        var existingAccount = await _context.EmailAccounts
            .FirstOrDefaultAsync(ea => ea.UserId == userId && 
                                      ea.Provider == "Outlook" && 
                                      ea.EmailAddress == userEmail, ct);

        EmailAccount emailAccount;
        if (existingAccount != null)
        {
            // Reuse existing account to preserve historical emails
            _logger.LogInformation("Reusing existing Outlook account {AccountId} for {Email}", existingAccount.Id, userEmail);
            existingAccount.AccessToken = encryptedAccessToken;
            existingAccount.RefreshToken = encryptedRefreshToken;
            existingAccount.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600);
            existingAccount.IsActive = true;
            emailAccount = existingAccount;
        }
        else
        {
            // Deactivate any other email accounts for this user
            var otherAccounts = await _context.EmailAccounts
            .Where(ea => ea.UserId == userId && ea.IsActive)
            .ToListAsync(ct);
        
            foreach (var account in otherAccounts)
        {
            account.IsActive = false;
        }

        // Create new email account
            emailAccount = new EmailAccount
        {
            UserId = userId,
            Provider = "Outlook",
            EmailAddress = userEmail,
            AccessToken = encryptedAccessToken,
            RefreshToken = encryptedRefreshToken,
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn ?? 3600),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailAccounts.Add(emailAccount);
        }
        
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Connected Outlook account {Email} for user {UserId}", userEmail, userId);

        // Trigger immediate sync in background using a new scope
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000); // Small delay to ensure account is saved
                
                // Create a new scope for the background task to get a fresh DbContext
                using var scope = _scopeFactory.CreateScope();
                var backgroundService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await backgroundService.SyncEmailsAsync(emailAccount.Id, CancellationToken.None);
                
                _logger.LogInformation("Initial sync completed for Outlook account {Email}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial sync for Outlook account {Email}", userEmail);
            }
        }, ct);

        return emailAccount;
    }

    public async Task<EmailAccount?> GetEmailAccountAsync(int userId, CancellationToken ct = default)
    {
        return await _context.EmailAccounts
            .FirstOrDefaultAsync(ea => ea.UserId == userId && ea.IsActive, ct);
    }

    public async Task DisconnectEmailAccountAsync(int userId, CancellationToken ct = default)
    {
        var emailAccount = await GetEmailAccountAsync(userId, ct);
        if (emailAccount != null)
        {
            // Cancel webhook subscription if exists
            if (!string.IsNullOrEmpty(emailAccount.WebhookSubscriptionId))
            {
                try
                {
                    await CancelWebhookSubscriptionAsync(emailAccount, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel webhook subscription for email account {AccountId}", emailAccount.Id);
                }
            }

            emailAccount.IsActive = false;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Disconnected email account {AccountId} for user {UserId}", emailAccount.Id, userId);
        }
    }

    public async Task SyncEmailsAsync(int emailAccountId, CancellationToken ct = default)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(new object[] { emailAccountId }, ct);
        if (emailAccount == null || !emailAccount.IsActive)
        {
            throw new InvalidOperationException("Email account not found or inactive");
        }

        _logger.LogInformation("Starting email sync for account {AccountId} ({Provider})", emailAccountId, emailAccount.Provider);

        // Ensure token is valid
        try
        {
            await EnsureValidTokenAsync(emailAccount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure valid token for account {AccountId}", emailAccountId);
            throw;
        }

        try
        {
            if (emailAccount.Provider == "Gmail")
            {
                await SyncGmailEmailsAsync(emailAccount, ct);
            }
            else if (emailAccount.Provider == "Outlook")
            {
                await SyncOutlookEmailsAsync(emailAccount, ct);
            }
            else
            {
                throw new NotSupportedException($"Email sync not supported for provider: {emailAccount.Provider}");
            }

            emailAccount.LastSyncAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            
            _logger.LogInformation("Completed email sync for account {AccountId}", emailAccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing emails for account {AccountId}", emailAccountId);
            throw;
        }
    }

    public async Task<string> SetupWebhookAsync(int emailAccountId, string webhookUrl, CancellationToken ct = default)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(new object[] { emailAccountId }, ct);
        if (emailAccount == null || !emailAccount.IsActive)
        {
            throw new InvalidOperationException("Email account not found or inactive");
        }

        await EnsureValidTokenAsync(emailAccount, ct);

        string subscriptionId;

        if (emailAccount.Provider == "Gmail")
        {
            subscriptionId = await SetupGmailWebhookAsync(emailAccount, webhookUrl, ct);
        }
        else if (emailAccount.Provider == "Outlook")
        {
            subscriptionId = await SetupOutlookWebhookAsync(emailAccount, webhookUrl, ct);
        }
        else
        {
            throw new NotSupportedException($"Webhook setup not supported for provider: {emailAccount.Provider}");
        }

        emailAccount.WebhookSubscriptionId = subscriptionId;
        await _context.SaveChangesAsync(ct);

        return subscriptionId;
    }

    public async Task RefreshAccessTokenAsync(int emailAccountId, CancellationToken ct = default)
    {
        var emailAccount = await _context.EmailAccounts.FindAsync(new object[] { emailAccountId }, ct);
        if (emailAccount == null || !emailAccount.IsActive)
        {
            throw new InvalidOperationException("Email account not found or inactive");
        }

        await EnsureValidTokenAsync(emailAccount, ct);
    }

    // Private helper methods

    private async Task<TokenResponse> ExchangeGmailCodeForTokensAsync(string authCode, string clientId, string clientSecret, string redirectUri, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authCode,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        _logger.LogDebug("Exchanging Gmail code with redirect URI: {RedirectUri}", redirectUri);

        var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to exchange Gmail code: Status {Status}, Content: {Content}", response.StatusCode, content);
            throw new InvalidOperationException($"Failed to exchange authorization code: {content}");
        }

        var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        
        return new TokenResponse
        {
            AccessToken = tokenData?["access_token"]?.ToString() ?? throw new InvalidOperationException("No access token received"),
            RefreshToken = tokenData?["refresh_token"]?.ToString(),
            ExpiresInSeconds = tokenData?.ContainsKey("expires_in") == true 
                ? int.Parse(tokenData["expires_in"].ToString()!) 
                : 3600
        };
    }

    private async Task<TokenResponse> ExchangeOutlookCodeForTokensAsync(string authCode, string clientId, string clientSecret, string redirectUri, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/common/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authCode,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to exchange Outlook code: {Content}", content);
            throw new InvalidOperationException($"Failed to exchange authorization code: {content}");
        }

        var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        
        return new TokenResponse
        {
            AccessToken = tokenData?["access_token"]?.ToString() ?? throw new InvalidOperationException("No access token received"),
            RefreshToken = tokenData?["refresh_token"]?.ToString(),
            ExpiresIn = tokenData?.ContainsKey("expires_in") == true 
                ? int.Parse(tokenData["expires_in"].ToString()!) 
                : 3600
        };
    }

    private async Task<string> GetGmailUserEmailAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Certio"
            });

            var profile = await service.Users.GetProfile("me").ExecuteAsync(ct);
            return profile.EmailAddress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Gmail user email. Make sure Gmail API is enabled in Google Cloud Console.");
            throw new InvalidOperationException($"Failed to get Gmail user email. Ensure Gmail API is enabled in Google Cloud Console: {ex.Message}", ex);
        }
    }

    private async Task<string> GetOutlookUserEmailAsync(string accessToken, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to get user email: {content}");
        }

        var userData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        return userData?["mail"]?.ToString() ?? userData?["userPrincipalName"]?.ToString() 
            ?? throw new InvalidOperationException("No email address found in user profile");
    }

    private async Task EnsureValidTokenAsync(EmailAccount emailAccount, CancellationToken ct)
    {
        if (emailAccount.TokenExpiresAt.HasValue && emailAccount.TokenExpiresAt.Value > DateTime.UtcNow)
        {
            return; // Token is still valid
        }

        if (string.IsNullOrEmpty(emailAccount.RefreshToken))
        {
            throw new InvalidOperationException("Token expired and no refresh token available");
        }

        // Refresh the token
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var decryptedRefreshToken = protector.Unprotect(emailAccount.RefreshToken);

        TokenResponse newTokens;
        if (emailAccount.Provider == "Gmail")
        {
            var clientId = _configuration["EmailIntegration:Gmail:ClientId"];
            var clientSecret = _configuration["EmailIntegration:Gmail:ClientSecret"];
            newTokens = await RefreshGmailTokenAsync(clientId!, clientSecret!, decryptedRefreshToken, ct);
        }
        else if (emailAccount.Provider == "Outlook")
        {
            var clientId = _configuration["EmailIntegration:Outlook:ClientId"];
            var clientSecret = _configuration["EmailIntegration:Outlook:ClientSecret"];
            newTokens = await RefreshOutlookTokenAsync(clientId!, clientSecret!, decryptedRefreshToken, ct);
        }
        else
        {
            throw new NotSupportedException($"Token refresh not supported for provider: {emailAccount.Provider}");
        }

        // Update tokens
        emailAccount.AccessToken = protector.Protect(newTokens.AccessToken);
        if (newTokens.RefreshToken != null)
        {
            emailAccount.RefreshToken = protector.Protect(newTokens.RefreshToken);
        }
        emailAccount.TokenExpiresAt = DateTime.UtcNow.AddSeconds(newTokens.ExpiresInSeconds ?? newTokens.ExpiresIn ?? 3600);

        await _context.SaveChangesAsync(ct);
    }

    private async Task<TokenResponse> RefreshGmailTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to refresh token: {content}");
        }

        var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        
        return new TokenResponse
        {
            AccessToken = tokenData?["access_token"]?.ToString() ?? throw new InvalidOperationException("No access token received"),
            RefreshToken = refreshToken, // Keep existing refresh token
            ExpiresInSeconds = tokenData?.ContainsKey("expires_in") == true 
                ? int.Parse(tokenData["expires_in"].ToString()!) 
                : 3600
        };
    }

    private async Task<TokenResponse> RefreshOutlookTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/common/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to refresh token: {content}");
        }

        var tokenData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        
        return new TokenResponse
        {
            AccessToken = tokenData?["access_token"]?.ToString() ?? throw new InvalidOperationException("No access token received"),
            RefreshToken = tokenData?["refresh_token"]?.ToString() ?? refreshToken,
            ExpiresIn = tokenData?.ContainsKey("expires_in") == true 
                ? int.Parse(tokenData["expires_in"].ToString()!) 
                : 3600
        };
    }

    private async Task SyncGmailEmailsAsync(EmailAccount emailAccount, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Certio"
        });

        var baseline = DateTime.UtcNow.AddDays(-7);
        var lastSyncUtc = emailAccount.LastSyncAt.HasValue
            ? DateTime.SpecifyKind(emailAccount.LastSyncAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        // Always fetch the last 7 days to ensure complete inbox history
        var syncStart = baseline;
        if (!lastSyncUtc.HasValue || lastSyncUtc.Value < baseline)
        {
            // First sync or stale sync: fetch from 7 days ago
            syncStart = baseline;
        }
        else
        {
            // Regular refresh: still fetch last 7 days to ensure we have complete history
            syncStart = baseline;
        }

        // Build Gmail search query - use newer_than:7d to robustly fetch last 7 days
        // This handles timezones better than a date-only "after:" filter
        var query = "in:inbox newer_than:7d";

        // Pre-fetch all existing ExternalEmailIds for this account to avoid duplicate checks in loop
        var existingExternalIds = await _context.EmailMessages
            .Where(em => em.EmailAccountId == emailAccount.Id)
            .Select(em => em.ExternalEmailId)
            .ToHashSetAsync(ct);

        var processedCount = 0;
        var processedInBatch = new HashSet<string>(); // Track IDs processed in this batch to avoid duplicates
        string? nextPageToken = null;
        var maxPages = 50; // Increased to allow more emails from past 7 days (5000 emails max)
        var pageCount = 0;

        do
        {
            var request = service.Users.Messages.List("me");
            request.Q = query;
            request.LabelIds = new[] { "INBOX" };
            request.MaxResults = 100; // Gmail API max per page
            request.PageToken = nextPageToken;

            var messagesResponse = await request.ExecuteAsync(ct);
            
            if (messagesResponse.Messages == null || !messagesResponse.Messages.Any())
            {
                if (pageCount == 0)
                {
                    _logger.LogInformation("No new Gmail messages to sync for account {AccountId}", emailAccount.Id);
                }
                break;
            }

            pageCount++;
            _logger.LogDebug("Processing page {Page} with {Count} messages for account {AccountId}", pageCount, messagesResponse.Messages.Count, emailAccount.Id);
        
            foreach (var messageRef in messagesResponse.Messages)
            {
                try
                {
                    var messageId = messageRef.Id;
                    
                    // Skip if already exists in database or already processed in this batch
                    if (existingExternalIds.Contains(messageId) || processedInBatch.Contains(messageId))
                    {
                        continue;
                    }
                    
                    processedInBatch.Add(messageId);
                    
                    // Get full message details
                    var message = await service.Users.Messages.Get("me", messageId).ExecuteAsync(ct);
                    
                    // Parse email
                    var emailMessage = await ParseGmailMessageAsync(message, emailAccount.Id, ct);
                    
                    if (emailMessage != null)
                    {
                        // Save individually to avoid batch insert conflicts and race conditions
                        try
                        {
                            // Double-check before inserting (race condition protection)
                            var exists = await _context.EmailMessages
                                .AnyAsync(em => em.ExternalEmailId == emailMessage.ExternalEmailId, ct);
                            
                            if (!exists)
                            {
                                _context.EmailMessages.Add(emailMessage);
                                await _context.SaveChangesAsync(ct);
                                processedCount++;
                                existingExternalIds.Add(emailMessage.ExternalEmailId); // Update cache for subsequent checks
                            }
                        }
                        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2601)
                        {
                            // Duplicate key - another sync may have inserted it, skip silently
                            _context.ChangeTracker.Clear();
                            _logger.LogDebug("Skipping duplicate email {ExternalEmailId} during sync", emailMessage.ExternalEmailId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing Gmail message {MessageId}", messageRef.Id);
                    _context.ChangeTracker.Clear(); // Clear any failed entities
                }
            }

            // Get next page token for pagination
            nextPageToken = messagesResponse.NextPageToken;
            
            // Break if we've reached max pages or no more pages
            if (pageCount >= maxPages || string.IsNullOrEmpty(nextPageToken))
            {
                break;
            }
        } while (!string.IsNullOrEmpty(nextPageToken) && pageCount < maxPages);

        _logger.LogInformation("Synced {Count} Gmail emails for account {AccountId} across {Pages} pages", processedCount, emailAccount.Id, pageCount);

        // Convert synced emails to Direct Messages
        if (processedCount > 0)
        {
            _logger.LogInformation("Converting {Count} new emails to Direct Messages", processedCount);
            await ConvertNewEmailsToDirectMessagesAsync(emailAccount, ct);
        }
    }

    private async Task SyncOutlookEmailsAsync(EmailAccount emailAccount, CancellationToken ct)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var baseline = DateTime.UtcNow.AddDays(-7);
        var lastSyncUtc = emailAccount.LastSyncAt.HasValue
            ? DateTime.SpecifyKind(emailAccount.LastSyncAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        // Ensure we always fetch at least the last 7 days to backfill
        var syncStart = lastSyncUtc.HasValue && lastSyncUtc.Value < baseline
            ? lastSyncUtc.Value
            : baseline;

        // Build filter for messages since sync start
        var filter = $"receivedDateTime ge {syncStart:yyyy-MM-ddTHH:mm:ssZ}";

        var url = $"https://graph.microsoft.com/v1.0/me/messages?$filter={Uri.EscapeDataString(filter)}&$top=50&$orderby=receivedDateTime desc";
        var response = await httpClient.GetAsync(url, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch Outlook messages: {Content}", content);
            throw new InvalidOperationException($"Failed to fetch Outlook messages: {content}");
        }

        var messagesResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
        if (messagesResponse == null || !messagesResponse.ContainsKey("value"))
        {
            _logger.LogInformation("No new Outlook messages to sync for account {AccountId}", emailAccount.Id);
            return;
        }

        var messages = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(messagesResponse["value"]!.ToString()!);
        if (messages == null || !messages.Any())
        {
            return;
        }

        var processedCount = 0;
        
        // Pre-fetch all existing ExternalEmailIds for this account to avoid duplicate checks in loop
        var existingExternalIds = await _context.EmailMessages
            .Where(em => em.EmailAccountId == emailAccount.Id)
            .Select(em => em.ExternalEmailId)
            .ToHashSetAsync(ct);
        
        var processedInBatch = new HashSet<string>(); // Track IDs processed in this batch to avoid duplicates
        
        foreach (var messageData in messages)
        {
            try
            {
                var messageId = messageData["id"]?.ToString();
                if (string.IsNullOrEmpty(messageId))
                {
                    continue;
                }
                
                // Skip if already exists in database or already processed in this batch
                if (existingExternalIds.Contains(messageId) || processedInBatch.Contains(messageId))
                {
                    continue;
                }
                
                processedInBatch.Add(messageId);

                // Parse email
                var emailMessage = ParseOutlookMessage(messageData, emailAccount.Id);
                
                if (emailMessage != null)
                {
                    // Save individually to avoid batch insert conflicts and race conditions
                    try
                    {
                        // Double-check before inserting (race condition protection)
                        var exists = await _context.EmailMessages
                            .AnyAsync(em => em.ExternalEmailId == emailMessage.ExternalEmailId, ct);
                        
                        if (!exists)
                        {
                            _context.EmailMessages.Add(emailMessage);
                            await _context.SaveChangesAsync(ct);
                            processedCount++;
                            existingExternalIds.Add(emailMessage.ExternalEmailId); // Update cache for subsequent checks
                        }
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2601)
                    {
                        // Duplicate key - another sync may have inserted it, skip silently
                        _context.ChangeTracker.Clear();
                        _logger.LogDebug("Skipping duplicate email {ExternalEmailId} during sync", emailMessage.ExternalEmailId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Outlook message");
                _context.ChangeTracker.Clear(); // Clear any failed entities
            }
        }
        _logger.LogInformation("Synced {Count} Outlook emails for account {AccountId}", processedCount, emailAccount.Id);

        // Convert synced emails to Direct Messages
        if (processedCount > 0)
        {
            _logger.LogInformation("Converting {Count} new emails to Direct Messages", processedCount);
            await ConvertNewEmailsToDirectMessagesAsync(emailAccount, ct);
        }
    }

    private async Task<EmailMessage?> ParseGmailMessageAsync(Message message, int emailAccountId, CancellationToken ct)
    {
        try
        {
            // Handle duplicate headers by grouping and taking the last value (or first for most headers)
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (message.Payload?.Headers != null)
            {
                foreach (var header in message.Payload.Headers)
                {
                    if (string.IsNullOrEmpty(header.Name))
                        continue;
                    
                    // For headers that can have multiple values, take the last one (most recent)
                    // This is especially important for "Received" headers
                    if (headers.ContainsKey(header.Name))
                    {
                        // For "Received" headers, keep the last one (most recent hop)
                        // For other headers, keep the first one (usually only one exists)
                        if (header.Name.Equals("Received", StringComparison.OrdinalIgnoreCase))
                        {
                            headers[header.Name] = header.Value ?? "";
                        }
                        // For other headers, keep first value (or concatenate if needed)
                    }
                    else
                    {
                        headers[header.Name] = header.Value ?? "";
                    }
                }
            }

            var fromHeader = headers.GetValueOrDefault("From", "");
            var (fromEmail, fromName) = ParseEmailAddress(fromHeader);
            
            var toHeader = headers.GetValueOrDefault("To", "");
            var toEmails = ParseEmailAddressList(toHeader);
            
            var ccHeader = headers.GetValueOrDefault("Cc", "");
            var ccEmails = ParseEmailAddressList(ccHeader);
            
            var bccHeader = headers.GetValueOrDefault("Bcc", "");
            var bccEmails = ParseEmailAddressList(bccHeader);

            var subject = headers.GetValueOrDefault("Subject", "");
            var dateHeader = headers.GetValueOrDefault("Date", "");
            var receivedAt = DateTime.UtcNow;
            
            if (DateTime.TryParse(dateHeader, out var parsedDate))
            {
                receivedAt = parsedDate.ToUniversalTime();
            }

            // Extract body
            var (body, bodyText) = ExtractGmailBody(message.Payload);

            // If we have HTML but no plain text, extract plain text from HTML
            if (!string.IsNullOrEmpty(body) && string.IsNullOrEmpty(bodyText))
            {
                bodyText = StripHtmlToPlainText(body);
            }

            var emailMessage = new EmailMessage
            {
                Id = Guid.NewGuid(),
                EmailAccountId = emailAccountId,
                ExternalEmailId = message.Id ?? Guid.NewGuid().ToString(),
                ThreadId = message.ThreadId,
                Subject = subject,
                FromEmail = fromEmail,
                FromName = fromName,
                ToEmails = System.Text.Json.JsonSerializer.Serialize(toEmails),
                CcEmails = ccEmails.Any() ? System.Text.Json.JsonSerializer.Serialize(ccEmails) : null,
                BccEmails = bccEmails.Any() ? System.Text.Json.JsonSerializer.Serialize(bccEmails) : null,
                Body = body,
                BodyText = bodyText,
                IsRead = message.LabelIds?.Contains("UNREAD") != true,
                ReceivedAt = receivedAt,
                CreatedAt = DateTime.UtcNow
            };

            return emailMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Gmail message {MessageId}", message.Id);
            return null;
        }
    }

    private EmailMessage? ParseOutlookMessage(Dictionary<string, object> messageData, int emailAccountId)
    {
        try
        {
            var messageId = messageData["id"]?.ToString() ?? Guid.NewGuid().ToString();
            var conversationId = messageData["conversationId"]?.ToString();
            
            var from = messageData["from"] as Dictionary<string, object>;
            var fromEmail = from?["emailAddress"] as Dictionary<string, object>;
            var fromEmailAddr = fromEmail?["address"]?.ToString() ?? "";
            var fromName = fromEmail?["name"]?.ToString();

            var toRecipients = messageData["toRecipients"] as System.Text.Json.JsonElement?;
            var toEmails = new List<string>();
            if (toRecipients?.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var recipient in toRecipients.Value.EnumerateArray())
                {
                    var emailAddr = recipient.GetProperty("emailAddress").GetProperty("address").GetString();
                    if (!string.IsNullOrEmpty(emailAddr))
                    {
                        toEmails.Add(emailAddr);
                    }
                }
            }

            var ccRecipients = messageData["ccRecipients"] as System.Text.Json.JsonElement?;
            var ccEmails = new List<string>();
            if (ccRecipients?.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var recipient in ccRecipients.Value.EnumerateArray())
                {
                    var emailAddr = recipient.GetProperty("emailAddress").GetProperty("address").GetString();
                    if (!string.IsNullOrEmpty(emailAddr))
                    {
                        ccEmails.Add(emailAddr);
                    }
                }
            }

            var subject = messageData["subject"]?.ToString() ?? "";
            var body = messageData["body"] as Dictionary<string, object>;
            var bodyContent = body?["content"]?.ToString() ?? "";
            var bodyContentType = body?["contentType"]?.ToString() ?? "Text";
            
            // Extract plain text from HTML if needed
            string? plainText = null;
            if (bodyContentType == "HTML" && !string.IsNullOrEmpty(bodyContent))
            {
                plainText = StripHtmlToPlainText(bodyContent);
            }
            
            var receivedAt = DateTime.UtcNow;
            if (messageData.ContainsKey("receivedDateTime") && messageData["receivedDateTime"] != null)
            {
                var dateStr = messageData["receivedDateTime"].ToString();
                if (DateTime.TryParse(dateStr, out var parsedDate))
                {
                    receivedAt = parsedDate.ToUniversalTime();
                }
            }

            var isRead = messageData.ContainsKey("isRead") && 
                        messageData["isRead"] is bool read && read;

            var emailMessage = new EmailMessage
            {
                Id = Guid.NewGuid(),
                EmailAccountId = emailAccountId,
                ExternalEmailId = messageId,
                ThreadId = conversationId,
                Subject = subject,
                FromEmail = fromEmailAddr,
                FromName = fromName,
                ToEmails = System.Text.Json.JsonSerializer.Serialize(toEmails),
                CcEmails = ccEmails.Any() ? System.Text.Json.JsonSerializer.Serialize(ccEmails) : null,
                Body = bodyContentType == "HTML" ? bodyContent : null,
                BodyText = bodyContentType == "Text" ? bodyContent : (plainText ?? bodyContent),
                IsRead = isRead,
                ReceivedAt = receivedAt,
                CreatedAt = DateTime.UtcNow
            };

            return emailMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Outlook message");
            return null;
        }
    }

    private (string body, string bodyText) ExtractGmailBody(MessagePart? payload)
    {
        string? body = null;
        string? bodyText = null;

        if (payload == null)
        {
            return ("", "");
        }

        // Check if this part has body data
        if (!string.IsNullOrEmpty(payload.Body?.Data))
        {
            var content = DecodeBase64Url(payload.Body.Data);
            if (payload.MimeType == "text/html")
            {
                body = content;
            }
            else if (payload.MimeType == "text/plain")
            {
                bodyText = content;
            }
        }

        // Recursively check parts
        if (payload.Parts != null)
        {
            foreach (var part in payload.Parts)
            {
                var (partBody, partBodyText) = ExtractGmailBody(part);
                if (!string.IsNullOrEmpty(partBody))
                {
                    body = partBody;
                }
                if (!string.IsNullOrEmpty(partBodyText))
                {
                    bodyText = partBodyText;
                }
            }
        }

        return (body ?? bodyText ?? "", bodyText ?? body ?? "");
    }

    /// <summary>
    /// Strips HTML tags and extracts plain text, handling style/script tags properly
    /// </summary>
    private string StripHtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // Remove script and style elements completely (including their content)
        html = System.Text.RegularExpressions.Regex.Replace(html, 
            @"<(script|style)[^>]*>.*?</\1>", 
            "", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        // Convert block elements to newlines before removing tags
        // Replace block-level elements with newlines
        html = System.Text.RegularExpressions.Regex.Replace(html, 
            @"</?(p|div|h[1-6]|li|tr|td|th|br)[^>]*>", "\n", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Replace <br> tags (including <br/> and <br />)
        html = System.Text.RegularExpressions.Regex.Replace(html, 
            @"<br\s*/?>", "\n", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace common HTML entities
        html = html.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");

        // Remove all remaining HTML tags
        html = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");

        // Decode other HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Normalize whitespace while preserving newlines
        // Replace multiple spaces/tabs with single space (but preserve newlines)
        html = System.Text.RegularExpressions.Regex.Replace(html, @"[ \t]+", " ");
        // Replace multiple consecutive newlines with max 2 newlines (for paragraph breaks)
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\n{3,}", "\n\n");
        // Clean up spaces at start/end of lines
        html = System.Text.RegularExpressions.Regex.Replace(html, @"[ \t]+\n", "\n");
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\n[ \t]+", "\n");
        
        return html.Trim();
    }

    private string DecodeBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        var bytes = Convert.FromBase64String(base64);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private (string email, string? name) ParseEmailAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return ("", null);
        }

        var match = System.Text.RegularExpressions.Regex.Match(address, @"^(.+?)\s*<(.+?)>$");
        if (match.Success)
        {
            return (match.Groups[2].Value.Trim(), match.Groups[1].Value.Trim('"'));
        }

        return (address.Trim(), null);
    }

    private List<string> ParseEmailAddressList(string addresses)
    {
        if (string.IsNullOrEmpty(addresses))
        {
            return new List<string>();
        }

        var emailList = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(addresses, @"([^,<>""]+?<.+?>|[^,]+)");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var address = match.Value.Trim();
            var (email, _) = ParseEmailAddress(address);
            if (!string.IsNullOrEmpty(email))
            {
                emailList.Add(email);
            }
        }

        return emailList;
    }

    private async Task ConvertNewEmailsToDirectMessagesAsync(EmailAccount emailAccount, CancellationToken ct)
    {
        // Get user's organizations
        var user = await _context.Users
            .Include(u => u.UserOrganizations)
            .FirstOrDefaultAsync(u => u.Id == emailAccount.UserId, ct);

        if (user == null)
        {
            return;
        }

        // Get emails that haven't been converted to DMs yet
        var unconvertedEmails = await _context.EmailMessages
            .Where(em => em.EmailAccountId == emailAccount.Id && em.DirectMessageId == null)
            .ToListAsync(ct);

        foreach (var email in unconvertedEmails)
        {
            try
            {
                // Parse recipient emails
                var toEmails = System.Text.Json.JsonSerializer.Deserialize<List<string>>(email.ToEmails ?? "[]") ?? new List<string>();
                
                // Try to convert email to DM for each organization the user belongs to
                foreach (var userOrg in user.UserOrganizations.Where(uo => uo.IsActive))
                {
                    try
                    {
                        // Parse recipient emails
                        var recipientEmails = toEmails; // Use already parsed list
                        
                        // Check if sender is in this organization
                        var senderUserId = await _emailToDmService.FindUserByEmailAsync(email.FromEmail, userOrg.OrganizationId, ct);
                        
                        // Check if any recipient is in this organization (including the connected user's email)
                        var recipientUserId = await _emailToDmService.FindUserByEmailAsync(emailAccount.EmailAddress, userOrg.OrganizationId, ct);
                        
                        // Also check other recipients
                        foreach (var recipientEmail in recipientEmails)
                        {
                            var recipientId = await _emailToDmService.FindUserByEmailAsync(recipientEmail, userOrg.OrganizationId, ct);
                            if (recipientId.HasValue)
                            {
                                recipientUserId = recipientId;
                                break;
                            }
                        }
                        
                        // Convert if:
                        // 1. Sender is in org AND recipient (connected user) is in org (email sent TO connected user)
                        // 2. OR recipient is in org AND sender is connected user (email sent FROM connected user)
                        var connectedUserIsSender = email.FromEmail.Equals(emailAccount.EmailAddress, StringComparison.OrdinalIgnoreCase);
                        var connectedUserIsRecipient = recipientEmails.Any(e => e.Equals(emailAccount.EmailAddress, StringComparison.OrdinalIgnoreCase));
                        
                        if (senderUserId.HasValue && (connectedUserIsRecipient || recipientUserId.HasValue))
                        {
                            // Email from org user TO connected user or another org user
                            await _emailToDmService.ConvertEmailToDirectMessageAsync(email, userOrg.OrganizationId, ct);
                            _logger.LogInformation("Converted email {EmailId} to DM - sender: {SenderEmail}, recipient: {RecipientEmails}", 
                                email.Id, email.FromEmail, string.Join(", ", recipientEmails));
                            break; // Converted successfully, move to next email
                        }
                        else if (connectedUserIsSender && recipientUserId.HasValue)
                        {
                            // Email FROM connected user TO org user
                            await _emailToDmService.ConvertEmailToDirectMessageAsync(email, userOrg.OrganizationId, ct);
                            _logger.LogInformation("Converted email {EmailId} to DM - sender: {SenderEmail}, recipient: {RecipientEmails}", 
                                email.Id, email.FromEmail, string.Join(", ", recipientEmails));
                            break; // Converted successfully, move to next email
                        }
                        else
                        {
                            _logger.LogDebug("Skipping email {EmailId} - sender: {SenderEmail} (in org: {SenderInOrg}), recipients: {Recipients} (connected user is recipient: {ConnectedIsRecipient})", 
                                email.Id, email.FromEmail, senderUserId.HasValue, string.Join(", ", recipientEmails), connectedUserIsRecipient);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error converting email {EmailId} to DM in org {OrgId}", email.Id, userOrg.OrganizationId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email {EmailId} for conversion", email.Id);
            }
        }
    }

    private async Task<string> SetupGmailWebhookAsync(EmailAccount emailAccount, string webhookUrl, CancellationToken ct)
    {
        // Gmail uses Pub/Sub for webhooks
        // This requires setting up a Google Cloud Pub/Sub topic and subscription
        // For now, return a placeholder - full implementation requires Google Cloud setup
        _logger.LogWarning("Gmail webhook setup requires Google Cloud Pub/Sub configuration");
        return $"gmail_webhook_{emailAccount.Id}_{DateTime.UtcNow.Ticks}";
    }

    private async Task<string> SetupOutlookWebhookAsync(EmailAccount emailAccount, string webhookUrl, CancellationToken ct)
    {
        // Outlook uses Microsoft Graph subscription API
        var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
        var accessToken = protector.Unprotect(emailAccount.AccessToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

        var subscription = new
        {
            changeType = "created",
            notificationUrl = webhookUrl,
            resource = "/me/messages",
            expirationDateTime = DateTime.UtcNow.AddDays(3).ToString("O"),
            clientState = $"certio_{emailAccount.Id}"
        };

        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(subscription), System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("https://graph.microsoft.com/v1.0/subscriptions", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to setup Outlook webhook: {Content}", responseContent);
            throw new InvalidOperationException($"Failed to setup webhook: {responseContent}");
        }

        var subscriptionData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
        return subscriptionData?["id"]?.ToString() ?? throw new InvalidOperationException("No subscription ID received");
    }

    private async Task CancelWebhookSubscriptionAsync(EmailAccount emailAccount, CancellationToken ct)
    {
        if (emailAccount.Provider == "Outlook" && !string.IsNullOrEmpty(emailAccount.WebhookSubscriptionId))
        {
            var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
            var accessToken = protector.Unprotect(emailAccount.AccessToken);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/subscriptions/{emailAccount.WebhookSubscriptionId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to cancel Outlook webhook subscription {SubscriptionId}", emailAccount.WebhookSubscriptionId);
            }
        }
        // Gmail webhook cancellation would require Pub/Sub cleanup
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string? RefreshToken { get; set; }
        public int? ExpiresInSeconds { get; set; }
        public int? ExpiresIn { get; set; }
    }
}

