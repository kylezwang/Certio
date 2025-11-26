# Setup Guide: File and Image Processing

## Quick Start

### 1. Environment Variables

Add these to your `.env` file (for AI agents) and environment configuration:

```bash
# Azure Document Intelligence (Required for OCR)
AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT=https://your-resource.cognitiveservices.azure.com/
AZURE_DOCUMENT_INTELLIGENCE_API_KEY=your_api_key_here

# Azure OpenAI (for vision models)
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your_api_key_here
```

### 2. Create Upload Directory

```bash
# From project root
mkdir -p uploads
chmod 755 uploads
```

### 3. Update appsettings.json

Already configured in `Certio.Web/appsettings.json`:
```json
{
  "FileStorage": {
    "LocalStoragePath": "uploads",
    "MaxFileSizeBytes": 10485760
  }
}
```

### 4. Test It

1. **Start the services**:
```bash
# Terminal 1: Start .NET web app
cd Certio.Web
dotnet run

# Terminal 2: Start AI agents
cd ai_agents
python main.py
```

2. **Upload a document** (via UI or API)

3. **Query the AI**:
"What documents do I have about contracts?"

4. **Test vision** (attach an image in chat):
"What's in this screenshot?"

---

## Azure Document Intelligence Setup

### Step 1: Create Resource

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new resource → AI + Machine Learning → Document Intelligence
3. Choose pricing tier:
   - **Free (F0)**: 500 pages/month free
   - **Standard (S0)**: Pay per page (~$0.0015/page)

### Step 2: Get Credentials

1. Go to your Document Intelligence resource
2. Navigate to "Keys and Endpoint"
3. Copy:
   - **Endpoint** → `AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT`
   - **Key 1** → `AZURE_DOCUMENT_INTELLIGENCE_API_KEY`

### Step 3: Test

```bash
curl -X POST "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT/formrecognizer/documentModels/prebuilt-read:analyze?api-version=2023-07-31" \
  -H "Ocp-Apim-Subscription-Key: YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  --data-binary "@sample.pdf"
```

---

## Azure OpenAI Vision Setup

### GPT-4o Vision

1. Ensure your Azure OpenAI deployment includes `gpt-4o`
2. This model has native vision support
3. No additional configuration needed

### Deployment Names

The system uses these model deployments:
- `gpt-4o` - For vision and complex tasks
- `gpt-4o-mini` - For simple text tasks
- `gpt-4-turbo` - Alternative vision model

---

## File Type Support

### Automatically Processed

| Type | Extension | OCR Support |
|------|-----------|-------------|
| PDF | `.pdf` | ✅ Yes |
| Word | `.docx`, `.doc` | ✅ Yes |
| Excel | `.xlsx` | ✅ Yes |
| PowerPoint | `.pptx` | ✅ Yes |
| Images | `.jpg`, `.png`, `.bmp`, `.tiff`, `.webp` | ✅ Yes (OCR) |
| Text | `.txt` | ✅ Yes |

### Upload Limits

- **File Size**: 10 MB (configurable via `FileStorage:MaxFileSizeBytes`)
- **OCR Size**: 40 MB (configurable via `DocumentExtraction:MaxDocumentSizeMb`)
- **Text Length**: 120,000 characters (configurable via `DocumentExtraction:MaxCharacters`)

---

## Verification Checklist

### ✅ File Upload Works
- [ ] Upload a PDF via Documents page
- [ ] File appears in `uploads/{year}/{month}/{doc-id}/` directory
- [ ] Document shows in Documents list

### ✅ OCR Processing Works
- [ ] Upload an image with text
- [ ] Wait 10-30 seconds for processing
- [ ] Query AI about content in the image
- [ ] AI can reference the extracted text

### ✅ Vision Analysis Works
- [ ] Go to AI Chat
- [ ] Attach an image
- [ ] Ask "What's in this image?"
- [ ] AI provides detailed analysis

### ✅ RAG Integration Works
- [ ] Upload a contract PDF
- [ ] Ask AI "What are the payment terms in my contracts?"
- [ ] AI references specific content from your document

