# Fixing Google OAuth Error 400 - Redirect URI Configuration

## The Problem
Google is returning Error 400 because the redirect URI in your OAuth request doesn't match what's configured in Google Cloud Console. Google requires **exact matches** including:
- `http://` vs `https://`
- Port number (`:5092`)
- Exact path (`/api/email-oauth/gmail/callback`)
- No trailing slashes

## Current Redirect URI
Your app is generating: `http://localhost:5092/api/email-oauth/gmail/callback`

## Steps to Fix

### 1. Set Environment Variable (Recommended)
Set the redirect URI explicitly in your environment:

**Windows PowerShell:**
```powershell
$env:GMAIL_REDIRECT_URI = "http://localhost:5092/api/email-oauth/gmail/callback"
```

**Windows CMD:**
```cmd
set GMAIL_REDIRECT_URI=http://localhost:5092/api/email-oauth/gmail/callback
```

### 2. Configure Google Cloud Console

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project (or create one)
3. Navigate to **APIs & Services** → **Credentials**
4. Click on your **OAuth 2.0 Client ID** (or create one)
5. Under **Authorized redirect URIs**, click **+ ADD URI**
6. Add **EXACTLY** this: `http://localhost:5092/api/email-oauth/gmail/callback`
   - Must be `http://` (not `https://`) for localhost
   - Must include port `:5092`
   - Must match the path exactly
7. Click **SAVE**

### 3. Verify Your Client ID and Secret

Make sure these are set in your environment:
- `GMAIL_CLIENT_ID` - Your OAuth 2.0 Client ID from Google Cloud Console
- `GMAIL_CLIENT_SECRET` - Your OAuth 2.0 Client Secret

### 4. Common Mistakes

❌ **Wrong:** `https://localhost:5092/api/email-oauth/gmail/callback` (https)
❌ **Wrong:** `http://localhost/api/email-oauth/gmail/callback` (no port)
❌ **Wrong:** `http://localhost:5092/api/email-oauth/gmail/callback/` (trailing slash)
✅ **Correct:** `http://localhost:5092/api/email-oauth/gmail/callback`

### 5. Check the Logs

After clicking "Connect Gmail", check your application logs. You should see:
```
Generating Gmail OAuth URL with redirect URI: http://localhost:5092/api/email-oauth/gmail/callback
```

Make sure this **exact** URI is in Google Cloud Console.

## For Outlook (Same Process)

1. Set environment variable:
   ```powershell
   $env:OUTLOOK_REDIRECT_URI = "http://localhost:5092/api/email-oauth/outlook/callback"
   ```

2. In Azure Portal → App Registrations → Your App → Authentication:
   - Add platform: **Web**
   - Redirect URI: `http://localhost:5092/api/email-oauth/outlook/callback`

## Quick Test

After configuring, try connecting again. The OAuth popup should now work instead of showing Error 400.

