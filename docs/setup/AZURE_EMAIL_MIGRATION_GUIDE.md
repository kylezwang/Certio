# Azure Communication Services Email Migration Guide

**Date:** January 2025  
**Purpose:** Migrate from SendGrid to Azure Communication Services Email  
**Sender Address:** `info@notal.org`  
**Implementation:** SMTP (using existing MailKit code)

---

## 📋 Overview

### What This Migration Does
- Replaces SendGrid with Azure Communication Services Email
- Sends system emails (2FA, password reset, change notices) from `info@notal.org`
- Uses SMTP (no REST API changes needed)
- Keeps existing MailKit implementation

### Cost Comparison

| Monthly Volume | SendGrid | Azure ACS Email | Savings |
|----------------|----------|-----------------|---------|
| 1,000 emails   | $20      | $0.25           | $19.75  |
| 10,000 emails  | $20      | $2.50           | $17.50  |
| 50,000 emails  | $20      | $12.50          | $7.50   |
| 100,000 emails | $40+     | $25.00          | $15+    |

**Azure Pricing:** $0.25 per 1,000 emails + $0.12 per GB data transfer

---

## 🔧 Azure Setup Steps

### Step 1: Create Email Communication Service (ECS)

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource**
3. Search for **"Email Communication Services"**
4. Click **Create**
5. Fill in:
   - **Resource group:** (use existing or create new)
   - **Name:** `certio-email-service` (or your preference)
   - **Location:** `Global`
   - **Data location:** `United States` (must match your Communication Service)
6. Click **Review + create** → **Create**
7. Wait for deployment (2-3 minutes)

### Step 2: Create Communication Services Resource (if not exists)

1. In Azure Portal, search for **"Communication Services"**
2. If you don't have one, click **Create**
3. Fill in:
   - **Resource group:** Same as ECS
   - **Name:** `certio-communication-service`
   - **Data location:** `United States` (MUST match ECS data location)
4. Click **Create**

**⚠️ IMPORTANT:** ECS and Communication Service must be in the **same data geography** or domain connection will fail.

### Step 3: Add and Verify Domain (notal.org)

1. Go to your **Email Communication Service** resource
2. In left menu: **Provision domains** → **Add domain**
3. Select **Custom domain**
4. Enter: `notal.org`
5. Click **Add**

#### 3a. Domain Ownership Verification (TXT Record)

1. In the domain details, you'll see a **TXT record** to add:
   - **Type:** TXT
   - **Host/Name:** `@` (or zone apex)
   - **Value:** `ms-domain-verification=<GUID>` (provided by Azure)

2. Add this TXT record to your DNS provider (where notal.org is hosted)

3. Wait 15-30 minutes for DNS propagation

4. Click **Verify** in Azure portal

**Verify DNS:**
```bash
nslookup -q=TXT notal.org
# Should show: ms-domain-verification=<GUID>
```

#### 3b. Configure SPF Record (TXT)

1. After ownership is verified, Azure will show **SPF configuration**
2. Add this TXT record to your DNS:
   - **Type:** TXT
   - **Host/Name:** `@`
   - **Value:** `v=spf1 include:spf.protection.outlook.com -all`
   
   **⚠️ CRITICAL:** Must use `-all` (not `~all`), or verification will fail.

3. Wait 15-30 minutes, then click **Verify** in Azure

#### 3c. Configure DKIM Records (CNAME)

1. Azure will show **two DKIM CNAME records** to add:

   **Record 1:**
   - **Type:** CNAME
   - **Host/Name:** `selector1-azurecomm-prod-net._domainkey`
   - **Target/Value:** `selector1-azurecomm-prod-net._domainkey.azurecomm.net`

   **Record 2:**
   - **Type:** CNAME
   - **Host/Name:** `selector2-azurecomm-prod-net._domainkey`
   - **Target/Value:** `selector2-azurecomm-prod-net._domainkey.azurecomm.net`

2. Add both CNAME records to your DNS

3. **⚠️ IMPORTANT:** If using Cloudflare or similar proxy, ensure these CNAMEs are **DNS-only** (not proxied)

4. Wait 15-30 minutes, then click **Verify** in Azure

**Verify DKIM:**
```bash
nslookup selector1-azurecomm-prod-net._domainkey.notal.org
nslookup selector2-azurecomm-prod-net._domainkey.notal.org
```

#### 3d. Optional: DMARC Record (Recommended)

Add for better deliverability:
- **Type:** TXT
- **Host/Name:** `_dmarc`
- **Value:** `v=DMARC1; p=none; rua=mailto:dmarc@notal.org; ruf=mailto:dmarc@notal.org; fo=1`

Start with `p=none`, then move to `p=quarantine` or `p=reject` once stable.

### Step 4: Connect Domain to Communication Service

1. Go to your **Communication Services** resource (not ECS)
2. Left menu: **Email** → **Domains**
3. Click **Connect domain**
4. Select your verified `notal.org` domain
5. Click **Connect**

