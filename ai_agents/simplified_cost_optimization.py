"""
Simplified Cost Optimization System - GPT-4o Mini and GPT-4o Only
Optimized for maximum cost savings and simplicity
"""

import asyncio
import time
import json
import logging
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass
from enum import Enum
from collections import defaultdict, deque, OrderedDict
import hashlib

logger = logging.getLogger(__name__)

class ModelType(Enum):
    """Model selection with tiered complexity support"""
    GPT_4O_MINI = "gpt-4o-mini"  # For simple tasks
    GPT_4_1_MINI = "gpt-4.1-mini"  # For intermediate tasks (o1-mini)
    GPT_4O = "gpt-4o"            # For complex tasks
    GPT_4_1 = "gpt-4.1"          # For highest complexity tasks (o1-preview)
    GPT_5 = "gpt-5"              # Azure GPT-5
    GPT_5_1 = "gpt-5.1"          # Azure GPT-5.1
    GPT_5_2 = "gpt-5.2"          # Azure GPT-5.2

@dataclass
class ModelInfo:
    """Model information including cost and capabilities"""
    name: str
    cost_per_1k_tokens_input: float
    cost_per_1k_tokens_output: float
    max_tokens: int
    context_window: int
    capabilities: List[str]
    speed_tier: int  # 1=fastest, 2=slower
    quality_tier: int  # 1=highest, 2=good

@dataclass
class TaskComplexity:
    """Task complexity assessment"""
    complexity_score: float  # 0.0 to 1.0
    estimated_tokens: int
    requires_reasoning: bool
    requires_creativity: bool
    requires_analysis: bool
    urgency: str  # "low", "medium", "high", "critical"

