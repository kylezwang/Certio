"""
Notal RAG (Retrieval-Augmented Generation) System
Enhances AI agents with project-specific knowledge and context
"""

import json
import logging
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
from datetime import datetime
import re
from collections import defaultdict
import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity
import pickle
import os

from certio_knowledge_base import notal_kb

logger = logging.getLogger(__name__)

@dataclass
class KnowledgeChunk:
    """Represents a chunk of knowledge for RAG"""
    id: str
    content: str
    source: str
    category: str
    metadata: Dict[str, Any]
    embedding: Optional[List[float]] = None

@dataclass
class RAGResult:
    """Result from RAG retrieval"""
    chunks: List[KnowledgeChunk]
    relevance_scores: List[float]
    total_chunks: int
    query: str

class NotalRAGSystem:
    """Retrieval-Augmented Generation system for Notal project knowledge"""
    
    def __init__(self, knowledge_base_path: str = "certio_knowledge_base.py"):
        self.knowledge_chunks: List[KnowledgeChunk] = []
        self.vectorizer = TfidfVectorizer(
            max_features=1000,
            stop_words='english',
            ngram_range=(1, 2)
        )
        self.embeddings: Optional[np.ndarray] = None
        self.is_fitted = False
        self.knowledge_base_path = knowledge_base_path
        
        # Initialize with project knowledge
        self._initialize_knowledge_chunks()
        self._build_embeddings()
    
    def _initialize_knowledge_chunks(self):
        """Initialize knowledge chunks from various sources"""
        chunks = []
        
        # Add feature knowledge
        for feature_name, feature in notal_kb.features.items():
            chunk = KnowledgeChunk(
                id=f"feature_{feature_name}",
                content=f"{feature.name}: {feature.description}. Technical details: {feature.technical_details}. Business value: {feature.business_value}",
                source="certio_knowledge_base",
                category="feature",
                metadata={
                    "feature_name": feature_name,
                    "user_types": feature.user_types,
                    "legal_areas": feature.legal_areas,
                    "api_endpoints": feature.api_endpoints
                }
            )
            chunks.append(chunk)
        
        # Add workflow knowledge
        for workflow_name, workflow in notal_kb.workflows.items():
            chunk = KnowledgeChunk(
                id=f"workflow_{workflow_name}",
                content=f"{workflow.name}: {workflow.description}. Steps: {' → '.join(workflow.steps)}. Participants: {', '.join(workflow.participants)}",
                source="certio_knowledge_base",
                category="workflow",
                metadata={
                    "workflow_name": workflow_name,
                    "participants": workflow.participants,
                    "legal_requirements": workflow.legal_requirements,
                    "ai_agents": workflow.ai_agents_involved
                }
            )
            chunks.append(chunk)
        
        # Add legal domain knowledge
        for domain, info in notal_kb.legal_domains.items():
            chunk = KnowledgeChunk(
                id=f"legal_{domain}",
                content=f"{domain.replace('_', ' ').title()}: {info['description']}. Common documents: {', '.join(info['common_documents'])}. Key terms: {', '.join(info['key_terms'])}",
                source="certio_knowledge_base",
                category="legal_domain",
                metadata={
                    "domain": domain,
                    "common_documents": info['common_documents'],
                    "key_terms": info['key_terms'],
                    "ai_applications": info['ai_applications']
                }
            )
            chunks.append(chunk)
        
        # Add user type knowledge
        for user_type, info in notal_kb.user_types.items():
            chunk = KnowledgeChunk(
                id=f"user_{user_type}",
                content=f"{user_type.title()}: {info['description']}. Permissions: {', '.join(info['permissions'])}. AI interactions: {', '.join(info['ai_interactions'])}",
                source="certio_knowledge_base",
                category="user_type",
                metadata={
                    "user_type": user_type,
                    "permissions": info['permissions'],
                    "ai_interactions": info['ai_interactions'],
                    "typical_needs": info['typical_needs']
                }
            )
            chunks.append(chunk)
        
        # Add technical architecture knowledge
        tech_arch = notal_kb.technical_architecture
        for component, details in tech_arch.items():
            chunk = KnowledgeChunk(
                id=f"tech_{component}",
                content=f"{component.title()}: {json.dumps(details, indent=2)}",
                source="certio_knowledge_base",
                category="technical",
                metadata={
                    "component": component,
                    "details": details
                }
            )
            chunks.append(chunk)
        
        # Add project-specific patterns and examples
        self._add_project_patterns(chunks)
        
        self.knowledge_chunks = chunks
        logger.info(f"Initialized {len(chunks)} knowledge chunks")
    
    def _add_project_patterns(self, chunks: List[KnowledgeChunk]):
        """Add project-specific patterns and examples"""
        
        # Legal conversation patterns
        legal_patterns = [
            "Client asks about contract terms → ClarityAgent explains in simple terms → ReplySuggester provides professional response",
            "Business needs compliance help → ClientGoalExtractor identifies requirements → Matter creation with compliance tasks",
            "Document review request → AI analyzes document → Legal team reviews → Client feedback → Final approval",
            "Urgent legal matter → ChatSummarizer detects urgency → Priority routing → Immediate lawyer assignment"
        ]
        
        for i, pattern in enumerate(legal_patterns):
            chunk = KnowledgeChunk(
                id=f"pattern_{i}",
                content=f"Legal workflow pattern: {pattern}",
                source="project_patterns",
                category="workflow_pattern",
                metadata={"pattern_type": "legal_conversation", "example": pattern}
            )
            chunks.append(chunk)
        
        # AI agent interaction patterns
        ai_patterns = [
            "ChatSummarizer analyzes conversation sentiment and urgency",
            "ClientGoalExtractor identifies primary and secondary business goals",
            "ReplySuggester generates contextually appropriate professional responses",
            "ClarityAgent simplifies legal language for client understanding"
        ]
        
        for i, pattern in enumerate(ai_patterns):
            chunk = KnowledgeChunk(
                id=f"ai_pattern_{i}",
                content=f"AI agent pattern: {pattern}",
                source="project_patterns",
                category="ai_pattern",
                metadata={"pattern_type": "ai_interaction", "agent": pattern.split()[0]}
            )
            chunks.append(chunk)
        
        # Common legal scenarios
        legal_scenarios = [
            "Startup incorporation: Business formation, corporate governance, compliance requirements",
            "Contract negotiation: Terms analysis, risk assessment, liability protection",
            "Employment issues: Discrimination, harassment, termination, severance",
            "Intellectual property: Patent filing, trademark registration, copyright protection",
            "Litigation support: Case analysis, document review, settlement negotiation",
            "Compliance management: Regulatory requirements, audit preparation, risk mitigation"
        ]
        
        for i, scenario in enumerate(legal_scenarios):
            chunk = KnowledgeChunk(
                id=f"scenario_{i}",
                content=f"Legal scenario: {scenario}",
                source="project_patterns",
                category="legal_scenario",
                metadata={"scenario_type": "common_legal_case", "description": scenario}
            )
            chunks.append(chunk)
    
    def _build_embeddings(self):
        """Build TF-IDF embeddings for knowledge chunks"""
        if not self.knowledge_chunks:
            logger.warning("No knowledge chunks available for embedding")
            return
        
        # Extract content for vectorization
        content_texts = [chunk.content for chunk in self.knowledge_chunks]
        
        # Fit vectorizer and create embeddings
        self.embeddings = self.vectorizer.fit_transform(content_texts)
        self.is_fitted = True
        
        logger.info(f"Built embeddings for {len(self.knowledge_chunks)} knowledge chunks")
    
    def retrieve_relevant_knowledge(self, query: str, top_k: int = 5, 
                                  category_filter: Optional[str] = None) -> RAGResult:
        """Retrieve relevant knowledge chunks for a given query"""
        if not self.is_fitted:
            logger.error("RAG system not fitted. Call _build_embeddings() first.")
            return RAGResult([], [], 0, query)
        
        # Transform query using fitted vectorizer
        query_vector = self.vectorizer.transform([query])
        
        # Calculate cosine similarity
        similarities = cosine_similarity(query_vector, self.embeddings).flatten()
        
        # Get top-k indices
        top_indices = np.argsort(similarities)[::-1][:top_k]
        
        # Filter by category if specified
        if category_filter:
            filtered_chunks = []
            filtered_scores = []
            for idx in top_indices:
                chunk = self.knowledge_chunks[idx]
                if chunk.category == category_filter:
                    filtered_chunks.append(chunk)
                    filtered_scores.append(similarities[idx])
            
            # If not enough chunks in category, add more
            if len(filtered_chunks) < top_k:
                for idx in top_indices:
                    chunk = self.knowledge_chunks[idx]
                    if chunk.category != category_filter and len(filtered_chunks) < top_k:
                        filtered_chunks.append(chunk)
                        filtered_scores.append(similarities[idx])
            
            chunks = filtered_chunks
            scores = filtered_scores
        else:
            chunks = [self.knowledge_chunks[idx] for idx in top_indices]
            scores = [similarities[idx] for idx in top_indices]
        
        return RAGResult(
            chunks=chunks,
            relevance_scores=scores,
            total_chunks=len(self.knowledge_chunks),
            query=query
        )
    
    def get_context_for_agent(self, agent_type: str, query: str, 
                             conversation_context: Optional[str] = None) -> str:
        """Get relevant context for a specific AI agent"""
        
        # Agent-specific category filters
        agent_categories = {
            "ChatSummarizer": ["workflow", "legal_scenario", "ai_pattern"],
            "ClientGoalExtractor": ["legal_domain", "workflow", "user_type"],
            "ReplySuggester": ["workflow_pattern", "user_type", "legal_scenario"],
            "ClarityAgent": ["legal_domain", "legal_scenario", "feature"]
        }
        
        # Retrieve relevant knowledge
        relevant_categories = agent_categories.get(agent_type, [])
        rag_result = self.retrieve_relevant_knowledge(query, top_k=3)
        
        # Build context string
        context_parts = []
        
        # Add agent-specific context
        context_parts.append(f"# {agent_type} Context")
        context_parts.append(f"Query: {query}")
        
        if conversation_context:
            context_parts.append(f"Conversation Context: {conversation_context}")
        
        # Add relevant knowledge chunks
        context_parts.append("\n# Relevant Knowledge:")
        for chunk, score in zip(rag_result.chunks, rag_result.relevance_scores):
            if not relevant_categories or chunk.category in relevant_categories:
                context_parts.append(f"## {chunk.category.title()}: {chunk.content}")
                if chunk.metadata:
                    context_parts.append(f"Metadata: {json.dumps(chunk.metadata, indent=2)}")
                context_parts.append(f"Relevance Score: {score:.3f}")
                context_parts.append("")
        
        return "\n".join(context_parts)
    
    def enhance_prompt_with_context(self, base_prompt: str, agent_type: str, 
                                   query: str, conversation_context: Optional[str] = None) -> str:
        """Enhance a base prompt with relevant context from RAG system"""
        
        # Get relevant context
        context = self.get_context_for_agent(agent_type, query, conversation_context)
        
        # Combine with base prompt
        enhanced_prompt = f"""
{context}

# AI Agent Instructions
{base_prompt}

# Additional Context
Use the relevant knowledge above to provide more accurate, project-specific responses. Consider the Notal platform's features, workflows, and legal domain expertise when generating your response.
"""
        
        return enhanced_prompt
    
    def add_custom_knowledge(self, content: str, source: str, category: str, 
                           metadata: Optional[Dict[str, Any]] = None):
        """Add custom knowledge chunk to the system"""
        chunk_id = f"custom_{len(self.knowledge_chunks)}"
        
        chunk = KnowledgeChunk(
            id=chunk_id,
            content=content,
            source=source,
            category=category,
            metadata=metadata or {}
        )
        
        self.knowledge_chunks.append(chunk)
        
        # Rebuild embeddings
        self._build_embeddings()
        
        logger.info(f"Added custom knowledge chunk: {chunk_id}")
    
    def search_knowledge(self, query: str, category: Optional[str] = None) -> List[Dict[str, Any]]:
        """Search knowledge base and return structured results"""
        rag_result = self.retrieve_relevant_knowledge(query, top_k=10, category_filter=category)
        
        results = []
        for chunk, score in zip(rag_result.chunks, rag_result.relevance_scores):
            results.append({
                "id": chunk.id,
                "content": chunk.content,
                "source": chunk.source,
                "category": chunk.category,
                "metadata": chunk.metadata,
                "relevance_score": float(score)
            })
        
        return results
    
    def get_knowledge_stats(self) -> Dict[str, Any]:
        """Get statistics about the knowledge base"""
        stats = {
            "total_chunks": len(self.knowledge_chunks),
            "categories": defaultdict(int),
            "sources": defaultdict(int),
            "is_fitted": self.is_fitted
        }
        
        for chunk in self.knowledge_chunks:
            stats["categories"][chunk.category] += 1
            stats["sources"][chunk.source] += 1
        
        return dict(stats)
    
    def save_knowledge_base(self, filepath: str):
        """Save the knowledge base to disk"""
        data = {
            "chunks": [
                {
                    "id": chunk.id,
                    "content": chunk.content,
                    "source": chunk.source,
                    "category": chunk.category,
                    "metadata": chunk.metadata
                }
                for chunk in self.knowledge_chunks
            ],
            "vectorizer": self.vectorizer,
            "embeddings": self.embeddings.tolist() if self.embeddings is not None else None,
            "is_fitted": self.is_fitted
        }
        
        with open(filepath, 'wb') as f:
            pickle.dump(data, f)
        
        logger.info(f"Saved knowledge base to {filepath}")
    
    def load_knowledge_base(self, filepath: str):
        """Load the knowledge base from disk"""
        with open(filepath, 'rb') as f:
            data = pickle.load(f)
        
        # Reconstruct knowledge chunks
        self.knowledge_chunks = [
            KnowledgeChunk(
                id=chunk_data["id"],
                content=chunk_data["content"],
                source=chunk_data["source"],
                category=chunk_data["category"],
                metadata=chunk_data["metadata"]
            )
            for chunk_data in data["chunks"]
        ]
        
        # Restore vectorizer and embeddings
        self.vectorizer = data["vectorizer"]
        self.embeddings = np.array(data["embeddings"]) if data["embeddings"] else None
        self.is_fitted = data["is_fitted"]
        
        logger.info(f"Loaded knowledge base from {filepath}")

# Global RAG system instance
notal_rag = NotalRAGSystem()
# Keep certio_rag as alias for backwards compatibility
certio_rag = notal_rag

# Utility functions for easy integration
def enhance_agent_prompt(agent_type: str, base_prompt: str, query: str, 
                        conversation_context: Optional[str] = None) -> str:
    """Enhance an agent prompt with RAG context"""
    return notal_rag.enhance_prompt_with_context(
        base_prompt, agent_type, query, conversation_context
    )

def get_relevant_context(agent_type: str, query: str, 
                        conversation_context: Optional[str] = None) -> str:
    """Get relevant context for an agent"""
    return notal_rag.get_context_for_agent(agent_type, query, conversation_context)

def search_project_knowledge(query: str, category: Optional[str] = None) -> List[Dict[str, Any]]:
    """Search the project knowledge base"""
    return notal_rag.search_knowledge(query, category)
