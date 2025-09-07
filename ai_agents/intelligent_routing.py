"""
Intelligent Task Routing System
Routes tasks to the most appropriate processing method to minimize API calls and costs
"""

import asyncio
import logging
import time
from typing import Dict, List, Optional, Any, Tuple
from dataclasses import dataclass
from enum import Enum
from datetime import datetime, timedelta
import json
import hashlib
from collections import defaultdict, deque

logger = logging.getLogger(__name__)

class ProcessingMethod(Enum):
    """Available processing methods"""
    IMMEDIATE_AI = "immediate_ai"  # Direct AI processing
    BACKGROUND_AI = "background_ai"  # Background AI processing
    CACHED_RESPONSE = "cached_response"  # Use cached response
    RULE_BASED = "rule_based"  # Use rule-based logic
    HYBRID = "hybrid"  # Combine multiple methods
    DEFERRED = "deferred"  # Defer processing

class TaskType(Enum):
    """Types of tasks that can be processed"""
    CONVERSATION_SUMMARY = "conversation_summary"
    CLIENT_GOAL_EXTRACTION = "client_goal_extraction"
    REPLY_SUGGESTION = "reply_suggestion"
    CLARITY_EXPLANATION = "clarity_explanation"
    CONVERSATIONAL_RESPONSE = "conversational_response"
    DOCUMENT_ANALYSIS = "document_analysis"
    COST_OPTIMIZATION = "cost_optimization"

@dataclass
class RoutingDecision:
    """Decision about how to route a task"""
    method: ProcessingMethod
    confidence: float  # 0.0 to 1.0
    estimated_cost: float
    estimated_time: float  # seconds
    reasoning: str
    fallback_methods: List[ProcessingMethod]

@dataclass
class TaskContext:
    """Context information for routing decisions"""
    task_type: TaskType
    user_type: str
    urgency: str
    complexity: float
    message_count: int
    conversation_age: float  # hours
    recent_ai_usage: int  # AI calls in last hour
    cost_budget: Optional[float]
    time_budget: Optional[float]

