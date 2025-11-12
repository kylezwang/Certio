"""
Certio AI Training Pipeline
Automated training and continuous improvement system for Certio AI agents
"""

import json
import logging
import asyncio
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass, asdict
from datetime import datetime, timedelta
import os
import pickle
from pathlib import Path
# Try to import numpy, fallback if not available
try:
    import numpy as np
    HAS_NUMPY = True
except ImportError:
    HAS_NUMPY = False
    # Create a simple fallback for basic operations
    class SimpleArray:
        def __init__(self, data):
            self.data = data
        def tolist(self):
            return self.data
        def __array__(self):
            return self.data

from collections import defaultdict, Counter
import re

from certio_knowledge_base import certio_kb

# Try to import RAG system, fallback if not available
try:
    from certio_rag_system import certio_rag
except ImportError:
    from certio_rag_system_fallback import certio_rag

logger = logging.getLogger(__name__)

@dataclass
class TrainingExample:
    """Represents a training example for the AI system"""
    id: str
    input_text: str
    expected_output: str
    agent_type: str
    user_type: str
    context: Dict[str, Any]
    quality_score: float
    created_at: datetime
    source: str

@dataclass
class PerformanceMetrics:
    """Performance metrics for AI agents"""
    agent_type: str
    accuracy: float
    response_time: float
    user_satisfaction: float
    cost_efficiency: float
    total_interactions: int
    successful_interactions: int
    last_updated: datetime

@dataclass
class TrainingFeedback:
    """Feedback from users or system evaluation"""
    example_id: str
    agent_type: str
    user_rating: Optional[int]  # 1-5 scale
    user_feedback: Optional[str]
    system_evaluation: Dict[str, Any]
    timestamp: datetime