---

## Common Issues

### Issue: "File storage path not configured"
**Solution**: Ensure `FileStorage:LocalStoragePath` is set in `appsettings.json`

### Issue: "Azure Document Intelligence not configured"
**Solution**: Set environment variables:
```bash
export AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT="https://..."
export AZURE_DOCUMENT_INTELLIGENCE_API_KEY="..."
```

### Issue: "No content extracted"
**Solution**: Check:
1. File type is in `AllowedContentTypes`
2. Azure Document Intelligence has credits
3. File is not corrupted

### Issue: Vision not working
**Solution**: 
1. Verify GPT-4o deployment exists
2. Check Azure OpenAI credits
3. Ensure image URL is accessible

### Issue: Permission denied on uploads/
**Solution**:
```bash
chmod -R 755 uploads/
chown -R your-user:your-user uploads/
```

---

## Production Recommendations

### Security
1. ✅ Use HTTPS only
2. ✅ Validate file types (already implemented)
3. ✅ Sanitize filenames (already implemented)
4. ✅ Check file sizes (already implemented)
5. ⚠️ Add virus scanning (recommended)
6. ⚠️ Use Azure Blob Storage (recommended)

### Storage
1. **Local Storage** (current):
   - Good for: Development, small deployments
   - Limits: Single server, no CDN, manual backups

2. **Azure Blob Storage** (recommended):
   - Good for: Production, scale, durability
   - Benefits: CDN, geo-redundancy, automatic backups
   - Migration: Update `FileStorageService` to use Azure SDK

### Performance
1. ✅ Async processing (already implemented)
2. ✅ Background workers (already implemented)
3. ✅ Rate limiting (already implemented)
4. ⚠️ Add caching for extracted content
5. ⚠️ Consider batch processing for large uploads

### Monitoring
1. Log file upload successes/failures
2. Monitor Azure Document Intelligence usage
3. Track OCR accuracy (confidence scores)
4. Alert on storage capacity
5. Monitor AI vision API costs

---

## Cost Estimation

### Azure Document Intelligence
- **Free Tier**: 500 pages/month
- **Standard**: $0.0015 per page
- **Example**: 10,000 pages/month = $15/month

### GPT-4o Vision
- **Input**: $5 per 1M tokens (~$0.005 per request)
- **Output**: $15 per 1M tokens (~$0.015 per response)
- **Images**: $0.085 per high-detail image
- **Example**: 1,000 vision requests/month = ~$100/month

### Storage
- **Local**: Free (server disk space)
- **Azure Blob**: $0.018 per GB/month (~$1.80 for 100 GB)

---

## Next Steps

### Immediate
1. Set up Azure Document Intelligence
2. Test file upload and OCR
3. Test vision in AI chat
4. Monitor usage and costs

### Short-term
1. Add thumbnail generation for images
2. Implement document preview
3. Add progress indicators for processing
4. Create admin dashboard for storage monitoring

### Long-term
1. Migrate to Azure Blob Storage
2. Add advanced OCR features (handwriting, forms)
3. Implement multi-modal RAG (search by image)
4. Add document comparison and version diffing

---

## Support

### Documentation
- Main implementation: `AI_IMAGE_AND_FILE_PROCESSING_IMPLEMENTATION.md`
- Azure Doc Intelligence: https://learn.microsoft.com/azure/ai-services/document-intelligence/
- GPT-4 Vision: https://platform.openai.com/docs/guides/vision

### Troubleshooting
1. Check logs in `Certio.Web` for upload/download errors
2. Check logs in `ai_agents` for processing errors
3. Verify Azure credentials are correct
4. Test with small files first

---

## Success Metrics

You'll know it's working when:
- ✅ Users can upload PDFs and images
- ✅ OCR extracts text from scanned documents
- ✅ AI can answer questions about uploaded content
- ✅ Vision analysis works on attached images
- ✅ RAG returns relevant document chunks
- ✅ No "content unavailable" messages

**Ready to test!** 🚀

