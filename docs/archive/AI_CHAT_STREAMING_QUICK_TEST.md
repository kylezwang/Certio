# AI Chat Streaming - Quick Testing Guide

## Prerequisites

1. **AI Agent Service Running**
   ```bash
   cd ai_agents
   python main.py
   ```
   Expected output:
   ```
   ✅ Using Enhanced RAG System with onboarding knowledge and intent detection
   🔵 Using Azure OpenAI
   INFO:     Started server process
   INFO:     Uvicorn running on http://0.0.0.0:8000
   ```

2. **Web Application Running**
   ```bash
   dotnet run --project Certio.Web
   ```

## Test Cases

### Test 1: Simple Greeting (GPT-4o-mini, Fast)

**Input:** "Hi there!"

**Expected Behavior:**
- ✅ Streaming placeholder appears immediately
- ✅ Blinking cursor visible
- ✅ Response streams in word-by-word
- ✅ Completes in ~1-2 seconds
- ✅ Cursor disappears on completion
- ✅ Message saved to database

**Expected Response Pattern:**
```
Hello! [cursor blinks] I'm Notal [cursor] AI, your friendly [cursor] legal assistant...
```

### Test 2: Complex Legal Question (GPT-4o, RAG-Enhanced)

**Input:** "What are the key legal considerations when incorporating a startup?"

**Expected Behavior:**
- ✅ Streaming placeholder appears
- ✅ Response streams with rich content
- ✅ Takes ~3-5 seconds (more content)
- ✅ RAG knowledge included (business formation, legal areas)
- ✅ HTML formatting preserved (paragraphs, lists)
- ✅ Completes successfully

**Expected Response Pattern:**
```
<p>When incorporating a startup, there are several key legal considerations...</p>
<ul>
  <li><strong>Business Structure:</strong> Choose between LLC, C-Corp, S-Corp...</li>
  <li><strong>Intellectual Property:</strong> Protect your innovations...</li>
  ...
</ul>
```

### Test 3: Multiple Messages in Sequence

**Input Sequence:**
1. "Hello!"
2. "What is Notal?"
3. "How do I create a matter?"

**Expected Behavior:**
- ✅ Each message streams independently
- ✅ No interference between streams
- ✅ Sticky message feature still works
- ✅ All messages saved correctly
- ✅ Proper message ordering maintained

### Test 4: Error Handling (Stop AI Service Mid-Stream)

**Steps:**
1. Start asking a question
2. Stop the AI agent service (Ctrl+C)
3. Continue in browser

**Expected Behavior:**
- ✅ Error message appears in chat
- ✅ No browser console errors
- ✅ User can send new message after restarting service
- ✅ Graceful degradation

**Expected Error Message:**
```
I apologize, but I'm experiencing technical difficulties right now. 
Please try again in a moment.
```

### Test 5: Long Response Streaming

**Input:** "Explain in detail the process of corporate merger and acquisition, including due diligence, valuation, and regulatory compliance."

**Expected Behavior:**
- ✅ Long response streams smoothly
- ✅ Auto-scroll follows streaming text
- ✅ No lag or stuttering
- ✅ Complete response renders with formatting
- ✅ Performance remains good

### Test 6: RAG System Integration

**Input:** "Where can I find the calendar in Notal?"

**Expected Behavior:**
- ✅ Response includes RAG-enhanced knowledge
- ✅ Mentions specific UI locations
- ✅ Provides navigation instructions
- ✅ Streams smoothly with RAG context

**Expected Response Pattern:**
```
You can find the calendar in Notal by clicking on the "Calendar" 
icon in the left sidebar. From there, you can...
```

## Browser Console Checks

Open Developer Tools (F12) and check:

### No Errors
```javascript
// Console should show:
Message sent successfully
DEBUG: Analyzing task - user_message='...', context_length=...
// NO red error messages
```

### Network Tab
- ✅ Request to `/Chat/GenerateAIResponseStream`
- ✅ Type: `eventsource` or `text/event-stream`
- ✅ Status: `200 OK`
- ✅ Response streaming (see chunks arriving)

### Console Logs (Optional Debug)
```javascript
// Enable in chat.js if needed:
console.log('Received chunk:', eventData.content);
console.log('Full content so far:', fullContent);
```

## Visual Indicators

### Streaming Active
- ✅ Blinking cursor: `▋` (appears and disappears)
- ✅ Text appears character by character or word by word
- ✅ Smooth auto-scroll

### Streaming Complete
- ✅ Cursor removed
- ✅ Full message displayed
- ✅ Timestamp visible
- ✅ Message clickable/selectable

## Performance Metrics

### Simple Message (GPT-4o-mini)
- **Time to First Chunk:** < 500ms
- **Total Streaming Time:** 1-2 seconds
- **Cost:** ~$0.0003

### Complex Message (GPT-4o)
- **Time to First Chunk:** < 800ms
- **Total Streaming Time:** 3-5 seconds
- **Cost:** ~$0.003-0.015

## Debugging

### If Streaming Doesn't Work

1. **Check AI Agent Service:**
   ```bash
   curl http://localhost:8000/health
   # Should return: {"status":"healthy","agents":[...]}
   ```

2. **Check Browser Console:**
   - Look for fetch errors
   - Check network tab for failed requests
   - Verify SSE format in response

3. **Check C# Logs:**
   - Look for exceptions in console
   - Check for "Error streaming AI response"
   - Verify AIAgentService connection

4. **Check Python Logs:**
   - Look for "Error in streaming response"
   - Check Azure OpenAI connection
   - Verify model deployment names

### Common Issues

**Issue:** No streaming, instant complete response
**Solution:** Check that `/GenerateAIResponseStream` endpoint is being called (not `/GenerateAIResponse`)

**Issue:** Cursor stays forever
**Solution:** Check that `done: true` event is received. Look for errors in Python logs.

**Issue:** Chunks appear but no formatting
**Solution:** Check that HTML content is being passed through (not escaped)

**Issue:** "Model not found" error
**Solution:** Check Azure OpenAI deployment names in environment variables

## Success Criteria

✅ **All tests pass**
✅ **No console errors**
✅ **Smooth streaming experience**
✅ **Proper error handling**
✅ **Messages saved to database**
✅ **Cost optimization working** (GPT-4o-mini for simple, GPT-4o for complex)
✅ **RAG system active** (contextual responses)

## Next Steps After Testing

1. ✅ Test with real users
2. ✅ Monitor performance metrics
3. ✅ Check cost analytics at `/analytics/cost-dashboard`
4. ✅ Review user feedback
5. ✅ Consider enabling streaming by default

## Rollback Plan (If Needed)

If issues arise, the old endpoint is still available:

**In `chat.js`, change:**
```javascript
// Rollback to non-streaming:
const response = await fetch(`/Client/${orgId}/Chat/GenerateAIResponse`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ conversationId, userMessage })
});

const result = await response.json();
if (result.success) {
    addMessageToChat(result.message);
}
```

No backend changes needed - both endpoints coexist!

## Monitoring

### Watch for:
- Response times increasing
- Error rates
- User satisfaction
- Cost trends
- RAG accuracy

### Check Analytics:
```bash
# Cost analytics
curl http://localhost:8000/analytics/cost-dashboard

# Usage analytics
curl http://localhost:8000/analytics/usage

# RAG stats
curl http://localhost:8000/knowledge/stats
```

---

**Happy Testing! 🚀**

The streaming should feel smooth and responsive, like ChatGPT or Cursor. If you see the blinking cursor and text appearing in real-time, it's working perfectly! 🎉