**⚠️ If "Connect" is disabled:**
- Ensure domain is fully verified (TXT, SPF, DKIM all green)
- Ensure ECS and Communication Service are in same data geography
- Wait 15-30 minutes and retry (backend sync can take time)

### Step 5: Create MailFrom Address (info@notal.org)

1. Go back to **Email Communication Service** resource
2. Left menu: **Provision domains** → Select `notal.org`
3. Click **MailFrom addresses** tab
4. Click **+ Add**
5. Fill in:
   - **MailFrom address (local part):** `info`
   - **Display name:** `Notal` (optional)
6. Click **Save**

**⚠️ If "+ Add" button is disabled:**
- You may need a quota increase. Generate some test traffic first, then open Azure support ticket requesting "Email quota increase" to enable MailFrom management.

**Alternative (Azure CLI):**
```bash
az communication email domain sender-username create \
  --email-service-name "certio-email-service" \
  --resource-group "your-rg" \
  --domain-name "notal.org" \
  --sender-username "info" \
  --username "info" \
  --subscription "your-sub-id"
```

### Step 6: Set Up SMTP Authentication

Azure ACS Email uses Microsoft Entra (Azure AD) for SMTP authentication.

#### 6a. Create Entra Application

1. Go to [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID**
2. Left menu: **App registrations** → **New registration**
3. Fill in:
   - **Name:** `certio-email-smtp`
   - **Supported account types:** Single tenant
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**

#### 6b. Create Client Secret

1. In your app registration, left menu: **Certificates & secrets**
2. Click **New client secret**
3. Fill in:
   - **Description:** `SMTP authentication`
   - **Expires:** 24 months (or your preference)
4. Click **Add**
5. **⚠️ COPY THE SECRET VALUE IMMEDIATELY** (you won't see it again)
   - This is your `SMTP_PASSWORD`

#### 6c. Grant Permissions to Communication Service

1. Go to your **Communication Services** resource
2. Left menu: **Access control (IAM)**
3. Click **Add** → **Add role assignment**
4. Select role: **Communication and Email Service Owner**
5. Click **Next**
6. **Assign access to:** Managed identity or User, group, or service principal
7. Click **Select members** → Find your app: `certio-email-smtp`
8. Click **Select** → **Review + assign**

#### 6d. Create SMTP Username in Communication Service

1. Go to your **Communication Services** resource
2. Left menu: **Email** → **SMTP Usernames**
3. Click **+ Add**
4. Fill in:
   - **SMTP Username:** `info@notal.org` (or any identifier you prefer)
   - **Application (client) ID:** (from Step 6a)
5. Click **Add**
6. Wait for status to show **"Ready to use"**

**Your SMTP credentials:**
- **Host:** `smtp.azurecomm.net`
- **Port:** `587`
- **Username:** (the SMTP username you created, e.g., `info@notal.org`)
- **Password:** (the Entra app client secret from Step 6b)
- **TLS/STARTTLS:** Required (TLS 1.2+)

---

## 💻 Code Changes

### Files Modified

1. `Certio.Web/Services/EmailSendingService.cs` - Remove SendGrid header, add Azure support
2. `Certio.Web/appsettings.json` - Update Provider to "Azure"
3. `Certio.Web/Program.cs` - (No changes needed, already reads env vars)

### Changes Made

✅ Removed SendGrid-specific `X-SMTPAPI` header (Azure doesn't need it)  
✅ Added support for "Azure" provider (in addition to "SendGrid" for backward compatibility)  
✅ Updated configuration to use Azure SMTP settings

---

## 🔐 Environment Variables

Update your environment variables (`.env`, Azure App Service, etc.):

```env
# === AZURE COMMUNICATION SERVICES EMAIL ===
# Provider
Security__TwoFactorEmail__Provider=Azure

# SMTP Settings
SMTP_HOST=smtp.azurecomm.net
SMTP_PORT=587
SMTP_SECURE=false
SMTP_USER=info@notal.org
SMTP_PASSWORD=your_entra_app_client_secret_here
SMTP_FROM_EMAIL=info@notal.org

# From Email/Name (used as fallback)
Security__TwoFactorEmail__FromEmail=info@notal.org
Security__TwoFactorEmail__FromName=Notal
```

**Remove (no longer needed):**
```env
SENDGRID_API_KEY=...
```

---

## ✅ Testing Checklist

### 1. Verify DNS Records
```bash
# Domain ownership
nslookup -q=TXT notal.org

# SPF
nslookup -q=TXT notal.org | grep spf

# DKIM
nslookup selector1-azurecomm-prod-net._domainkey.notal.org
nslookup selector2-azurecomm-prod-net._domainkey.notal.org
```

### 2. Test SMTP Connection

You can test SMTP manually:
```bash
# Using PowerShell (Windows)
$smtp = New-Object System.Net.Mail.SmtpClient("smtp.azurecomm.net", 587)
$smtp.EnableSsl = $true
$smtp.Credentials = New-Object System.Net.NetworkCredential("info@notal.org", "your-secret")
$smtp.Send("info@notal.org", "your-test@email.com", "Test", "Test body")
```

### 3. Test in Application

1. **Update environment variables** with Azure SMTP settings
2. **Restart application**
3. **Test 2FA email:**
   - Go to login page
   - Enter credentials
   - Request 2FA code
   - Check email inbox
4. **Test password reset:**
   - Go to forgot password page
   - Enter email
   - Check email inbox
5. **Check application logs:**
   - Look for: `System email sent via SMTP to {Email}`
   - No errors about SendGrid

### 4. Verify Email Delivery

- Check recipient inbox (including spam folder)
- Verify sender shows as `info@notal.org`
- Verify links in emails work (no SSL errors)
- Check Azure Communication Services logs in portal

---

## 🐛 Troubleshooting

### "SMTP configuration is incomplete"
- **Check:** All environment variables are set correctly
- **Check:** No `${...}` placeholders in values
- **Check:** `SMTP_HOST` is exactly `smtp.azurecomm.net`

### "Failed to authenticate"
- **Check:** SMTP username matches what you created in Communication Service
- **Check:** Password is the Entra app client secret (not the app ID)
- **Check:** Entra app has "Communication and Email Service Owner" role on Communication Service
- **Check:** SMTP username status is "Ready to use" in portal

### "Domain not connected"
- **Check:** ECS and Communication Service are in same data geography
- **Check:** Domain is fully verified (TXT, SPF, DKIM all green)
- **Check:** You clicked "Connect domain" in Communication Service (not ECS)
- **Wait:** Backend sync can take 15-30 minutes

### "MailFrom address not found"
- **Check:** You created `info@notal.org` as MailFrom in ECS domain settings
- **Check:** MailFrom status is active
- **Check:** If "+ Add" was disabled, you may need quota increase (contact Azure support)

### "SPF verification failed"
- **Check:** SPF record uses `-all` (not `~all`)
- **Check:** Only one SPF record exists at domain apex
- **Check:** DNS has propagated (wait 15-30 minutes)

### "DKIM verification failed"
- **Check:** Both CNAME records are added exactly as shown
- **Check:** CNAMEs are not proxied (Cloudflare orange cloud off)
- **Check:** DNS supports underscores in hostnames
- **Check:** DNS has propagated

### Emails not arriving
- **Check:** Azure Communication Services logs in portal
- **Check:** Recipient spam folder
- **Check:** Sender address matches configured MailFrom exactly
- **Check:** Application logs for SMTP errors

---

## 📊 Monitoring

### Azure Portal
1. Go to **Communication Services** resource
2. **Email** → **Metrics**
3. Monitor:
   - Emails sent
   - Delivery status
   - Bounce rate

### Application Logs
Look for:
- `System email sent via SMTP to {Email}` ✅
- `Failed to send system email via SMTP` ❌
- SMTP authentication errors

### Cost Tracking
- Monitor Azure billing for Communication Services
- Expected: ~$0.25 per 1,000 emails
- Set up budget alerts if needed

---

## 🔄 Rollback Plan

If you need to rollback to SendGrid:

1. **Update environment variables:**
   ```env
   Security__TwoFactorEmail__Provider=SendGrid
   SMTP_HOST=smtp.sendgrid.net
   SMTP_USER=apikey
   SMTP_PASSWORD=your_sendgrid_api_key
   ```

2. **Restart application**

3. **Test email delivery**

The code supports both providers, so rollback is instant.

---

## 📚 References

- [Azure Communication Services Email Documentation](https://learn.microsoft.com/azure/communication-services/concepts/email/email-overview)
- [Add Custom Verified Domain](https://learn.microsoft.com/azure/communication-services/quickstarts/email/add-custom-verified-domains)
- [SMTP Authentication Setup](https://learn.microsoft.com/azure/communication-services/quickstarts/email/send-email-smtp/smtp-authentication)
- [Email Pricing](https://learn.microsoft.com/azure/communication-services/concepts/email-pricing)
- [Troubleshooting Domain Configuration](https://learn.microsoft.com/azure/communication-services/concepts/email/email-domain-configuration-troubleshooting)

---

## ✅ Migration Complete Checklist

- [ ] Email Communication Service created
- [ ] Communication Services resource created (same data geography)
- [ ] Domain `notal.org` added and verified (TXT, SPF, DKIM)
- [ ] Domain connected to Communication Service
- [ ] MailFrom address `info@notal.org` created
- [ ] Entra application created with client secret
- [ ] Entra app granted "Communication and Email Service Owner" role
- [ ] SMTP username created in Communication Service
- [ ] Environment variables updated
- [ ] Code changes deployed
- [ ] Application restarted
- [ ] 2FA email tested successfully
- [ ] Password reset email tested successfully
- [ ] Change notice emails tested successfully
- [ ] Monitoring configured
- [ ] SendGrid subscription cancelled (optional)

---

**Estimated Setup Time:** 2-3 hours (mostly waiting for DNS propagation)  
**Code Changes:** Minimal (already SMTP-compatible)  
**Downtime:** None (can run both providers in parallel during migration)
