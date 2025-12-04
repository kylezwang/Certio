from fastapi import FastAPI, HTTPException, Depends, Request, status
from pydantic import BaseModel
from typing import List, Dict, Any, Optional, Tuple, Union
from openai import OpenAI
import os
from dotenv import load_dotenv

import json
import logging
import asyncio
from datetime import datetime, timezone
import re
import time
from collections import deque
import secrets
import base64
import mimetypes
from simplified_cost_optimization import (
    SimplifiedModelSelector, TaskComplexityAnalyzer, UsageTracker,
    ModelType, TaskComplexity
)
from context_manager import IntelligentContextManager

# Force deployment trigger for user data sync endpoint
from background_agents import BackgroundAgentManager, TaskPriority
from intelligent_routing import IntelligentRouter, TaskContext, TaskType, ProcessingMethod
from certio_training_pipeline import (
    add_conversation_training_data, update_agent_performance, 
    get_training_recommendations, should_retrain_agent
)
# CostAnalytics removed - using simplified cost tracking in UsageTracker

# Load environment variables
load_dotenv()

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Try to import RAG system, fallback if dependencies not available
try:
    from certio_rag_system_enhanced import (
        enhance_agent_prompt, 
        get_relevant_context, 
        search_project_knowledge,
        enhanced_notal_rag,
        get_knowledge_stats
    )
    logger.info("✅ Using Enhanced RAG System with onboarding knowledge and intent detection")
    RAG_SYSTEM = "enhanced"
except ImportError as e:
    logger.warning(f"Enhanced RAG system not available ({e}), trying standard RAG")
    try:
        from certio_rag_system import enhance_agent_prompt, get_relevant_context, search_project_knowledge, notal_rag
        enhanced_notal_rag = notal_rag  # Alias for compatibility
        logger.info("Using standard RAG system with numpy/scikit-learn")
        RAG_SYSTEM = "standard"
        def get_knowledge_stats():
            return notal_rag.get_knowledge_stats() if hasattr(notal_rag, 'get_knowledge_stats') else {}
    except ImportError as e2:
        logger.warning(f"Standard RAG system not available ({e2}), using fallback version")
        from certio_rag_system_fallback import enhance_agent_prompt, get_relevant_context, search_project_knowledge, notal_rag
        enhanced_notal_rag = notal_rag  # Alias for compatibility
        RAG_SYSTEM = "fallback"
        def get_knowledge_stats():
            return notal_rag.get_knowledge_stats() if hasattr(notal_rag, 'get_knowledge_stats') else {}

# Import user data RAG system
try:
    from user_data_rag_system import user_data_rag, get_user_data_context
    logger.info("✅ User Data RAG System loaded successfully")
    USER_DATA_RAG_AVAILABLE = True
except ImportError as e:
    logger.warning(f"User Data RAG system not available: {e}")
    USER_DATA_RAG_AVAILABLE = False
    user_data_rag = None
    async def get_user_data_context(*args, **kwargs):
        return ""

# Initialize FastAPI app
app = FastAPI(title="Notal AI Agents", version="1.0.0")

# Configure Azure OpenAI with fallback to regular OpenAI
def create_openai_client():
    """Create OpenAI client with Azure OpenAI preference"""
    azure_endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    azure_api_key = os.getenv("AZURE_OPENAI_API_KEY")
    
    if azure_endpoint and azure_api_key:
        # Use Azure OpenAI
        from openai import AzureOpenAI
        logger.info("🔵 Using Azure OpenAI")
        return AzureOpenAI(
            azure_endpoint=azure_endpoint,
            api_key=azure_api_key,
            api_version=os.getenv("AZURE_OPENAI_VERSION", "2024-02-15-preview"),
            max_retries=0
        )
    else:
        # Fallback to regular OpenAI
        logger.info("🟢 Using regular OpenAI (Azure not configured)")
        return OpenAI(
            api_key=os.getenv("OPENAI_API_KEY"),
            max_retries=0
        )

client = create_openai_client()

# Global function to get model name (Azure deployment names if using Azure)
def get_model_name(model_type):
    """Get the appropriate model name for Azure or regular OpenAI"""
    if os.getenv("AZURE_OPENAI_ENDPOINT"):
        # Use Azure deployment names
        azure_mapping = {
            ModelType.GPT_4O: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT4O", "gpt-4o"),
            ModelType.GPT_4O_MINI: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT4O_MINI", "gpt-4o-mini"),
            ModelType.GPT_4_1_MINI: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT4_1_MINI", "gpt-4.1-mini"),
            ModelType.GPT_4_1: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT4_1", "gpt-4.1"),
            ModelType.GPT_5: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT5", "gpt-5")
        }
        return azure_mapping.get(model_type, "gpt-4o-mini")
    else:
        # Use regular OpenAI model names
        regular_mapping = {
            ModelType.GPT_4O: "gpt-4o",
            ModelType.GPT_4O_MINI: "gpt-4o-mini",
            ModelType.GPT_4_1_MINI: "o1-mini",
            ModelType.GPT_4_1: "o1-preview",
            ModelType.GPT_5: "gpt-5"
        }
        return regular_mapping.get(model_type, "gpt-4o-mini")

# Rate limiter to prevent hitting OpenAI limits
class RateLimiter:
    def __init__(self, max_requests_per_minute=50):
        self.max_requests = max_requests_per_minute
        self.requests = deque()
        self.lock = asyncio.Lock()
    
    async def wait_if_needed(self):
        async with self.lock:
            now = time.time()
            # Remove requests older than 1 minute
            while self.requests and self.requests[0] <= now - 60:
                self.requests.popleft()
            
            # If we're at the limit, wait
            if len(self.requests) >= self.max_requests:
                wait_time = 60 - (now - self.requests[0])
                if wait_time > 0:
                    logger.info(f"Rate limit reached, waiting {wait_time:.2f} seconds")
                    await asyncio.sleep(wait_time)
                    # Clean up old requests after waiting
                    while self.requests and self.requests[0] <= time.time() - 60:
                        self.requests.popleft()
            
            # Add current request
            self.requests.append(now)

# Global rate limiter - very conservative to avoid 429 errors
rate_limiter = RateLimiter(max_requests_per_minute=15)  # Very conservative limit


class APIRateLimiter:
    """Lightweight per-client API rate limiter"""

    def __init__(self, max_requests: int, window_seconds: int = 60):
        self.max_requests = max_requests
        self.window_seconds = window_seconds
        self.access_log: Dict[str, deque] = {}
        self.lock = asyncio.Lock()

    async def check(self, identifier: str):
        async with self.lock:
            now = time.time()
            request_log = self.access_log.setdefault(identifier, deque())

            # Remove expired requests
            while request_log and request_log[0] <= now - self.window_seconds:
                request_log.popleft()

            if len(request_log) >= self.max_requests:
                retry_after = max(0.0, self.window_seconds - (now - request_log[0]))
                raise HTTPException(
                    status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                    detail="Rate limit exceeded",
                    headers={"Retry-After": str(int(retry_after) + 1)}
                )

            request_log.append(now)

    def reset(self):
        """Testing helper to clear counters."""
        self.access_log.clear()


api_rate_limit_per_minute = int(os.getenv("AI_API_RATE_LIMIT_PER_MINUTE", "60"))
api_rate_limit_window_seconds = int(os.getenv("AI_API_RATE_LIMIT_WINDOW_SECONDS", "60"))
api_rate_limiter = APIRateLimiter(max_requests=api_rate_limit_per_minute, window_seconds=api_rate_limit_window_seconds)


async def authenticate_request(request: Request) -> str:
    expected_key = os.getenv("AI_API_KEY")
    if not expected_key:
        logger.error("AI_API_KEY is not configured; rejecting request")
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail="AI service credentials not configured")

    provided_key = request.headers.get("x-api-key")
    if not provided_key:
        auth_header = request.headers.get("Authorization", "")
        if auth_header.startswith("Bearer "):
            provided_key = auth_header[7:]

    if not provided_key or not secrets.compare_digest(provided_key.strip(), expected_key.strip()):
        client_host = request.client.host if request.client else "unknown"
        logger.warning("Unauthorized AI service request from %s", client_host)
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Unauthorized")

    client_identifier = provided_key.strip()
    if request.client and request.client.host:
        client_identifier = f"{client_identifier}:{request.client.host}"

    await api_rate_limiter.check(client_identifier)
    return provided_key

# Initialize cost optimization components
model_selector = SimplifiedModelSelector()
task_analyzer = TaskComplexityAnalyzer()
usage_tracker = UsageTracker()
context_manager = IntelligentContextManager()
background_agent_manager = BackgroundAgentManager()
intelligent_router = IntelligentRouter()
# CostAnalytics removed - using simplified cost tracking

# Conversation context constraints
MAX_CONVERSATION_CONTEXT_TOKENS = int(os.getenv("AI_CONVERSATION_TOKEN_BUDGET", "4800"))
CONVERSATION_RECENT_MESSAGE_WINDOW = max(3, int(os.getenv("AI_CONVERSATION_RECENT_WINDOW", "6")))

# Pydantic models for request/response
class ChatMessage(BaseModel):
    id: int
    conversation_id: Union[str, int]  # Accept both string and int
    user_id: Optional[Union[str, int]] = None  # AI messages have null user_id
    user_type: str
    content: str
    message_type: str
    is_from_ai: bool
    ai_agent_type: Optional[str] = None
    created_at: str
    is_read: bool

class ConversationSummary(BaseModel):
    summary: str
    key_points: List[str]
    sentiment: str
    urgency: str
    suggested_actions: List[str]

class ClientGoal(BaseModel):
    primary_goal: str
    secondary_goals: List[str]
    business_type: str
    legal_area: str
    timeline: str
    budget: str
    required_documents: List[str]

class ReplySuggestion(BaseModel):
    suggested_reply: str
    tone: str
    purpose: str
    key_points: List[str]
    requires_legal_review: bool

class ClarityExplanation(BaseModel):
    original_text: str
    simplified_explanation: str
    key_terms: List[str]
    implications: List[str]
    risk_level: str
    recommended_actions: List[str]

class ImageAttachment(BaseModel):
    """Represents an image attachment in a message"""
    url: str  # Can be a URL or base64-encoded data URI
    mime_type: Optional[str] = None
    document_id: Optional[str] = None

class AIAgentRequest(BaseModel):
    conversation_id: str
    messages: List[ChatMessage]
    attachments: Optional[List[ImageAttachment]] = None
    user_type: Optional[str] = None
    text: Optional[str] = None

class AIAgentResponse(BaseModel):
    agent_type: str
    content: str
    confidence: str
    metadata: Dict[str, Any]
    requires_review: bool

# Helper functions for image/file processing
def encode_image_to_base64(image_path: str) -> str:
    """Encode an image file to base64 string"""
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode('utf-8')

def get_image_mime_type(image_path: str) -> str:
    """Get MIME type of an image file"""
    mime_type, _ = mimetypes.guess_type(image_path)
    return mime_type or "image/jpeg"

def is_image_url(url: str) -> bool:
    """Check if URL is an image"""
    image_extensions = {'.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.tiff', '.svg'}
    return any(url.lower().endswith(ext) for ext in image_extensions) or url.startswith('data:image/')

def prepare_vision_message(text: str, image_urls: List[str]) -> List[Dict[str, Any]]:
    """Prepare message content for vision models (GPT-4 Vision)"""
    content = []
    
    # Add text content
    if text:
        content.append({
            "type": "text",
            "text": text
        })
    
    # Add image content
    for img_url in image_urls:
        content.append({
            "type": "image_url",
            "image_url": {
                "url": img_url,
                "detail": "high"  # Use "high" for detailed analysis, "low" for faster/cheaper processing
            }
        })
    
    return content

# Enhanced AI Agent Classes with Agentic Capabilities
class BaseAgent:
    """Base class for all AI agents with common functionality"""
    
    def __init__(self, agent_type: str):
        self.agent_type = agent_type
        self.confidence_threshold = 0.7
        self.max_retries = 3
    
    def _convert_messages(self, messages):
        """Convert dictionaries to ChatMessage objects if needed"""
        if not messages:
            return messages
        
        if isinstance(messages[0], dict):
            chat_messages = []
            for msg in messages:
                chat_msg = ChatMessage(
                    id=msg["id"],
                    conversation_id=msg["conversation_id"],
                    user_id=msg["user_id"],
                    user_type=msg["user_type"],
                    content=msg["content"],
                    message_type=msg["message_type"],
                    is_from_ai=msg["is_from_ai"],
                    ai_agent_type=msg["ai_agent_type"],
                    created_at=msg["created_at"],
                    is_read=msg["is_read"]
                )
                chat_messages.append(chat_msg)
            return chat_messages
        return messages
    
    async def _call_openai(self, prompt: str, model: str = "gpt-3.5-turbo", max_tokens: int = 1000, temperature: float = 0.3, user_type: str = "Client", image_urls: Optional[List[str]] = None) -> str:
        """Enhanced OpenAI API call with intelligent model selection, cost optimization, and vision support"""
        # If images are provided, use vision model
        if image_urls:
            return await self._call_openai_vision(prompt, image_urls, max_tokens, temperature)
        
        # Analyze task complexity for optimal model selection
        task_complexity = task_analyzer.analyze_task(prompt, len(prompt), user_type)
        
        # Select optimal model based on complexity and cost
        optimal_model_type, estimated_cost = model_selector.select_optimal_model(
            task_complexity, 
            budget_constraint=None,  # No budget constraint for now
            time_constraint="normal"
        )
        
        # Use the global get_model_name function
        
        selected_model = get_model_name(optimal_model_type) if optimal_model_type else model
        
        # Log cost optimization decision
        logger.info(f"Selected model: {selected_model} (estimated cost: ${estimated_cost:.4f}) for complexity: {task_complexity.complexity_score:.2f}")
        
        # Wait for rate limiter before making request
        await rate_limiter.wait_if_needed()
        
        for attempt in range(self.max_retries):
            try:
                response = client.chat.completions.create(
                    model=selected_model,
                    messages=[{"role": "user", "content": prompt}],
                    max_tokens=max_tokens,
                    temperature=temperature
                )
                
                # Track actual usage for cost optimization
                actual_tokens = response.usage.total_tokens if hasattr(response, 'usage') else max_tokens
                usage_tracker.record_model_selection(optimal_model_type, task_complexity, estimated_cost)
                
                return response.choices[0].message.content
            except Exception as e:
                if "429" in str(e) or "rate limit" in str(e).lower():
                    # Rate limit hit, wait longer
                    wait_time = min(60, 10 * (2 ** attempt))  # Cap at 60 seconds
                    logger.warning(f"Rate limit hit, waiting {wait_time}s before retry {attempt + 1}")
                    await asyncio.sleep(wait_time)
                elif "quota" in str(e).lower() or "insufficient_quota" in str(e).lower():
                    # Quota exceeded, don't retry
                    logger.error(f"OpenAI quota exceeded: {e}")
                    raise Exception("OpenAI quota exceeded. Please check your billing and add credits.")
                elif attempt == self.max_retries - 1:
                    logger.error(f"OpenAI API call failed after {self.max_retries} attempts: {e}")
                    raise
                else:
                    # Wait before retry with exponential backoff
                    wait_time = 2 ** attempt
                    logger.warning(f"OpenAI API call failed (attempt {attempt + 1}), retrying in {wait_time}s: {e}")
                    await asyncio.sleep(wait_time)
    
    async def _call_openai_vision(self, prompt: str, image_urls: List[str], max_tokens: int = 2000, temperature: float = 0.3) -> str:
        """Call OpenAI with vision model (GPT-4 Vision) for image analysis"""
        # Use GPT-4o for vision tasks (supports vision natively)
        vision_model = get_model_name(ModelType.GPT_4O) if os.getenv("AZURE_OPENAI_ENDPOINT") else "gpt-4o"
        
        logger.info(f"Using vision model: {vision_model} for {len(image_urls)} image(s)")
        
        # Prepare vision message content
        message_content = prepare_vision_message(prompt, image_urls)
        
        # Wait for rate limiter before making request
        await rate_limiter.wait_if_needed()
        
        for attempt in range(self.max_retries):
            try:
                response = client.chat.completions.create(
                    model=vision_model,
                    messages=[{
                        "role": "user",
                        "content": message_content
                    }],
                    max_tokens=max_tokens,
                    temperature=temperature
                )
                
                logger.info(f"Vision API call successful. Tokens used: {response.usage.total_tokens if hasattr(response, 'usage') else 'unknown'}")
                
                return response.choices[0].message.content
            except Exception as e:
                if "429" in str(e) or "rate limit" in str(e).lower():
                    wait_time = min(60, 10 * (2 ** attempt))
                    logger.warning(f"Rate limit hit on vision API, waiting {wait_time}s before retry {attempt + 1}")
                    await asyncio.sleep(wait_time)
                elif "quota" in str(e).lower() or "insufficient_quota" in str(e).lower():
                    logger.error(f"OpenAI quota exceeded on vision API: {e}")
                    raise Exception("OpenAI quota exceeded. Please check your billing and add credits.")
                elif attempt == self.max_retries - 1:
                    logger.error(f"Vision API call failed after {self.max_retries} attempts: {e}")
                    raise
                else:
                    wait_time = 2 ** attempt
                    logger.warning(f"Vision API call failed (attempt {attempt + 1}), retrying in {wait_time}s: {e}")
                    await asyncio.sleep(wait_time)
    
    def _extract_json_from_response(self, response: str) -> dict:
        """Extract JSON from AI response, handling various formats"""
        try:
            # Try to find JSON in the response
            json_match = re.search(r'\{.*\}', response, re.DOTALL)
            if json_match:
                return json.loads(json_match.group())
            return json.loads(response)
        except json.JSONDecodeError:
            logger.warning(f"Failed to parse JSON from response: {response[:200]}...")
            return {}

