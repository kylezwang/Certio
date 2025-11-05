from fastapi import FastAPI, HTTPException, Depends, Request, status
from pydantic import BaseModel
from typing import List, Dict, Any, Optional, Union
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
from simplified_cost_optimization import (
    SimplifiedModelSelector, TaskComplexityAnalyzer, UsageTracker,
    ModelType, TaskComplexity
)
from context_manager import IntelligentContextManager
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
            ModelType.GPT_4O_MINI: os.getenv("AZURE_OPENAI_DEPLOYMENT_GPT4O_MINI", "gpt-4o-mini")
        }
        return azure_mapping.get(model_type, "gpt-4o-mini")
    else:
        # Use regular OpenAI model names
        regular_mapping = {
            ModelType.GPT_4O: "gpt-4o",
            ModelType.GPT_4O_MINI: "gpt-4o-mini"
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

class AIAgentRequest(BaseModel):
    conversation_id: str
    messages: List[ChatMessage]
    user_type: Optional[str] = None
    text: Optional[str] = None

class AIAgentResponse(BaseModel):
    agent_type: str
    content: str
    confidence: str
    metadata: Dict[str, Any]
    requires_review: bool

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
    
    async def _call_openai(self, prompt: str, model: str = "gpt-3.5-turbo", max_tokens: int = 1000, temperature: float = 0.3, user_type: str = "Client") -> str:
        """Enhanced OpenAI API call with intelligent model selection and cost optimization"""
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
        stream = payload.get("stream", False)  # Support streaming
        
        # Quick analysis for conversation context (no API call)
        conversation_analysis = _quick_conversation_analysis(messages, user_message, user_type)
        
        # Check if this is a simple greeting or short message
        is_simple_message = _is_simple_message(user_message, conversation_analysis)
        
        if is_simple_message:
            # Simple response for greetings and short messages - enhanced with RAG
            base_simple_prompt = f"""You are Notal AI, a friendly legal assistant. The user said: "{user_message}"

Respond with a brief, warm greeting and offer to help with legal questions. Keep it conversational and under 50 words. Use HTML formatting with proper <p> tags and <br> for line breaks.

Be friendly, professional, and concise. Format your response as proper HTML."""
            
            # Even simple messages get RAG enhancement for Notal context
            simple_context = {"user_type": user_type, "message_type": "greeting"}
            if RAG_SYSTEM == "enhanced":
                system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, 
                                                    user_type=user_type, conversation_context=simple_context)
            else:
                system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, simple_context)
            
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
            
            base_system_prompt = f"""You are Notal AI, an advanced legal assistant. Provide a comprehensive response that includes both conversation and analysis.

CONVERSATION CONTEXT:
- User Type: {user_type}
- Message Count: {len(messages)}
- Legal Topics: {', '.join(conversation_analysis.get('legal_topics', []))}
- Urgency: {conversation_analysis.get('urgency_level', 'Medium')}
- Conversation Stage: {conversation_analysis.get('conversation_stage', 'Initial')}

RECENT CONVERSATION:
{_build_conversation_context(messages, 6)}

CURRENT REQUEST: {user_message}"""  # Close the base prompt here

            # Enhance prompt with RAG context for Certio-specific knowledge
            conversation_context = {
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
                                                      user_type=user_type, conversation_context=conversation_context)
            else:
                enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, conversation_context)
            logger.info(f"✅ RAG enhancement completed for conversational response")
            
            # Add the response requirements to the enhanced prompt
            system_prompt = enhanced_prompt + f"""

RESPONSE REQUIREMENTS:
1. Provide a helpful, conversational response to the user's request
2. Include relevant legal insights and suggestions
3. Identify key legal topics and potential next steps
4. Be specific and actionable
5. Use proper HTML formatting with <p> tags, <br> for line breaks, <ul><li> for lists, and <strong> for emphasis

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
<legal_topics>Detected legal topics</legal_topics>
<suggested_actions>
- Action 1
- Action 2
</suggested_actions>
</analysis>

IMPORTANT: Format your response using proper HTML tags, not markdown or raw text. Use <p> for paragraphs, <br> for line breaks, <ul><li> for lists, and <strong> for bold text.

Respond as an intelligent legal assistant:"""
            
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
            response = client.chat.completions.create(
                model=selected_model,
                messages=[
                    {"role": "system", "content": system_prompt}
                ],
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
        logger.error(f"Error in conversational_response: {str(e)}")
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
            
            # Quick analysis for conversation context (no API call)
            conversation_analysis = _quick_conversation_analysis(messages, user_message, user_type)
            
            # Check if this is a simple greeting or short message
            is_simple_message = _is_simple_message(user_message, conversation_analysis)
            
            if is_simple_message:
                # Simple response for greetings and short messages - enhanced with RAG
                base_simple_prompt = f"""You are Notal AI, a friendly legal assistant. The user said: "{user_message}"

Respond with a brief, warm greeting and offer to help with legal questions. Keep it conversational and under 50 words.

IMPORTANT: Return ONLY the HTML content with <p> tags and <br> for line breaks. Do NOT wrap your response in ```html code blocks or any other markdown formatting. Return the raw HTML directly."""
                
                # Even simple messages get RAG enhancement for Notal context
                simple_context = {"user_type": user_type, "message_type": "greeting"}
                if RAG_SYSTEM == "enhanced":
                    system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, 
                                                        user_type=user_type, conversation_context=simple_context)
                else:
                    system_prompt = enhance_agent_prompt("ConversationalAI", base_simple_prompt, user_message, simple_context)
                
                # Use GPT-4o-mini for simple responses
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
                
                base_system_prompt = f"""You are Notal AI, an advanced legal assistant. Provide a comprehensive, helpful response.

CONVERSATION CONTEXT:
- User Type: {user_type}
- Message Count: {len(messages)}
- Legal Topics: {', '.join(conversation_analysis.get('legal_topics', []))}
- Urgency: {conversation_analysis.get('urgency_level', 'Medium')}
- Conversation Stage: {conversation_analysis.get('conversation_stage', 'Initial')}

RECENT CONVERSATION:
{_build_conversation_context(messages, 6)}

CURRENT REQUEST: {user_message}"""

                # Enhance prompt with RAG context for Certio-specific knowledge
                conversation_context = {
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
                                                          user_type=user_type, conversation_context=conversation_context)
                else:
                    enhanced_prompt = enhance_agent_prompt("ConversationalAI", base_system_prompt, user_message, conversation_context)
                logger.info(f"✅ RAG enhancement completed for streaming response")
                
                # Add the response requirements to the enhanced prompt
                system_prompt = enhanced_prompt + """

RESPONSE REQUIREMENTS:
1. Provide a helpful, conversational response to the user's request
2. Include relevant legal insights and suggestions
3. Be specific and actionable
4. Use proper HTML formatting with <p> tags for paragraphs, <ul><li> for lists, and <strong> for emphasis
5. Do NOT use <br> tags - use separate <p> tags for new paragraphs instead

CRITICAL: Return ONLY the HTML content. Do NOT wrap your response in ```html code blocks or any markdown formatting. Return the raw HTML directly - just the <p> tags and their content.

Respond as an intelligent legal assistant:"""
                
                # Use GPT-4o for complex responses
                # Increase max_tokens for onboarding queries to ensure complete responses
                selected_model = get_model_name(ModelType.GPT_4O)
                max_tokens = 3000 if is_onboarding_query else 1500
                temperature = 0.7
            
            # Analyze task complexity for cost optimization
            context_length = len(messages) if messages else 0
            task_complexity = task_analyzer.analyze_task(user_message, context_length, user_type)
            
            # Override model selection for simple messages
            if is_simple_message:
                optimal_model_type = ModelType.GPT_4O_MINI
                estimated_cost = 0.0003
            else:
                optimal_model_type, estimated_cost = model_selector.select_optimal_model(task_complexity)
                if optimal_model_type:
                    selected_model = get_model_name(optimal_model_type)
            
            # Log cost optimization decision
            logger.info(f"Streaming response - Selected model: {selected_model} (estimated cost: ${estimated_cost:.4f}) for complexity: {task_complexity.complexity_score:.2f}")
            
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
            
            # Track usage for cost optimization
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
            logger.error(f"Error in streaming response: {str(e)}")
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
    
    # NEVER treat "get started" queries as simple - they need comprehensive guidance
    getting_started_phrases = [
        "get started", "getting started", "let's get started", "lets get started",
        "how do i get started", "how to get started", "how can i get started",
        "where do i start", "where should i start", "where to start",
        "i'm new", "im new", "i am new", "new to notal", "new user"
    ]
    
    if any(phrase in message_lower for phrase in getting_started_phrases):
        return False  # Always treat as complex for comprehensive response
    
    # Simple greetings
    simple_greetings = [
        "hello", "hi", "hey", "good morning", "good afternoon", "good evening",
        "how are you", "how are you?", "what's up", "what's up?", "how's it going",
        "how's it going?", "how do you do", "how do you do?", "nice to meet you",
        "thanks", "thank you", "ok", "okay", "yes", "no", "sure", "alright",
        "bye", "goodbye", "see you", "later", "ok bye", "thanks bye"
    ]
    
    if message_lower in simple_greetings:
        return True
    
    # Very short messages
    if len(message_lower) < 10:
        return True
    
    # Messages that are just punctuation or numbers
    if all(c in '.,!?;:()[]{}"\'`~@#$%^&*+=|\\/<>' or c.isdigit() or c.isspace() for c in message_lower):
        return True
    
    # If no legal content detected and message is short
    if not conversation_analysis.get('has_legal_content', False) and len(message_lower) < 50:
        return True
    
    return False

def _build_conversation_context(messages: List[dict], max_messages: int = 6) -> str:
    """Build conversation context from recent messages"""
    if not messages:
        return "No previous conversation"
    
    recent_messages = messages[-max_messages:] if len(messages) > max_messages else messages
    context_parts = []
    
    for msg in recent_messages:
        user_type = msg.get('user_type', 'User')
        content = msg.get('content', '')
        context_parts.append(f"{user_type}: {content}")
    
    return "\n".join(context_parts)

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