class SimplifiedModelSelector:
    """Simplified model selector with only GPT-4o Mini and GPT-4o"""
    
    def __init__(self):
        self.models = self._initialize_models()
        self.usage_tracker = UsageTracker()
        self.cache_manager = IntelligentCacheManager()
        
    def _initialize_models(self) -> Dict[ModelType, ModelInfo]:
        """Initialize models with tiered complexity support"""
        return {
            # GPT-4o Mini - For simple tasks - Basic tier
            ModelType.GPT_4O_MINI: ModelInfo(
                name="GPT-4o Mini",
                cost_per_1k_tokens_input=0.00015,  # $0.15 per 1M tokens
                cost_per_1k_tokens_output=0.0006,  # $0.60 per 1M tokens
                max_tokens=16384,
                context_window=128000,
                capabilities=["analysis", "creativity", "code_generation"],
                speed_tier=1,
                quality_tier=4
            ),
            # GPT-4.1 Mini (o1-mini) - For intermediate tasks - Intermediate tier
            ModelType.GPT_4_1_MINI: ModelInfo(
                name="GPT-4.1 Mini",
                cost_per_1k_tokens_input=0.003,  # $3.00 per 1M tokens
                cost_per_1k_tokens_output=0.012,  # $12.00 per 1M tokens
                max_tokens=65536,
                context_window=128000,
                capabilities=["reasoning", "analysis", "creativity", "code_generation", "o1_reasoning"],
                speed_tier=1,
                quality_tier=3
            ),
            # GPT-4o - For complex tasks (50% cost savings vs GPT-4 Turbo) - Advanced tier
            ModelType.GPT_4O: ModelInfo(
                name="GPT-4o",
                cost_per_1k_tokens_input=0.005,  # $5.00 per 1M tokens
                cost_per_1k_tokens_output=0.015,  # $15.00 per 1M tokens
                max_tokens=4096,
                context_window=128000,
                capabilities=["reasoning", "analysis", "creativity", "code_generation", "multimodal"],
                speed_tier=1,
                quality_tier=2
            ),
            # GPT-4.1 (o1-preview) - For highest complexity tasks - Premium tier
            ModelType.GPT_4_1: ModelInfo(
                name="GPT-4.1",
                cost_per_1k_tokens_input=0.015,  # $15.00 per 1M tokens
                cost_per_1k_tokens_output=0.06,  # $60.00 per 1M tokens
                max_tokens=32768,
                context_window=128000,
                capabilities=["advanced_reasoning", "analysis", "creativity", "code_generation", "multimodal", "o1_reasoning"],
                speed_tier=2,
                quality_tier=1
            ),
            # GPT-5 - Azure GPT-5
            ModelType.GPT_5: ModelInfo(
                name="GPT-5",
                cost_per_1k_tokens_input=0.01,  # $10.00 per 1M tokens (estimated)
                cost_per_1k_tokens_output=0.03,  # $30.00 per 1M tokens (estimated)
                max_tokens=8192,
                context_window=200000,
                capabilities=["advanced_reasoning", "analysis", "creativity", "code_generation", "multimodal", "agentic"],
                speed_tier=2,
                quality_tier=1
            ),
            # GPT-5.1 - Azure GPT-5.1
            ModelType.GPT_5_1: ModelInfo(
                name="GPT-5.1",
                cost_per_1k_tokens_input=0.012,  # $12.00 per 1M tokens (estimated)
                cost_per_1k_tokens_output=0.036,  # $36.00 per 1M tokens (estimated)
                max_tokens=16384,
                context_window=256000,
                capabilities=["advanced_reasoning", "analysis", "creativity", "code_generation", "multimodal", "agentic", "extended_context"],
                speed_tier=2,
                quality_tier=1
            ),
            # GPT-5.2 - Azure GPT-5.2
            ModelType.GPT_5_2: ModelInfo(
                name="GPT-5.2",
                cost_per_1k_tokens_input=0.015,  # $15.00 per 1M tokens (estimated)
                cost_per_1k_tokens_output=0.045,  # $45.00 per 1M tokens (estimated)
                max_tokens=32768,
                context_window=512000,
                capabilities=["advanced_reasoning", "analysis", "creativity", "code_generation", "multimodal", "agentic", "extended_context", "expert_reasoning"],
                speed_tier=2,
                quality_tier=1
            )
        }
    
    def select_optimal_model(self, task_complexity: TaskComplexity, 
                           budget_constraint: Optional[float] = None,
                           time_constraint: Optional[str] = None) -> Tuple[ModelType, float]:
        """
        Select the most cost-effective model for a given task
        Simplified logic: GPT-4o Mini for simple tasks, GPT-4o for complex tasks
        """
        # Debug: Check types
        logger.info(f"DEBUG: task_complexity.complexity_score = {task_complexity.complexity_score} (type: {type(task_complexity.complexity_score)})")
        
        # Ensure complexity_score is a float
        if isinstance(task_complexity.complexity_score, str):
            try:
                complexity_score = float(task_complexity.complexity_score)
                logger.warning(f"Converted complexity_score from string to float: {complexity_score}")
                task_complexity.complexity_score = complexity_score
            except ValueError:
                logger.error(f"Could not convert complexity_score '{task_complexity.complexity_score}' to float, using 0.5")
                task_complexity.complexity_score = 0.5
        
        # Check cache first
        cache_key = self._generate_cache_key(task_complexity)
        cached_result = self.cache_manager.get_cached_response(cache_key)
        if cached_result:
            logger.info("Using cached response for cost optimization")
            return ModelType.GPT_4O_MINI, 0.0  # Cached responses are free
        
        # Three-tier complexity selection logic
        if task_complexity.complexity_score < 0.4:
            # Simple tasks: Use GPT-4o Mini (most cost-effective)
            selected_model = ModelType.GPT_4O_MINI
        elif task_complexity.complexity_score < 0.7:
            # Complex tasks: Use GPT-4o (balanced performance)
            selected_model = ModelType.GPT_4O
        else:
            # Highest complexity: Use GPT-4.1 (premium quality)
            selected_model = ModelType.GPT_4_1
        
        # Calculate estimated cost
        estimated_cost = self._estimate_cost(selected_model, task_complexity)
        
        # Check budget constraint
        if budget_constraint and estimated_cost > budget_constraint:
            # Fallback to cheaper model if over budget
            selected_model = ModelType.GPT_4O_MINI
            estimated_cost = self._estimate_cost(selected_model, task_complexity)
        
        # Track usage for analytics
        self.usage_tracker.record_model_selection(selected_model, task_complexity, estimated_cost)
        
        return selected_model, estimated_cost
    
    def _estimate_cost(self, model_type: ModelType, task_complexity: TaskComplexity) -> float:
        """Estimate cost for using a model for a specific task"""
        model_info = self.models[model_type]
        
        # Ensure estimated_tokens is a number
        estimated_tokens = task_complexity.estimated_tokens
        if isinstance(estimated_tokens, str):
            estimated_tokens = int(estimated_tokens) if estimated_tokens.isdigit() else 0
        elif not isinstance(estimated_tokens, (int, float)):
            estimated_tokens = 0
            
        # Estimate input and output tokens (70% input, 30% output)
        input_tokens = min(estimated_tokens * 0.7, model_info.max_tokens * 0.7)
        output_tokens = min(500, model_info.max_tokens - input_tokens)  # Assume 500 output tokens
        
        input_cost = (input_tokens / 1000) * model_info.cost_per_1k_tokens_input
        output_cost = (output_tokens / 1000) * model_info.cost_per_1k_tokens_output
        
        return input_cost + output_cost
    
    def _generate_cache_key(self, task_complexity: TaskComplexity) -> str:
        """Generate cache key for task complexity"""
        key_data = {
            "complexity_score": round(task_complexity.complexity_score, 2),
            "estimated_tokens": task_complexity.estimated_tokens,
            "requires_reasoning": task_complexity.requires_reasoning,
            "requires_creativity": task_complexity.requires_creativity,
            "requires_analysis": task_complexity.requires_analysis,
            "urgency": task_complexity.urgency
        }
        return hashlib.md5(json.dumps(key_data, sort_keys=True).encode()).hexdigest()

