# AI Image and File Processing Implementation

## Overview
Implemented comprehensive image and file processing support for the AI/RAG pipeline, enabling the system to extract and analyze content from user-uploaded files and images.

## What Was Fixed

### The Problem
1. **No File Storage**: Uploaded files were saved to the database but the actual file bytes were lost (no physical storage)
2. **No Content Extraction**: Internal uploads were skipped for Azure Document Intelligence processing - only GoogleDrive/OneDrive files were processed
3. **No Vision Support**: AI agents couldn't analyze images directly - no GPT-4 Vision integration
4. **Incomplete RAG Pipeline**: Document content wasn't being included in the RAG context for AI responses

### The Solution
Implemented a complete file processing pipeline with:
- Local file storage system
- Azure Document Intelligence for text/image OCR
- GPT-4 Vision for direct image analysis
- Full RAG integration

---

## Architecture

### 1. File Storage Layer (`FileStorageService`)
**Location**: `Certio.Application/Services/Documents/FileStorageService.cs`

**Features**:
- Stores uploaded files in organized directory structure: `{year}/{month}/{documentId}/`
- Sanitizes filenames to prevent security issues
- Supports file retrieval, existence checks, and deletion
- Automatic cleanup of empty directories
- Configurable storage path

**Storage Structure**:
```
uploads/
  └── 2025/
      └── 11/
          └── {document-guid}/
              └── {document-guid}_{sanitized-filename}
```

### 2. Document Content Extraction
**Location**: `Certio.Application/Services/Documents/DocumentContentService.cs`

**Updated to Process Internal Uploads**:
```csharp
// Before: Internal uploads were skipped
DocumentSourceType.InternalUpload => DocumentContentResult.Empty("internal_upload_not_supported")

// After: Full processing with Azure Document Intelligence
DocumentSourceType.InternalUpload => await FetchFromInternalUploadAsync(document, versionId, cancellationToken)
```

**Processing Flow**:
1. Retrieve file from storage using `FileStorageService`
2. Send to Azure Document Intelligence for OCR/text extraction
3. Extract text from PDFs, images, Office docs
4. Return sanitized content for embedding

**Supported File Types** (via Azure Document Intelligence):
- PDFs (`application/pdf`)
- Word documents (`.docx`, `.doc`)
- Excel spreadsheets (`.xlsx`)
- PowerPoint presentations (`.pptx`)
- Images with OCR:
  - JPEG/JPG (`image/jpeg`, `image/jpg`)
  - PNG (`image/png`)
  - BMP (`image/bmp`)
  - TIFF (`image/tiff`)
  - WebP (`image/webp`)
- Plain text (`text/plain`)

### 3. Document Upload with Storage
**Location**: `Certio.Web/Controllers/DocumentsApiController.cs`

**Enhanced Upload Flow**:
```csharp
POST /api/documents/upload
```

**Process**:
1. Create document record in database (get ID)
2. **Store actual file content** using `FileStorageService`
3. Save storage path in `DocumentVersion.StorageUrl`
4. Queue for embedding/AI processing
5. Azure Document Intelligence extracts content
6. Content is chunked and vectorized for RAG

**Returns**: `{ document.Id, storagePath }`

### 4. Document Download
**Location**: `Certio.Web/Controllers/DocumentsApiController.cs`

**New Endpoint**:
```csharp
GET /api/documents/{documentId}/download
```

**Features**:
- Retrieves stored files for internal uploads
- Redirects to external URLs for GoogleDrive/OneDrive
- Proper content-type and filename headers
- Authorization checks

### 5. AI Vision Model Support
**Location**: `ai_agents/main.py`

**New Capabilities**:
- **Image Processing Functions**:
  - `encode_image_to_base64()` - Encode images for API
  - `is_image_url()` - Detect image attachments
  - `prepare_vision_message()` - Format for GPT-4 Vision

- **Vision Model Integration**:
  - Automatically uses GPT-4o when images are attached
  - Supports multiple images per request
  - High-detail image analysis
  - Integrated with RAG context

**API Update**:
```python
POST /agents/conversational-response
{
  "user_message": "What's in this image?",
  "attachments": [
    {
      "url": "https://example.com/image.jpg",
      "mime_type": "image/jpeg",
      "document_id": "abc-123"
    }
  ],
  // ... other fields
}
```

**Vision Processing Flow**:
1. Detect image attachments in request
2. Extract image URLs
3. Automatically switch to GPT-4o (vision model)
4. Prepare vision message format
5. Include images with text prompt
6. Return comprehensive analysis

