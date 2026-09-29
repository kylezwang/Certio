"""
Comprehensive Cost Analytics and Optimization Dashboard
Provides detailed cost analysis and optimization recommendations
"""

import asyncio
import logging
import time
from typing import Dict, List, Optional, Any, Tuple
from dataclasses import dataclass
from datetime import datetime, timedelta
import json
import statistics
from collections import defaultdict, deque
import matplotlib.pyplot as plt
import pandas as pd
from io import BytesIO
import base64

logger = logging.getLogger(__name__)

@dataclass
class CostMetrics:
    """Cost metrics for analysis"""
    total_cost: float
    total_tokens: int
    total_requests: int
    average_cost_per_request: float
    average_tokens_per_request: float
    cost_per_token: float
    peak_hourly_cost: float
    daily_cost: float
    weekly_cost: float

@dataclass
class OptimizationRecommendation:
    """Optimization recommendation"""
    category: str
    priority: str  # "high", "medium", "low"
    title: str
    description: str
    potential_savings: float
    implementation_effort: str  # "low", "medium", "high"
    confidence: float

class CostAnalytics:
    """Full cost analytics and optimization system"""
    
    def __init__(self):
        self.cost_history = deque(maxlen=10000)  # Keep last 10k entries
        self.model_costs = {
            "gpt-4": {"input": 0.03, "output": 0.06},
            "gpt-4-turbo-preview": {"input": 0.01, "output": 0.03},
            "gpt-3.5-turbo": {"input": 0.001, "output": 0.002},
            "gpt-3.5-turbo-16k": {"input": 0.003, "output": 0.004},
            "claude-3-opus-20240229": {"input": 0.015, "output": 0.075},
            "claude-3-sonnet-20240229": {"input": 0.003, "output": 0.015},
            "claude-3-haiku-20240307": {"input": 0.00025, "output": 0.00125}
        }
        self.optimization_rules = self._initialize_optimization_rules()
        
    def _initialize_optimization_rules(self) -> List[Dict[str, Any]]:
        """Initialize optimization rules for cost analysis"""
        return [
            {
                "name": "expensive_model_usage",
                "category": "Model Selection",
                "condition": lambda data: data.get("expensive_model_ratio", 0) > 0.3,
                "priority": "high",
                "title": "Reduce Expensive Model Usage",
                "description": "More than 30% of requests are using expensive models (GPT-4, Claude Opus)",
                "potential_savings": 0.4,
                "implementation_effort": "medium"
            },
            {
                "name": "low_complexity_expensive_models",
                "category": "Model Selection",
                "condition": lambda data: data.get("low_complexity_expensive_ratio", 0) > 0.2,
                "priority": "high",
                "title": "Use Cheaper Models for Simple Tasks",
                "description": "Simple tasks are using expensive models unnecessarily",
                "potential_savings": 0.6,
                "implementation_effort": "low"
            },
            {
                "name": "high_token_usage",
                "category": "Context Management",
                "condition": lambda data: data.get("avg_tokens_per_request", 0) > 2000,
                "priority": "medium",
                "title": "Optimize Context Length",
                "description": "Average token usage per request is high",
                "potential_savings": 0.3,
                "implementation_effort": "medium"
            },
            {
                "name": "low_cache_hit_rate",
                "category": "Caching",
                "condition": lambda data: data.get("cache_hit_rate", 0) < 0.2,
                "priority": "medium",
                "title": "Improve Caching Strategy",
                "description": "Cache hit rate is low, missing opportunities for cost savings",
                "potential_savings": 0.5,
                "implementation_effort": "medium"
            },
            {
                "name": "high_retry_rate",
                "category": "Error Handling",
                "condition": lambda data: data.get("retry_rate", 0) > 0.1,
                "priority": "medium",
                "title": "Reduce API Retries",
                "description": "High retry rate indicates potential issues causing unnecessary costs",
                "potential_savings": 0.2,
                "implementation_effort": "low"
            },
            {
                "name": "inefficient_batching",
                "category": "Request Optimization",
                "condition": lambda data: data.get("batch_efficiency", 0) < 0.5,
                "priority": "low",
                "title": "Improve Request Batching",
                "description": "Requests could be batched more efficiently",
                "potential_savings": 0.15,
                "implementation_effort": "high"
            }
        ]
    
    def record_cost_event(self, model: str, input_tokens: int, output_tokens: int, 
                         success: bool, retry_count: int = 0, user_type: str = "Client"):
        """Record a cost event for analysis"""
        model_costs = self.model_costs.get(model, {"input": 0.001, "output": 0.002})
        
        input_cost = (input_tokens / 1000) * model_costs["input"]
        output_cost = (output_tokens / 1000) * model_costs["output"]
        total_cost = input_cost + output_cost
        
        event = {
            "timestamp": datetime.utcnow(),
            "model": model,
            "input_tokens": input_tokens,
            "output_tokens": output_tokens,
            "total_tokens": input_tokens + output_tokens,
            "input_cost": input_cost,
            "output_cost": output_cost,
            "total_cost": total_cost,
            "success": success,
            "retry_count": retry_count,
            "user_type": user_type,
            "is_expensive_model": model in ["gpt-4", "claude-3-opus-20240229"]
        }
        
        self.cost_history.append(event)
    
    def get_cost_metrics(self, time_period: str = "24h") -> CostMetrics:
        """Get full cost metrics for a time period"""
        cutoff_time = self._get_cutoff_time(time_period)
        recent_events = [e for e in self.cost_history if e["timestamp"] >= cutoff_time]
        
        if not recent_events:
            return CostMetrics(
                total_cost=0.0,
                total_tokens=0,
                total_requests=0,
                average_cost_per_request=0.0,
                average_tokens_per_request=0.0,
                cost_per_token=0.0,
                peak_hourly_cost=0.0,
                daily_cost=0.0,
                weekly_cost=0.0
            )
        
        total_cost = sum(e["total_cost"] for e in recent_events)
        total_tokens = sum(e["total_tokens"] for e in recent_events)
        total_requests = len(recent_events)
        
        # Calculate hourly costs for peak analysis
        hourly_costs = defaultdict(float)
        for event in recent_events:
            hour_key = event["timestamp"].strftime("%Y-%m-%d %H:00")
            hourly_costs[hour_key] += event["total_cost"]
        
        peak_hourly_cost = max(hourly_costs.values()) if hourly_costs else 0.0
        
        # Calculate daily and weekly costs
        daily_cost = sum(e["total_cost"] for e in self.cost_history 
                        if e["timestamp"] >= datetime.utcnow() - timedelta(days=1))
        weekly_cost = sum(e["total_cost"] for e in self.cost_history 
                         if e["timestamp"] >= datetime.utcnow() - timedelta(weeks=1))
        
        return CostMetrics(
            total_cost=total_cost,
            total_tokens=total_tokens,
            total_requests=total_requests,
            average_cost_per_request=total_cost / total_requests if total_requests > 0 else 0.0,
            average_tokens_per_request=total_tokens / total_requests if total_requests > 0 else 0.0,
            cost_per_token=total_cost / total_tokens if total_tokens > 0 else 0.0,
            peak_hourly_cost=peak_hourly_cost,
            daily_cost=daily_cost,
            weekly_cost=weekly_cost
        )
    
    def _get_cutoff_time(self, time_period: str) -> datetime:
        """Get cutoff time for time period"""
        now = datetime.utcnow()
        if time_period == "1h":
            return now - timedelta(hours=1)
        elif time_period == "24h":
            return now - timedelta(days=1)
        elif time_period == "7d":
            return now - timedelta(days=7)
        elif time_period == "30d":
            return now - timedelta(days=30)
        else:
            return now - timedelta(days=1)
    
    def get_optimization_recommendations(self) -> List[OptimizationRecommendation]:
        """Get optimization recommendations based on current usage patterns"""
        metrics = self.get_cost_metrics("24h")
        analysis_data = self._analyze_usage_patterns()
        
        recommendations = []
        
        for rule in self.optimization_rules:
            if rule["condition"](analysis_data):
                recommendation = OptimizationRecommendation(
                    category=rule["category"],
                    priority=rule["priority"],
                    title=rule["title"],
                    description=rule["description"],
                    potential_savings=rule["potential_savings"],
                    implementation_effort=rule["implementation_effort"],
                    confidence=self._calculate_confidence(rule, analysis_data)
                )
                recommendations.append(recommendation)
        
        # Sort by priority and potential savings
        recommendations.sort(key=lambda x: (x.priority == "high", x.potential_savings), reverse=True)
        
        return recommendations
    
    def _analyze_usage_patterns(self) -> Dict[str, Any]:
        """Analyze usage patterns for optimization recommendations"""
        recent_events = list(self.cost_history)[-1000:]  # Last 1000 events
        
        if not recent_events:
            return {}
        
        # Calculate ratios and metrics
        total_requests = len(recent_events)
        expensive_models = ["gpt-4", "claude-3-opus-20240229"]
        
        expensive_model_count = sum(1 for e in recent_events if e["model"] in expensive_models)
        expensive_model_ratio = expensive_model_count / total_requests if total_requests > 0 else 0
        
        # Analyze low complexity tasks using expensive models
        # This would need complexity data from the task analyzer
        low_complexity_expensive_ratio = 0.1  # Placeholder - would need actual complexity data
        
        # Calculate cache hit rate (would need cache data)
        cache_hit_rate = 0.3  # Placeholder - would need actual cache data
        
        # Calculate retry rate
        retry_count = sum(e["retry_count"] for e in recent_events)
        retry_rate = retry_count / total_requests if total_requests > 0 else 0
        
        # Calculate average tokens per request
        avg_tokens_per_request = sum(e["total_tokens"] for e in recent_events) / total_requests
        
        # Calculate batch efficiency (placeholder)
        batch_efficiency = 0.6  # Placeholder - would need actual batching data
        
        return {
            "expensive_model_ratio": expensive_model_ratio,
            "low_complexity_expensive_ratio": low_complexity_expensive_ratio,
            "cache_hit_rate": cache_hit_rate,
            "retry_rate": retry_rate,
            "avg_tokens_per_request": avg_tokens_per_request,
            "batch_efficiency": batch_efficiency,
            "total_requests": total_requests
        }
    
    def _calculate_confidence(self, rule: Dict[str, Any], analysis_data: Dict[str, Any]) -> float:
        """Calculate confidence in optimization recommendation"""
        # Base confidence on how far the metric is from the threshold
        if rule["name"] == "expensive_model_usage":
            ratio = analysis_data.get("expensive_model_ratio", 0)
            return min(1.0, (ratio - 0.3) / 0.3) if ratio > 0.3 else 0.0
        elif rule["name"] == "low_complexity_expensive_models":
            ratio = analysis_data.get("low_complexity_expensive_ratio", 0)
            return min(1.0, (ratio - 0.2) / 0.2) if ratio > 0.2 else 0.0
        elif rule["name"] == "high_token_usage":
            tokens = analysis_data.get("avg_tokens_per_request", 0)
            return min(1.0, (tokens - 2000) / 2000) if tokens > 2000 else 0.0
        elif rule["name"] == "low_cache_hit_rate":
            hit_rate = analysis_data.get("cache_hit_rate", 0)
            return min(1.0, (0.2 - hit_rate) / 0.2) if hit_rate < 0.2 else 0.0
        elif rule["name"] == "high_retry_rate":
            retry_rate = analysis_data.get("retry_rate", 0)
            return min(1.0, (retry_rate - 0.1) / 0.1) if retry_rate > 0.1 else 0.0
        else:
            return 0.5  # Default confidence
    
    def generate_cost_report(self, time_period: str = "24h") -> Dict[str, Any]:
        """Generate full cost report"""
        metrics = self.get_cost_metrics(time_period)
        recommendations = self.get_optimization_recommendations()
        analysis_data = self._analyze_usage_patterns()
        
        # Calculate potential savings
        total_potential_savings = sum(rec.potential_savings * metrics.total_cost for rec in recommendations)
        
        # Generate cost trend
        cost_trend = self._calculate_cost_trend(time_period)
        
        # Generate model usage breakdown
        model_usage = self._get_model_usage_breakdown(time_period)
        
        # Generate hourly cost distribution
        hourly_distribution = self._get_hourly_cost_distribution(time_period)
        
        return {
            "time_period": time_period,
            "metrics": {
                "total_cost": round(metrics.total_cost, 4),
                "total_tokens": metrics.total_tokens,
                "total_requests": metrics.total_requests,
                "average_cost_per_request": round(metrics.average_cost_per_request, 4),
                "average_tokens_per_request": round(metrics.average_tokens_per_request, 2),
                "cost_per_token": round(metrics.cost_per_token, 6),
                "peak_hourly_cost": round(metrics.peak_hourly_cost, 4),
                "daily_cost": round(metrics.daily_cost, 4),
                "weekly_cost": round(metrics.weekly_cost, 4)
            },
            "recommendations": [
                {
                    "category": rec.category,
                    "priority": rec.priority,
                    "title": rec.title,
                    "description": rec.description,
                    "potential_savings": round(rec.potential_savings * 100, 1),
                    "potential_cost_savings": round(rec.potential_savings * metrics.total_cost, 4),
                    "implementation_effort": rec.implementation_effort,
                    "confidence": round(rec.confidence, 2)
                }
                for rec in recommendations
            ],
            "analysis": analysis_data,
            "cost_trend": cost_trend,
            "model_usage": model_usage,
            "hourly_distribution": hourly_distribution,
            "total_potential_savings": round(total_potential_savings, 4),
            "generated_at": datetime.utcnow().isoformat()
        }
    
    def _calculate_cost_trend(self, time_period: str) -> str:
        """Calculate cost trend over time"""
        cutoff_time = self._get_cutoff_time(time_period)
        recent_events = [e for e in self.cost_history if e["timestamp"] >= cutoff_time]
        
        if len(recent_events) < 10:
            return "insufficient_data"
        
        # Split into two halves
        mid_point = len(recent_events) // 2
        first_half = recent_events[:mid_point]
        second_half = recent_events[mid_point:]
        
        first_half_cost = sum(e["total_cost"] for e in first_half)
        second_half_cost = sum(e["total_cost"] for e in second_half)
        
        if second_half_cost > first_half_cost * 1.1:
            return "increasing"
        elif second_half_cost < first_half_cost * 0.9:
            return "decreasing"
        else:
            return "stable"
    
    def _get_model_usage_breakdown(self, time_period: str) -> Dict[str, Any]:
        """Get model usage breakdown"""
        cutoff_time = self._get_cutoff_time(time_period)
        recent_events = [e for e in self.cost_history if e["timestamp"] >= cutoff_time]
        
        model_stats = defaultdict(lambda: {"count": 0, "cost": 0.0, "tokens": 0})
        
        for event in recent_events:
            model = event["model"]
            model_stats[model]["count"] += 1
            model_stats[model]["cost"] += event["total_cost"]
            model_stats[model]["tokens"] += event["total_tokens"]
        
        total_requests = len(recent_events)
        total_cost = sum(e["total_cost"] for e in recent_events)
        
        breakdown = {}
        for model, stats in model_stats.items():
            breakdown[model] = {
                "request_count": stats["count"],
                "percentage": round((stats["count"] / total_requests) * 100, 1),
                "total_cost": round(stats["cost"], 4),
                "cost_percentage": round((stats["cost"] / total_cost) * 100, 1),
                "average_cost_per_request": round(stats["cost"] / stats["count"], 4),
                "total_tokens": stats["tokens"],
                "average_tokens_per_request": round(stats["tokens"] / stats["count"], 2)
            }
        
        return breakdown
    
    def _get_hourly_cost_distribution(self, time_period: str) -> Dict[str, float]:
        """Get hourly cost distribution"""
        cutoff_time = self._get_cutoff_time(time_period)
        recent_events = [e for e in self.cost_history if e["timestamp"] >= cutoff_time]
        
        hourly_costs = defaultdict(float)
        for event in recent_events:
            hour_key = event["timestamp"].strftime("%H:00")
            hourly_costs[hour_key] += event["total_cost"]
        
        return dict(hourly_costs)
    
    def export_cost_data(self, time_period: str = "24h", format: str = "json") -> str:
        """Export cost data in specified format"""
        cutoff_time = self._get_cutoff_time(time_period)
        recent_events = [e for e in self.cost_history if e["timestamp"] >= cutoff_time]
        
        if format == "json":
            return json.dumps(recent_events, default=str, indent=2)
        elif format == "csv":
            # Convert to CSV format
            import csv
            from io import StringIO
            
            output = StringIO()
            if recent_events:
                writer = csv.DictWriter(output, fieldnames=recent_events[0].keys())
                writer.writeheader()
                writer.writerows(recent_events)
            return output.getvalue()
        else:
            raise ValueError("Unsupported format. Use 'json' or 'csv'")

# Global cost analytics instance
cost_analytics = CostAnalytics()
