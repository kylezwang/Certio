# Security Fixes Summary

## Date
November 17, 2025

## Overview
Conducted security audit of email sending and join code functionality. Identified and fixed 3 security vulnerabilities.

## Vulnerabilities Fixed

### 1. HIGH - CSRF Protection Missing on Join Code Enrollment
**File**: `Certio.Web/Controllers/HomeController.cs`

**Issue**: The `ApplyJoinCode` endpoint accepted POST requests without CSRF token validation. An attacker could create a malicious website that would silently POST a join code and force an authenticated victim into the attacker's organization.

**Fix**: 
- Added `[Authorize]` attribute for clarity
- Added `[ValidateAntiForgeryToken]` attribute to enforce CSRF protection
- Updated client-side JavaScript in `_Layout.cshtml` to include antiforgery token in request body

**Code Changes**:
```csharp
[Authorize]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ApplyJoinCode([FromBody] ApplyJoinCodeRequest request)
```

---

### 2. HIGH - Email Header Injection via Subject Line
**File**: `Certio.Web/Services/EmailSendingService.cs`

**Issue**: The Gmail raw message was constructed by directly interpolating user-controlled subject lines into email headers. An attacker could inject newline characters (`\r\n`) to add arbitrary email headers, including BCC recipients, or modify the message routing.

**Fix**:
- Created `SanitizeEmailSubject()` method that:
  - Removes all control characters (CR, LF, NULL)
  - Preserves only printable characters and tabs
  - Collapses multiple spaces
  - Enforces 200-character limit
  - Returns safe default if sanitization results in empty string
- Applied sanitization to all email subjects before header construction

**Code Changes**:
```csharp
var rawSubject = metadata["EmailSubject"].ToString() ?? subject;
subject = SanitizeEmailSubject(rawSubject);
```

---

### 3. MEDIUM - Unsanitized HTML in Outbound Emails
**File**: `Certio.Web/Services/EmailSendingService.cs`

**Issue**: Direct message body content was inserted directly into HTML email templates without encoding. This could forward XSS payloads or malicious HTML to external email recipients.

**Fix**:
- HTML-encode both sender name and message body using `System.Net.WebUtility.HtmlEncode()`
- Applied encoding before inserting into HTML template

**Code Changes**:
```csharp
var encodedSenderName = System.Net.WebUtility.HtmlEncode(senderName);
var encodedBody = System.Net.WebUtility.HtmlEncode(directMessage.Body);
```

---

## Additional Security Enhancement

### Email Account Ownership Verification
**File**: `Certio.Web/Services/EmailSendingService.cs`

**Enhancement**: Added defense-in-depth check to verify that the email account being used to send an email actually belongs to the message sender.

**Code**:
```csharp
// Security check: Verify email account belongs to the message sender
if (emailAccount.UserId != directMessage.SenderId)
{
    throw new UnauthorizedAccessException("Email account does not belong to message sender");
}
```

---

## Authorization Policy Verification

Verified that existing authorization policies are correctly applied:

### Controller-Level Authorization
- `DirectMessagesController`: Protected with `[Authorize(Policy = "OrgMember")]`
- `ClientController.AddPeople`: Protected with `[Authorize(Policy = "OrgMember")]` + `[ValidateAntiForgeryToken]`
- `ClientController.GetJoinCode`: Protected with `[Authorize]` + manual access checks

### Method-Level Checks
- `DirectMessagesController.SendEmail`: Verifies thread participation via `IsParticipantAsync()`
- `DirectMessagesController.NotalizeJoinCode`: Validates join code exists and belongs to target organization
- `ClientController.AddPeople` (POST): Validates `CanCreateJoinCodes()` permission

### Domain-Level Authorization
- `User.CanCreateJoinCodes()`: Enforces role-based permissions (Owner, Manager, Lawyer for Clients; Partners and Associates for Law Firms)
- `DirectMessageService.GetOrCreateThreadAsync()`: Validates users share organization or have relationship
- `DirectMessageService.SendAsync()`: Validates participant membership before allowing message send

---

## Testing Recommendations

1. **CSRF Protection**
   - Verify join code enrollment fails without valid antiforgery token
   - Test from different domain/origin to ensure cross-site requests are blocked

2. **Email Header Injection**
   - Test subjects with `\r\n` characters
   - Test subjects with null bytes
   - Verify long subjects are truncated properly

3. **HTML Encoding**
   - Send DM with HTML/script tags and verify they're encoded in email
   - Test with various XSS payloads

4. **Authorization**
   - Verify non-members cannot access org-scoped endpoints
   - Verify users cannot send emails from other users' email accounts
   - Verify join code operations respect organization membership

---

## Security Practices Followed

### 1. Defense in Depth
Multiple layers of security checks:
- Controller-level authorization (`[Authorize(Policy = "OrgMember")]`)
- Method-level validation (participant checks, ownership checks)
- Service-level validation (email account ownership)
- Domain-level permissions (`CanCreateJoinCodes()`)

### 2. Secure by Default
- CSRF protection required on state-changing operations
- Input sanitization applied at service layer
- HTML encoding applied before external output
- Authorization required on all sensitive endpoints

### 3. Principle of Least Privilege
- Users can only perform operations on resources they own or have explicit access to
- Role-based permissions for join code creation
- Thread participation required for messaging operations

### 4. Input Validation
- Email addresses normalized and validated
- Subject lines sanitized for control characters
- HTML content stripped or encoded
- Request parameters validated for required fields

---

## Files Modified

1. `Certio.Web/Controllers/HomeController.cs` - Added CSRF protection
2. `Certio.Web/Views/Shared/_Layout.cshtml` - Updated client to send antiforgery token
3. `Certio.Web/Services/EmailSendingService.cs` - Added subject sanitization, HTML encoding, and ownership check

## Lines of Code Changed
- Added: ~70 lines (including sanitization method)
- Modified: ~15 lines
- Total impact: ~85 lines

## No Breaking Changes
All fixes are backward compatible and do not change the API surface or user experience.