class TaskComplexityAnalyzer:
    """Task complexity analyzer for better model selection"""
    
    def analyze_task(self, prompt: str, context_length: int = 0, user_type: str = "Client", force_mini: bool = False) -> TaskComplexity:
        """Analyze task complexity with improved logic
        
        Args:
            prompt: The user's prompt text
            context_length: Length of conversation context
            user_type: Type of user (Client, Lawyer, etc.)
            force_mini: If True, force complexity score < 0.5 to use gpt-4o-mini
        """
        
        # Complexity indicators
        complexity_indicators = {
            "reasoning_indicators": [
                "analyze", "compare", "evaluate", "assess", "determine", "conclude",
                "reasoning", "logic", "deduce", "infer", "synthesize", "explain",
                "why", "how", "what if", "pros and cons", "advantages", "disadvantages"
            ],
            "creativity_indicators": [
                "create", "generate", "design", "innovate", "brainstorm", "imagine",
                "suggest", "propose", "develop", "craft", "build", "write",
                "compose", "draft", "formulate", "conceive"
            ],
            "analysis_indicators": [
                "review", "examine", "investigate", "study", "research", "explore",
                "break down", "deconstruct", "analyze", "scrutinize", "assess",
                "evaluate", "critique", "interpret", "understand"
            ],
            "legal_indicators": [
                "contract", "agreement", "liability", "breach", "clause", "terms",
                "legal", "law", "regulation", "compliance", "risk", "indemnify",
                "warranty", "disclaimer", "jurisdiction", "litigation"
            ],
            "technical_indicators": [
                "code", "programming", "algorithm", "implementation", "architecture",
                "system", "database", "api", "integration", "deployment", "security"
            ],
            "urgency_indicators": {
                "high": ["urgent", "asap", "immediately", "critical", "emergency", "deadline"],
                "medium": ["soon", "priority", "important", "timely"],
                "low": ["when possible", "eventually", "someday", "no rush"]
            }
        }
        
        prompt_lower = prompt.lower()
        
        # Calculate complexity score with extra logic
        reasoning_count = sum(1 for indicator in complexity_indicators["reasoning_indicators"] 
                             if indicator in prompt_lower)
        creativity_count = sum(1 for indicator in complexity_indicators["creativity_indicators"] 
                              if indicator in prompt_lower)
        analysis_count = sum(1 for indicator in complexity_indicators["analysis_indicators"] 
                            if indicator in prompt_lower)
        legal_count = sum(1 for indicator in complexity_indicators["legal_indicators"] 
                         if indicator in prompt_lower)
        technical_count = sum(1 for indicator in complexity_indicators["technical_indicators"] 
                             if indicator in prompt_lower)
        
        # Base complexity from indicators
        total_indicators = reasoning_count + creativity_count + analysis_count + legal_count + technical_count
        complexity_score = min(total_indicators / 8.0, 1.0)  # Normalize to 0-1
        
        # Force mini mode: cap complexity at 0.4 to ensure gpt-4o-mini is used
        if force_mini and complexity_score >= 0.5:
            logger.info(f"Force mini mode enabled: reducing complexity score from {complexity_score} to 0.4")
            complexity_score = 0.4
        
        # Adjust based on prompt length
        if len(prompt) > 500:
            complexity_score += 0.2
        elif len(prompt) > 200:
            complexity_score += 0.1
        
        # Adjust based on context length
        if context_length > 10000:
            complexity_score += 0.3
        elif context_length > 5000:
            complexity_score += 0.2
        elif context_length > 2000:
            complexity_score += 0.1
        
        # Adjust based on user type
        if user_type in ["Lawyer", "SeedJura"]:
            complexity_score += 0.2  # Legal professionals have more complex needs
        
        # Adjust for question complexity
        if "?" in prompt:
            if any(word in prompt_lower for word in ["what", "how", "why", "when", "where"]):
                complexity_score += 0.1
        
        # Cap at 1.0
        complexity_score = min(complexity_score, 1.0)
        
        # Determine urgency
        urgency = "medium"
        for urgency_level, indicators in complexity_indicators["urgency_indicators"].items():
            if any(indicator in prompt_lower for indicator in indicators):
                urgency = urgency_level
                break
        
        # Estimate tokens (improved approximation)
        # Ensure context_length is a number
        if isinstance(context_length, str):
            context_length = len(context_length)
        elif not isinstance(context_length, (int, float)):
            context_length = 0
            
        estimated_tokens = len(prompt.split()) * 1.3 + context_length * 0.8
        
        return TaskComplexity(
            complexity_score=complexity_score,
            estimated_tokens=int(estimated_tokens),
            requires_reasoning=reasoning_count > 0 or legal_count > 0,
            requires_creativity=creativity_count > 0,
            requires_analysis=analysis_count > 0 or technical_count > 0,
            urgency=urgency
        )

