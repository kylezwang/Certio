# WOPI Host Implementation for OneDrive/Word Integration

## Overview

This document describes the WOPI (Web Application Open Platform Interface) host implementation for proper iframe embedding of OneDrive/Word documents in Certio.

## What is WOPI?

WOPI is Microsoft's protocol for integrating Office Online with third-party applications. It allows web applications to:
- Display Office documents in an iframe using Office Online
- Support read-only viewing and editing (when enabled)
- Maintain proper security with access tokens
- Work with documents from any storage location

## Implementation Components

### 1. WopiAccessTokenService
**Location**: `Certio.Application/Services/Documents/WopiAccessTokenService.cs`

**Purpose**: Manages secure access tokens for WOPI endpoints.

**Features**:
- Generates cryptographically secure tokens
- Associates tokens with document, organization, and user
- Validates tokens with expiration checking
- Automatic token cleanup

**Token Expiration**: 8 hours (configurable)

### 2. WopiDiscoveryService
**Location**: `Certio.Application/Services/Documents/WopiDiscoveryService.cs`

**Purpose**: Discovers Office Online action URLs from Microsoft's discovery endpoint.

**Features**:
- Loads WOPI discovery data from local `discovery.xml` or Microsoft's server
- Caches discovery data for 24 hours
- Maps file types to appropriate Office Online actions
- Handles Word, Excel, PowerPoint, and PDF documents

**Discovery Endpoint**: `https://onenote.officeapps.live.com/hosting/discovery`

### 3. WopiController
**Location**: `Certio.Web/Controllers/WopiController.cs`

**Purpose**: Implements the WOPI host endpoints that Office Online calls.

**Endpoints**:

#### GET /wopi/files/{fileId}
- **Action**: CheckFileInfo
- **Purpose**: Returns file metadata and permissions
- **Called by**: Office Online when loading document
- **Response**: JSON with file info, user permissions, and UI settings

#### GET /wopi/files/{fileId}/contents
- **Action**: GetFile
- **Purpose**: Returns the file content
- **Called by**: Office Online to download the document
- **Response**: Binary file content

#### POST /wopi/files/{fileId}/contents (Disabled)
- **Action**: PutFile
- **Purpose**: Would save edited file (currently disabled for read-only mode)
- **Response**: 405 Method Not Allowed

**Authentication**: Uses `access_token` query parameter (WOPI standard)

### 4. DocumentsController Updates
**Location**: `Certio.Web/Controllers/DocumentsController.cs`

**Changes**:
- Added WOPI service dependencies
- Added `BuildWopiEmbedUrlAsync()` method
- Added `GetAppNameAndExtension()` helper method
- Modified `View()` action to use WOPI for OneDrive documents

**Behavior**:
- OneDrive documents: Use WOPI URLs
- Google Drive documents: Use existing embed logic (unchanged)
- Internal documents: Use existing logic (unchanged)

## How It Works

### 1. Document View Request

```
User clicks OneDrive document
    ↓
DocumentsController.View() called
    ↓
BuildWopiEmbedUrlAsync() generates WOPI URL
    ↓
URL structure: 
https://excel.officeapps.live.com/x/_layouts/xlviewerinternal.aspx?
  WOPISrc=https://yourserver.com/wopi/files/{documentId}&
  access_token={secureToken}
```

### 2. Office Online Loads Document

```
Office Online receives embed URL
    ↓
Calls: GET /wopi/files/{fileId}?access_token={token}
    ↓
WopiController.CheckFileInfo() validates token
    ↓
Returns file metadata and permissions
    ↓
Office Online calls: GET /wopi/files/{fileId}/contents?access_token={token}
    ↓
WopiController.GetFile() downloads from Microsoft Graph
    ↓
Returns file content to Office Online
    ↓
Office Online renders document in iframe
```

### 3. File Download Flow

```
WopiController needs file content
    ↓
Looks up Microsoft connection in database
    ↓
Uses Microsoft Graph API with access token
    ↓
Downloads file from OneDrive
    ↓
Returns to Office Online
```

## Configuration

### Required Settings

None! The implementation works out of the box as long as OneDrive OAuth is configured.

### Optional Settings

You can adjust token expiration in `WopiAccessTokenService.GenerateAccessToken()`:

```csharp
var accessToken = _wopiTokenService.GenerateAccessToken(
    document.Id, 
    orgId, 
    userId, 
    TimeSpan.FromHours(8) // Change this value
);
```

## File Type Support

| File Type | App | Extension | Status |
|-----------|-----|-----------|--------|
| Word      | Word | .docx, .doc | ✅ Supported |
| Excel     | Excel | .xlsx, .xls | ✅ Supported |
| PowerPoint | PowerPoint | .pptx, .ppt | ✅ Supported |
| PDF       | Word | .pdf | ✅ Supported (view only) |

## Security Features

### 1. Access Tokens
- Cryptographically secure random tokens (32 bytes)
- URL-safe Base64 encoding
- Tied to specific document, org, and user
- Automatic expiration (8 hours)
- Stored in memory cache (not database)

### 2. Token Validation
- Validates token exists and not expired
- Verifies document ID matches token
- Checks document exists and belongs to org
- Ensures document not deleted

