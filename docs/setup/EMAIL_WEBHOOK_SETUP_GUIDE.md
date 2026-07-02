# Email Integration Webhook Setup Guide

## Overview

This guide explains how to set up webhooks for Gmail and Outlook to enable real-time email synchronization with the Direct Messaging module.

## Gmail Webhook Setup (Google Cloud Pub/Sub)

### Prerequisites
1. Google Cloud Project with billing enabled
2. Gmail API enabled
3. Cloud Pub/Sub API enabled
4. Service account with appropriate permissions

### Steps

#### 1. Create Pub/Sub Topic
```bash
gcloud pubsub topics create gmail-notifications
```

#### 2. Create Pub/Sub Subscription
```bash
gcloud pubsub subscriptions create certio-gmail-subscription \
  --topic=gmail-notifications \
  --push-endpoint=https://your-domain.com/api/email-webhook/gmail \
  --push-auth-service-account=your-service-account@project.iam.gserviceaccount.com
```

#### 3. Set up Gmail Watch
Update `SetupGmailWebhookAsync` in `EmailService.cs`:

```csharp
private async Task<string> SetupGmailWebhookAsync(EmailAccount emailAccount, string webhookUrl, CancellationToken ct)
{
    var protector = _dataProtectionProvider.CreateProtector("EmailTokens");
    var accessToken = protector.Unprotect(emailAccount.AccessToken);

    var credential = GoogleCredential.FromAccessToken(accessToken);
    var service = new GmailService(new BaseClientService.Initializer
    {
        HttpClientInitializer = credential,
        ApplicationName = "Certio"
    });

    // Create watch request
    var watchRequest = new WatchRequest
    {
        TopicName = "projects/YOUR_PROJECT_ID/topics/gmail-notifications",
        LabelIds = new List<string> { "INBOX" }
    };

    var watchResponse = await service.Users.Watch(watchRequest, "me").ExecuteAsync(ct);
    
    // Store history ID for future syncs
    return watchResponse.HistoryId ?? $"gmail_watch_{emailAccount.Id}";
}
```

#### 4. Configure Webhook Secret
Set `EMAIL_WEBHOOK_SECRET` environment variable for webhook validation.

#### 5. Verify Webhook
Google Cloud Pub/Sub will validate your endpoint by sending a verification request. Ensure your endpoint returns 200 OK.

## Outlook Webhook Setup (Microsoft Graph)

### Prerequisites
1. Azure App Registration
2. Microsoft Graph API permissions:
   - `Mail.Read`
   - `Mail.Send`
   - `Mail.ReadWrite`

### Steps

#### 1. Create Subscription
The `SetupOutlookWebhookAsync` method already creates subscriptions. You need to:

1. **Configure Redirect URI** in Azure Portal:
   - Add `https://your-domain.com/api/email-webhook/outlook` as a redirect URI
   - Set it as a Web platform

2. **Set Webhook URL**:
   ```csharp
   var webhookUrl = "https://your-domain.com/api/email-webhook/outlook";
   await _emailService.SetupWebhookAsync(emailAccountId, webhookUrl);
   ```

#### 2. Subscription Renewal
Outlook subscriptions expire after 3 days. Add renewal logic to `EmailSyncService`:

```csharp
private async Task RenewOutlookSubscriptionsAsync(CancellationToken ct)
{
    using var scope = _serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

    var outlookAccounts = await context.EmailAccounts
        .Where(ea => ea.IsActive && 
                    ea.Provider == "Outlook" && 
                    ea.WebhookSubscriptionId != null)
        .ToListAsync(ct);

    foreach (var account in outlookAccounts)
    {
        // Check if subscription expires soon (within 24 hours)
        // Renew if needed
        var webhookUrl = _configuration["EmailIntegration:Outlook:WebhookUrl"];
        try
        {
            await emailService.SetupWebhookAsync(account.Id, webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to renew subscription for account {AccountId}", account.Id);
        }
    }
}
```

#### 3. Webhook Validation
Microsoft Graph sends a validation token that must be returned immediately:

```csharp
if (notification.ContainsKey("validationToken"))
{
    var validationToken = notification["validationToken"]?.ToString();
    return Content(validationToken ?? "", "text/plain");
}
```

This is already implemented in `EmailWebhookController`.

### Webhook Security

#### Gmail (Pub/Sub)
- Verify message signatures using Google Cloud Pub/Sub authentication
- Use service account authentication
- Validate message format

#### Outlook (Microsoft Graph)
- Validate webhook signature (if `EMAIL_WEBHOOK_SECRET` is configured)
- Verify subscription IDs match expected accounts
- Rate limit webhook processing

## Testing Webhooks

### Gmail Testing
1. Send a test email to the connected Gmail account
2. Check webhook logs for Pub/Sub message
3. Verify email appears in Direct Messages

### Outlook Testing
1. Send a test email to the connected Outlook account
2. Check webhook logs for Microsoft Graph notification
3. Verify email appears in Direct Messages

## Troubleshooting

### Gmail Webhooks Not Working
- Verify Pub/Sub topic and subscription exist
- Check service account permissions
- Verify webhook URL is publicly accessible
- Check Gmail API quota limits

### Outlook Webhooks Not Working
- Verify subscription was created successfully
- Check subscription expiration date
- Ensure webhook URL is HTTPS and publicly accessible
- Verify Microsoft Graph API permissions

### Emails Not Converting to DMs
- Check that both sender and recipient are users in the same organization
- Verify email addresses match User.Email exactly (case-insensitive)
- Check logs for conversion errors
- Ensure EmailToDmService is properly registered

## Production Considerations

1. **Rate Limiting**: Implement rate limiting on webhook endpoints
2. **Error Handling**: Implement retry logic for failed webhook processing
3. **Monitoring**: Add logging and monitoring for webhook events
4. **Security**: Implement proper webhook signature validation
5. **Scalability**: Consider using message queues for webhook processing