class ChatSummarizer(BaseAgent):
    def __init__(self):
        super().__init__("ChatSummarizer")
        self.analysis_depth = "comprehensive"
    
    async def process(self, messages) -> ConversationSummary:
        """Enhanced conversation summarization with deeper analysis"""
        messages = self._convert_messages(messages)
        
        if not messages:
            return ConversationSummary(
                summary="No messages to analyze",
                key_points=[],
                sentiment="Neutral",
                urgency="Low",
                suggested_actions=[]
            )
        
        # Analyze conversation patterns and context
        conversation_analysis = self._analyze_conversation_patterns(messages)
        conversation_text = "\n".join([f"{msg.user_type}: {msg.content}" for msg in messages])
        
        # Get relevant context from RAG system
        conversation_context = f"Participants: {', '.join(set(msg.user_type for msg in messages))}, Time span: {self._calculate_time_span(messages)}"
        rag_context = get_relevant_context("ChatSummarizer", conversation_text, conversation_context)
        
        base_prompt = f"""
        As an expert legal conversation analyst for the Notal platform, provide a comprehensive analysis of this legal services conversation:
        
        Conversation Context:
        - Total messages: {len(messages)}
        - Participants: {', '.join(set(msg.user_type for msg in messages))}
        - Time span: {self._calculate_time_span(messages)}
        - Message types: {', '.join(set(msg.message_type for msg in messages))}
        
        Conversation:
        {conversation_text}
        
        Analysis Requirements:
        1. Provide a detailed summary highlighting legal issues, client needs, and key decisions
        2. Extract critical legal and business points that require attention
        3. Assess emotional tone and client satisfaction level
        4. Determine urgency based on legal deadlines, client stress, and business impact
        5. Suggest specific, actionable next steps for legal professionals
        
        Consider these factors:
        - Legal complexity and risk level
        - Client urgency and emotional state
        - Business impact and timeline constraints
        - Required legal expertise and documentation
        - Potential follow-up actions
        
        Format your response as JSON:
        {{
            "summary": "Comprehensive summary of legal discussion and client needs",
            "key_points": ["Critical legal point 1", "Business requirement 2", "Timeline concern 3"],
            "sentiment": "Positive/Negative/Neutral/Concerned/Urgent",
            "urgency": "Low/Medium/High/Urgent/Critical",
            "suggested_actions": ["Specific action 1", "Document review needed", "Client follow-up required"]
        }}
        """
        
        # Enhance prompt with RAG context
        prompt = enhance_agent_prompt("ChatSummarizer", base_prompt, conversation_text, conversation_context)
        
        try:
            response = await self._call_openai(prompt, max_tokens=1200, user_type="Client")
            result = self._extract_json_from_response(response)
            
            # Validate and enhance the result
            return ConversationSummary(
                summary=result.get("summary", "Unable to generate summary"),
                key_points=result.get("key_points", []),
                sentiment=result.get("sentiment", "Neutral"),
                urgency=result.get("urgency", "Medium"),
                suggested_actions=result.get("suggested_actions", [])
            )
        except Exception as e:
            logger.error(f"Error in ChatSummarizer: {e}")
            return ConversationSummary(
                summary="Error processing conversation analysis",
                key_points=["Technical error occurred during analysis"],
                sentiment="Neutral",
                urgency="Medium",
                suggested_actions=["Manual review required"]
            )
    
    def _analyze_conversation_patterns(self, messages: List[ChatMessage]) -> dict:
        """Analyze conversation patterns for better context"""
        patterns = {
            "message_count": len(messages),
            "participants": list(set(msg.user_type for msg in messages)),
            "ai_messages": len([m for m in messages if m.is_from_ai]),
            "client_messages": len([m for m in messages if m.user_type in ["Client", "Business"]]),
            "legal_terms": self._count_legal_terms(messages),
            "question_count": sum(1 for m in messages if "?" in m.content),
            "urgency_indicators": self._detect_urgency_indicators(messages)
        }
        return patterns
    
    def _count_legal_terms(self, messages: List[ChatMessage]) -> int:
        """Count legal terminology in messages"""
        legal_terms = [
            "contract", "agreement", "liability", "breach", "damages", "litigation",
            "compliance", "regulation", "intellectual property", "patent", "trademark",
            "copyright", "employment", "discrimination", "harassment", "termination",
            "severance", "non-disclosure", "confidentiality", "merger", "acquisition"
        ]
        count = 0
        for message in messages:
            content_lower = message.content.lower()
            count += sum(1 for term in legal_terms if term in content_lower)
        return count
    
    def _detect_urgency_indicators(self, messages: List[ChatMessage]) -> List[str]:
        """Detect urgency indicators in messages"""
        urgency_words = [
            "urgent", "asap", "immediately", "deadline", "emergency", "critical",
            "rush", "priority", "time-sensitive", "expires", "due date"
        ]
        indicators = []
        for message in messages:
            content_lower = message.content.lower()
            for word in urgency_words:
                if word in content_lower:
                    indicators.append(f"{word} in {message.user_type} message")
        return indicators
    
    def _calculate_time_span(self, messages: List[ChatMessage]) -> str:
        """Calculate time span of conversation"""
        if len(messages) < 2:
            return "Single message"
        
        try:
            first_time = datetime.fromisoformat(messages[0].created_at.replace('Z', '+00:00'))
            last_time = datetime.fromisoformat(messages[-1].created_at.replace('Z', '+00:00'))
            duration = last_time - first_time
            
            if duration.days > 0:
                return f"{duration.days} days"
            elif duration.seconds > 3600:
                return f"{duration.seconds // 3600} hours"
            else:
                return f"{duration.seconds // 60} minutes"
        except:
            return "Unknown duration"

class ClientGoalExtractor(BaseAgent):
    def __init__(self):
        super().__init__("ClientGoalExtractor")
        self.legal_areas = [
            "Corporate Law", "Contract Law", "Employment Law", "Intellectual Property",
            "Real Estate Law", "Litigation", "Tax Law", "Immigration Law",
            "Family Law", "Criminal Law", "Estate Planning", "Business Formation"
        ]
    
    async def process(self, messages) -> ClientGoal:
        """Enhanced client goal extraction with business intelligence"""
        messages = self._convert_messages(messages)
        
        if not messages:
            return ClientGoal(
                primary_goal="No conversation data available",
                secondary_goals=[],
                business_type="Unknown",
                legal_area="General",
                timeline="Not specified",
                budget="Not specified",
                required_documents=[]
            )
        
        # Analyze client messages specifically
        client_messages = [m for m in messages if m.user_type in ["Client", "Business"]]
        conversation_text = "\n".join([f"{msg.user_type}: {msg.content}" for msg in messages])
        
        # Extract business context and legal indicators
        business_context = self._analyze_business_context(client_messages)
        legal_indicators = self._identify_legal_indicators(conversation_text)
        
        # Get relevant context from RAG system
        business_context_text = f"Business type: {business_context}, Legal indicators: {legal_indicators['legal_terms']}"
        rag_context = get_relevant_context("ClientGoalExtractor", conversation_text, business_context_text)
        
        base_prompt = f"""
        As a legal business analyst for the Notal platform, extract comprehensive client goals and requirements from this legal services conversation:
        
        Business Context Analysis:
        - Client message count: {len(client_messages)}
        - Business indicators: {business_context}
        - Legal terminology detected: {legal_indicators['legal_terms']}
        - Urgency signals: {legal_indicators['urgency_signals']}
        - Financial mentions: {legal_indicators['financial_mentions']}
        
        Full Conversation:
        {conversation_text}
        
        Analysis Requirements:
        1. Identify the PRIMARY business/legal objective with specific details
        2. Extract ALL secondary goals and requirements mentioned
        3. Determine business type and industry sector
        4. Classify legal practice area (from: {', '.join(self.legal_areas)})
        5. Extract timeline expectations and deadlines
        6. Identify budget range and financial constraints
        7. List all required documents and evidence
        8. Assess complexity level and risk factors
        
        Consider these business factors:
        - Industry-specific legal requirements
        - Regulatory compliance needs
        - Risk management objectives
        - Growth and expansion goals
        - Operational efficiency improvements
        - Competitive advantage strategies
        
        Format your response as JSON:
        {{
            "primary_goal": "Detailed primary business/legal objective",
            "secondary_goals": ["Specific secondary goal 1", "Secondary goal 2", "etc."],
            "business_type": "Specific industry/business type",
            "legal_area": "Primary legal practice area",
            "timeline": "Specific timeline with deadlines",
            "budget": "Budget range and financial constraints",
            "required_documents": ["Document 1", "Document 2", "Evidence needed"]
        }}
        """
        
        # Enhance prompt with RAG context
        prompt = enhance_agent_prompt("ClientGoalExtractor", base_prompt, conversation_text, business_context_text)
        
        try:
            response = await self._call_openai(prompt, max_tokens=1000, user_type="Client")
            result = self._extract_json_from_response(response)
            
            return ClientGoal(
                primary_goal=result.get("primary_goal", "Unable to determine primary goal"),
                secondary_goals=result.get("secondary_goals", []),
                business_type=result.get("business_type", "Unknown"),
                legal_area=result.get("legal_area", "General"),
                timeline=result.get("timeline", "Not specified"),
                budget=result.get("budget", "Not specified"),
                required_documents=result.get("required_documents", [])
            )
        except Exception as e:
            logger.error(f"Error in ClientGoalExtractor: {e}")
            return ClientGoal(
                primary_goal="Error extracting client goals",
                secondary_goals=["Manual review required"],
                business_type="Unknown",
                legal_area="General",
                timeline="Not specified",
                budget="Not specified",
                required_documents=[]
            )
    
    def _analyze_business_context(self, client_messages: List[ChatMessage]) -> dict:
        """Analyze business context from client messages"""
        context = {
            "industry_indicators": [],
            "business_size_indicators": [],
            "growth_stage": "Unknown",
            "compliance_mentions": 0,
            "partnership_mentions": 0
        }
        
        business_keywords = {
            "startup": ["startup", "new business", "founding", "launching"],
            "small_business": ["small business", "local", "family business", "mom and pop"],
            "enterprise": ["enterprise", "corporation", "multinational", "global"],
            "nonprofit": ["nonprofit", "charity", "foundation", "501c3"],
            "tech": ["software", "technology", "app", "platform", "digital"],
            "retail": ["retail", "store", "shop", "ecommerce", "online store"],
            "manufacturing": ["manufacturing", "production", "factory", "assembly"],
            "healthcare": ["healthcare", "medical", "hospital", "clinic", "patient"],
            "finance": ["financial", "banking", "investment", "trading", "fintech"]
        }
        
        for message in client_messages:
            content_lower = message.content.lower()
            
            # Check for industry indicators
            for industry, keywords in business_keywords.items():
                if any(keyword in content_lower for keyword in keywords):
                    context["industry_indicators"].append(industry)
            
            # Count compliance and partnership mentions
            if any(word in content_lower for word in ["compliance", "regulation", "audit", "certification"]):
                context["compliance_mentions"] += 1
            
            if any(word in content_lower for word in ["partnership", "joint venture", "collaboration", "merger"]):
                context["partnership_mentions"] += 1
        
        return context
    
    def _identify_legal_indicators(self, conversation_text: str) -> dict:
        """Identify legal indicators in conversation"""
        text_lower = conversation_text.lower()
        
        legal_terms = [
            "contract", "agreement", "liability", "breach", "damages", "litigation",
            "compliance", "regulation", "intellectual property", "patent", "trademark",
            "copyright", "employment", "discrimination", "harassment", "termination",
            "severance", "non-disclosure", "confidentiality", "merger", "acquisition",
            "due diligence", "warranty", "indemnification", "force majeure"
        ]
        
        urgency_signals = [
            "urgent", "asap", "immediately", "deadline", "emergency", "critical",
            "rush", "priority", "time-sensitive", "expires", "due date", "deadline"
        ]
        
        financial_mentions = [
            "budget", "cost", "price", "fee", "payment", "investment", "funding",
            "revenue", "profit", "loss", "expense", "financial", "monetary"
        ]
        
        return {
            "legal_terms": [term for term in legal_terms if term in text_lower],
            "urgency_signals": [signal for signal in urgency_signals if signal in text_lower],
            "financial_mentions": [mention for mention in financial_mentions if mention in text_lower]
        }