class UsageTracker:
    """Track usage patterns for cost optimization"""
    
    def __init__(self):
        self.usage_history = deque(maxlen=1000)
        self.model_usage = defaultdict(int)
        self.total_cost = 0.0
        self.total_tokens = 0
        self.request_count = 0
        
    def record_model_selection(self, model_type: ModelType, task_complexity: TaskComplexity, cost: float):
        """Record model selection for analytics"""
        self.model_usage[model_type.value] += 1
        self.total_cost += cost
        self.total_tokens += task_complexity.estimated_tokens
        self.request_count += 1
        
        self.usage_history.append({
            "timestamp": time.time(),
            "model": model_type.value,
            "complexity": task_complexity.complexity_score,
            "tokens": task_complexity.estimated_tokens,
            "cost": cost
        })
    
    def get_usage_analytics(self) -> Dict[str, Any]:
        """Get usage analytics"""
        if self.request_count == 0:
            return {
                "total_requests": 0,
                "total_cost": 0.0,
                "total_tokens": 0,
                "cache_hit_rate": 0.0,
                "cost_trend": "stable"
            }
        
        # Calculate cache hit rate (simplified)
        cache_hits = sum(1 for usage in self.usage_history if usage["cost"] == 0.0)
        cache_hit_rate = cache_hits / self.request_count if self.request_count > 0 else 0.0
        
        # Calculate cost trend
        recent_costs = [usage["cost"] for usage in list(self.usage_history)[-10:]]
        if len(recent_costs) >= 2:
            avg_recent = sum(recent_costs) / len(recent_costs)
            avg_older = sum([usage["cost"] for usage in list(self.usage_history)[-20:-10]]) / 10
            if avg_recent > avg_older * 1.1:
                cost_trend = "increasing"
            elif avg_recent < avg_older * 0.9:
                cost_trend = "decreasing"
            else:
                cost_trend = "stable"
        else:
            cost_trend = "stable"
        
        return {
            "total_requests": self.request_count,
            "total_cost": self.total_cost,
            "total_tokens": self.total_tokens,
            "cache_hit_rate": cache_hit_rate,
            "cost_trend": cost_trend,
            "model_usage": dict(self.model_usage) if self.model_usage else {}
        }

class IntelligentCacheManager:
    """LRU cache manager for cost optimization"""
    
    def __init__(self):
        self.cache: OrderedDict[str, Any] = OrderedDict()
        self.max_size = 1000
        
    def get_cached_response(self, cache_key: str) -> Optional[Any]:
        """Get cached response, promoting to most-recently-used on hit"""
        if cache_key in self.cache:
            self.cache.move_to_end(cache_key)
            return self.cache[cache_key]
        return None
    
    def cache_response(self, cache_key: str, response: Any):
        """Cache response with LRU eviction"""
        if cache_key in self.cache:
            self.cache.move_to_end(cache_key)
            self.cache[cache_key] = response
        else:
            if len(self.cache) >= self.max_size:
                self.cache.popitem(last=False)
            self.cache[cache_key] = response

# Global instances for use across the application
model_selector = SimplifiedModelSelector()
usage_tracker = UsageTracker()
cache_manager = IntelligentCacheManager()
task_analyzer = TaskComplexityAnalyzer()

def analyze_task(prompt: str, context_length: int = 0, user_type: str = "Client", force_mini: bool = False) -> TaskComplexity:
    """Convenience function for task analysis"""
    return task_analyzer.analyze_task(prompt, context_length, user_type, force_mini)