class IntelligentRouter:
    """Intelligent task routing system"""
    
    def __init__(self):
        self.routing_rules = self._initialize_routing_rules()
        self.performance_history = defaultdict(list)
        self.cost_history = defaultdict(list)
        self.routing_cache = {}
        self.cache_hit_rate = 0.0
        self.total_requests = 0
        
    def _initialize_routing_rules(self) -> Dict[TaskType, List[Dict[str, Any]]]:
        """Initialize routing rules for different task types"""
        return {
            TaskType.CONVERSATION_SUMMARY: [
                {
                    "condition": lambda ctx: ctx.message_count < 5 and ctx.complexity < 0.3,
                    "method": ProcessingMethod.RULE_BASED,
                    "confidence": 0.9,
                    "cost": 0.0,
                    "time": 0.1
                },
                {
                    "condition": lambda ctx: ctx.urgency == "low" and ctx.conversation_age > 1,
                    "method": ProcessingMethod.BACKGROUND_AI,
                    "confidence": 0.8,
                    "cost": 0.01,
                    "time": 30.0
                },
                {
                    "condition": lambda ctx: True,
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.7,
                    "cost": 0.05,
                    "time": 2.0
                }
            ],
            TaskType.CLIENT_GOAL_EXTRACTION: [
                {
                    "condition": lambda ctx: ctx.user_type in ["Client", "Business"] and ctx.complexity > 0.5,
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.9,
                    "cost": 0.03,
                    "time": 3.0
                },
                {
                    "condition": lambda ctx: ctx.urgency == "low",
                    "method": ProcessingMethod.BACKGROUND_AI,
                    "confidence": 0.7,
                    "cost": 0.01,
                    "time": 45.0
                }
            ],
            TaskType.REPLY_SUGGESTION: [
                {
                    "condition": lambda ctx: ctx.urgency == "high" and ctx.complexity < 0.4,
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.9,
                    "cost": 0.02,
                    "time": 1.5
                },
                {
                    "condition": lambda ctx: ctx.user_type == "Lawyer" and ctx.complexity > 0.6,
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.8,
                    "cost": 0.04,
                    "time": 2.5
                },
                {
                    "condition": lambda ctx: True,
                    "method": ProcessingMethod.BACKGROUND_AI,
                    "confidence": 0.6,
                    "cost": 0.01,
                    "time": 20.0
                }
            ],
            TaskType.CLARITY_EXPLANATION: [
                {
                    "condition": lambda ctx: ctx.urgency == "high",
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.9,
                    "cost": 0.02,
                    "time": 2.0
                },
                {
                    "condition": lambda ctx: True,
                    "method": ProcessingMethod.BACKGROUND_AI,
                    "confidence": 0.7,
                    "cost": 0.01,
                    "time": 15.0
                }
            ],
            TaskType.CONVERSATIONAL_RESPONSE: [
                {
                    "condition": lambda ctx: ctx.urgency == "high" or ctx.user_type in ["Client", "Business"],
                    "method": ProcessingMethod.IMMEDIATE_AI,
                    "confidence": 0.9,
                    "cost": 0.05,
                    "time": 3.0
                },
                {
                    "condition": lambda ctx: ctx.complexity < 0.3,
                    "method": ProcessingMethod.RULE_BASED,
                    "confidence": 0.8,
                    "cost": 0.0,
                    "time": 0.5
                },
                {
                    "condition": lambda ctx: True,
                    "method": ProcessingMethod.BACKGROUND_AI,
                    "confidence": 0.6,
                    "cost": 0.01,
                    "time": 30.0
                }
            ]
        }
    
    def route_task(self, task_context: TaskContext) -> RoutingDecision:
        """Route a task to the most appropriate processing method"""
        self.total_requests += 1
        
        # Check cache first
        cache_key = self._generate_cache_key(task_context)
        if cache_key in self.routing_cache:
            self.cache_hit_rate = (self.cache_hit_rate * (self.total_requests - 1) + 1) / self.total_requests
            return self.routing_cache[cache_key]
        
        # Get routing rules for task type
        rules = self.routing_rules.get(task_context.task_type, [])
        
        # Evaluate each rule
        best_decision = None
        best_score = -1
        
        for rule in rules:
            if rule["condition"](task_context):
                # Calculate score based on confidence, cost, and time
                score = self._calculate_rule_score(rule, task_context)
                
                if score > best_score:
                    best_score = score
                    best_decision = RoutingDecision(
                        method=rule["method"],
                        confidence=rule["confidence"],
                        estimated_cost=rule["cost"],
                        estimated_time=rule["time"],
                        reasoning=self._generate_reasoning(rule, task_context),
                        fallback_methods=self._get_fallback_methods(rule["method"])
                    )
        
        # If no rule matches, use default
        if not best_decision:
            best_decision = self._get_default_decision(task_context)
        
        # Cache the decision
        self.routing_cache[cache_key] = best_decision
        
        # Update cache hit rate
        self.cache_hit_rate = (self.cache_hit_rate * (self.total_requests - 1)) / self.total_requests
        
        return best_decision
    
    def _generate_cache_key(self, task_context: TaskContext) -> str:
        """Generate cache key for task context"""
        key_data = {
            "task_type": task_context.task_type.value,
            "user_type": task_context.user_type,
            "urgency": task_context.urgency,
            "complexity": round(task_context.complexity, 2),
            "message_count": task_context.message_count
        }
        return hashlib.md5(json.dumps(key_data, sort_keys=True).encode()).hexdigest()
    
    def _calculate_rule_score(self, rule: Dict[str, Any], task_context: TaskContext) -> float:
        """Calculate score for a routing rule"""
        base_score = rule["confidence"]
        
        # Adjust for cost constraints
        if task_context.cost_budget and rule["cost"] > task_context.cost_budget:
            base_score *= 0.5
        
        # Adjust for time constraints
        if task_context.time_budget and rule["time"] > task_context.time_budget:
            base_score *= 0.7
        
        # Adjust for urgency
        if task_context.urgency == "high" and rule["time"] > 5.0:
            base_score *= 0.8
        elif task_context.urgency == "low" and rule["time"] < 10.0:
            base_score *= 1.1
        
        # Adjust for recent AI usage (avoid overloading)
        if task_context.recent_ai_usage > 10 and rule["method"] == ProcessingMethod.IMMEDIATE_AI:
            base_score *= 0.6
        
        return base_score
    
    def _generate_reasoning(self, rule: Dict[str, Any], task_context: TaskContext) -> str:
        """Generate human-readable reasoning for routing decision"""
        method = rule["method"]
        confidence = rule["confidence"]
        
        if method == ProcessingMethod.IMMEDIATE_AI:
            return f"Immediate AI processing selected (confidence: {confidence:.2f}) - High priority or complex task"
        elif method == ProcessingMethod.BACKGROUND_AI:
            return f"Background AI processing selected (confidence: {confidence:.2f}) - Non-urgent task suitable for async processing"
        elif method == ProcessingMethod.CACHED_RESPONSE:
            return f"Cached response selected (confidence: {confidence:.2f}) - Similar request found in cache"
        elif method == ProcessingMethod.RULE_BASED:
            return f"Rule-based processing selected (confidence: {confidence:.2f}) - Simple task that doesn't require AI"
        elif method == ProcessingMethod.HYBRID:
            return f"Hybrid processing selected (confidence: {confidence:.2f}) - Complex task requiring multiple approaches"
        else:
            return f"Deferred processing selected (confidence: {confidence:.2f}) - Task will be processed later"
    
    def _get_fallback_methods(self, primary_method: ProcessingMethod) -> List[ProcessingMethod]:
        """Get fallback methods for a primary method"""
        fallback_map = {
            ProcessingMethod.IMMEDIATE_AI: [ProcessingMethod.BACKGROUND_AI, ProcessingMethod.RULE_BASED],
            ProcessingMethod.BACKGROUND_AI: [ProcessingMethod.IMMEDIATE_AI, ProcessingMethod.RULE_BASED],
            ProcessingMethod.CACHED_RESPONSE: [ProcessingMethod.IMMEDIATE_AI, ProcessingMethod.BACKGROUND_AI],
            ProcessingMethod.RULE_BASED: [ProcessingMethod.IMMEDIATE_AI, ProcessingMethod.BACKGROUND_AI],
            ProcessingMethod.HYBRID: [ProcessingMethod.IMMEDIATE_AI, ProcessingMethod.BACKGROUND_AI],
            ProcessingMethod.DEFERRED: [ProcessingMethod.BACKGROUND_AI, ProcessingMethod.RULE_BASED]
        }
        return fallback_map.get(primary_method, [])
    
    def _get_default_decision(self, task_context: TaskContext) -> RoutingDecision:
        """Get default routing decision when no rules match"""
        if task_context.urgency == "high":
            method = ProcessingMethod.IMMEDIATE_AI
            cost = 0.05
            time = 3.0
        elif task_context.complexity > 0.5:
            method = ProcessingMethod.BACKGROUND_AI
            cost = 0.01
            time = 30.0
        else:
            method = ProcessingMethod.RULE_BASED
            cost = 0.0
            time = 1.0
        
        return RoutingDecision(
            method=method,
            confidence=0.5,
            estimated_cost=cost,
            estimated_time=time,
            reasoning="Default routing decision - no specific rules matched",
            fallback_methods=self._get_fallback_methods(method)
        )
    
    def record_performance(self, task_type: TaskType, method: ProcessingMethod, 
                          actual_cost: float, actual_time: float, success: bool):
        """Record performance metrics for routing decisions"""
        self.performance_history[task_type].append({
            "method": method,
            "actual_cost": actual_cost,
            "actual_time": actual_time,
            "success": success,
            "timestamp": datetime.utcnow()
        })
        
        # Keep only recent history (last 100 entries per task type)
        if len(self.performance_history[task_type]) > 100:
            self.performance_history[task_type] = self.performance_history[task_type][-100:]
    
    def get_routing_analytics(self) -> Dict[str, Any]:
        """Get routing analytics and performance metrics"""
        analytics = {
            "total_requests": self.total_requests,
            "cache_hit_rate": round(self.cache_hit_rate, 3),
            "cached_decisions": len(self.routing_cache),
            "task_type_performance": {},
            "method_effectiveness": {},
            "cost_savings": 0.0,
            "time_savings": 0.0
        }
        
        # Analyze performance by task type
        for task_type, history in self.performance_history.items():
            if not history:
                continue
            
            recent_history = history[-20:]  # Last 20 entries
            success_rate = sum(1 for h in recent_history if h["success"]) / len(recent_history)
            avg_cost = sum(h["actual_cost"] for h in recent_history) / len(recent_history)
            avg_time = sum(h["actual_time"] for h in recent_history) / len(recent_history)
            
            analytics["task_type_performance"][task_type.value] = {
                "success_rate": round(success_rate, 3),
                "average_cost": round(avg_cost, 4),
                "average_time": round(avg_time, 2),
                "sample_size": len(recent_history)
            }
        
        # Analyze method effectiveness
        all_history = []
        for history in self.performance_history.values():
            all_history.extend(history)
        
        if all_history:
            method_stats = defaultdict(lambda: {"count": 0, "success": 0, "total_cost": 0, "total_time": 0})
            
            for entry in all_history:
                method = entry["method"]
                method_stats[method]["count"] += 1
                if entry["success"]:
                    method_stats[method]["success"] += 1
                method_stats[method]["total_cost"] += entry["actual_cost"]
                method_stats[method]["total_time"] += entry["actual_time"]
            
            for method, stats in method_stats.items():
                analytics["method_effectiveness"][method.value] = {
                    "usage_count": stats["count"],
                    "success_rate": round(stats["success"] / stats["count"], 3),
                    "average_cost": round(stats["total_cost"] / stats["count"], 4),
                    "average_time": round(stats["total_time"] / stats["count"], 2)
                }
        
        return analytics
    
    def optimize_routing_rules(self):
        """Optimize routing rules based on performance history"""
        # This is a simplified optimization - in practice, you'd use more sophisticated ML
        for task_type, history in self.performance_history.items():
            if len(history) < 10:  # Need sufficient data
                continue
            
            recent_history = history[-20:]
            
            # Find most effective method for this task type
            method_performance = defaultdict(lambda: {"success": 0, "count": 0, "cost": 0, "time": 0})
            
            for entry in recent_history:
                method = entry["method"]
                method_performance[method]["count"] += 1
                if entry["success"]:
                    method_performance[method]["success"] += 1
                method_performance[method]["cost"] += entry["actual_cost"]
                method_performance[method]["time"] += entry["actual_time"]
            
            # Update rule confidence based on performance
            rules = self.routing_rules.get(task_type, [])
            for rule in rules:
                method = rule["method"]
                if method in method_performance:
                    perf = method_performance[method]
                    success_rate = perf["success"] / perf["count"]
                    # Adjust confidence based on actual performance
                    rule["confidence"] = min(1.0, rule["confidence"] * (0.5 + success_rate))
        
        logger.info("Routing rules optimized based on performance history")
    
    def clear_cache(self):
        """Clear routing cache"""
        self.routing_cache.clear()
        logger.info("Routing cache cleared")

# Global router instance
intelligent_router = IntelligentRouter()