class ReplySuggester(BaseAgent):
    def __init__(self):
        super().__init__("ReplySuggester")
        self.tone_guidelines = {
            "urgent": "Professional but empathetic",
            "legal_complex": "Formal and precise",
            "business_development": "Friendly and consultative",
            "complaint": "Apologetic and solution-focused",
            "inquiry": "Helpful and informative"
        }
    
    async def process(self, messages, user_type: str) -> ReplySuggestion:
        """Enhanced reply suggestion with context-aware intelligence"""
        messages = self._convert_messages(messages)
        
        if not messages:
            return ReplySuggestion(
                suggested_reply="Thank you for reaching out. How can I assist you today?",
                tone="Professional",
                purpose="Initial greeting",
                key_points=["Establish contact", "Offer assistance"],
                requires_legal_review=False
            )
        
        conversation_text = "\n".join([f"{msg.user_type}: {msg.content}" for msg in messages])
        last_message = messages[-1] if messages else None
        
        # Analyze conversation context for better suggestions
        context_analysis = self._analyze_conversation_context(messages)
        urgency_level = self._assess_urgency_level(messages)
        legal_complexity = self._assess_legal_complexity(conversation_text)
        
        # Get relevant context from RAG system
        reply_context = f"User type: {user_type}, Urgency: {urgency_level}, Legal complexity: {legal_complexity}"
        rag_context = get_relevant_context("ReplySuggester", conversation_text, reply_context)
        
        base_prompt = f"""
        As an expert {user_type} representative for the Notal platform, suggest a highly professional and contextually appropriate reply:
        
        Conversation Context:
        - Total messages: {len(messages)}
        - Last message from: {last_message.user_type if last_message else "Unknown"}
        - Urgency level: {urgency_level}
        - Legal complexity: {legal_complexity}
        - Client sentiment: {context_analysis['sentiment']}
        - Key topics: {', '.join(context_analysis['key_topics'])}
        
        Full Conversation:
        {conversation_text}
        
        Last message to respond to:
        {last_message.content if last_message else "No messages"}
        
        Reply Requirements:
        1. Craft a professional, contextually appropriate response
        2. Address the client's specific concerns and needs
        3. Use appropriate tone based on urgency and complexity
        4. Include specific next steps or actions
        5. Maintain legal professionalism while being approachable
        6. Consider the client's business goals and legal requirements
        
        Tone Guidelines:
        - Urgent matters: Professional but empathetic
        - Complex legal issues: Formal and precise
        - Business development: Friendly and consultative
        - Complaints: Apologetic and solution-focused
        - General inquiries: Helpful and informative
        
        Format your response as JSON:
        {{
            "suggested_reply": "Complete professional reply addressing all concerns",
            "tone": "Professional/Friendly/Formal/Casual/Empathetic",
            "purpose": "Specific purpose of this reply",
            "key_points": ["Key point 1", "Key point 2", "Next steps"],
            "requires_legal_review": true/false
        }}
        """
        
        # Enhance prompt with RAG context
        prompt = enhance_agent_prompt("ReplySuggester", base_prompt, conversation_text, reply_context)
        
        try:
            response = await self._call_openai(prompt, max_tokens=800, temperature=0.4, user_type=user_type)
            result = self._extract_json_from_response(response)
            
            return ReplySuggestion(
                suggested_reply=result.get("suggested_reply", "I'll review this and get back to you shortly."),
                tone=result.get("tone", "Professional"),
                purpose=result.get("purpose", "Response to client inquiry"),
                key_points=result.get("key_points", []),
                requires_legal_review=result.get("requires_legal_review", True)
            )
        except Exception as e:
            logger.error(f"Error in ReplySuggester: {e}")
            return ReplySuggestion(
                suggested_reply="Thank you for your message. I'm reviewing your request and will provide a detailed response shortly.",
                tone="Professional",
                purpose="Acknowledgment and follow-up",
                key_points=["Acknowledge receipt", "Promise detailed response"],
                requires_legal_review=True
            )
    
    def _analyze_conversation_context(self, messages: List[ChatMessage]) -> dict:
        """Analyze conversation context for better reply suggestions"""
        context = {
            "sentiment": "Neutral",
            "key_topics": [],
            "client_concerns": [],
            "urgency_indicators": []
        }
        
        # Analyze sentiment and topics
        all_content = " ".join([msg.content for msg in messages])
        content_lower = all_content.lower()
        
        # Sentiment analysis
        positive_words = ["thank", "appreciate", "great", "excellent", "helpful", "satisfied"]
        negative_words = ["problem", "issue", "concern", "worried", "frustrated", "disappointed"]
        
        positive_count = sum(1 for word in positive_words if word in content_lower)
        negative_count = sum(1 for word in negative_words if word in content_lower)
        
        if positive_count > negative_count:
            context["sentiment"] = "Positive"
        elif negative_count > positive_count:
            context["sentiment"] = "Negative"
        else:
            context["sentiment"] = "Neutral"
        
        # Extract key topics
        topic_keywords = {
            "contracts": ["contract", "agreement", "terms", "clause"],
            "compliance": ["compliance", "regulation", "audit", "certification"],
            "employment": ["employment", "hiring", "termination", "discrimination"],
            "intellectual_property": ["patent", "trademark", "copyright", "intellectual property"],
            "litigation": ["lawsuit", "litigation", "dispute", "court"],
            "business_formation": ["incorporation", "llc", "corporation", "partnership"]
        }
        
        for topic, keywords in topic_keywords.items():
            if any(keyword in content_lower for keyword in keywords):
                context["key_topics"].append(topic)
        
        return context
    
    def _assess_urgency_level(self, messages: List[ChatMessage]) -> str:
        """Assess urgency level of the conversation"""
        urgency_indicators = [
            "urgent", "asap", "immediately", "emergency", "critical", "deadline",
            "rush", "priority", "time-sensitive", "expires", "due date"
        ]
        
        for message in messages:
            content_lower = message.content.lower()
            if any(indicator in content_lower for indicator in urgency_indicators):
                return "High"
        
        # Check for time-sensitive language
        time_indicators = ["today", "tomorrow", "this week", "deadline", "expires"]
        for message in messages:
            content_lower = message.content.lower()
            if any(indicator in content_lower for indicator in time_indicators):
                return "Medium"
        
        return "Low"
    
    def _assess_legal_complexity(self, conversation_text: str) -> str:
        """Assess legal complexity of the conversation"""
        complex_legal_terms = [
            "litigation", "jurisdiction", "precedent", "statute of limitations",
            "due diligence", "indemnification", "force majeure", "arbitration",
            "confidentiality agreement", "non-compete", "intellectual property"
        ]
        
        content_lower = conversation_text.lower()
        complex_term_count = sum(1 for term in complex_legal_terms if term in content_lower)
        
        if complex_term_count >= 3:
            return "High"
        elif complex_term_count >= 1:
            return "Medium"
        else:
            return "Low"

class ClarityAgent(BaseAgent):
    def __init__(self):
        super().__init__("ClarityAgent")
        self.legal_glossary = {
            "liability": "Legal responsibility for something, especially costs or damages",
            "breach": "Breaking or failing to follow a contract or agreement",
            "damages": "Money awarded to compensate for loss or injury",
            "litigation": "The process of taking legal action through the court system",
            "jurisdiction": "The authority of a court to hear and decide cases",
            "precedent": "A previous court decision that serves as a guide for future cases",
            "statute of limitations": "The time limit for bringing a legal action",
            "due diligence": "Thorough investigation before making a business decision",
            "indemnification": "Protection against legal liability or loss",
            "force majeure": "Unforeseeable circumstances that prevent fulfilling a contract",
            "arbitration": "Settling disputes outside of court with a neutral third party",
            "confidentiality": "Keeping information private and not sharing it",
            "non-compete": "Agreement preventing someone from working for competitors",
            "intellectual property": "Creations of the mind like inventions, designs, or artistic works"
        }
    
    async def process(self, text: str, user_type: str) -> ClarityExplanation:
        """Enhanced legal language explanation with user-specific context"""
        if not text or not text.strip():
            return ClarityExplanation(
                original_text=text,
                simplified_explanation="No text provided for analysis.",
                key_terms=[],
                implications=[],
                risk_level="Low",
                recommended_actions=["Provide text for analysis"]
            )
        
        # Analyze the text for legal complexity and context
        complexity_analysis = self._analyze_legal_complexity(text)
        risk_assessment = self._assess_legal_risk(text)
        user_context = self._get_user_context(user_type)
        
        # Get relevant context from RAG system
        clarity_context = f"User type: {user_type}, Complexity: {complexity_analysis['complexity_level']}, Document type: {complexity_analysis['document_type']}"
        rag_context = get_relevant_context("ClarityAgent", text, clarity_context)
        
        base_prompt = f"""
        As a legal communication expert for the Notal platform, explain this legal text in clear, accessible terms for a {user_type}:
        
        Original Legal Text: {text}
        
        User Context:
        - User Type: {user_type}
        - Expected Knowledge Level: {user_context['knowledge_level']}
        - Primary Concerns: {', '.join(user_context['primary_concerns'])}
        - Communication Style: {user_context['communication_style']}
        
        Text Analysis:
        - Legal Complexity: {complexity_analysis['complexity_level']}
        - Legal Terms Found: {', '.join(complexity_analysis['legal_terms'])}
        - Risk Indicators: {', '.join(risk_assessment['risk_indicators'])}
        - Document Type: {complexity_analysis['document_type']}
        
        Explanation Requirements:
        1. Provide a clear, simple explanation that a {user_type} can understand
        2. Define all legal terms in plain language
        3. Explain potential implications and consequences
        4. Assess risk level with specific reasoning
        5. Provide actionable recommendations
        6. Use analogies and examples when helpful
        7. Address the user's likely concerns and questions
        
        Format your response as JSON:
        {{
            "original_text": "{text}",
            "simplified_explanation": "Clear, comprehensive explanation in simple terms",
            "key_terms": ["term1 with definition", "term2 with definition"],
            "implications": ["Specific implication 1", "Specific implication 2"],
            "risk_level": "Low/Medium/High/Critical",
            "recommended_actions": ["Specific action 1", "Specific action 2", "Next steps"]
        }}
        """
        
        # Enhance prompt with RAG context
        prompt = enhance_agent_prompt("ClarityAgent", base_prompt, text, clarity_context)
        
        try:
            response = await self._call_openai(prompt, max_tokens=1000, temperature=0.3, user_type=user_type)
            result = self._extract_json_from_response(response)
            
            return ClarityExplanation(
                original_text=text,
                simplified_explanation=result.get("simplified_explanation", "Unable to process this text."),
                key_terms=result.get("key_terms", []),
                implications=result.get("implications", []),
                risk_level=result.get("risk_level", "Unknown"),
                recommended_actions=result.get("recommended_actions", [])
            )
        except Exception as e:
            logger.error(f"Error in ClarityAgent: {e}")
            return ClarityExplanation(
                original_text=text,
                simplified_explanation="I apologize, but I'm having trouble processing this text right now. Please try again or contact our legal team for assistance.",
                key_terms=[],
                implications=[],
                risk_level="Unknown",
                recommended_actions=["Contact legal team for assistance"]
            )
    
    def _analyze_legal_complexity(self, text: str) -> dict:
        """Analyze the legal complexity of the text"""
        text_lower = text.lower()
        
        # Count legal terms
        legal_terms_found = [term for term in self.legal_glossary.keys() if term in text_lower]
        
        # Determine document type
        document_indicators = {
            "contract": ["agreement", "contract", "terms", "clause", "party"],
            "legal_notice": ["notice", "demand", "cease", "desist", "violation"],
            "court_document": ["plaintiff", "defendant", "court", "judge", "motion"],
            "policy": ["policy", "procedure", "guidelines", "standards", "compliance"]
        }
        
        document_type = "General Legal Text"
        for doc_type, indicators in document_indicators.items():
            if any(indicator in text_lower for indicator in indicators):
                document_type = doc_type.title()
                break
        
        # Assess complexity level
        if len(legal_terms_found) >= 5:
            complexity_level = "High"
        elif len(legal_terms_found) >= 2:
            complexity_level = "Medium"
        else:
            complexity_level = "Low"
        
        return {
            "complexity_level": complexity_level,
            "legal_terms": legal_terms_found,
            "document_type": document_type
        }
    
    def _assess_legal_risk(self, text: str) -> dict:
        """Assess legal risk indicators in the text"""
        text_lower = text.lower()
        
        high_risk_indicators = [
            "liability", "breach", "damages", "penalty", "fine", "violation",
            "illegal", "prohibited", "forbidden", "termination", "default"
        ]
        
        medium_risk_indicators = [
            "obligation", "responsibility", "compliance", "requirement",
            "condition", "restriction", "limitation"
        ]
        
        risk_indicators = []
        risk_level = "Low"
        
        high_risk_count = sum(1 for indicator in high_risk_indicators if indicator in text_lower)
        medium_risk_count = sum(1 for indicator in medium_risk_indicators if indicator in text_lower)
        
        if high_risk_count >= 2:
            risk_level = "High"
            risk_indicators.extend([indicator for indicator in high_risk_indicators if indicator in text_lower])
        elif high_risk_count >= 1 or medium_risk_count >= 3:
            risk_level = "Medium"
            risk_indicators.extend([indicator for indicator in medium_risk_indicators if indicator in text_lower])
        
        return {
            "risk_level": risk_level,
            "risk_indicators": risk_indicators
        }
    
    def _get_user_context(self, user_type: str) -> dict:
        """Get user-specific context for explanation"""
        context_map = {
            "Client": {
                "knowledge_level": "Basic legal knowledge",
                "primary_concerns": ["Understanding obligations", "Risk assessment", "Cost implications"],
                "communication_style": "Simple, practical, with examples"
            },
            "Business": {
                "knowledge_level": "Business-focused legal understanding",
                "primary_concerns": ["Business impact", "Compliance requirements", "Operational implications"],
                "communication_style": "Professional, business-oriented, strategic"
            },
            "Lawyer": {
                "knowledge_level": "Expert legal knowledge",
                "primary_concerns": ["Legal precision", "Case strategy", "Client representation"],
                "communication_style": "Technical, precise, comprehensive"
            }
        }
        
        return context_map.get(user_type, {
            "knowledge_level": "General knowledge",
            "primary_concerns": ["Understanding", "Clarity", "Next steps"],
            "communication_style": "Clear and helpful"
        })

# Agent Orchestration System
class AgentOrchestrator:
    """Orchestrates multiple AI agents for comprehensive conversation analysis"""
    
    def __init__(self):
        self.agents = {
            "summarizer": ChatSummarizer(),
            "goal_extractor": ClientGoalExtractor(),
            "reply_suggester": ReplySuggester(),
            "clarity_agent": ClarityAgent()
        }
        self.agent_priorities = {
            "summarizer": 1,  # Always run
            "goal_extractor": 2,  # Run for client conversations
            "reply_suggester": 3,  # Run when response needed
            "clarity_agent": 4  # Run on demand
        }
    
    async def process_conversation_intelligently(self, messages, user_type: str = "Client") -> dict:
        """Intelligently process conversation with multiple agents based on context"""
        # Convert dictionaries to ChatMessage objects if needed
        if messages and isinstance(messages[0], dict):
            chat_messages = []
            for msg in messages:
                chat_msg = ChatMessage(
                    id=msg["id"],
                    conversation_id=msg["conversation_id"],
                    user_id=msg["user_id"],
                    user_type=msg["user_type"],
                    content=msg["content"],
                    message_type=msg["message_type"],
                    is_from_ai=msg["is_from_ai"],
                    ai_agent_type=msg["ai_agent_type"],
                    created_at=msg["created_at"],
                    is_read=msg["is_read"]
                )
                chat_messages.append(chat_msg)
            messages = chat_messages
        
        results = {}
        
        # Always run summarizer
        try:
            results["summary"] = await self.agents["summarizer"].process(messages)
        except Exception as e:
            logger.error(f"Error in summarizer: {e}")
            results["summary"] = ConversationSummary(
                summary="Error in conversation analysis",
                key_points=[],
                sentiment="Neutral",
                urgency="Medium",
                suggested_actions=[]
            )
        
        # Run goal extractor for client/business conversations
        if any(msg.user_type in ["Client", "Business"] for msg in messages):
            try:
                results["goals"] = await self.agents["goal_extractor"].process(messages)
            except Exception as e:
                logger.error(f"Error in goal extractor: {e}")
                results["goals"] = ClientGoal(
                    primary_goal="Unable to extract goals",
                    secondary_goals=[],
                    business_type="Unknown",
                    legal_area="General",
                    timeline="Not specified",
                    budget="Not specified",
                    required_documents=[]
                )
        
        # Run reply suggester if last message is from client/business
        if messages and messages[-1].user_type in ["Client", "Business"]:
            try:
                results["reply_suggestion"] = await self.agents["reply_suggester"].process(messages, user_type)
            except Exception as e:
                logger.error(f"Error in reply suggester: {e}")
                results["reply_suggestion"] = ReplySuggestion(
                    suggested_reply="I'll review this and get back to you shortly.",
                    tone="Professional",
                    purpose="Acknowledgment",
                    key_points=[],
                    requires_legal_review=True
                )
        
        return results
    
    async def process_clarity_request(self, text: str, user_type: str) -> ClarityExplanation:
        """Process clarity explanation request"""
        try:
            return await self.agents["clarity_agent"].process(text, user_type)
        except Exception as e:
            logger.error(f"Error in clarity agent: {e}")
            return ClarityExplanation(
                original_text=text,
                simplified_explanation="Unable to process this text at the moment.",
                key_terms=[],
                implications=[],
                risk_level="Unknown",
                recommended_actions=[]
            )