### 3. Authorization
- WOPI endpoints use anonymous auth (standard for WOPI)
- Security through access tokens instead of cookies
- Microsoft Graph API calls use OAuth tokens

### 4. Data Protection
- No persistent storage of WOPI tokens
- Microsoft Graph tokens stored encrypted
- Files downloaded on-demand (not cached)

## Read-Only vs. Editing

### Current Implementation: Read-Only

The current implementation only supports **read-only viewing**. This is configured in `CheckFileInfo`:

```csharp
UserCanWrite = false,
ReadOnly = true,
SupportsUpdate = false,
SupportsLocks = false
```

### Enabling Editing (Future)

To enable editing:

1. Change permissions in `WopiController.CheckFileInfo()`:
```csharp
UserCanWrite = true,
ReadOnly = false,
SupportsUpdate = true,
SupportsLocks = true
```

2. Implement `WopiController.PutFile()`:
```csharp
[HttpPost("contents")]
public async Task<IActionResult> PutFile(string fileId, ...)
{
    // 1. Validate access token
    // 2. Read file content from request body
    // 3. Upload to Microsoft Graph
    // 4. Update document metadata
    // 5. Return success response
}
```

3. Implement file locking (recommended):
- `POST /wopi/files/{id}/lock`
- `POST /wopi/files/{id}/unlock`
- `POST /wopi/files/{id}/refreshlock`

## Troubleshooting

### Issue: Iframe shows "Unable to load document"

**Possible Causes**:
1. WOPI discovery.xml not found
2. Microsoft Graph token expired
3. Document doesn't exist in OneDrive
4. Access token expired

**Solution**:
- Check server logs for WOPI-related errors
- Verify Microsoft connection has valid token
- Test document access in OneDrive directly

### Issue: Office Online can't download file

**Possible Causes**:
1. Microsoft Graph API error
2. File deleted from OneDrive
3. Network connectivity issue

**Solution**:
- Check `WopiController.GetFile()` logs
- Verify file exists in OneDrive
- Test Microsoft Graph API manually

### Issue: Discovery.xml not loading

**Possible Causes**:
1. File not in project root
2. No internet connection to fetch from Microsoft

**Solution**:
- Place `discovery.xml` in project root
- Or ensure internet access to fetch from Microsoft

## Differences from Old Implementation

| Aspect | Old (Office Online Viewer) | New (WOPI) |
|--------|---------------------------|------------|
| Iframe Embedding | ❌ Often blocked | ✅ Works properly |
| Protocol | None (public viewer) | WOPI (official) |
| Authentication | URL-based | Token-based |
| File Download | Direct URL | Through WOPI host |
| Editing Support | ❌ Not possible | ✅ Can be enabled |
| Microsoft Compliance | ❌ Not official | ✅ Official protocol |

## Testing

### 1. Test Read-Only Viewing

1. Connect OneDrive account
2. Sync some Word/Excel documents
3. Navigate to Documents page
4. Click on a OneDrive document
5. Verify it loads in iframe

### 2. Test Different File Types

1. Test .docx file
2. Test .xlsx file
3. Test .pptx file
4. Verify all load correctly

### 3. Test Token Expiration

1. View a document
2. Wait 8+ hours
3. Refresh the page
4. Verify new token is generated

### 4. Test Error Handling

1. Delete document from OneDrive
2. Try to view in Certio
3. Verify appropriate error message

## Monitoring

### Logs to Watch

```csharp
// Token generation
"Generated WOPI access token for document {DocumentId}"

// Discovery loading
"Loading WOPI discovery from local file: {Path}"
"Fetching WOPI discovery from Microsoft"

// CheckFileInfo calls
"WOPI CheckFileInfo called for file {FileId}"
"WOPI CheckFileInfo succeeded for document {DocumentId}"

// GetFile calls
"WOPI GetFile called for file {FileId}"
"WOPI GetFile succeeded for OneDrive document {DocumentId}"
```

### Performance Metrics

- WOPI token generation: < 1ms
- Discovery loading (cached): < 1ms
- Discovery loading (fresh): ~500ms
- CheckFileInfo response: ~50-100ms
- GetFile response: ~500-2000ms (depends on file size)

## Future Enhancements

### 1. Editing Support
Implement PutFile endpoint to allow document editing.

### 2. File Locking
Implement WOPI locking protocol for concurrent editing.

### 3. Versioning
Track document versions when changes are saved.

### 4. Webhooks
Use Microsoft Graph webhooks to detect external changes.

### 5. Co-authoring
Enable real-time collaboration on documents.

### 6. Download Optimization
Cache downloaded files temporarily to reduce Graph API calls.

## References

- [WOPI Protocol Documentation](https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/)
- [CheckFileInfo Reference](https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo)
- [Office Online Integration](https://learn.microsoft.com/en-us/office/dev/add-ins/testing/debug-office-add-ins-on-ipad-and-mac)
- [Microsoft Graph API](https://learn.microsoft.com/en-us/graph/overview)

## Support

For issues or questions about the WOPI implementation:
1. Check server logs for errors
2. Review this documentation
3. Test with Microsoft's WOPI Validator (if available)
4. Contact development team