class CertioTrainingPipeline:
    """Automated training pipeline for continuous AI improvement"""
    
    def __init__(self, data_dir: str = "training_data"):
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(exist_ok=True)
        
        # Training data storage
        self.training_examples: List[TrainingExample] = []
        self.performance_metrics: Dict[str, PerformanceMetrics] = {}
        self.feedback_data: List[TrainingFeedback] = []
        
        # Training configuration
        self.min_examples_per_agent = 50
        self.retraining_threshold = 0.8  # Retrain if performance drops below 80%
        self.feedback_weight = 0.3  # Weight of user feedback in evaluation
        
        # Load existing data
        self._load_training_data()
        self._load_performance_metrics()
        self._load_feedback_data()
    
    def _load_training_data(self):
        """Load existing training examples"""
        training_file = self.data_dir / "training_examples.json"
        if training_file.exists():
            try:
                with open(training_file, 'r') as f:
                    data = json.load(f)
                
                self.training_examples = [
                    TrainingExample(
                        id=ex['id'],
                        input_text=ex['input_text'],
                        expected_output=ex['expected_output'],
                        agent_type=ex['agent_type'],
                        user_type=ex['user_type'],
                        context=ex['context'],
                        quality_score=ex['quality_score'],
                        created_at=datetime.fromisoformat(ex['created_at']),
                        source=ex['source']
                    )
                    for ex in data
                ]
                logger.info(f"Loaded {len(self.training_examples)} training examples")
            except Exception as e:
                logger.error(f"Error loading training data: {e}")
                self.training_examples = []
    
    def _load_performance_metrics(self):
        """Load existing performance metrics"""
        metrics_file = self.data_dir / "performance_metrics.json"
        if metrics_file.exists():
            try:
                with open(metrics_file, 'r') as f:
                    data = json.load(f)
                
                self.performance_metrics = {
                    agent_type: PerformanceMetrics(
                        agent_type=metrics['agent_type'],
                        accuracy=metrics['accuracy'],
                        response_time=metrics['response_time'],
                        user_satisfaction=metrics['user_satisfaction'],
                        cost_efficiency=metrics['cost_efficiency'],
                        total_interactions=metrics['total_interactions'],
                        successful_interactions=metrics['successful_interactions'],
                        last_updated=datetime.fromisoformat(metrics['last_updated'])
                    )
                    for agent_type, metrics in data.items()
                }
                logger.info(f"Loaded performance metrics for {len(self.performance_metrics)} agents")
            except Exception as e:
                logger.error(f"Error loading performance metrics: {e}")
                self.performance_metrics = {}
    
    def _load_feedback_data(self):
        """Load existing feedback data"""
        feedback_file = self.data_dir / "feedback_data.json"
        if feedback_file.exists():
            try:
                with open(feedback_file, 'r') as f:
                    data = json.load(f)
                
                self.feedback_data = [
                    TrainingFeedback(
                        example_id=fb['example_id'],
                        agent_type=fb['agent_type'],
                        user_rating=fb.get('user_rating'),
                        user_feedback=fb.get('user_feedback'),
                        system_evaluation=fb['system_evaluation'],
                        timestamp=datetime.fromisoformat(fb['timestamp'])
                    )
                    for fb in data
                ]
                logger.info(f"Loaded {len(self.feedback_data)} feedback entries")
            except Exception as e:
                logger.error(f"Error loading feedback data: {e}")
                self.feedback_data = []
    
    def _save_training_data(self):
        """Save training examples to disk"""
        training_file = self.data_dir / "training_examples.json"
        data = [
            {
                'id': ex.id,
                'input_text': ex.input_text,
                'expected_output': ex.expected_output,
                'agent_type': ex.agent_type,
                'user_type': ex.user_type,
                'context': ex.context,
                'quality_score': ex.quality_score,
                'created_at': ex.created_at.isoformat(),
                'source': ex.source
            }
            for ex in self.training_examples
        ]
        
        with open(training_file, 'w') as f:
            json.dump(data, f, indent=2)
    
    def _save_performance_metrics(self):
        """Save performance metrics to disk"""
        metrics_file = self.data_dir / "performance_metrics.json"
        data = {
            agent_type: {
                'agent_type': metrics.agent_type,
                'accuracy': metrics.accuracy,
                'response_time': metrics.response_time,
                'user_satisfaction': metrics.user_satisfaction,
                'cost_efficiency': metrics.cost_efficiency,
                'total_interactions': metrics.total_interactions,
                'successful_interactions': metrics.successful_interactions,
                'last_updated': metrics.last_updated.isoformat()
            }
            for agent_type, metrics in self.performance_metrics.items()
        }
        
        with open(metrics_file, 'w') as f:
            json.dump(data, f, indent=2)
    
    def _save_feedback_data(self):
        """Save feedback data to disk"""
        feedback_file = self.data_dir / "feedback_data.json"
        data = [
            {
                'example_id': fb.example_id,
                'agent_type': fb.agent_type,
                'user_rating': fb.user_rating,
                'user_feedback': fb.user_feedback,
                'system_evaluation': fb.system_evaluation,
                'timestamp': fb.timestamp.isoformat()
            }
            for fb in self.feedback_data
        ]
        
        with open(feedback_file, 'w') as f:
            json.dump(data, f, indent=2)
    
    def add_training_example(self, input_text: str, expected_output: str, 
                           agent_type: str, user_type: str, context: Dict[str, Any],
                           quality_score: float = 1.0, source: str = "manual") -> str:
        """Add a new training example"""
        example_id = f"ex_{len(self.training_examples)}_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        
        example = TrainingExample(
            id=example_id,
            input_text=input_text,
            expected_output=expected_output,
            agent_type=agent_type,
            user_type=user_type,
            context=context,
            quality_score=quality_score,
            created_at=datetime.now(),
            source=source
        )
        
        self.training_examples.append(example)
        self._save_training_data()
        
        logger.info(f"Added training example {example_id} for {agent_type}")
        return example_id
    
    def add_feedback(self, example_id: str, agent_type: str, 
                    user_rating: Optional[int] = None, user_feedback: Optional[str] = None,
                    system_evaluation: Optional[Dict[str, Any]] = None):
        """Add feedback for a training example"""
        feedback = TrainingFeedback(
            example_id=example_id,
            agent_type=agent_type,
            user_rating=user_rating,
            user_feedback=user_feedback,
            system_evaluation=system_evaluation or {},
            timestamp=datetime.now()
        )
        
        self.feedback_data.append(feedback)
        self._save_feedback_data()
        
        logger.info(f"Added feedback for example {example_id}")
    
    def generate_training_examples_from_conversations(self, conversations: List[Dict[str, Any]]) -> int:
        """Generate training examples from real conversations"""
        examples_added = 0
        
        for conversation in conversations:
            messages = conversation.get('messages', [])
            if len(messages) < 2:
                continue
            
            # Generate examples for each agent type
            examples_added += self._generate_chat_summarizer_examples(messages)
            examples_added += self._generate_goal_extractor_examples(messages)
            examples_added += self._generate_reply_suggester_examples(messages)
            examples_added += self._generate_clarity_agent_examples(messages)
        
        logger.info(f"Generated {examples_added} training examples from conversations")
        return examples_added
    
    def _generate_chat_summarizer_examples(self, messages: List[Dict[str, Any]]) -> int:
        """Generate training examples for ChatSummarizer"""
        examples_added = 0
        
        if len(messages) < 3:
            return examples_added
        
        # Create conversation text
        conversation_text = "\n".join([f"{msg.get('user_type', 'User')}: {msg.get('content', '')}" for msg in messages])
        
        # Generate expected summary based on conversation patterns
        expected_summary = self._generate_expected_summary(messages)
        
        if expected_summary:
            self.add_training_example(
                input_text=conversation_text,
                expected_output=expected_summary,
                agent_type="ChatSummarizer",
                user_type="System",
                context={"message_count": len(messages), "participants": list(set(msg.get('user_type', 'User') for msg in messages))},
                quality_score=0.8,
                source="conversation_analysis"
            )
            examples_added += 1
        
        return examples_added
    
    def _generate_goal_extractor_examples(self, messages: List[Dict[str, Any]]) -> int:
        """Generate training examples for ClientGoalExtractor"""
        examples_added = 0
        
        # Find client messages
        client_messages = [msg for msg in messages if msg.get('user_type') in ['Client', 'Business']]
        
        if not client_messages:
            return examples_added
        
        conversation_text = "\n".join([f"{msg.get('user_type', 'User')}: {msg.get('content', '')}" for msg in messages])
        
        # Extract goals from client messages
        goals = self._extract_goals_from_messages(client_messages)
        
        if goals:
            expected_output = json.dumps(goals, indent=2)
            self.add_training_example(
                input_text=conversation_text,
                expected_output=expected_output,
                agent_type="ClientGoalExtractor",
                user_type="Client",
                context={"client_message_count": len(client_messages), "goals_found": len(goals)},
                quality_score=0.7,
                source="conversation_analysis"
            )
            examples_added += 1
        
        return examples_added
    
    def _generate_reply_suggester_examples(self, messages: List[Dict[str, Any]]) -> int:
        """Generate training examples for ReplySuggester"""
        examples_added = 0
        
        if len(messages) < 2:
            return examples_added
        
        # Find client messages that need responses
        for i, msg in enumerate(messages):
            if msg.get('user_type') in ['Client', 'Business'] and i < len(messages) - 1:
                # Check if next message is from SeedJura or Lawyer
                next_msg = messages[i + 1]
                if next_msg.get('user_type') in ['SeedJura', 'Lawyer']:
                    conversation_context = "\n".join([f"{m.get('user_type', 'User')}: {m.get('content', '')}" for m in messages[:i+1]])
                    expected_reply = next_msg.get('content', '')
                    
                    self.add_training_example(
                        input_text=conversation_context,
                        expected_output=expected_reply,
                        agent_type="ReplySuggester",
                        user_type=next_msg.get('user_type', 'SeedJura'),
                        context={"conversation_length": i+1, "urgency_detected": self._detect_urgency(msg.get('content', ''))},
                        quality_score=0.9,
                        source="conversation_analysis"
                    )
                    examples_added += 1
        
        return examples_added
    
    def _generate_clarity_agent_examples(self, messages: List[Dict[str, Any]]) -> int:
        """Generate training examples for ClarityAgent"""
        examples_added = 0
        
        # Find messages with legal terminology
        for msg in messages:
            content = msg.get('content', '')
            legal_terms = self._extract_legal_terms(content)
            
            if legal_terms:
                # Generate simplified explanation
                simplified_explanation = self._generate_simplified_explanation(content, legal_terms)
                
                if simplified_explanation:
                    self.add_training_example(
                        input_text=content,
                        expected_output=simplified_explanation,
                        agent_type="ClarityAgent",
                        user_type=msg.get('user_type', 'Client'),
                        context={"legal_terms": legal_terms, "complexity": len(legal_terms)},
                        quality_score=0.8,
                        source="conversation_analysis"
                    )
                    examples_added += 1
        
        return examples_added
    
    def _generate_expected_summary(self, messages: List[Dict[str, Any]]) -> Optional[str]:
        """Generate expected summary from conversation messages"""
        if not messages:
            return None
        
        # Simple summary generation based on message patterns
        participants = list(set(msg.get('user_type', 'User') for msg in messages))
        message_count = len(messages)
        
        # Extract key topics
        all_content = " ".join([msg.get('content', '') for msg in messages])
        topics = self._extract_topics(all_content)
        
        # Determine sentiment
        sentiment = self._analyze_sentiment(all_content)
        
        # Determine urgency
        urgency = self._detect_urgency(all_content)
        
        summary = {
            "summary": f"Conversation between {', '.join(participants)} with {message_count} messages",
            "key_points": topics[:3],  # Top 3 topics
            "sentiment": sentiment,
            "urgency": urgency,
            "suggested_actions": ["Follow up on key topics", "Address urgent matters"]
        }
        
        return json.dumps(summary, indent=2)
    
    def _extract_goals_from_messages(self, messages: List[Dict[str, Any]]) -> Dict[str, Any]:
        """Extract goals from client messages"""
        goals = {
            "primary_goal": "General legal assistance",
            "secondary_goals": [],
            "business_type": "Unknown",
            "legal_area": "General",
            "timeline": "Not specified",
            "budget": "Not specified",
            "required_documents": []
        }
        
        all_content = " ".join([msg.get('content', '') for msg in messages])
        
        # Extract business type
        business_keywords = {
            "startup": ["startup", "new business", "founding"],
            "corporation": ["corporation", "corporate", "company"],
            "small_business": ["small business", "local business"],
            "nonprofit": ["nonprofit", "charity", "foundation"]
        }
        
        for business_type, keywords in business_keywords.items():
            if any(keyword in all_content.lower() for keyword in keywords):
                goals["business_type"] = business_type
                break
        
        # Extract legal area
        legal_areas = {
            "contract_law": ["contract", "agreement", "terms"],
            "employment_law": ["employment", "hiring", "termination"],
            "business_formation": ["incorporation", "llc", "partnership"],
            "intellectual_property": ["patent", "trademark", "copyright"],
            "litigation": ["lawsuit", "litigation", "dispute"]
        }
        
        for legal_area, keywords in legal_areas.items():
            if any(keyword in all_content.lower() for keyword in keywords):
                goals["legal_area"] = legal_area
                break
        
        return goals
    
    def _extract_legal_terms(self, text: str) -> List[str]:
        """Extract legal terms from text"""
        legal_terms = [
            "liability", "breach", "damages", "litigation", "compliance",
            "regulation", "intellectual property", "patent", "trademark",
            "copyright", "employment", "discrimination", "harassment",
            "termination", "severance", "non-disclosure", "confidentiality",
            "merger", "acquisition", "due diligence", "warranty",
            "indemnification", "force majeure", "arbitration"
        ]
        
        found_terms = []
        text_lower = text.lower()
        
        for term in legal_terms:
            if term in text_lower:
                found_terms.append(term)
        
        return found_terms
    
    def _generate_simplified_explanation(self, text: str, legal_terms: List[str]) -> Optional[str]:
        """Generate simplified explanation for legal text"""
        if not legal_terms:
            return None
        
        explanations = {
            "liability": "Legal responsibility for something, especially costs or damages",
            "breach": "Breaking or failing to follow a contract or agreement",
            "damages": "Money awarded to compensate for loss or injury",
            "litigation": "The process of taking legal action through the court system",
            "compliance": "Following rules and regulations",
            "regulation": "Official rules that control how something is done",
            "intellectual property": "Creations of the mind like inventions, designs, or artistic works",
            "patent": "Legal protection for inventions",
            "trademark": "Legal protection for brand names and logos",
            "copyright": "Legal protection for creative works like books, music, or art",
            "employment": "Work-related matters and relationships",
            "discrimination": "Unfair treatment based on protected characteristics",
            "harassment": "Unwanted behavior that creates a hostile environment",
            "termination": "Ending of employment",
            "severance": "Payment given when employment ends",
            "non-disclosure": "Agreement to keep information secret",
            "confidentiality": "Keeping information private and not sharing it",
            "merger": "Combining two companies into one",
            "acquisition": "One company buying another",
            "due diligence": "Thorough investigation before making a business decision",
            "warranty": "Promise that something will work as described",
            "indemnification": "Protection against legal liability or loss",
            "force majeure": "Unforeseeable circumstances that prevent fulfilling a contract",
            "arbitration": "Settling disputes outside of court with a neutral third party"
        }
        
        explanation_parts = []
        for term in legal_terms[:3]:  # Limit to 3 terms
            if term in explanations:
                explanation_parts.append(f"{term.title()}: {explanations[term]}")
        
        if explanation_parts:
            return "\n".join(explanation_parts)
        
        return None
    
    def _extract_topics(self, text: str) -> List[str]:
        """Extract topics from text"""
        topics = []
        text_lower = text.lower()
        
        topic_keywords = {
            "contracts": ["contract", "agreement", "terms"],
            "employment": ["employment", "hiring", "termination"],
            "business": ["business", "company", "corporation"],
            "legal": ["legal", "law", "attorney"],
            "compliance": ["compliance", "regulation", "audit"],
            "litigation": ["lawsuit", "litigation", "dispute"]
        }
        
        for topic, keywords in topic_keywords.items():
            if any(keyword in text_lower for keyword in keywords):
                topics.append(topic)
        
        return topics
    
    def _analyze_sentiment(self, text: str) -> str:
        """Analyze sentiment of text"""
        text_lower = text.lower()
        
        positive_words = ["good", "great", "excellent", "helpful", "thank", "appreciate"]
        negative_words = ["bad", "terrible", "problem", "issue", "concern", "worried"]
        
        positive_count = sum(1 for word in positive_words if word in text_lower)
        negative_count = sum(1 for word in negative_words if word in text_lower)
        
        if positive_count > negative_count:
            return "Positive"
        elif negative_count > positive_count:
            return "Negative"
        else:
            return "Neutral"
    
    def _detect_urgency(self, text: str) -> str:
        """Detect urgency in text"""
        text_lower = text.lower()
        
        urgent_words = ["urgent", "asap", "immediately", "emergency", "critical", "deadline"]
        if any(word in text_lower for word in urgent_words):
            return "High"
        
        time_words = ["today", "tomorrow", "this week", "deadline"]
        if any(word in text_lower for word in time_words):
            return "Medium"
        
        return "Low"
    
    def update_performance_metrics(self, agent_type: str, interaction_data: Dict[str, Any]):
        """Update performance metrics for an agent"""
        if agent_type not in self.performance_metrics:
            self.performance_metrics[agent_type] = PerformanceMetrics(
                agent_type=agent_type,
                accuracy=0.0,
                response_time=0.0,
                user_satisfaction=0.0,
                cost_efficiency=0.0,
                total_interactions=0,
                successful_interactions=0,
                last_updated=datetime.now()
            )
        
        metrics = self.performance_metrics[agent_type]
        
        # Update metrics
        metrics.total_interactions += 1
        
        if interaction_data.get('success', False):
            metrics.successful_interactions += 1
        
        # Update accuracy
        metrics.accuracy = metrics.successful_interactions / metrics.total_interactions
        
        # Update response time (exponential moving average)
        response_time = interaction_data.get('response_time', 0.0)
        if metrics.response_time == 0.0:
            metrics.response_time = response_time
        else:
            metrics.response_time = 0.9 * metrics.response_time + 0.1 * response_time
        
        # Update user satisfaction
        user_rating = interaction_data.get('user_rating')
        if user_rating:
            if metrics.user_satisfaction == 0.0:
                metrics.user_satisfaction = user_rating
            else:
                metrics.user_satisfaction = 0.9 * metrics.user_satisfaction + 0.1 * user_rating
        
        # Update cost efficiency
        cost = interaction_data.get('cost', 0.0)
        if cost > 0:
            efficiency = 1.0 / (1.0 + cost)  # Higher cost = lower efficiency
            if metrics.cost_efficiency == 0.0:
                metrics.cost_efficiency = efficiency
            else:
                metrics.cost_efficiency = 0.9 * metrics.cost_efficiency + 0.1 * efficiency
        
        metrics.last_updated = datetime.now()
        
        self._save_performance_metrics()
    
    def should_retrain_agent(self, agent_type: str) -> bool:
        """Determine if an agent should be retrained"""
        if agent_type not in self.performance_metrics:
            return False
        
        metrics = self.performance_metrics[agent_type]
        
        # Check if performance is below threshold
        if metrics.accuracy < self.retraining_threshold:
            return True
        
        # Check if we have enough training examples
        agent_examples = [ex for ex in self.training_examples if ex.agent_type == agent_type]
        if len(agent_examples) >= self.min_examples_per_agent:
            return True
        
        return False
    
    def get_training_recommendations(self) -> List[Dict[str, Any]]:
        """Get recommendations for training improvements"""
        recommendations = []
        
        for agent_type in ["ChatSummarizer", "ClientGoalExtractor", "ReplySuggester", "ClarityAgent"]:
            agent_examples = [ex for ex in self.training_examples if ex.agent_type == agent_type]
            
            if len(agent_examples) < self.min_examples_per_agent:
                recommendations.append({
                    "agent_type": agent_type,
                    "recommendation": "Add more training examples",
                    "current_count": len(agent_examples),
                    "target_count": self.min_examples_per_agent,
                    "priority": "High"
                })
            
            if agent_type in self.performance_metrics:
                metrics = self.performance_metrics[agent_type]
                if metrics.accuracy < self.retraining_threshold:
                    recommendations.append({
                        "agent_type": agent_type,
                        "recommendation": "Retrain agent - low accuracy",
                        "current_accuracy": metrics.accuracy,
                        "target_accuracy": self.retraining_threshold,
                        "priority": "Critical"
                    })
        
        return recommendations
    
    def export_training_data(self, agent_type: Optional[str] = None) -> Dict[str, Any]:
        """Export training data for external analysis"""
        if agent_type:
            examples = [ex for ex in self.training_examples if ex.agent_type == agent_type]
        else:
            examples = self.training_examples
        
        return {
            "training_examples": [asdict(ex) for ex in examples],
            "performance_metrics": {k: asdict(v) for k, v in self.performance_metrics.items()},
            "feedback_data": [asdict(fb) for fb in self.feedback_data],
            "export_timestamp": datetime.now().isoformat(),
            "total_examples": len(examples)
        }
    
    def get_training_stats(self) -> Dict[str, Any]:
        """Get comprehensive training statistics"""
        stats = {
            "total_examples": len(self.training_examples),
            "examples_by_agent": Counter(ex.agent_type for ex in self.training_examples),
            "examples_by_source": Counter(ex.source for ex in self.training_examples),
            "performance_metrics": {k: asdict(v) for k, v in self.performance_metrics.items()},
            "total_feedback": len(self.feedback_data),
            "feedback_by_agent": Counter(fb.agent_type for fb in self.feedback_data),
            "training_recommendations": self.get_training_recommendations()
        }
        
        return stats

# Global training pipeline instance
certio_training = CertioTrainingPipeline()

# Utility functions
def add_conversation_training_data(conversations: List[Dict[str, Any]]) -> int:
    """Add training data from conversations"""
    return certio_training.generate_training_examples_from_conversations(conversations)

def update_agent_performance(agent_type: str, interaction_data: Dict[str, Any]):
    """Update agent performance metrics"""
    certio_training.update_performance_metrics(agent_type, interaction_data)

def get_training_recommendations() -> List[Dict[str, Any]]:
    """Get training recommendations"""
    return certio_training.get_training_recommendations()

def should_retrain_agent(agent_type: str) -> bool:
    """Check if agent should be retrained"""
    return certio_training.should_retrain_agent(agent_type)