---

## Configuration

### appsettings.json

**File Storage Configuration**:
```json
{
  "FileStorage": {
    "LocalStoragePath": "uploads",
    "MaxFileSizeBytes": 10485760  // 10 MB
  }
}
```

**Document Extraction Configuration** (updated with image support):
```json
{
  "DocumentExtraction": {
    "Endpoint": "${AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT}",
    "ApiKey": "${AZURE_DOCUMENT_INTELLIGENCE_API_KEY}",
    "ModelId": "prebuilt-read",
    "MaxDocumentSizeMb": 40,
    "MaxCharacters": 120000,
    "OperationTimeout": "00:00:45",
    "AllowedContentTypes": [
      "application/pdf",
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      "application/msword",
      "text/plain",
      "image/jpeg",
      "image/jpg",
      "image/png",
      "image/bmp",
      "image/tiff",
      "image/webp"
    ]
  }
}
```

### Service Registration

**Location**: `Certio.Web/Program.cs`

```csharp
// File Storage
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// Document Content Service (updated with FileStorageService dependency)
builder.Services.AddScoped<IDocumentContentService>(sp => {
    // ... dependencies
    var fileStorageService = sp.GetRequiredService<IFileStorageService>();
    return new DocumentContentService(/* ... */, fileStorageService, logger);
});
```

---

## RAG Integration Flow

### Complete Pipeline

1. **User Uploads File**
   ```
   User -> Upload Endpoint -> Store File -> Save Metadata -> Queue for Processing
   ```

2. **Background Processing**
   ```
   Embedding Worker -> Retrieve File -> Azure Doc Intelligence -> Extract Text
   ```

3. **Text Extraction & OCR**
   ```
   Azure Document Intelligence:
   - PDFs: Extract text and layout
   - Images: Perform OCR with confidence scores
   - Office Docs: Extract content and metadata
   ```

4. **Chunking & Vectorization**
   ```
   Extracted Content -> Chunk (overlap strategy) -> Generate Embeddings -> Store in Vector DB
   ```

5. **AI Query with RAG**
   ```
   User Question -> Vector Search -> Retrieve Relevant Chunks -> Include in Context -> AI Response
   ```

### RAG Context Enhancement

**Before**: Only metadata was indexed
```
Title: document.pdf
Category: General
Status: Draft
SanitizedContent: [Content unavailable – secure extraction returned no body]
```

**After**: Full content is extracted and indexed
```
Title: document.pdf
Category: General
Status: Draft
SanitizedContent: 
[Actual extracted text from the document with OCR from images]
This is the content that was in the PDF including text from embedded images...
```

### AI Vision Analysis

**Direct Image Analysis** (GPT-4 Vision):
- User attaches image in chat
- AI receives image directly
- Analyzes visual content, diagrams, screenshots
- Provides contextual legal analysis

**Document OCR** (Azure Document Intelligence):
- User uploads scanned document
- Text is extracted via OCR
- Content is indexed for RAG
- AI can search and reference the text

**Combined Power**: 
- OCR provides searchable text corpus
- Vision provides real-time visual analysis
- RAG connects context across documents

---

## Usage Examples

### 1. Upload a Document
```http
POST /api/documents/upload
Content-Type: multipart/form-data

orgId: {org-guid}
matterId: {matter-guid}
category: "Contract"
isPrivate: false
tags: ["legal", "contract"]
file: [binary file data]
```

**Response**:
```json
{
  "id": "abc-123-def-456",
  "storagePath": "2025/11/abc-123-def-456/abc-123-def-456_contract.pdf"
}
```

### 2. Download a Document
```http
GET /api/documents/{documentId}/download
Authorization: Bearer {token}
```

**Response**: Binary file with proper content-type header

### 3. Chat with Image Analysis
```http
POST /agents/conversational-response

{
  "conversation_id": "conv-123",
  "user_message": "Can you review this contract clause and tell me if there are any issues?",
  "user_type": "Attorney",
  "attachments": [
    {
      "url": "data:image/jpeg;base64,/9j/4AAQSkZJRg...",
      "mime_type": "image/jpeg"
    }
  ]
}
```

**AI Response**: Analyzes the image using GPT-4 Vision and provides legal insights

### 4. Search Documents in RAG
```http
POST /agents/conversational-response

{
  "user_message": "What did the Morrison contract say about payment terms?",
  "document_context": {
    "includeDocuments": true
  }
}
```

