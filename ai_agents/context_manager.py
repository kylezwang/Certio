"""
Intelligent Context Management System
Optimizes context window usage to reduce token costs while maintaining quality
"""

import re
import json
import logging
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
from datetime import datetime, timedelta
import hashlib
from collections import defaultdict

logger = logging.getLogger(__name__)

@dataclass
class ContextChunk:
    """Represents a chunk of context with metadata"""
    content: str
    importance_score: float  # 0.0 to 1.0
    chunk_type: str  # "message", "summary", "metadata", "instruction"
    timestamp: datetime
    user_type: str
    is_ai_generated: bool
    token_count: int

@dataclass
class ContextSummary:
    """Summary of context for compression"""
    key_points: List[str]
    participants: List[str]
    time_span: str
    main_topics: List[str]
    urgency_level: str
    sentiment: str

class IntelligentContextManager:
    """Manages context intelligently to optimize token usage"""
    
    def __init__(self, max_context_tokens: int = 8000, compression_ratio: float = 0.7):
        self.max_context_tokens = max_context_tokens
        self.compression_ratio = compression_ratio
        self.context_cache = {}
        self.compression_patterns = self._initialize_compression_patterns()
        
    def _initialize_compression_patterns(self) -> Dict[str, Any]:
        """Initialize patterns for context compression"""
        return {
            "redundant_phrases": [
                r"\b(thank you|thanks|please|kindly)\b",
                r"\b(I understand|I see|got it|okay|ok)\b",
                r"\b(hello|hi|hey|good morning|good afternoon)\b"
            ],
            "filler_words": [
                r"\b(um|uh|er|ah|like|you know|basically|actually|literally)\b"
            ],
            "repetitive_patterns": [
                r"(\b\w+\b)(\s+\1)+",  # Repeated words
                r"(\b\w+\s+\w+\b)(\s+\1)+"  # Repeated phrases
            ]
        }
    
    def optimize_context(self, messages: List[Dict[str, Any]], 
                        user_type: str = "Client",
                        task_complexity: float = 0.5) -> Tuple[str, Dict[str, Any]]:
        """
        Optimize context for maximum information density while staying within token limits
        
        Args:
            messages: List of conversation messages
            user_type: Type of user (Client, Lawyer, etc.)
            task_complexity: Complexity score of the task (0.0 to 1.0)
            
        Returns:
            Tuple of (optimized_context, metadata)
        """
        # Convert messages to context chunks
        chunks = self._convert_messages_to_chunks(messages)
        
        # Calculate total tokens
        total_tokens = sum(chunk.token_count for chunk in chunks)
        
        # If within limits, return as-is
        if total_tokens <= self.max_context_tokens:
            context = self._chunks_to_context(chunks)
            return context, {
                "original_tokens": total_tokens,
                "optimized_tokens": total_tokens,
                "compression_applied": False,
                "chunks_used": len(chunks)
            }
        
        # Apply intelligent compression
        optimized_chunks = self._compress_context_intelligently(chunks, task_complexity)
        optimized_context = self._chunks_to_context(optimized_chunks)
        
        # Generate metadata
        metadata = {
            "original_tokens": total_tokens,
            "optimized_tokens": sum(chunk.token_count for chunk in optimized_chunks),
            "compression_applied": True,
            "compression_ratio": len(optimized_chunks) / len(chunks),
            "chunks_used": len(optimized_chunks),
            "chunks_removed": len(chunks) - len(optimized_chunks)
        }
        
        return optimized_context, metadata
    
    def _convert_messages_to_chunks(self, messages: List[Dict[str, Any]]) -> List[ContextChunk]:
        """Convert messages to context chunks with importance scoring"""
        chunks = []
        
        for message in messages:
            content = message.get("content", "")
            if not content.strip():
                continue
            
            # Calculate importance score
            importance = self._calculate_importance_score(message, messages)
            
            # Estimate token count
            token_count = self._estimate_token_count(content)
            
            chunk = ContextChunk(
                content=content,
                importance_score=importance,
                chunk_type="message",
                timestamp=datetime.fromisoformat(message.get("created_at", datetime.utcnow().isoformat())),
                user_type=message.get("user_type", "Unknown"),
                is_ai_generated=message.get("is_from_ai", False),
                token_count=token_count
            )
            
            chunks.append(chunk)
        
        return chunks
    
    def _calculate_importance_score(self, message: Dict[str, Any], all_messages: List[Dict[str, Any]]) -> float:
        """Calculate importance score for a message"""
        content = message.get("content", "").lower()
        user_type = message.get("user_type", "Unknown")
        is_ai = message.get("is_from_ai", False)
        
        score = 0.5  # Base score
        
        # Higher importance for recent messages
        message_index = all_messages.index(message)
        recency_factor = 1.0 - (message_index / len(all_messages)) * 0.3
        score += recency_factor * 0.2
        
        # Higher importance for user messages (not AI)
        if not is_ai:
            score += 0.2
        
        # Higher importance for certain user types
        if user_type in ["Client", "Business"]:
            score += 0.1
        elif user_type in ["Lawyer", "SeedJura"]:
            score += 0.05
        
        # Higher importance for messages with questions
        if "?" in message.get("content", ""):
            score += 0.15
        
        # Higher importance for messages with urgency indicators
        urgency_words = ["urgent", "asap", "immediately", "critical", "emergency", "deadline"]
        if any(word in content for word in urgency_words):
            score += 0.2
        
        # Higher importance for messages with legal terms
        legal_terms = ["contract", "agreement", "liability", "breach", "compliance", "legal"]
        if any(term in content for term in legal_terms):
            score += 0.1
        
        # Lower importance for very short messages
        if len(content) < 20:
            score -= 0.1
        
        # Lower importance for AI-generated responses (they can be regenerated)
        if is_ai:
            score -= 0.1
        
        return max(0.0, min(1.0, score))  # Clamp between 0 and 1
    
    def _estimate_token_count(self, text: str) -> int:
        """Estimate token count for text"""
        # Rough estimation: 1 token ≈ 4 characters for English text
        return len(text) // 4
    
    def _compress_context_intelligently(self, chunks: List[ContextChunk], 
                                      task_complexity: float) -> List[ContextChunk]:
        """Apply intelligent compression to context chunks"""
        # Sort by importance score (descending)
        sorted_chunks = sorted(chunks, key=lambda x: x.importance_score, reverse=True)
        
        # Calculate target number of chunks
        target_chunks = int(len(chunks) * self.compression_ratio)
        
        # For high complexity tasks, keep more context
        if task_complexity > 0.7:
            target_chunks = int(target_chunks * 1.2)
        elif task_complexity < 0.3:
            target_chunks = int(target_chunks * 0.8)
        
        # Select most important chunks
        selected_chunks = sorted_chunks[:target_chunks]
        
        # Apply text compression to selected chunks
        compressed_chunks = []
        for chunk in selected_chunks:
            compressed_content = self._compress_text(chunk.content)
            compressed_token_count = self._estimate_token_count(compressed_content)
            
            compressed_chunk = ContextChunk(
                content=compressed_content,
                importance_score=chunk.importance_score,
                chunk_type=chunk.chunk_type,
                timestamp=chunk.timestamp,
                user_type=chunk.user_type,
                is_ai_generated=chunk.is_ai_generated,
                token_count=compressed_token_count
            )
            
            compressed_chunks.append(compressed_chunk)
        
        return compressed_chunks
    
    def _compress_text(self, text: str) -> str:
        """Compress text while preserving important information"""
        compressed = text
        
        # Remove redundant phrases
        for pattern in self.compression_patterns["redundant_phrases"]:
            compressed = re.sub(pattern, "", compressed, flags=re.IGNORECASE)
        
        # Remove filler words
        for pattern in self.compression_patterns["filler_words"]:
            compressed = re.sub(pattern, "", compressed, flags=re.IGNORECASE)
        
        # Remove repetitive patterns
        for pattern in self.compression_patterns["repetitive_patterns"]:
            compressed = re.sub(pattern, r"\1", compressed)
        
        # Remove extra whitespace
        compressed = re.sub(r'\s+', ' ', compressed).strip()
        
        # If compression is too aggressive, keep original
        if len(compressed) < len(text) * 0.5:
            return text
        
        return compressed
    
    def _chunks_to_context(self, chunks: List[ContextChunk]) -> str:
        """Convert chunks back to context string"""
        context_parts = []
        
        for chunk in chunks:
            # Add user type prefix for clarity
            prefix = f"[{chunk.user_type}]" if chunk.user_type != "Unknown" else ""
            context_parts.append(f"{prefix} {chunk.content}")
        
        return "\n".join(context_parts)
    
    def generate_context_summary(self, messages: List[Dict[str, Any]]) -> ContextSummary:
        """Generate a summary of the context for reference"""
        if not messages:
            return ContextSummary(
                key_points=[],
                participants=[],
                time_span="No messages",
                main_topics=[],
                urgency_level="Low",
                sentiment="Neutral"
            )
        
        # Extract participants
        participants = list(set(msg.get("user_type", "Unknown") for msg in messages))
        
        # Calculate time span
        if len(messages) > 1:
            first_time = datetime.fromisoformat(messages[0].get("created_at", datetime.utcnow().isoformat()))
            last_time = datetime.fromisoformat(messages[-1].get("created_at", datetime.utcnow().isoformat()))
            time_span = str(last_time - first_time)
        else:
            time_span = "Single message"
        
        # Extract main topics
        all_content = " ".join(msg.get("content", "") for msg in messages).lower()
        main_topics = self._extract_main_topics(all_content)
        
        # Determine urgency level
        urgency_level = self._assess_urgency_level(all_content)
        
        # Assess sentiment
        sentiment = self._assess_sentiment(all_content)
        
        # Extract key points
        key_points = self._extract_key_points(messages)
        
        return ContextSummary(
            key_points=key_points,
            participants=participants,
            time_span=time_span,
            main_topics=main_topics,
            urgency_level=urgency_level,
            sentiment=sentiment
        )
    
    def _extract_main_topics(self, content: str) -> List[str]:
        """Extract main topics from content"""
        topic_keywords = {
            "Contract Law": ["contract", "agreement", "terms", "clause", "breach"],
            "Employment Law": ["employment", "hiring", "termination", "discrimination"],
            "Business Formation": ["incorporation", "llc", "corporation", "partnership"],
            "Intellectual Property": ["patent", "trademark", "copyright", "intellectual property"],
            "Real Estate": ["real estate", "property", "lease", "rental"],
            "Litigation": ["lawsuit", "litigation", "dispute", "court"],
            "Compliance": ["compliance", "regulation", "audit", "certification"]
        }
        
        topics = []
        for topic, keywords in topic_keywords.items():
            if any(keyword in content for keyword in keywords):
                topics.append(topic)
        
        return topics
    
    def _assess_urgency_level(self, content: str) -> str:
        """Assess urgency level from content"""
        urgent_indicators = ["urgent", "asap", "immediately", "emergency", "critical", "deadline"]
        if any(indicator in content for indicator in urgent_indicators):
            return "High"
        
        time_indicators = ["today", "tomorrow", "this week", "soon"]
        if any(indicator in content for indicator in time_indicators):
            return "Medium"
        
        return "Low"
    
    def _assess_sentiment(self, content: str) -> str:
        """Assess sentiment from content"""
        positive_words = ["thank", "appreciate", "great", "excellent", "helpful", "satisfied"]
        negative_words = ["problem", "issue", "concern", "worried", "frustrated", "disappointed"]
        
        positive_count = sum(1 for word in positive_words if word in content)
        negative_count = sum(1 for word in negative_words if word in content)
        
        if positive_count > negative_count:
            return "Positive"
        elif negative_count > positive_count:
            return "Negative"
        else:
            return "Neutral"
    
    def _extract_key_points(self, messages: List[Dict[str, Any]]) -> List[str]:
        """Extract key points from messages"""
        key_points = []
        
        for message in messages:
            content = message.get("content", "")
            
            # Look for questions (important for understanding needs)
            if "?" in content:
                key_points.append(f"Question: {content[:100]}...")
            
            # Look for statements with important keywords
            important_keywords = ["need", "want", "require", "must", "should", "important", "critical"]
            if any(keyword in content.lower() for keyword in important_keywords):
                key_points.append(f"Requirement: {content[:100]}...")
        
        return key_points[:5]  # Limit to 5 key points
    
    def get_compression_stats(self) -> Dict[str, Any]:
        """Get compression statistics"""
        return {
            "max_context_tokens": self.max_context_tokens,
            "compression_ratio": self.compression_ratio,
            "cached_contexts": len(self.context_cache),
            "compression_patterns": len(self.compression_patterns)
        }

# Global context manager instance
context_manager = IntelligentContextManager()