# Initialize agents and orchestrator
chat_summarizer = ChatSummarizer()
goal_extractor = ClientGoalExtractor()
reply_suggester = ReplySuggester()
clarity_agent = ClarityAgent()
agent_orchestrator = AgentOrchestrator()

# API Endpoints
@app.get("/")
async def root():
    return {"message": "Notal AI Agents Service", "status": "running"}

@app.get("/health")
async def health_check():
    return {
        "status": "healthy", 
        "agents": ["ChatSummarizer", "ClientGoalExtractor", "ReplySuggester", "ClarityAgent"],
        "rate_limiter": {
            "max_requests_per_minute": rate_limiter.max_requests,
            "current_requests": len(rate_limiter.requests)
        },
        "cost_optimization": {
            "enabled": True,
            "intelligent_model_selection": True,
            "caching_enabled": True
        }
    }

@app.get("/analytics/usage")
async def get_usage_analytics(_: str = Depends(authenticate_request)):
    """Get comprehensive usage analytics for cost optimization"""
    try:
        analytics = usage_tracker.get_usage_analytics()
        return {
            "status": "success",
            "analytics": analytics,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting usage analytics: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/cost-optimization")
async def get_cost_optimization_analytics(_: str = Depends(authenticate_request)):
    """Get cost optimization recommendations and analytics"""
    try:
        usage_analytics = usage_tracker.get_usage_analytics()
        cache_stats = model_selector.cache_manager.get_cache_stats()
        
        return {
            "status": "success",
            "usage_analytics": usage_analytics,
            "cache_performance": cache_stats,
            "optimization_recommendations": usage_analytics.get("optimization_suggestions", []),
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting cost optimization analytics: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/analytics/task-complexity")
async def analyze_task_complexity(request: dict, _: str = Depends(authenticate_request)):
    """Analyze task complexity for optimization recommendations"""
    try:
        prompt = request.get("prompt", "")
        user_type = request.get("user_type", "Client")
        context_length = request.get("context_length", 0)
        
        task_complexity = task_analyzer.analyze_task(prompt, context_length, user_type)
        optimal_model, estimated_cost = model_selector.select_optimal_model(task_complexity)
        
        return {
            "status": "success",
            "task_analysis": {
                "complexity_score": task_complexity.complexity_score,
                "estimated_tokens": task_complexity.estimated_tokens,
                "requires_reasoning": task_complexity.requires_reasoning,
                "requires_creativity": task_complexity.requires_creativity,
                "requires_analysis": task_complexity.requires_analysis,
                "urgency": task_complexity.urgency
            },
            "recommended_model": optimal_model.value,
            "estimated_cost": estimated_cost,
            "optimization_recommendations": "Use GPT-4o Mini for simple tasks, GPT-4o for complex tasks"
        }
    except Exception as e:
        logger.error(f"Error analyzing task complexity: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/background-agents")
async def get_background_agent_stats(_: str = Depends(authenticate_request)):
    """Get background agent statistics and status"""
    try:
        stats = background_agent_manager.get_stats()
        return {
            "status": "success",
            "background_agents": stats,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting background agent stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/background-agents/submit-task")
async def submit_background_task(request: dict, _: str = Depends(authenticate_request)):
    """Submit a task for background processing"""
    try:
        task_type = request.get("task_type")
        payload = request.get("payload", {})
        priority = TaskPriority(request.get("priority", "MEDIUM"))
        max_retries = request.get("max_retries", 3)
        timeout_seconds = request.get("timeout_seconds", 300)
        
        task_id = background_agent_manager.submit_task(
            task_type=task_type,
            payload=payload,
            priority=priority,
            max_retries=max_retries,
            timeout_seconds=timeout_seconds
        )
        
        return {
            "status": "success",
            "task_id": task_id,
            "message": "Task submitted for background processing"
        }
    except Exception as e:
        logger.error(f"Error submitting background task: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/background-agents/task-status/{task_id}")
async def get_background_task_status(task_id: str, _: str = Depends(authenticate_request)):
    """Get the status of a background task"""
    try:
        task = background_agent_manager.get_task_status(task_id)
        if not task:
            raise HTTPException(status_code=404, detail="Task not found")
        
        return {
            "status": "success",
            "task": {
                "task_id": task.task_id,
                "task_type": task.task_type,
                "status": task.status.value,
                "priority": task.priority.name,
                "created_at": task.created_at.isoformat(),
                "started_at": task.started_at.isoformat() if task.started_at else None,
                "completed_at": task.completed_at.isoformat() if task.completed_at else None,
                "retry_count": task.retry_count,
                "error": task.error
            }
        }
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error getting task status: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/context-optimization")
async def get_context_optimization_stats(_: str = Depends(authenticate_request)):
    """Get context optimization statistics"""
    try:
        stats = context_manager.get_compression_stats()
        return {
            "status": "success",
            "context_optimization": stats,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting context optimization stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/context/optimize")
async def optimize_context_endpoint(request: dict, _: str = Depends(authenticate_request)):
    """Optimize context for a conversation"""
    try:
        messages = request.get("messages", [])
        user_type = request.get("user_type", "Client")
        task_complexity = request.get("task_complexity", 0.5)
        
        optimized_context, metadata = context_manager.optimize_context(
            messages, user_type, task_complexity
        )
        
        return {
            "status": "success",
            "optimized_context": optimized_context,
            "metadata": metadata,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error optimizing context: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/cost-dashboard")
async def get_cost_dashboard(_: str = Depends(authenticate_request)):
    """Get simplified cost analytics dashboard"""
    try:
        analytics = usage_tracker.get_usage_analytics()
        return {
            "status": "success",
            "dashboard": {
                "total_requests": analytics.get("total_requests", 0),
                "total_cost": analytics.get("total_cost", 0.0),
                "average_cost_per_request": analytics.get("total_cost", 0.0) / max(analytics.get("total_requests", 1), 1),
                "model_usage": analytics.get("model_usage", {}),
                "cost_trend": analytics.get("cost_trend", "stable")
            },
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error generating cost dashboard: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/cost-report/{time_period}")
async def get_cost_report(time_period: str, _: str = Depends(authenticate_request)):
    """Get detailed cost report for specified time period"""
    try:
        if time_period not in ["1h", "24h", "7d", "30d"]:
            raise HTTPException(status_code=400, detail="Invalid time period. Use: 1h, 24h, 7d, 30d")
        
        analytics = usage_tracker.get_usage_analytics()
        return {
            "status": "success",
            "cost_report": {
                "time_period": time_period,
                "total_requests": analytics.get("total_requests", 0),
                "total_cost": analytics.get("total_cost", 0.0),
                "model_usage": analytics.get("model_usage", {}),
                "cost_trend": analytics.get("cost_trend", "stable")
            },
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error generating cost report: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/routing-performance")
async def get_routing_performance(_: str = Depends(authenticate_request)):
    """Get intelligent routing performance analytics"""
    try:
        analytics = intelligent_router.get_routing_analytics()
        return {
            "status": "success",
            "routing_analytics": analytics,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting routing performance: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/routing/optimize-rules")
async def optimize_routing_rules(_: str = Depends(authenticate_request)):
    """Optimize routing rules based on performance history"""
    try:
        intelligent_router.optimize_routing_rules()
        return {
            "status": "success",
            "message": "Routing rules optimized successfully",
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error optimizing routing rules: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/routing/route-task")
async def route_task_endpoint(request: dict, _: str = Depends(authenticate_request)):
    """Route a task using intelligent routing system"""
    try:
        task_type = TaskType(request.get("task_type", "conversational_response"))
        user_type = request.get("user_type", "Client")
        urgency = request.get("urgency", "medium")
        complexity = request.get("complexity", 0.5)
        message_count = request.get("message_count", 1)
        conversation_age = request.get("conversation_age", 0.0)
        recent_ai_usage = request.get("recent_ai_usage", 0)
        cost_budget = request.get("cost_budget")
        time_budget = request.get("time_budget")
        
        task_context = TaskContext(
            task_type=task_type,
            user_type=user_type,
            urgency=urgency,
            complexity=complexity,
            message_count=message_count,
            conversation_age=conversation_age,
            recent_ai_usage=recent_ai_usage,
            cost_budget=cost_budget,
            time_budget=time_budget
        )
        
        routing_decision = intelligent_router.route_task(task_context)
        
        return {
            "status": "success",
            "routing_decision": {
                "method": routing_decision.method.value,
                "confidence": routing_decision.confidence,
                "estimated_cost": routing_decision.estimated_cost,
                "estimated_time": routing_decision.estimated_time,
                "reasoning": routing_decision.reasoning,
                "fallback_methods": [m.value for m in routing_decision.fallback_methods]
            },
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error routing task: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/analytics/record-cost-event")
async def record_cost_event(request: dict, _: str = Depends(authenticate_request)):
    """Record a cost event for analytics"""
    try:
        model = request.get("model", "gpt-3.5-turbo")
        input_tokens = request.get("input_tokens", 0)
        output_tokens = request.get("output_tokens", 0)
        success = request.get("success", True)
        retry_count = request.get("retry_count", 0)
        user_type = request.get("user_type", "Client")
        
        # Record cost event using simplified tracking
        usage_tracker.record_usage(
            model=model,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            cost=0.0  # Cost will be calculated by UsageTracker
        )
        
        return {
            "status": "success",
            "message": "Cost event recorded successfully",
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error recording cost event: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/knowledge/search")
async def search_knowledge_endpoint(query: str, category: Optional[str] = None, _: str = Depends(authenticate_request)):
    """Search the Notal knowledge base"""
    try:
        results = search_project_knowledge(query, category)
        return {
            "status": "success",
            "query": query,
            "category": category,
            "results": results,
            "total_results": len(results),
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error searching knowledge: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/knowledge/stats")
async def get_knowledge_stats(_: str = Depends(authenticate_request)):
    """Get knowledge base statistics"""
    try:
        # Get stats from the active RAG system
        stats = get_knowledge_stats()
        stats["rag_system_type"] = RAG_SYSTEM
        
        return {
            "status": "success",
            "knowledge_stats": stats,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting knowledge stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/knowledge/add")
async def add_custom_knowledge(request: dict, _: str = Depends(authenticate_request)):
    """Add custom knowledge to the system"""
    try:
        content = request.get("content")
        source = request.get("source", "custom")
        category = request.get("category", "custom")
        metadata = request.get("metadata", {})
        
        if not content:
            raise HTTPException(status_code=400, detail="Content is required")
        
        # Add to the active RAG system
        enhanced_notal_rag.add_custom_knowledge(content, source, category, metadata)
        
        return {
            "status": "success",
            "message": "Custom knowledge added successfully",
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error adding custom knowledge: {e}")
        raise HTTPException(status_code=500, detail=str(e))

# ============================================
# USER DATA RAG ENDPOINTS
# ============================================

@app.post("/data-context/sync")
async def sync_user_data_context(payload: dict, _: str = Depends(authenticate_request)):
    """
    Endpoint to sync user data context from C# backend
    Receives comprehensive user data and indexes it for RAG
    """
    try:
        if not USER_DATA_RAG_AVAILABLE:
            raise HTTPException(status_code=503, detail="User Data RAG system not available")
        
        user_id = payload.get('userId')
        organization_id = payload.get('organizationId')
        user_name = payload.get('userName', 'User')  # Get actual user name
        module_data = payload.get('moduleData', {})
        metadata = payload.get('metadata', {})
        
        if not user_id or not organization_id:
            raise HTTPException(status_code=400, detail="userId and organizationId are required")
        
        success = await user_data_rag.sync_user_data(
            user_id, organization_id, module_data, metadata, user_name
        )
        
        if success:
            return {
                "success": True,
                "message": f"Successfully synced data for user {user_id}",
                "total_chunks": sum(m.get('totalItems', 0) for m in module_data.values() if isinstance(m, dict))
            }
        else:
            raise HTTPException(status_code=500, detail="Failed to sync user data")
            
    except Exception as e:
        logger.error(f"Error syncing user data: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/data-context/summary/{user_id}/{organization_id}")
async def get_user_data_summary_endpoint(user_id: int, organization_id: int, _: str = Depends(authenticate_request)):
    """Get summary of available user data"""
    try:
        if not USER_DATA_RAG_AVAILABLE:
            return {"has_data": False, "message": "User Data RAG system not available"}
        
        summary = await user_data_rag.get_user_summary(user_id, organization_id)
        return summary
    except Exception as e:
        logger.error(f"Error getting user data summary: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/data-context/search")
async def search_user_data_endpoint(payload: dict, _: str = Depends(authenticate_request)):
    """Search user data with semantic/keyword matching"""
    try:
        if not USER_DATA_RAG_AVAILABLE:
            raise HTTPException(status_code=503, detail="User Data RAG system not available")
        
        user_id = payload.get('userId')
        organization_id = payload.get('organizationId')
        query = payload.get('query', '')
        top_k = payload.get('topK', 10)
        module_filter = payload.get('moduleFilter')
        
        if not user_id or not organization_id or not query:
            raise HTTPException(status_code=400, detail="userId, organizationId, and query are required")
        
        from user_data_rag_system import search_user_data
        result = await search_user_data(user_id, organization_id, query, top_k, module_filter)
        
        return {
            "query": query,
            "total_available": result.total_available,
            "results_count": len(result.chunks),
            "module_breakdown": result.module_breakdown,
            "chunks": [
                {
                    "id": chunk.id,
                    "module": chunk.module_name,
                    "entity_type": chunk.entity_type,
                    "title": chunk.title,
                    "content": chunk.content[:200] + "..." if len(chunk.content) > 200 else chunk.content,
                    "relevance_score": score
                }
                for chunk, score in zip(result.chunks, result.relevance_scores)
            ]
        }
    except Exception as e:
        logger.error(f"Error searching user data: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/training/add-conversations")
async def add_conversation_training_data_endpoint(request: dict, _: str = Depends(authenticate_request)):
    """Add training data from conversations"""
    try:
        conversations = request.get("conversations", [])
        if not conversations:
            raise HTTPException(status_code=400, detail="Conversations are required")
        
        examples_added = add_conversation_training_data(conversations)
        
        return {
            "status": "success",
            "examples_added": examples_added,
            "total_conversations": len(conversations),
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error adding conversation training data: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/training/update-performance")
async def update_agent_performance_endpoint(request: dict, _: str = Depends(authenticate_request)):
    """Update agent performance metrics"""
    try:
        agent_type = request.get("agent_type")
        interaction_data = request.get("interaction_data", {})
        
        if not agent_type:
            raise HTTPException(status_code=400, detail="Agent type is required")
        
        update_agent_performance(agent_type, interaction_data)
        
        return {
            "status": "success",
            "agent_type": agent_type,
            "message": "Performance metrics updated",
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error updating agent performance: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/training/recommendations")
async def get_training_recommendations_endpoint(_: str = Depends(authenticate_request)):
    """Get training recommendations"""
    try:
        recommendations = get_training_recommendations()
        return {
            "status": "success",
            "recommendations": recommendations,
            "total_recommendations": len(recommendations),
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting training recommendations: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/training/stats")
async def get_training_stats_endpoint(_: str = Depends(authenticate_request)):
    """Get comprehensive training statistics"""
    try:
        from certio_training_pipeline import certio_training
        stats = certio_training.get_training_stats()
        return {
            "status": "success",
            "training_stats": stats,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error getting training stats: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/training/should-retrain/{agent_type}")
async def should_retrain_agent_endpoint(agent_type: str, _: str = Depends(authenticate_request)):
    """Check if an agent should be retrained"""
    try:
        should_retrain = should_retrain_agent(agent_type)
        return {
            "status": "success",
            "agent_type": agent_type,
            "should_retrain": should_retrain,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error checking retrain status: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/analytics/optimization-summary")
async def get_optimization_summary(_: str = Depends(authenticate_request)):
    """Get comprehensive optimization summary"""
    try:
        # Get all analytics
        usage_analytics = usage_tracker.get_usage_analytics()
        routing_analytics = intelligent_router.get_routing_analytics()
        context_stats = context_manager.get_compression_stats()
        background_stats = background_agent_manager.get_stats()
        
        # Calculate overall optimization score
        optimization_score = _calculate_optimization_score(
            usage_analytics, usage_analytics, routing_analytics, context_stats, background_stats
        )
        
        return {
            "status": "success",
            "optimization_summary": {
                "overall_score": optimization_score,
                "usage_analytics": usage_analytics,
                "cost_analytics": usage_analytics,
                "routing_analytics": routing_analytics,
                "context_optimization": context_stats,
                "background_agents": background_stats,
                "key_metrics": {
                    "total_cost_24h": usage_analytics.get("total_cost", 0.0),
                    "total_requests_24h": usage_analytics.get("total_requests", 0),
                    "average_cost_per_request": usage_analytics.get("total_cost", 0.0) / max(usage_analytics.get("total_requests", 1), 1),
                    "cache_hit_rate": routing_analytics.get("cache_hit_rate", 0),
                    "background_task_success_rate": background_stats.get("completed_tasks", 0) / max(background_stats.get("total_tasks", 1), 1)
                },
                "top_recommendations": ["Use GPT-4o Mini for simple tasks", "Use GPT-4o for complex tasks", "Monitor cost trends regularly"]
            },
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
    except Exception as e:
        logger.error(f"Error generating optimization summary: {e}")
        raise HTTPException(status_code=500, detail=str(e))

def _calculate_optimization_score(usage_analytics, cost_report, routing_analytics, context_stats, background_stats):
    """Calculate overall optimization score (0-100)"""
    score = 100.0
    
    # Deduct points for high costs
    if cost_report["metrics"]["average_cost_per_request"] > 0.05:
        score -= 20
    
    # Deduct points for low cache hit rate
    if routing_analytics.get("cache_hit_rate", 0) < 0.3:
        score -= 15
    
    # Deduct points for high retry rate
    if usage_analytics.get("cost_trend") == "increasing":
        score -= 10
    
    # Deduct points for low background agent efficiency
    if background_stats.get("completed_tasks", 0) / max(background_stats.get("total_tasks", 1), 1) < 0.8:
        score -= 10
    
    # Add points for good practices
    if cost_report["metrics"]["total_cost"] < 1.0:  # Less than $1 per day
        score += 10
    
    if routing_analytics.get("cache_hit_rate", 0) > 0.5:
        score += 15
    
    return max(0, min(100, score))

@app.post("/agents/summarize", response_model=ConversationSummary)
async def summarize_conversation(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Summarize a conversation using ChatSummarizer agent"""
    try:
        result = await chat_summarizer.process(request.messages)
        return result
    except Exception as e:
        logger.error(f"Error in summarize endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/extract-goals", response_model=ClientGoal)
async def extract_client_goals(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Extract client goals using ClientGoalExtractor agent"""
    try:
        result = await goal_extractor.process(request.messages)
        return result
    except Exception as e:
        logger.error(f"Error in extract-goals endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/suggest-reply", response_model=ReplySuggestion)
async def suggest_reply(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Suggest a reply using ReplySuggester agent"""
    try:
        result = await reply_suggester.process(request.messages, request.user_type)
        return result
    except Exception as e:
        logger.error(f"Error in suggest-reply endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/explain-clarity", response_model=ClarityExplanation)
async def explain_clarity(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Explain legal language using ClarityAgent"""
    try:
        if not request.text:
            raise HTTPException(status_code=400, detail="Text is required for clarity explanation")
        result = await clarity_agent.process(request.text, request.user_type or "Client")
        return result
    except Exception as e:
        logger.error(f"Error in explain-clarity endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/process-all")
async def process_all_agents(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Process all agents for a conversation using intelligent orchestration"""
    try:
        user_type = request.user_type or "Client"
        results = await agent_orchestrator.process_conversation_intelligently(request.messages, user_type)
        
        # Add metadata about processing
        results["processing_metadata"] = {
            "processed_at": datetime.now(timezone.utc).isoformat(),
            "message_count": len(request.messages),
            "user_type": user_type,
            "agents_used": list(results.keys())
        }
        
        return results
    except Exception as e:
        logger.error(f"Error in process-all endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/process-intelligent")
async def process_intelligent(request: AIAgentRequest, _: str = Depends(authenticate_request)):
    """Intelligently process conversation with context-aware agent selection"""
    try:
        user_type = request.user_type or "Client"
        results = await agent_orchestrator.process_conversation_intelligently(request.messages, user_type)
        
        # Add intelligent processing metadata
        results["intelligent_processing"] = {
            "conversation_analysis": {
                "message_count": len(request.messages),
                "participants": list(set(msg.user_type for msg in request.messages)),
                "has_client_messages": any(msg.user_type in ["Client", "Business"] for msg in request.messages),
                "last_message_from": request.messages[-1].user_type if request.messages else None
            },
            "agents_executed": list(results.keys()),
            "processing_timestamp": datetime.now(timezone.utc).isoformat(),
            "user_context": user_type
        }
        
        return results
    except Exception as e:
        logger.error(f"Error in process-intelligent endpoint: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/agents/conversational-response")
async def conversational_response(payload: dict, _: str = Depends(authenticate_request)):
    """Generate highly intelligent conversational AI response with integrated analysis - Cursor-style single API call"""
    try:
        conversation_id = payload.get("conversation_id", "")
        messages = payload.get("messages", [])
        user_message = payload.get("user_message", "")
        user_type = payload.get("user_type", "Client")
        document_context = payload.get("document_context", None)
        stream = payload.get("stream", False)  # Support streaming
        attachments = payload.get("attachments", [])  # Image/file attachments
        
        # Extract image URLs from attachments
        image_urls = []
        if attachments:
            for attachment in attachments:
                if isinstance(attachment, dict):
                    url = attachment.get("url", "")
                    if url and is_image_url(url):
                        image_urls.append(url)
                        logger.info(f"Image attachment detected: {url[:100]}...")
        
        # Add note about images to user message if present
        if image_urls:
            user_message = f"{user_message}\n\n[Note: User has attached {len(image_urls)} image(s) for analysis]"
            logger.info(f"Processing request with {len(image_urls)} image(s)")
        
        # Quick analysis for conversation context (no API call)
        conversation_analysis = _quick_conversation_analysis(messages, user_message, user_type)
        
        # Check if this is a simple greeting or short message
        is_simple_message = _is_simple_message(user_message, conversation_analysis)
        
        if is_simple_message:
            # Simple response for greetings and short messages - enhanced with RAG
            base_simple_prompt = f"""You are Notal AI, a friendly legal assistant. The user said: "{user_message}"

Respond with a brief, warm greeting and offer to help with legal questions. Keep it conversational and under 50 words. Use HTML formatting with proper <p> tags and <br> for line breaks.

CRITICAL RULES:
- Do NOT start with "Hello [Name]" or greet the user again if they already said hi
- Only say their name in the VERY FIRST message of a new conversation
- For follow-up messages, jump straight to answering their question
- Be friendly but don't waste words on repeated greetings
Format your response as proper HTML with <p> tags."""
            
            # Even simple messages get RAG enhancement for Notal context
            simple_context = {"user_type": user_type, "message_type": "greeting"}
            if RAG_SYSTEM == "enhanced":
                system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, 
                                                    user_type=user_type, conversation_context=simple_context)
            else:
                system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, simple_context)
            
            # For simple messages, include recent history (last 3-5 messages) so AI knows if this is a follow-up
            history_messages = []
            if messages:
                recent_messages = messages[-5:]  # Last 5 messages
                for msg in recent_messages:
                    content = msg.get("content", "").strip() if isinstance(msg, dict) else msg.content.strip()
                    if content:
                        role = "assistant" if (msg.get("is_from_ai") if isinstance(msg, dict) else msg.is_from_ai) else "user"
                        history_messages.append({"role": role, "content": content})
            
            # Use GPT-4o-mini for simple responses
            selected_model = get_model_name(ModelType.GPT_4O_MINI) if os.getenv("AZURE_OPENAI_ENDPOINT") else "gpt-4o-mini"
            max_tokens = 200
            temperature = 0.3
        else:
            # Comprehensive response with integrated analysis for complex queries
            # Check if this is an onboarding/getting started query - needs more tokens
            user_message_lower = user_message.lower()
            is_onboarding_query = any(phrase in user_message_lower for phrase in [
                "get started", "getting started", "let's get started", "lets get started",
                "how do i get started", "how to get started", "how can i get started",
                "where do i start", "where should i start", "where to start",
                "i'm new", "im new", "i am new", "new to notal", "new user"
            ])
            
            # Build document context section if available
            doc_context_section = _build_document_context_section(document_context)
            
            # Prepare conversation history as proper message objects
            history_messages, context_metadata = await _prepare_conversation_history_as_messages(
                messages,
                user_type,
                conversation_analysis,
                token_budget=MAX_CONVERSATION_CONTEXT_TOKENS
            )

            logger.info(
                "Context strategy '%s' applied for conversation %s (original≈%s tokens, optimized≈%s tokens, budget=%s)",
                context_metadata.get("history_strategy"),
                conversation_id or "unknown",
                context_metadata.get("original_token_estimate"),
                context_metadata.get("optimized_token_estimate"),
                context_metadata.get("token_budget")
            )
            
            base_system_prompt = f"""You are Notal AI, an advanced legal assistant. Provide a comprehensive response that includes both conversation and analysis.

CONVERSATION CONTEXT:
- User Type: {user_type}
- Message Count: {len(messages)}
- Legal Topics: {', '.join(conversation_analysis.get('legal_topics', []))}
- Urgency: {conversation_analysis.get('urgency_level', 'Medium')}
- Conversation Stage: {conversation_analysis.get('conversation_stage', 'Initial')}

NOTE: You will receive the full conversation history as separate messages. Pay attention to:
- References like "that matter", "this task", "the second option" refer to previous messages
- When user says "tell me more" or "what about X", look at the immediate previous context
- Maintain continuity across the conversation

{doc_context_section if doc_context_section else ''}"""

            # Enhance prompt with RAG context for Certio-specific knowledge
            conversation_context_payload = {
                "user_type": user_type,
                "message_count": len(messages),
                "legal_topics": conversation_analysis.get('legal_topics', []),
                "urgency": conversation_analysis.get('urgency_level', 'Medium'),
                "conversation_stage": conversation_analysis.get('conversation_stage', 'Initial')
            }
            
            # Use RAG enhancement to inject Notal-specific knowledge
            logger.info(f"🔍 Enhancing conversational prompt with RAG for user_type: {user_type}")
            # Enhanced RAG system supports user_type parameter for better context
            if RAG_SYSTEM == "enhanced":
                enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, 
                                                      user_type=user_type, conversation_context=conversation_context_payload)
            else:
                enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, conversation_context_payload)
            logger.info(f"✅ RAG enhancement completed for conversational response")
            
            # Add user data context from all modules (Matters, Tasks, Calendar, etc.)
            user_data_context_section = ""
            if USER_DATA_RAG_AVAILABLE:
                try:
                    # Extract user_id and organization_id from payload if available
                    user_id = payload.get("user_id")
                    organization_id = payload.get("organization_id")
                    
                    if user_id and organization_id:
                        logger.info(f"🔍 Fetching user data context for User {user_id} in Org {organization_id}")
                        user_data_context = await get_user_data_context(
                            user_id, organization_id, user_message, 
                            agent_type="ConversationalAI", top_k=8
                        )
                        if user_data_context and len(user_data_context) > 50:  # Has meaningful content
                            user_data_context_section = f"\n\n{user_data_context}"
                            logger.info(f"✅ Added user data context ({len(user_data_context)} chars) from user's modules")
                        else:
                            logger.info("ℹ️ No relevant user data context found")
                    else:
                        logger.debug("ℹ️ User ID or Organization ID not provided, skipping user data context")
                except Exception as e:
                    logger.warning(f"Failed to retrieve user data context: {e}")
            
            enhanced_prompt = enhanced_prompt + user_data_context_section
            
            # Add the response requirements to the enhanced prompt
            system_prompt = enhanced_prompt + f"""

🚨 CRITICAL ANTI-HALLUCINATION RULES:
1. If you received "USER'S ACTUAL DATA" context above, you MUST use ONLY that exact data
2. DO NOT invent or make up information about matters, practice areas, dates, or team members
3. If a field says "Not specified" or is missing, acknowledge it - don't fill it in
4. For questions about user data (matters, tasks, messages), quote the actual data provided
5. Do NOT start responses with "Hello [Name]," - only greet in the first message of a conversation
6. Pay attention to pronouns like "that matter", "this task" - they refer to the previous message

📄 DOCUMENT & CITATION RULES:
1. If you FOUND the requested document/section in the data above, CITE IT DIRECTLY - do NOT say "I cannot access" or "I'm unable to access"
2. When asked to "cite" something, extract and present the EXACT text from the document
3. Always specify which document you're citing from (e.g., "From EECS 170LA Post-Lab 7:")
4. If the document exists but the specific section is not in the extracted content, say "The document is in your files but the specific section wasn't extracted. Here's what I found..."
5. ONLY say "I don't have that document" if the document literally does not appear in the data above
6. When you have the content, be CONFIDENT - present it directly without hedging

🎯 TEMPORAL & CONTEXTUAL UNDERSTANDING:
1. When user says "most recent", "recent", "latest", "last" - LOOK AT THE DATA and find the actual most recent items by date
2. When user asks to "analyze" something specific - READ THE ACTUAL CONTENT and provide SPECIFIC analysis, NOT generic instructions
3. When user says "tell me about X" - EXTRACT AND PRESENT the actual data about X, don't explain how they could look it up
4. For "before/after" requests - SHOW ACTUAL CONTENT with specific improvements, not generic advice
5. NEVER give "how-to" instructions when the user wants you to DO THE ANALYSIS YOURSELF
6. If the data contains the answer, USE IT DIRECTLY - don't redirect the user to review it themselves

RESPONSE REQUIREMENTS:
1. Provide a helpful, conversational response to the user's request
2. When referencing user data, use the EXACT information from the context (e.g., exact practice areas, dates, names)
3. Include relevant insights and suggestions
4. Identify key topics and potential next steps
5. Be specific and actionable
6. Use proper HTML formatting with <p> tags, <br> for line breaks, <ul><li> for lists, and <strong> for emphasis
7. NO repeated greetings - jump straight to answering the question

RESPONSE FORMAT:
<response>
[Your conversational response here - use proper HTML formatting]
</response>

<analysis>
<summary>Brief conversation summary</summary>
<key_points>
- Key point 1
- Key point 2
</key_points>
<legal_topics>Detected topics</legal_topics>
<suggested_actions>
- Action 1
- Action 2
</suggested_actions>
</analysis>

IMPORTANT: Format your response using proper HTML tags, not markdown or raw text. Use <p> for paragraphs, <br> for line breaks, <ul><li> for lists, and <strong> for bold text.

Respond as an intelligent assistant:"""
            
            # Use GPT-4o for complex responses
            # Use is_onboarding_query from earlier check
            selected_model = get_model_name(ModelType.GPT_4O) if os.getenv("AZURE_OPENAI_ENDPOINT") else "gpt-4o"
            # Increase max_tokens for onboarding queries to ensure complete responses
            max_tokens = 3000 if is_onboarding_query else 1500
            temperature = 0.7
        
        # Analyze task complexity for cost optimization
        context_length = len(messages) if messages else 0
        logger.info(f"DEBUG: Analyzing task - user_message='{user_message}', context_length={context_length}, user_type='{user_type}'")
        task_complexity = task_analyzer.analyze_task(user_message, context_length, user_type)
        logger.info(f"DEBUG: Task complexity result - score={task_complexity.complexity_score}, tokens={task_complexity.estimated_tokens}")
        
        # Override model selection for simple messages
        if is_simple_message:
            optimal_model_type = ModelType.GPT_4O_MINI
            estimated_cost = 0.0003
        else:
            optimal_model_type, estimated_cost = model_selector.select_optimal_model(task_complexity)
            # Use the get_model_name function for consistency
            if optimal_model_type:
                selected_model = get_model_name(optimal_model_type)
        
        # Log cost optimization decision
        logger.info(f"Conversational response - Selected model: {selected_model} (estimated cost: ${estimated_cost:.4f}) for complexity: {task_complexity.complexity_score:.2f}")
        
        # Wait for rate limiter before making request
        await rate_limiter.wait_if_needed()
        
        # If streaming is not requested, return complete response
        if not stream:
            # Build proper message array with conversation history
            api_messages = []
            
            # Add system message first
            api_messages.append({"role": "system", "content": system_prompt})
            
            # Add conversation history messages (if not simple message)
            if not is_simple_message and history_messages:
                api_messages.extend(history_messages)
            
            # Add current user message (with images if present)
            if image_urls:
                # Use vision model for image analysis
                logger.info(f"Using vision model with {len(image_urls)} image(s)")
                vision_model = get_model_name(ModelType.GPT_4O) if os.getenv("AZURE_OPENAI_ENDPOINT") else "gpt-4o"
                selected_model = vision_model
                
                # Prepare vision message content
                vision_content = prepare_vision_message(user_message, image_urls)
                api_messages.append({"role": "user", "content": vision_content})
                max_tokens = max(max_tokens, 2000)  # Vision analysis may need more tokens
            else:
                # Standard text message
                api_messages.append({"role": "user", "content": user_message})
            
            logger.info(f"Sending {len(api_messages)} messages to AI (including {len(history_messages) if history_messages else 0} history messages)")
            
            # Make API call with full conversation context
            response = client.chat.completions.create(
                model=selected_model,
                messages=api_messages,
                max_tokens=max_tokens,
                temperature=temperature
            )
            
            # Track usage for cost optimization
            usage_tracker.record_model_selection(optimal_model_type, task_complexity, estimated_cost)
            
            # Extract response content
            response_content = response.choices[0].message.content.strip()
            
            # For simple messages, return the response directly
            if is_simple_message:
                return response_content
            
            # For complex messages, extract the response part and return it
            # The analysis part will be processed by background agents if needed
            if "<response>" in response_content and "</response>" in response_content:
                response_part = response_content.split("<response>")[1].split("</response>")[0].strip()
                return response_part
            else:
                return response_content
        
    except Exception as e:
        import traceback
        error_trace = traceback.format_exc()
        logger.error(f"Error in conversational_response: {str(e)}\n{error_trace}")
        
        # Check for common error types and provide more specific messages
        error_str = str(e).lower()
        if "api key" in error_str or "authentication" in error_str or "401" in error_str or "403" in error_str:
            logger.error("Azure OpenAI authentication failed - check AZURE_OPENAI_API_KEY and AZURE_OPENAI_ENDPOINT")
            return "I apologize, but I'm experiencing authentication issues with the AI service. Please contact support."
        elif "quota" in error_str or "429" in error_str:
            logger.error("Azure OpenAI quota/rate limit exceeded")
            return "I apologize, but the AI service is currently experiencing high demand. Please try again in a moment."
        elif "endpoint" in error_str or "connection" in error_str:
            logger.error("Azure OpenAI endpoint connection failed - check AZURE_OPENAI_ENDPOINT")
            return "I apologize, but I'm unable to connect to the AI service. Please contact support."
        else:
            # Generic error - log full traceback for debugging
            logger.error(f"Unexpected error in conversational_response: {error_trace}")
            return "I apologize, but I'm experiencing technical difficulties right now. Please try again in a moment, or contact our support team if the issue persists. I'm here to help with your legal questions and concerns."

@app.post("/agents/conversational-response-stream")
async def conversational_response_stream(payload: dict, _: str = Depends(authenticate_request)):
    """Generate streaming conversational AI response with RAG and dynamic model selection"""
    from starlette.responses import StreamingResponse
    
    async def generate_stream():
        try:
            conversation_id = payload.get("conversation_id", "")
            messages = payload.get("messages", [])
            user_message = payload.get("user_message", "")
            user_type = payload.get("user_type", "Client")
            document_context = payload.get("document_context", None)
            ai_model_tier = payload.get("ai_model_tier", "Auto")  # Get tier preference
            user_id = payload.get("user_id")
            organization_id = payload.get("organization_id")

            async def fetch_user_data_context():
                """Retrieve user-specific data context for RAG if available."""
                if not USER_DATA_RAG_AVAILABLE or not user_id or not organization_id:
                    return ""
                try:
                    logger.info(f"🔍 Fetching user data context (stream) for User {user_id} Org {organization_id}")
                    context_text = await get_user_data_context(
                        user_id,
                        organization_id,
                        user_message,
                        agent_type="ConversationalAI",
                        top_k=8
                    )
                    if context_text and len(context_text) > 50:
                        logger.info(f"✅ Added streaming user data context ({len(context_text)} chars)")
                        return f"\n\n{context_text}"
                    logger.info("ℹ️ No streaming user data context available")
                except Exception as exc:
                    logger.warning(f"Streaming user data context unavailable: {exc}")
                return ""
            
            # Check if this is a dashboard card request (conversation_id starts with "dashboard-")
            is_dashboard_card = conversation_id.startswith("dashboard-")
            
            # Quick analysis for conversation context (no API call)
            conversation_analysis = _quick_conversation_analysis(messages, user_message, user_type)
            
            # Check if this is a simple greeting or short message
            is_simple_message = _is_simple_message(user_message, conversation_analysis)
            
            # Determine model selection based on tier preference
            force_model_type = None
            if is_dashboard_card:
                # Always use mini for dashboard cards to reduce costs
                force_model_type = ModelType.GPT_4O_MINI
                logger.info("Dashboard card detected - forcing gpt-4o-mini for cost optimization")
            elif ai_model_tier == "Basic":
                force_model_type = ModelType.GPT_4O_MINI
            elif ai_model_tier == "Intermediate":
                force_model_type = ModelType.GPT_4_1_MINI
            elif ai_model_tier == "Advanced":
                force_model_type = ModelType.GPT_4O
            elif ai_model_tier == "Premium":
                # For Premium tier, use GPT-4.1 (o1-preview) - will auto-upgrade to GPT-5 when available
                gpt5_deployment = os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT5", "")
                if gpt5_deployment:
                    force_model_type = ModelType.GPT_5
                    logger.info("Premium tier using GPT-5")
                else:
                    force_model_type = ModelType.GPT_4_1
                    logger.info("Premium tier using GPT-4.1 (o1-preview)")
            # else: Auto - use dynamic selection (3-tier complexity)
            
            if is_simple_message:
                # Simple response for greetings and short messages - enhanced with RAG
                base_simple_prompt = f"""You are Notal AI, a friendly legal assistant. The user said: "{user_message}"

Respond with a brief, warm greeting and offer to help with legal questions. Keep it conversational and under 50 words.

CRITICAL RULES:
- Do NOT start with "Hello [Name]" or greet the user again if they already said hi
- Only say their name in the VERY FIRST message of a new conversation
- For follow-up messages, jump straight to answering their question
- Be friendly but don't waste words on repeated greetings
IMPORTANT: Return ONLY the HTML content with <p> tags and <br> for line breaks. Do NOT wrap your response in ```html code blocks or any other markdown formatting. Return the raw HTML directly."""
                
                # Even simple messages get RAG enhancement for Notal context
                simple_context = {"user_type": user_type, "message_type": "greeting"}
                if RAG_SYSTEM == "enhanced":
                    system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, 
                                                        user_type=user_type, conversation_context=simple_context)
                else:
                    system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, simple_context)

                # Attach user data context if available
                user_data_context_section = await fetch_user_data_context()
                if user_data_context_section:
                    system_prompt += user_data_context_section
                
                # Use GPT-4o-mini for simple responses (unless tier forces different model)
                if force_model_type:
                    selected_model = get_model_name(force_model_type)
                else:
                    selected_model = get_model_name(ModelType.GPT_4O_MINI)
                max_tokens = 200
                temperature = 0.3
            else:
                # Comprehensive response with integrated analysis for complex queries
                # Check if this is an onboarding/getting started query - needs more tokens
                user_message_lower = user_message.lower()
                is_onboarding_query = any(phrase in user_message_lower for phrase in [
                    "get started", "getting started", "let's get started", "lets get started",
                    "how do i get started", "how to get started", "how can i get started",
                    "where do i start", "where should i start", "where to start",
                    "i'm new", "im new", "i am new", "new to notal", "new user"
                ])
                
                # Build document context section if available
                doc_context_section = _build_document_context_section(document_context)
                
                history_block, context_metadata = await _prepare_conversation_history(
                    messages,
                    user_type,
                    conversation_analysis,
                    token_budget=MAX_CONVERSATION_CONTEXT_TOKENS
                )

                logger.info(
                    "Streaming context strategy '%s' applied for conversation %s (original≈%s tokens, optimized≈%s tokens, budget=%s)",
                    context_metadata.get("history_strategy"),
                    conversation_id or "unknown",
                    context_metadata.get("original_token_estimate"),
                    context_metadata.get("optimized_token_estimate"),
                    context_metadata.get("token_budget")
                )
                
                base_system_prompt = f"""You are Notal AI, an advanced legal assistant. Provide a comprehensive, helpful response.

CONVERSATION CONTEXT:
- User Type: {user_type}
- Message Count: {len(messages)}
- Legal Topics: {', '.join(conversation_analysis.get('legal_topics', []))}
- Urgency: {conversation_analysis.get('urgency_level', 'Medium')}
- Conversation Stage: {conversation_analysis.get('conversation_stage', 'Initial')}

OPTIMIZED CONVERSATION HISTORY ({context_metadata.get('history_strategy', 'full')}):
{history_block}

CURRENT REQUEST: {user_message}{doc_context_section}"""

                # Enhance prompt with RAG context for Certio-specific knowledge
                conversation_context_payload = {
                    "user_type": user_type,
                    "message_count": len(messages),
                    "legal_topics": conversation_analysis.get('legal_topics', []),
                    "urgency": conversation_analysis.get('urgency_level', 'Medium'),
                    "conversation_stage": conversation_analysis.get('conversation_stage', 'Initial')
                }
                
                # Use RAG enhancement to inject Notal-specific knowledge
                logger.info(f"🔍 Enhancing streaming prompt with RAG for user_type: {user_type}")
                if RAG_SYSTEM == "enhanced":
                    enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, 
                                                          user_type=user_type, conversation_context=conversation_context_payload)
                else:
                    enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, conversation_context_payload)
                logger.info(f"✅ RAG enhancement completed for streaming response")

                # Attach user data context if available
                user_data_context_section = await fetch_user_data_context()
                if user_data_context_section:
                    enhanced_prompt = enhanced_prompt + user_data_context_section
                
                # Add the response requirements to the enhanced prompt
                system_prompt = enhanced_prompt + """

🚨 CRITICAL ANTI-HALLUCINATION RULES:
1. If you received "USER'S ACTUAL DATA" context above, you MUST use ONLY that exact data
2. DO NOT invent or make up information about matters, practice areas, dates, or team members  
3. If a field says "Not specified" or is missing, acknowledge it - don't fill it in
4. For questions about user data (matters, tasks, messages), quote the actual data provided
5. Do NOT start responses with "Hello [Name]," - only greet in the first message of a conversation
6. Pay attention to pronouns like "that matter", "this task" - they refer to the previous message

📄 DOCUMENT & CITATION RULES:
1. If you FOUND the requested document/section in the data above, CITE IT DIRECTLY - do NOT say "I cannot access" or "I'm unable to access"
2. When asked to "cite" something, extract and present the EXACT text from the document
3. Always specify which document you're citing from (e.g., "From EECS 170LA Post-Lab 7:")
4. If the document exists but the specific section is not in the extracted content, say "The document is in your files but the specific section wasn't extracted. Here's what I found..."
5. ONLY say "I don't have that document" if the document literally does not appear in the data above
6. When you have the content, be CONFIDENT - present it directly without hedging

🎯 TEMPORAL & CONTEXTUAL UNDERSTANDING:
1. When user says "most recent", "recent", "latest", "last" - LOOK AT THE DATA and find the actual most recent items by date
2. When user asks to "analyze" something specific - READ THE ACTUAL CONTENT and provide SPECIFIC analysis, NOT generic instructions
3. When user says "tell me about X" - EXTRACT AND PRESENT the actual data about X, don't explain how they could look it up
4. For "before/after" requests - SHOW ACTUAL CONTENT with specific improvements, not generic advice
5. NEVER give "how-to" instructions when the user wants you to DO THE ANALYSIS YOURSELF
6. If the data contains the answer, USE IT DIRECTLY - don't redirect the user to review it themselves

RESPONSE REQUIREMENTS:
1. Provide a helpful, conversational response to the user's request
2. When referencing user data, use the EXACT information from the context (e.g., exact practice areas, dates, names)
3. Include relevant insights and suggestions
4. Be specific and actionable
5. Use proper HTML formatting with <p> tags for paragraphs, <ul><li> for lists, and <strong> for emphasis
6. Do NOT use <br> tags - use separate <p> tags for new paragraphs instead
7. NO repeated greetings - jump straight to answering the question

CRITICAL: Return ONLY the HTML content. Do NOT wrap your response in ```html code blocks or any markdown formatting. Return the raw HTML directly - just the <p> tags and their content.

Respond as an intelligent assistant:"""
                
                # Determine model based on tier preference or dynamic selection
                optimal_model_type = None
                estimated_cost = 0.0
                
                if force_model_type:
                    # Tier preference overrides dynamic selection
                    optimal_model_type = force_model_type
                    selected_model = get_model_name(optimal_model_type)
                    # Set cost estimates based on model type
                    if optimal_model_type == ModelType.GPT_5:
                        estimated_cost = 0.01  # GPT-5 cost estimate
                    elif optimal_model_type == ModelType.GPT_4_1:
                        estimated_cost = 0.02  # GPT-4.1 (o1-preview) cost estimate
                    elif optimal_model_type == ModelType.GPT_4O:
                        estimated_cost = 0.003
                    elif optimal_model_type == ModelType.GPT_4_1_MINI:
                        estimated_cost = 0.005  # GPT-4.1-mini (o1-mini) cost estimate
                    else:
                        estimated_cost = 0.0003
                else:
                    # Auto mode: Use dynamic selection based on complexity
                    # Analyze task complexity for cost optimization
                    context_length = len(messages) if messages else 0
                    # Force mini for dashboard cards by passing force_mini=True
                    task_complexity = task_analyzer.analyze_task(user_message, context_length, user_type, force_mini=is_dashboard_card)
                    optimal_model_type, estimated_cost = model_selector.select_optimal_model(task_complexity)
                    if optimal_model_type:
                        selected_model = get_model_name(optimal_model_type)
                    else:
                        selected_model = get_model_name(ModelType.GPT_4O)
                
                # Set max_tokens and temperature for complex responses
                max_tokens = 3000 if is_onboarding_query else 1500
                temperature = 0.7
            
            # For simple messages, set optimal_model_type if not already set
            if is_simple_message:
                if not force_model_type:
                    optimal_model_type = ModelType.GPT_4O_MINI
                    estimated_cost = 0.0003
                elif force_model_type and optimal_model_type is None:
                    optimal_model_type = force_model_type
            
            # Log cost optimization decision
            tier_info = f" (tier: {ai_model_tier})" if ai_model_tier != "Auto" else ""
            logger.info(f"Streaming response - Selected model: {selected_model}{tier_info} (estimated cost: ${estimated_cost:.4f})")
            
            # Wait for rate limiter before making request
            await rate_limiter.wait_if_needed()
            
            # Create streaming response from Azure OpenAI
            stream_response = client.chat.completions.create(
                model=selected_model,
                messages=[
                    {"role": "system", "content": system_prompt}
                ],
                max_tokens=max_tokens,
                temperature=temperature,
                stream=True  # Enable streaming
            )
            
            # Track usage for cost optimization (only if we have optimal_model_type)
            if optimal_model_type:
                context_length = len(messages) if messages else 0
                task_complexity = task_analyzer.analyze_task(user_message, context_length, user_type)
                usage_tracker.record_model_selection(optimal_model_type, task_complexity, estimated_cost)
            
            # Stream chunks to client
            full_content = ""
            for chunk in stream_response:
                if chunk.choices and len(chunk.choices) > 0:
                    delta = chunk.choices[0].delta
                    if hasattr(delta, 'content') and delta.content:
                        content = delta.content
                        full_content += content
                        # Send chunk as Server-Sent Event
                        yield f"data: {json.dumps({'content': content, 'done': False})}\n\n"
            
            # For complex messages, extract only the response part
            if not is_simple_message and "<response>" in full_content and "</response>" in full_content:
                # We've already streamed it, just signal completion
                yield f"data: {json.dumps({'content': '', 'done': True})}\n\n"
            else:
                # Signal completion
                yield f"data: {json.dumps({'content': '', 'done': True})}\n\n"
                
        except Exception as e:
            import traceback
            error_trace = traceback.format_exc()
            logger.error(f"Error in streaming response: {str(e)}\n{error_trace}")
            
            # Check for common error types
            error_str = str(e).lower()
            if "api key" in error_str or "authentication" in error_str or "401" in error_str or "403" in error_str:
                logger.error("Azure OpenAI authentication failed - check AZURE_OPENAI_API_KEY and AZURE_OPENAI_ENDPOINT")
                error_message = "I apologize, but I'm experiencing authentication issues with the AI service. Please contact support."
            elif "quota" in error_str or "429" in error_str:
                logger.error("Azure OpenAI quota/rate limit exceeded")
                error_message = "I apologize, but the AI service is currently experiencing high demand. Please try again in a moment."
            elif "endpoint" in error_str or "connection" in error_str:
                logger.error("Azure OpenAI endpoint connection failed - check AZURE_OPENAI_ENDPOINT")
                error_message = "I apologize, but I'm unable to connect to the AI service. Please contact support."
            else:
                logger.error(f"Unexpected error in streaming response: {error_trace}")
                error_message = "I apologize, but I'm experiencing technical difficulties right now. Please try again in a moment."
            yield f"data: {json.dumps({'content': error_message, 'done': True, 'error': True})}\n\n"
    
    return StreamingResponse(
        generate_stream(), 
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",  # Disable nginx buffering
        }
    )

def _quick_conversation_analysis(messages: List[dict], user_message: str, user_type: str) -> dict:
    """Quick conversation analysis without API calls - similar to Cursor's approach"""
    analysis = {
        'conversation_stage': 'Initial',
        'legal_topics': [],
        'urgency_level': 'Low',
        'message_count': len(messages),
        'has_legal_content': False
    }
    
    if not messages:
        return analysis
    
    # Determine conversation stage
    if len(messages) == 1:
        analysis['conversation_stage'] = 'Initial'
    elif len(messages) < 5:
        analysis['conversation_stage'] = 'Early'
    elif len(messages) < 10:
        analysis['conversation_stage'] = 'Developing'
    else:
        analysis['conversation_stage'] = 'Advanced'
    
    # Quick legal topic detection
    all_content = " ".join([msg.get('content', '') for msg in messages]) + " " + user_message
    content_lower = all_content.lower()
    
    legal_topics = []
    topic_keywords = {
        "Contract Law": ["contract", "agreement", "terms", "clause", "breach", "liability"],
        "Employment Law": ["employment", "hiring", "termination", "discrimination", "harassment", "wage"],
        "Business Formation": ["incorporation", "llc", "corporation", "partnership", "business formation"],
        "Intellectual Property": ["patent", "trademark", "copyright", "intellectual property", "ip"],
        "Real Estate": ["real estate", "property", "lease", "rental", "mortgage", "title"],
        "Litigation": ["lawsuit", "litigation", "dispute", "court", "settlement", "trial"],
        "Compliance": ["compliance", "regulation", "audit", "certification", "regulatory"]
    }
    
    for topic, keywords in topic_keywords.items():
        if any(keyword in content_lower for keyword in keywords):
            legal_topics.append(topic)
            analysis['has_legal_content'] = True
    
    analysis['legal_topics'] = legal_topics
    
    # Quick urgency assessment
    urgency_indicators = ["urgent", "asap", "immediately", "emergency", "critical", "deadline", "rush"]
    if any(indicator in content_lower for indicator in urgency_indicators):
        analysis['urgency_level'] = 'High'
    elif any(word in content_lower for word in ["today", "tomorrow", "this week", "deadline"]):
        analysis['urgency_level'] = 'Medium'
    
    return analysis

def _is_simple_message(user_message: str, conversation_analysis: dict) -> bool:
    """Determine if this is a simple message that doesn't need complex processing"""
    message_lower = user_message.lower().strip()
    
    if not message_lower:
        return True
    
    # NEVER treat "get started" queries as simple - they need comprehensive guidance
    getting_started_phrases = [
        "get started", "getting started", "let's get started", "lets get started",
        "how do i get started", "how to get started", "how can i get started",
        "where do i start", "where should i start", "where to start",
        "i'm new", "im new", "i am new", "new to notal", "new user"
    ]
    
    if any(phrase in message_lower for phrase in getting_started_phrases):
        return False  # Always treat as complex for comprehensive response
    
    # Treat explicit inquiries or anything with a question mark as complex
    if "?" in message_lower:
        return False
    
    inquiry_keywords = [
        "tell me", "tell us", "what", "who", "where", "when", "why", "how",
        "explain", "describe", "detail", "analyze", "analysis", "review",
        "document", "contract", "policy", "job", "role", "position",
        "lab", "notes", "summary", "compare", "difference"
    ]
    
    if any(keyword in message_lower for keyword in inquiry_keywords):
        return False
    
    # Simple greetings / acknowledgements
    simple_greetings = [
        "hello", "hi", "hey", "good morning", "good afternoon", "good evening",
        "how are you", "how are you?", "what's up", "what's up?", "how's it going",
        "how's it going?", "how do you do", "how do you do?", "nice to meet you",
        "thanks", "thank you", "ok", "okay", "yes", "no", "sure", "alright",
        "bye", "goodbye", "see you", "later", "ok bye", "thanks bye"
    ]
    
    if message_lower in simple_greetings:
        return True
    
    # Very short acknowledgements (non-inquiries)
    if len(message_lower) <= 4:
        return True
    
    # Messages that are just punctuation or numbers
    if all(c in r'.,!?;:()[]{}"\'`~@#$%^&*+=|\\/<>' or c.isdigit() or c.isspace() for c in message_lower):
        return True
    
    # Preserve messages that already triggered legal content detection
    if conversation_analysis.get('has_legal_content', False):
        return False
    
    return False

def _normalize_messages(messages: List[Union[dict, ChatMessage]]) -> List[Dict[str, Any]]:
    """Normalize conversation messages to dictionaries"""
    normalized: List[Dict[str, Any]] = []
    for msg in messages:
        if isinstance(msg, ChatMessage):
            normalized.append(msg.model_dump())
        else:
            normalized.append(msg)
    def _sort_key(item: Dict[str, Any]) -> datetime:
        created_at = item.get("created_at")
        if not created_at:
            return datetime.min.replace(tzinfo=timezone.utc)
        try:
            if isinstance(created_at, (int, float)):
                # Treat numeric timestamps as epoch seconds
                return datetime.fromtimestamp(float(created_at), tz=timezone.utc)
            if isinstance(created_at, datetime):
                return created_at if created_at.tzinfo else created_at.replace(tzinfo=timezone.utc)
            created_str = str(created_at)
            if created_str.endswith("Z"):
                created_str = created_str.replace("Z", "+00:00")
            return datetime.fromisoformat(created_str)
        except Exception:
            return datetime.min.replace(tzinfo=timezone.utc)

    normalized.sort(key=_sort_key)
    return normalized

def _format_messages_for_context(messages: List[Dict[str, Any]], max_messages: Optional[int] = None) -> str:
    """Format messages into a readable context block"""
    if not messages:
        return "No previous conversation."

    selected_messages = messages[-max_messages:] if max_messages else messages
    context_parts: List[str] = []

    for msg in selected_messages:
        speaker = msg.get("user_type") or ("AI" if msg.get("is_from_ai") else "User")
        content = (msg.get("content") or "").strip()
        if not content:
            continue
        context_parts.append(f"{speaker}: {content}")

    return "\n".join(context_parts) if context_parts else "No previous conversation."

def _estimate_token_count_from_text(text: str) -> int:
    """Rough token estimate assuming ~4 characters per token"""
    if not text:
        return 0
    return max(1, len(text) // 4)

def _conversation_stage_to_complexity(stage: str) -> float:
    """Convert conversation stage to approximate complexity score"""
    stage_map = {
        "Initial": 0.25,
        "Early": 0.4,
        "Developing": 0.7,
        "Advanced": 0.9
    }
    return stage_map.get(stage, 0.5)

def _format_summary_for_context(summary: Optional[ConversationSummary]) -> str:
    """Convert a ConversationSummary into a context string"""
    if not summary:
        return "Summary unavailable."

    parts = [
        f"Summary: {summary.summary}",
    ]

    if summary.key_points:
        parts.append("Key Points:")
        for point in summary.key_points[:5]:
            parts.append(f"- {point}")

    if summary.suggested_actions:
        parts.append("Suggested Actions:")
        for action in summary.suggested_actions[:3]:
            parts.append(f"- {action}")

    if summary.urgency:
        parts.append(f"Urgency: {summary.urgency}")

    if summary.sentiment:
        parts.append(f"Sentiment: {summary.sentiment}")

    return "\n".join(parts)

def _quick_local_summary(messages: List[Dict[str, Any]]) -> ConversationSummary:
    """Generate a lightweight summary without external API calls"""
    if not messages:
        return ConversationSummary(
            summary="No prior messages available.",
            key_points=[],
            sentiment="Neutral",
            urgency="Low",
            suggested_actions=[]
        )

    user_messages = [m for m in messages if not m.get("is_from_ai")]
    ai_messages = [m for m in messages if m.get("is_from_ai")]

    latest_user = user_messages[-1]["content"] if user_messages else messages[-1].get("content", "")
    earliest_user = user_messages[0]["content"] if user_messages else latest_user

    summary_parts = []
    if earliest_user and earliest_user != latest_user:
        summary_parts.append(f"Initial request: {earliest_user[:280]}")
    if latest_user:
        summary_parts.append(f"Most recent user message: {latest_user[:280]}")

    if not summary_parts:
        summary_parts.append("Conversation contains primarily AI responses.")

    key_points = []
    for msg in user_messages[-3:]:
        content = msg.get("content", "")
        if content:
            key_points.append(content[:160])

    sentiment = "Neutral"
    urgency = "Low"
    lowered = " ".join(m.get("content", "").lower() for m in user_messages[-5:])
    if any(word in lowered for word in ["urgent", "asap", "immediately", "deadline"]):
        urgency = "High"
    elif any(word in lowered for word in ["today", "tomorrow", "soon"]):
        urgency = "Medium"

    if any(word in lowered for word in ["thank", "great", "awesome"]):
        sentiment = "Positive"
    elif any(word in lowered for word in ["upset", "frustrated", "annoyed", "angry"]):
        sentiment = "Negative"

    return ConversationSummary(
        summary=" ".join(summary_parts),
        key_points=key_points,
        sentiment=sentiment,
        urgency=urgency,
        suggested_actions=[]
    )

async def _prepare_conversation_history_as_messages(
    messages: List[Union[dict, ChatMessage]],
    user_type: str,
    conversation_analysis: Dict[str, Any],
    token_budget: Optional[int] = None
) -> Tuple[List[Dict[str, str]], Dict[str, Any]]:
    """
    Build conversation context as proper message objects for OpenAI API.
    Returns list of message dicts with 'role' and 'content' keys.
    """
    if not messages:
        return [], {
            "history_strategy": "empty",
            "original_token_estimate": 0,
            "optimized_token_estimate": 0
        }

    normalized_messages = _normalize_messages(messages)
    token_budget = token_budget or MAX_CONVERSATION_CONTEXT_TOKENS

    # Convert to OpenAI message format
    api_messages = []
    total_tokens = 0
    
    for msg in normalized_messages:
        content = (msg.get("content") or "").strip()
        if not content:
            continue
        
        # Determine role - AI messages are "assistant", user messages are "user"
        role = "assistant" if msg.get("is_from_ai") else "user"
        
        api_messages.append({
            "role": role,
            "content": content
        })
        
        total_tokens += _estimate_token_count_from_text(content)

    history_metadata: Dict[str, Any] = {
        "history_strategy": "full",
        "original_token_estimate": total_tokens,
        "token_budget": token_budget,
        "message_count": len(api_messages)
    }

    # If under budget, return all messages
    if total_tokens <= token_budget:
        history_metadata["optimized_token_estimate"] = total_tokens
        history_metadata["compression_applied"] = False
        return api_messages, history_metadata

    # If over budget, take most recent messages that fit
    logger.info(f"Conversation history ({total_tokens} tokens) exceeds budget ({token_budget}), applying compression")
    
    compressed_messages = []
    running_total = 0
    
    # Take messages from most recent backwards
    for msg in reversed(api_messages):
        msg_tokens = _estimate_token_count_from_text(msg["content"])
        if running_total + msg_tokens > token_budget:
            break
        compressed_messages.insert(0, msg)  # Insert at beginning to maintain order
        running_total += msg_tokens
    
    # If we dropped messages, add a summary at the beginning
    if len(compressed_messages) < len(api_messages):
        dropped_count = len(api_messages) - len(compressed_messages)
        summary_msg = {
            "role": "system",
            "content": f"[Earlier conversation context: {dropped_count} earlier messages summarized - conversation involves {', '.join(conversation_analysis.get('legal_topics', []))}]"
        }
        compressed_messages.insert(0, summary_msg)
    
    history_metadata.update({
        "history_strategy": "compressed",
        "optimized_token_estimate": running_total,
        "compression_applied": True,
        "messages_kept": len(compressed_messages),
        "messages_dropped": len(api_messages) - len(compressed_messages)
    })
    
    return compressed_messages, history_metadata

async def _prepare_conversation_history(
    messages: List[Union[dict, ChatMessage]],
    user_type: str,
    conversation_analysis: Dict[str, Any],
    token_budget: Optional[int] = None
) -> Tuple[str, Dict[str, Any]]:
    """
    Build conversation context with intelligent compression and summarization.
    Implements rolling history strategy to stay within token limits.
    (Legacy version - returns string format)
    """
    if not messages:
        return "No previous conversation.", {
            "history_strategy": "empty",
            "original_token_estimate": 0,
            "optimized_token_estimate": 0
        }

    normalized_messages = _normalize_messages(messages)
    token_budget = token_budget or MAX_CONVERSATION_CONTEXT_TOKENS

    # Initial full context attempt
    full_context = _format_messages_for_context(normalized_messages)
    full_token_estimate = _estimate_token_count_from_text(full_context)

    history_metadata: Dict[str, Any] = {
        "history_strategy": "full",
        "original_token_estimate": full_token_estimate,
        "token_budget": token_budget,
        "message_count": len(normalized_messages),
        "recent_window": CONVERSATION_RECENT_MESSAGE_WINDOW
    }

    if full_token_estimate <= token_budget:
        history_metadata["optimized_token_estimate"] = full_token_estimate
        history_metadata["compression_applied"] = False
        return full_context, history_metadata

    # Intelligent compression using context manager
    stage = conversation_analysis.get("conversation_stage", "Initial")
    complexity_score = _conversation_stage_to_complexity(stage)

    optimized_context, cm_metadata = context_manager.optimize_context(
        normalized_messages,
        user_type=user_type,
        task_complexity=complexity_score
    )

    optimized_token_estimate = _estimate_token_count_from_text(optimized_context)
    history_metadata.update({
        "history_strategy": "compressed",
        "compression_applied": True,
        "compression_metadata": cm_metadata,
        "optimized_token_estimate": optimized_token_estimate
    })

    if optimized_token_estimate <= token_budget:
        return optimized_context, history_metadata

    # Fallback: summarize historical messages and retain most recent window
    recent_window = min(CONVERSATION_RECENT_MESSAGE_WINDOW, len(normalized_messages))
    historical_messages = normalized_messages[:-recent_window]
    recent_messages = normalized_messages[-recent_window:]

    summary_result: Optional[ConversationSummary] = None
    summary_text = "Summary unavailable."

    if historical_messages:
        try:
            summary_result = _quick_local_summary(historical_messages)
            summary_text = _format_summary_for_context(summary_result)
        except Exception as exc:
            logger.warning("Failed to summarize historical conversation: %s", exc, exc_info=exc)

    recent_context = _format_messages_for_context(recent_messages)
    combined_context = (
        "CONVERSATION HISTORY SUMMARY:\n"
        f"{summary_text}\n\n"
        f"RECENT CONVERSATION (last {recent_window} messages):\n"
        f"{recent_context}"
    )

    combined_token_estimate = _estimate_token_count_from_text(combined_context)
    history_metadata.update({
        "history_strategy": "summary_recent",
        "summary_applied": True,
        "optimized_token_estimate": combined_token_estimate,
        "historical_message_count": len(historical_messages),
        "summary": summary_result.model_dump() if summary_result else None
    })

    if combined_token_estimate <= token_budget:
        return combined_context, history_metadata

    # Final safeguard: truncate recent conversation to fit budget
    truncated_recent = recent_context[-(token_budget * 4):]  # Approximate char budget
    fallback_context = (
        "CONVERSATION HISTORY SUMMARY (TRUNCATED):\n"
        f"{summary_text}\n\n"
        "RECENT CONVERSATION (truncated to fit token budget):\n"
        f"{truncated_recent}"
    )

    history_metadata.update({
        "history_strategy": "summary_recent_truncated",
        "optimized_token_estimate": _estimate_token_count_from_text(fallback_context),
        "truncated": True
    })

    return fallback_context, history_metadata

def _build_document_context_section(document_context: Optional[dict], summary_limit: int = 3000, excerpt_limit: int = 1200) -> str:
    """Format document context (metadata + live excerpts) for the prompt"""
    if not document_context:
        return ""

    section_lines: List[str] = []
    doc_count = document_context.get("documentContentDocumentCount") or document_context.get("documentCount") or "0"
    provider_breakdown = document_context.get("providerBreakdown", "unknown")

    section_lines.append("# RELEVANT DOCUMENTS FROM YOUR ORGANIZATION")
    section_lines.append(f"You have access to {doc_count} relevant document(s) from: {provider_breakdown}")

    combined_context = document_context.get("combinedContext", "")
    if combined_context:
        section_lines.append("")
        section_lines.append("## Vector Summary")
        section_lines.append(combined_context[:summary_limit])

    document_contents_raw = document_context.get("documentContents")
    if document_contents_raw:
        try:
            contents = json.loads(document_contents_raw)
            if contents:
                section_lines.append("")
                section_lines.append("## Live Excerpts")
                doc_groups: Dict[str, Dict[str, Any]] = {}

                def _get_value(data: Dict[str, Any], *keys: str, default: Any = None) -> Any:
                    for key in keys:
                        if key in data:
                            return data[key]
                    return default

                def _as_bool(value: Any) -> bool:
                    if isinstance(value, bool):
                        return value
                    if value is None:
                        return False
                    return str(value).strip().lower() == "true"

                for snippet in contents:
                    if not isinstance(snippet, dict):
                        continue

                    document_id = _get_value(snippet, "documentId", "DocumentId", default=str(len(doc_groups)))
                    title = _get_value(snippet, "title", "Title", default="Untitled Document")
                    provider = _get_value(snippet, "provider", "Provider", default="unknown")
                    content_type = _get_value(snippet, "contentType", "ContentType", default="unknown")
                    metadata = _get_value(snippet, "metadata", "Metadata", default={}) or {}

                    doc_entry = doc_groups.setdefault(document_id, {
                        "title": title,
                        "provider": provider,
                        "content_type": content_type,
                        "notes": set(),
                        "chunks": []
                    })

                    has_content = _as_bool(_get_value(snippet, "hasContent", "HasContent"))
                    is_partial = _as_bool(_get_value(snippet, "isPartial", "IsPartial"))
                    is_truncated = _as_bool(_get_value(snippet, "isTruncated", "IsTruncated"))

                    if not has_content:
                        doc_entry["notes"].add("content not yet extracted")
                    if is_partial:
                        doc_entry["notes"].add("partial extraction")
                    if is_truncated:
                        doc_entry["notes"].add("excerpt shortened for prompt")

                    chunk_index = _get_value(snippet, "chunkIndex", "ChunkIndex", default=len(doc_entry["chunks"]))
                    batch_position = _get_value(metadata, "batchPosition", "batch_position", default=len(doc_entry["chunks"]) + 1)
                    chunk_source = _get_value(metadata, "chunkSource", "chunk_source", default="vector")

                    raw_excerpt = _get_value(snippet, "excerpt", "Excerpt", default="")
                    excerpt = (raw_excerpt or "").strip()
                    if excerpt and len(excerpt) > excerpt_limit:
                        excerpt = excerpt[:excerpt_limit].rstrip() + " …"

                    doc_entry["chunks"].append({
                        "order": int(batch_position) if str(batch_position).isdigit() else len(doc_entry["chunks"]) + 1,
                        "chunk_index": chunk_index,
                        "source": str(chunk_source),
                        "excerpt": excerpt
                    })

                for doc in doc_groups.values():
                    section_lines.append(f"Document: {doc['title']} (Provider: {doc['provider']}, Type: {doc['content_type']})")
                    if doc["notes"]:
                        section_lines.append(f"Notes: {', '.join(sorted(doc['notes']))}")

                    ordered_chunks = sorted(doc["chunks"], key=lambda c: c["order"])
                    for chunk in ordered_chunks:
                        section_lines.append(f"- Segment {chunk['order']} (chunk {chunk['chunk_index']}, source: {chunk['source']})")
                        if chunk["excerpt"]:
                            section_lines.append(f"  {chunk['excerpt']}")
                        else:
                            section_lines.append("  [No text extracted]")
                    section_lines.append("")
        except Exception as exc:
            logger.warning("Failed to parse documentContents for prompt enrichment: %s", exc)

    section_lines.append("")
    section_lines.append("NOTE: Reference these documents by name when they are relevant.")

    return "\n".join(line for line in section_lines if line is not None)

async def _analyze_conversation_intelligently(messages: List[dict], user_message: str, user_type: str) -> dict:
    """Perform intelligent analysis of the conversation and user message"""
    
    # Convert messages to ChatMessage objects for analysis
    chat_messages = []
    for msg in messages:
        chat_msg = ChatMessage(
            id=msg.get('id', 0),
            conversation_id=msg.get('conversation_id', ''),
            user_id=msg.get('user_id', ''),
            user_type=msg.get('user_type', ''),
            content=msg.get('content', ''),
            message_type=msg.get('message_type', 'Text'),
            is_from_ai=msg.get('is_from_ai', False),
            ai_agent_type=msg.get('ai_agent_type'),
            created_at=msg.get('created_at', ''),
            is_read=msg.get('is_read', False)
        )
        chat_messages.append(chat_msg)
    
    # Run intelligent analysis using our agents
    analysis = {
        'conversation_stage': 'Initial',
        'legal_topics': [],
        'urgency_level': 'Medium',
        'user_intent': 'General inquiry',
        'suggested_actions': [],
        'legal_complexity': 'Low'
    }
    
    # Analyze conversation patterns
    if len(chat_messages) > 0:
        # Determine conversation stage
        if len(chat_messages) == 1:
            analysis['conversation_stage'] = 'Initial'
        elif len(chat_messages) < 5:
            analysis['conversation_stage'] = 'Early'
        elif len(chat_messages) < 10:
            analysis['conversation_stage'] = 'Developing'
        else:
            analysis['conversation_stage'] = 'Advanced'
        
        # Analyze legal topics
        all_content = " ".join([msg.content for msg in chat_messages]) + " " + user_message
        analysis['legal_topics'] = _extract_legal_topics(all_content)
        
        # Determine urgency
        analysis['urgency_level'] = _assess_urgency(all_content)
        
        # Determine user intent
        analysis['user_intent'] = _determine_user_intent(user_message, all_content)
        
        # Assess legal complexity
        analysis['legal_complexity'] = _assess_legal_complexity(all_content)
        
        # Generate suggested actions
        analysis['suggested_actions'] = _generate_suggested_actions(analysis, user_message)
    
    return analysis

def _build_rich_context(messages: List[dict], analysis: dict) -> str:
    """Build rich conversation context for the AI"""
    context_parts = []
    
    # Add conversation summary
    context_parts.append(f"CONVERSATION SUMMARY:")
    context_parts.append(f"- Stage: {analysis.get('conversation_stage', 'Initial')}")
    context_parts.append(f"- Legal Topics: {', '.join(analysis.get('legal_topics', []))}")
    context_parts.append(f"- Urgency: {analysis.get('urgency_level', 'Medium')}")
    context_parts.append(f"- User Intent: {analysis.get('user_intent', 'General inquiry')}")
    context_parts.append("")
    
    # Add recent conversation history
    context_parts.append("RECENT CONVERSATION:")
    for msg in messages[-6:]:  # Last 6 messages
        if msg.get("is_from_ai"):
            context_parts.append(f"AI: {msg.get('content', '')}")
        else:
            context_parts.append(f"{msg.get('user_type', 'User')}: {msg.get('content', '')}")
    
    return "\n".join(context_parts)

def _extract_legal_topics(content: str) -> List[str]:
    """Extract legal topics from content"""
    content_lower = content.lower()
    topics = []
    
    topic_keywords = {
        "Contract Law": ["contract", "agreement", "terms", "clause", "breach", "liability"],
        "Employment Law": ["employment", "hiring", "termination", "discrimination", "harassment", "wage"],
        "Business Formation": ["incorporation", "llc", "corporation", "partnership", "business formation"],
        "Intellectual Property": ["patent", "trademark", "copyright", "intellectual property", "ip"],
        "Real Estate": ["real estate", "property", "lease", "rental", "mortgage", "title"],
        "Litigation": ["lawsuit", "litigation", "dispute", "court", "settlement", "trial"],
        "Compliance": ["compliance", "regulation", "audit", "certification", "regulatory"],
        "Document Review": ["document review", "contract review", "legal review", "due diligence"]
    }
    
    for topic, keywords in topic_keywords.items():
        if any(keyword in content_lower for keyword in keywords):
            topics.append(topic)
    
    return topics

def _assess_urgency(content: str) -> str:
    """Assess urgency level of the content"""
    content_lower = content.lower()
    
    urgent_indicators = ["urgent", "asap", "immediately", "emergency", "critical", "deadline", "rush"]
    if any(indicator in content_lower for indicator in urgent_indicators):
        return "High"
    
    time_indicators = ["today", "tomorrow", "this week", "deadline", "expires"]
    if any(indicator in content_lower for indicator in time_indicators):
        return "Medium"
    
    return "Low"

def _determine_user_intent(user_message: str, full_content: str) -> str:
    """Determine the user's intent"""
    message_lower = user_message.lower()
    content_lower = full_content.lower()
    
    if "document review" in message_lower or "review" in message_lower:
        return "Document Review Request"
    elif "contract" in message_lower or "agreement" in message_lower:
        return "Contract Assistance"
    elif "help" in message_lower or "assistance" in message_lower:
        return "General Legal Help"
    elif "question" in message_lower or "?" in user_message:
        return "Legal Question"
    elif "hello" in message_lower or "hi" in message_lower:
        return "Greeting/Introduction"
    else:
        return "General Legal Inquiry"

def _assess_legal_complexity(content: str) -> str:
    """Assess legal complexity of the content"""
    content_lower = content.lower()
    
    complex_terms = [
        "litigation", "jurisdiction", "precedent", "statute of limitations",
        "due diligence", "indemnification", "force majeure", "arbitration",
        "confidentiality agreement", "non-compete", "intellectual property",
        "securities", "merger", "acquisition", "antitrust"
    ]
    
    complex_count = sum(1 for term in complex_terms if term in content_lower)
    
    if complex_count >= 3:
        return "High"
    elif complex_count >= 1:
        return "Medium"
    else:
        return "Low"

def _generate_suggested_actions(analysis: dict, user_message: str) -> List[str]:
    """Generate suggested actions based on analysis"""
    actions = []
    
    if "Document Review" in analysis.get('user_intent', ''):
        actions.extend([
            "Upload the document for AI analysis",
            "Schedule a legal review consultation",
            "Provide document type and context"
        ])
    elif "Contract" in analysis.get('user_intent', ''):
        actions.extend([
            "Review contract terms and conditions",
            "Identify potential legal risks",
            "Suggest contract modifications"
        ])
    elif analysis.get('legal_complexity') == 'High':
        actions.extend([
            "Schedule consultation with legal expert",
            "Gather additional documentation",
            "Prepare detailed legal analysis"
        ])
    else:
        actions.extend([
            "Provide specific legal guidance",
            "Answer questions in detail",
            "Suggest next steps"
        ])
    
    return actions

def _analyze_user_message(message: str, user_type: str) -> str:
    """Analyze user message for context and intent"""
    message_lower = message.lower()
    
    # Detect question types
    question_indicators = ["?", "how", "what", "when", "where", "why", "can", "should", "would"]
    is_question = any(indicator in message_lower for indicator in question_indicators)
    
    # Detect urgency
    urgency_indicators = ["urgent", "asap", "immediately", "emergency", "critical", "deadline"]
    is_urgent = any(indicator in message_lower for indicator in urgency_indicators)
    
    # Detect legal topics
    legal_topics = []
    topic_keywords = {
        "contracts": ["contract", "agreement", "terms"],
        "employment": ["employment", "hiring", "termination", "discrimination"],
        "business": ["business", "incorporation", "llc", "corporation"],
        "litigation": ["lawsuit", "litigation", "dispute", "court"],
        "compliance": ["compliance", "regulation", "audit"]
    }
    
    for topic, keywords in topic_keywords.items():
        if any(keyword in message_lower for keyword in keywords):
            legal_topics.append(topic)
    
    analysis = f"Question: {is_question}, Urgent: {is_urgent}, Legal Topics: {', '.join(legal_topics) if legal_topics else 'General'}"
    return analysis

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