**AI Process**:
1. Vector search finds relevant chunks from uploaded documents
2. Includes extracted text in context
3. AI references specific content from the document
4. Provides accurate answer based on actual document content

---

## Benefits

### For Users
✅ **Upload Any Document** - PDFs, images, Office docs all supported
✅ **AI Reads Your Files** - Actual content is extracted and analyzed
✅ **Smart Search** - Find information across all uploaded documents
✅ **Image Analysis** - Show AI screenshots, diagrams, contracts for instant analysis
✅ **Accurate Responses** - AI has access to real document content, not just titles

### For Developers
✅ **Complete Pipeline** - Upload → Storage → OCR → Embedding → RAG → AI
✅ **Proper Separation** - Storage, extraction, and AI layers are decoupled
✅ **Scalable** - Can easily migrate to Azure Blob Storage
✅ **Secure** - Proper auth checks, sanitized filenames, encrypted tokens
✅ **Extensible** - Easy to add new file types or processing methods

### Technical Improvements
✅ **Files Are Actually Stored** - No more lost uploads
✅ **OCR on Images** - Scanned documents become searchable
✅ **Vision Models** - Real-time visual analysis
✅ **Rich RAG Context** - Full document content in search results
✅ **Cost Optimized** - Smart model selection (GPT-4o for vision, GPT-4o-mini for simple text)

---

## Migration Notes

### Existing Documents
- Documents uploaded before this update won't have stored files
- They will show "file not found" errors on download
- Re-upload needed to enable new features
- Consider adding a migration script to download from GoogleDrive/OneDrive and store locally

### Azure Document Intelligence
- **Required** for OCR and document processing
- Set environment variables:
  - `AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT`
  - `AZURE_DOCUMENT_INTELLIGENCE_API_KEY`
- Model: `prebuilt-read` (supports images, PDFs, Office docs)
- Pricing: ~$0.0015 per page

### File Storage
- Default: `./uploads` directory (relative to app root)
- Production: Set `FileStorage:LocalStoragePath` to absolute path
- Consider Azure Blob Storage for production (already structured for migration)

---

## Testing

### 1. Upload a PDF
```bash
curl -X POST http://localhost:5000/api/documents/upload \
  -H "Authorization: Bearer {token}" \
  -F "orgId={org-guid}" \
  -F "category=Test" \
  -F "file=@test-document.pdf"
```

### 2. Verify Storage
- Check `uploads/{year}/{month}/{document-id}/` directory
- File should exist with sanitized name

### 3. Test OCR
- Upload an image with text
- Wait for embedding processing
- Query AI about content in the image
- AI should be able to reference the extracted text

### 4. Test Vision
- Send a chat message with image attachment
- AI should analyze the visual content
- Response should include specific details from the image

---

## Troubleshooting

### "File not found" on download
- **Cause**: Document was uploaded before this implementation
- **Fix**: Re-upload the document or check storage path configuration

### "No content extracted"
- **Cause**: Azure Document Intelligence not configured or file type not supported
- **Fix**: Check environment variables and `AllowedContentTypes` config

### Vision API errors
- **Cause**: Model doesn't support vision or quota exceeded
- **Fix**: Ensure using `gpt-4o` or `gpt-4-turbo` with vision enabled

### Storage path errors
- **Cause**: Insufficient permissions or invalid path
- **Fix**: Ensure app has write access to `FileStorage:LocalStoragePath`

---

## Future Enhancements

### Planned Improvements
1. **Azure Blob Storage** - Migrate from local storage to cloud
2. **Thumbnail Generation** - Preview images for documents
3. **Batch Processing** - Process multiple files at once
4. **Advanced OCR** - Support for handwriting recognition
5. **Multi-modal RAG** - Search by image similarity
6. **Document Comparison** - Visual diff between versions
7. **Real-time Collaboration** - Live document annotation with AI

### Architecture Ready For
- ✅ Cloud storage migration (abstracted via `IFileStorageService`)
- ✅ Advanced OCR models (configurable `ModelId`)
- ✅ Multiple AI providers (abstracted via model selection)
- ✅ Horizontal scaling (stateless file operations)

---

## Summary

**What We Built**: A complete file and image processing pipeline that makes your AI actually read and understand user uploads.

**Key Achievement**: Closed the gap where files were uploaded but never processed. Now:
- Files are stored securely
- Content is extracted with OCR
- Text is searchable via RAG
- Images can be analyzed directly by AI

**The Result**: Your AI went from being blind to uploaded content to having full visibility into documents, images, and file attachments. Lock in. 🔥

