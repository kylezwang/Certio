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

### 4. Add Test Users (Required for Testing Mode)

If your OAuth app is in **Testing** mode (which it likely is for localhost development), you must add test users who can authorize the app:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project
3. Navigate to **APIs & Services** → **OAuth consent screen**
4. Scroll down to **Test users** section
5. Click **+ ADD USERS**
6. Add the email addresses of any Google accounts you want to test with
   - Example: `your-test-email@gmail.com`
   - You can add multiple test users
7. Click **ADD**
8. Click **SAVE**

**Important:** Only users listed as test users will be able to authorize your app when it's in Testing mode. If you try to authorize with an email that's not in the test users list, Google will show an error.

### 5. Common Mistakes

❌ **Wrong:** `https://localhost:5092/api/email-oauth/gmail/callback` (https)
❌ **Wrong:** `http://localhost/api/email-oauth/gmail/callback` (no port)
❌ **Wrong:** `http://localhost:5092/api/email-oauth/gmail/callback/` (trailing slash)
✅ **Correct:** `http://localhost:5092/api/email-oauth/gmail/callback`

### 6. Check the Logs

After clicking "Connect Gmail", check your application logs. You should see:
```
Generating Gmail OAuth URL with redirect URI: http://localhost:5092/api/email-oauth/gmail/callback
```

Make sure this **exact** URI is in Google Cloud Console.

### 7. Troubleshooting

**If you see "Error 403: access_denied" or "This app isn't verified":**
- Make sure you've added your email as a test user in the OAuth consent screen (Step 4 above)
- Verify your app is in "Testing" mode (not "In production" unless you've published it)

**If you see "Error 400: redirect_uri_mismatch":**
- Double-check the redirect URI matches exactly (including http://, port, and path)
- Make sure you saved the changes in Google Cloud Console

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

