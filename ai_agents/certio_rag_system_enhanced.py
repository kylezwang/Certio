"""
Enhanced Notal RAG (Retrieval-Augmented Generation) System
Improved retrieval with onboarding knowledge, better query understanding, and intent detection
"""

import json
import logging
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
from datetime import datetime
import re
from collections import defaultdict

# Try importing vector-based RAG, fall back to basic if unavailable
try:
    import numpy as np
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.metrics.pairwise import cosine_similarity
    VECTOR_AVAILABLE = True
except ImportError:
    VECTOR_AVAILABLE = False
    logging.warning("numpy/sklearn not available, using basic text matching")

from certio_knowledge_base import notal_kb
from certio_onboarding_knowledge import notal_onboarding_kb

logger = logging.getLogger(__name__)

@dataclass
class KnowledgeChunk:
    """Represents a chunk of knowledge for RAG"""
    id: str
    content: str
    source: str
    category: str
    metadata: Dict[str, Any]
    priority: int = 1  # Higher priority chunks rank higher

@dataclass
class QueryIntent:
    """Detected intent from user query"""
    intent_type: str  # "how_to", "what_is", "where_is", "troubleshooting", "general"
    confidence: float
    user_level: str  # "beginner", "intermediate", "advanced"
    specific_feature: Optional[str] = None
    query_category: Optional[str] = None

@dataclass
class RAGResult:
    """Result from RAG retrieval"""
    chunks: List[KnowledgeChunk]
    relevance_scores: List[float]
    total_chunks: int
    query: str
    intent: Optional[QueryIntent] = None

class EnhancedNotalRAGSystem:
    """Retrieval-augmented generation system with onboarding knowledge and intent detection"""
    
    def __init__(self):
        self.knowledge_chunks: List[KnowledgeChunk] = []
        self.vector_available = VECTOR_AVAILABLE
        
        if self.vector_available:
            self.vectorizer = TfidfVectorizer(
                max_features=1500,
                stop_words='english',
                ngram_range=(1, 3),  # Increased to capture more phrases
                min_df=1,
                max_df=0.9
            )
            self.embeddings: Optional[np.ndarray] = None
            self.is_fitted = False
        
        # Intent detection patterns
        self._initialize_intent_patterns()
        
        # Initialize all knowledge
        self._initialize_knowledge_chunks()
        
        if self.vector_available:
            self._build_embeddings()
    
    def _initialize_intent_patterns(self):
        """Initialize patterns for intent detection"""
        self.intent_patterns = {
            "how_to": [
                r"how (do|can|to|do i|can i)",
                r"what (is the|are the) (steps?|process|way)",
                r"walk me through",
                r"guide me",
                r"show me how",
                r"teach me",
                r"help me (create|make|do|set up|configure|use)",
                r"(create|make|set up|configure|add|assign|schedule|start|begin)",
                r"i (want to|need to|would like to)"
            ],
            "what_is": [
                r"what (is|are|does|do)",
                r"explain (what|the|this)",
                r"define",
                r"tell me about",
                r"describe",
                r"(meaning|definition|explanation) of"
            ],
            "where_is": [
                r"where (is|can i find|do i)",
                r"(find|locate|access)",
                r"which (page|section|tab)",
                r"navigate to",
                r"i can't find",
                r"looking for"
            ],
            "troubleshooting": [
                r"(not working|doesn't work|isn't working|won't)",
                r"(error|problem|issue|bug)",
                r"(can't|cannot|unable to)",
                r"(broken|failed|failing)",
                r"why (isn't|is not|won't|can't)",
                r"help.*(not|problem|issue)",
                r"stuck"
            ],
            "onboarding": [
                r"(new|first time|getting started|beginner|just started)",
                r"never used",
                r"don't know how",
                r"(overview|introduction|basics)",
                r"where (do i|should i) start",
                r"i'm (new|lost)",
                r"(let's|let us) get started",
                r"how do i get started",
                r"notal.*get started",
                r"get started.*notal"
            ]
        }
        
        # Compile patterns for efficiency
        self.compiled_patterns = {
            intent: [re.compile(pattern, re.IGNORECASE) for pattern in patterns]
            for intent, patterns in self.intent_patterns.items()
        }
        
        # Feature keywords for specific feature detection
        self.feature_keywords = {
            "events": ["event", "matter", "case", "client case", "legal matter"],
            "tasks": ["task", "assignment", "work item", "to do", "todo"],
            "calendar": ["calendar", "event", "meeting", "appointment", "schedule"],
            "communications": ["message", "chat", "communicate", "conversation", "dm", "direct message", "channel"],
            "notal_ai_assistant": ["ai", "assistant", "notal ai", "chatbot"],
            "documents": ["document", "file", "upload", "download"],
            "teams": ["team", "member", "user", "person", "people", "colleague"],
            "dashboard": ["dashboard", "home", "overview", "main page"],
            "settings": ["setting", "configure", "preference"],
            "history": ["history", "audit", "log", "activity"]
        }
    
    def _detect_query_intent(self, query: str) -> QueryIntent:
        """Detect the intent and context from user query"""
        query_lower = query.lower()
        
        # Detect intent type
        intent_scores = defaultdict(int)
        for intent, patterns in self.compiled_patterns.items():
            for pattern in patterns:
                if pattern.search(query_lower):
                    intent_scores[intent] += 1
        
        # Determine primary intent
        if intent_scores:
            primary_intent = max(intent_scores.items(), key=lambda x: x[1])[0]
            confidence = intent_scores[primary_intent] / sum(intent_scores.values())
        else:
            primary_intent = "general"
            confidence = 0.5
        
        # Detect user level
        user_level = "intermediate"  # default
        if any(word in query_lower for word in ["new", "first time", "beginner", "never used", "just started", "getting started"]):
            user_level = "beginner"
        elif any(word in query_lower for word in ["advanced", "expert", "complex", "technical"]):
            user_level = "advanced"
        
        # Detect specific feature
        specific_feature = None
        for feature, keywords in self.feature_keywords.items():
            if any(keyword in query_lower for keyword in keywords):
                specific_feature = feature
                break
        
        # Determine query category
        query_category = self._categorize_query(query_lower, primary_intent)
        
        return QueryIntent(
            intent_type=primary_intent,
            confidence=confidence,
            user_level=user_level,
            specific_feature=specific_feature,
            query_category=query_category
        )
    
    def _categorize_query(self, query_lower: str, primary_intent: str) -> str:
        """Categorize the query for better routing"""
        if primary_intent in ["how_to", "onboarding"]:
            return "tutorial"
        elif primary_intent == "troubleshooting":
            return "troubleshooting"
        elif primary_intent in ["what_is", "general"]:
            if any(word in query_lower for word in ["work", "feature", "use", "purpose"]):
                return "explanation"
            return "general"
        elif primary_intent == "where_is":
            return "navigation"
        return "general"
    
    def _initialize_knowledge_chunks(self):
        """Initialize all knowledge chunks from various sources"""
        chunks = []
        chunk_id = 0
        
        # 1. Add feature knowledge from base KB (PRIORITY: 3)
        for feature_name, feature in notal_kb.features.items():
            chunk = KnowledgeChunk(
                id=f"feature_{feature_name}_{chunk_id}",
                content=f"{feature.name}: {feature.description}. Technical details: {feature.technical_details}. Business value: {feature.business_value}",
                source="knowledge_base",
                category="feature",
                metadata={
                    "feature_name": feature_name,
                    "user_types": feature.user_types,
                    "event_categories": feature.legal_areas,
                    "api_endpoints": feature.api_endpoints
                },
                priority=3
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 2. Add workflow knowledge (PRIORITY: 3)
        for workflow_name, workflow in notal_kb.workflows.items():
            chunk = KnowledgeChunk(
                id=f"workflow_{workflow_name}_{chunk_id}",
                content=f"{workflow.name}: {workflow.description}. Steps: {' → '.join(workflow.steps)}. Participants: {', '.join(workflow.participants)}",
                source="knowledge_base",
                category="workflow",
                metadata={
                    "workflow_name": workflow_name,
                    "participants": workflow.participants,
                    "requirements": workflow.legal_requirements,
                    "ai_agents": workflow.ai_agents_involved
                },
                priority=3
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 3. Add getting started guides (PRIORITY: 5 - HIGHEST for onboarding)
        for guide_name, guide in notal_onboarding_kb.getting_started_guides.items():
            chunk = KnowledgeChunk(
                id=f"guide_{guide_name}_{chunk_id}",
                content=f"{guide.title}: {guide.content} Steps: {' | '.join(guide.steps)}. Tips: {' | '.join(guide.tips)}",
                source="onboarding_guide",
                category="getting_started",
                metadata={
                    "guide_name": guide_name,
                    "user_types": guide.user_types,
                    "related_features": guide.related_features,
                    "common_questions": guide.common_questions
                },
                priority=5
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 4. Add FAQs (PRIORITY: 5 - HIGHEST for direct questions)
        for faq in notal_onboarding_kb.faqs:
            chunk = KnowledgeChunk(
                id=f"faq_{chunk_id}",
                content=f"Q: {faq.question} A: {faq.answer}",
                source="faq",
                category="faq",
                metadata={
                    "question": faq.question,
                    "answer": faq.answer,
                    "faq_category": faq.category,
                    "user_types": faq.user_types,
                    "related_features": faq.related_features
                },
                priority=5
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 5. Add task tutorials (PRIORITY: 4 - HIGH for how-to queries)
        for tutorial_name, tutorial in notal_onboarding_kb.task_tutorials.items():
            chunk = KnowledgeChunk(
                id=f"tutorial_{tutorial_name}_{chunk_id}",
                content=f"{tutorial.title}: {tutorial.content} Steps: {' | '.join(tutorial.steps)}. Tips: {' | '.join(tutorial.tips)}",
                source="tutorial",
                category="tutorial",
                metadata={
                    "tutorial_name": tutorial_name,
                    "user_types": tutorial.user_types,
                    "related_features": tutorial.related_features,
                    "common_questions": tutorial.common_questions
                },
                priority=4
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 6. Add navigation guides (PRIORITY: 4 - HIGH for navigation queries)
        for nav_name, nav_content in notal_onboarding_kb.navigation_guides.items():
            chunk = KnowledgeChunk(
                id=f"navigation_{nav_name}_{chunk_id}",
                content=f"Navigation Guide - {nav_name.replace('_', ' ').title}: {nav_content}",
                source="navigation_guide",
                category="navigation",
                metadata={"guide_name": nav_name},
                priority=4
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 7. Add common scenarios (PRIORITY: 4)
        for scenario_name, scenario in notal_onboarding_kb.common_scenarios.items():
            chunk = KnowledgeChunk(
                id=f"scenario_{scenario_name}_{chunk_id}",
                content=f"{scenario.title}: {scenario.content} Steps: {' | '.join(scenario.steps)}. Tips: {' | '.join(scenario.tips)}",
                source="scenario",
                category="scenario",
                metadata={
                    "scenario_name": scenario_name,
                    "user_types": scenario.user_types,
                    "related_features": scenario.related_features
                },
                priority=4
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 8. Add troubleshooting (PRIORITY: 5 - HIGHEST for problem queries)
        for problem_key, troubleshoot in notal_onboarding_kb.troubleshooting.items():
            chunk = KnowledgeChunk(
                id=f"troubleshoot_{problem_key}_{chunk_id}",
                content=f"Problem: {troubleshoot['problem']} Solutions: {troubleshoot['solutions']}",
                source="troubleshooting",
                category="troubleshooting",
                metadata={"problem_key": problem_key},
                priority=5
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 9. Add event category knowledge (PRIORITY: 2)
        for domain, info in notal_kb.legal_domains.items():
            chunk = KnowledgeChunk(
                id=f"event_category_{domain}_{chunk_id}",
                content=f"{domain.replace('_', ' ').title}: {info['description']}. Common documents: {', '.join(info['common_documents'])}. Key terms: {', '.join(info['key_terms'])}",
                source="knowledge_base",
                category="event_category",
                metadata={
                    "domain": domain,
                    "common_documents": info['common_documents'],
                    "key_terms": info['key_terms'],
                    "ai_applications": info['ai_applications']
                },
                priority=2
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 10. Add user type knowledge (PRIORITY: 3)
        for user_type, info in notal_kb.user_types.items():
            chunk = KnowledgeChunk(
                id=f"user_{user_type}_{chunk_id}",
                content=f"{user_type.title}: {info['description']}. Permissions: {', '.join(info['permissions'])}. AI interactions: {', '.join(info['ai_interactions'])}",
                source="knowledge_base",
                category="user_type",
                metadata={
                    "user_type": user_type,
                    "permissions": info['permissions'],
                    "ai_interactions": info['ai_interactions'],
                    "typical_needs": info['typical_needs']
                },
                priority=3
            )
            chunks.append(chunk)
            chunk_id += 1
        
        # 11. Add technical architecture (PRIORITY: 2)
        tech_arch = notal_kb.technical_architecture
        for component, details in tech_arch.items():
            chunk = KnowledgeChunk(
                id=f"tech_{component}_{chunk_id}",
                content=f"Technical Architecture - {component.title}: {json.dumps(details, indent=2)}",
                source="knowledge_base",
                category="technical",
                metadata={"component": component, "details": details},
                priority=2
            )
            chunks.append(chunk)
            chunk_id += 1
        
        self.knowledge_chunks = chunks
        logger.info(f"Initialized {len(chunks)} enhanced knowledge chunks with onboarding content")
    
    def _build_embeddings(self):
        """Build TF-IDF embeddings for knowledge chunks"""
        if not self.vector_available or not self.knowledge_chunks:
            return
        
        content_texts = [chunk.content for chunk in self.knowledge_chunks]
        self.embeddings = self.vectorizer.fit_transform(content_texts)
        self.is_fitted = True
        logger.info(f"Built embeddings for {len(self.knowledge_chunks)} knowledge chunks")
    
    def retrieve_relevant_knowledge(self, query: str, top_k: int = 5, 
                                  user_type: Optional[str] = None,
                                  context: Optional[Dict[str, Any]] = None) -> RAGResult:
        """
        Retrieve relevant knowledge chunks with intent-aware retrieval
        """
        # Detect query intent
        intent = self._detect_query_intent(query)
        
        # Adjust retrieval strategy based on intent
        if intent.intent_type == "onboarding":
            category_boost = ["getting_started", "faq", "tutorial", "navigation"]
        elif intent.intent_type == "how_to":
            category_boost = ["tutorial", "getting_started", "faq", "scenario"]
        elif intent.intent_type == "where_is":
            category_boost = ["navigation", "faq", "feature"]
        elif intent.intent_type == "troubleshooting":
            category_boost = ["troubleshooting", "faq"]
        elif intent.intent_type == "what_is":
            category_boost = ["feature", "faq", "getting_started"]
        else:
            category_boost = []
        
        # Perform retrieval
        if self.vector_available and self.is_fitted:
            chunks, scores = self._vector_retrieve(query, top_k, category_boost, intent, user_type)
        else:
            chunks, scores = self._basic_retrieve(query, top_k, category_boost, intent, user_type)
        
        return RAGResult(
            chunks=chunks,
            relevance_scores=scores,
            total_chunks=len(self.knowledge_chunks),
            query=query,
            intent=intent
        )
    
    def _vector_retrieve(self, query: str, top_k: int, category_boost: List[str],
                        intent: QueryIntent, user_type: Optional[str]) -> Tuple[List[KnowledgeChunk], List[float]]:
        """Vector-based retrieval with TF-IDF"""
        query_vector = self.vectorizer.transform([query])
        similarities = cosine_similarity(query_vector, self.embeddings).flatten()
        
        # Apply priority and category boosting
        boosted_scores = []
        for idx, (chunk, score) in enumerate(zip(self.knowledge_chunks, similarities)):
            # Base score
            boosted_score = score
            
            # Priority boost (multiply by priority level)
            boosted_score *= chunk.priority
            
            # Category boost
            if chunk.category in category_boost:
                boost_idx = category_boost.index(chunk.category)
                boosted_score *= (2.0 - (boost_idx * 0.2))  # Earlier categories get higher boost
            
            # User type relevance boost
            if user_type and "user_types" in chunk.metadata:
                if user_type in chunk.metadata["user_types"] or "All" in chunk.metadata["user_types"]:
                    boosted_score *= 1.3
            
            # Specific feature boost
            if intent.specific_feature and "feature_name" in chunk.metadata:
                if chunk.metadata["feature_name"] == intent.specific_feature:
                    boosted_score *= 1.5
            
            boosted_scores.append(boosted_score)
        
        # Get top-k indices
        top_indices = np.argsort(boosted_scores)[::-1][:top_k]
        
        chunks = [self.knowledge_chunks[idx] for idx in top_indices]
        scores = [boosted_scores[idx] for idx in top_indices]
        
        return chunks, scores
    
    def _basic_retrieve(self, query: str, top_k: int, category_boost: List[str],
                       intent: QueryIntent, user_type: Optional[str]) -> Tuple[List[KnowledgeChunk], List[float]]:
        """Basic text matching retrieval for when vectors aren't available"""
        query_lower = query.lower()
        query_words = set(query_lower.split())
        
        scored_chunks = []
        
        for chunk in self.knowledge_chunks:
            content_lower = chunk.content.lower()
            content_words = set(content_lower.split())
            
            # Calculate word overlap score
            overlap = len(query_words.intersection(content_words))
            total_words = len(query_words.union(content_words))
            score = overlap / total_words if total_words > 0 else 0.0
            
            # Boost for exact phrase matches
            if any(word in content_lower for word in query_words):
                score += 0.1
            
            # Apply same boosts as vector retrieval
            score *= chunk.priority
            
            if chunk.category in category_boost:
                boost_idx = category_boost.index(chunk.category)
                score *= (2.0 - (boost_idx * 0.2))
            
            if user_type and "user_types" in chunk.metadata:
                if user_type in chunk.metadata["user_types"] or "All" in chunk.metadata["user_types"]:
                    score *= 1.3
            
            if intent.specific_feature and "feature_name" in chunk.metadata:
                if chunk.metadata["feature_name"] == intent.specific_feature:
                    score *= 1.5
            
            scored_chunks.append((chunk, score))
        
        # Sort and return top-k
        scored_chunks.sort(key=lambda x: x[1], reverse=True)
        top_chunks = scored_chunks[:top_k]
        
        chunks = [chunk for chunk, _ in top_chunks]
        scores = [score for _, score in top_chunks]
        
        return chunks, scores
    
    def get_context_for_agent(self, agent_type: str, query: str, 
                             user_type: Optional[str] = None,
                             conversation_context: Optional[Dict[str, Any]] = None) -> str:
        """Get relevant context for a specific AI agent with extra retrieval"""
        
        # Detect intent first to determine how many chunks to retrieve
        intent = self._detect_query_intent(query)
        
        # For onboarding/getting started queries, retrieve more chunks for full guidance
        if intent.intent_type == "onboarding" or "get started" in query.lower() or "let's get started" in query.lower():
            top_k = 10  # Get more comprehensive knowledge for onboarding
        elif intent.intent_type == "how_to":
            top_k = 8   # Get more steps for how-to queries
        else:
            top_k = 5  # Default for other queries
        
        # Retrieve relevant knowledge with intent detection
        rag_result = self.retrieve_relevant_knowledge(query, top_k=top_k, user_type=user_type, context=conversation_context)
        
        # Build context string
        context_parts = []
        
        # Add intent information
        if rag_result.intent:
            context_parts.append(f"# Query Intent Analysis")
            context_parts.append(f"Intent Type: {rag_result.intent.intent_type.replace('_', ' ').title()}")
            context_parts.append(f"User Level: {rag_result.intent.user_level.title()}")
            if rag_result.intent.specific_feature:
                context_parts.append(f"Specific Feature: {rag_result.intent.specific_feature}")
            if rag_result.intent.query_category:
                context_parts.append(f"Query Category: {rag_result.intent.query_category}")
            context_parts.append("")
        
        # Add agent-specific context
        context_parts.append(f"# {agent_type} Context")
        context_parts.append(f"Query: {query}")
        if user_type:
            context_parts.append(f"User Type: {user_type}")
        context_parts.append("")
        
        if conversation_context:
            context_parts.append(f"Conversation Context: {json.dumps(conversation_context, indent=2)}")
            context_parts.append("")
        
        # Add relevant knowledge chunks
        context_parts.append("# Relevant Knowledge (Ranked by Relevance):")
        for i, (chunk, score) in enumerate(zip(rag_result.chunks, rag_result.relevance_scores), 1):
            context_parts.append(f"\n## Knowledge #{i} [{chunk.category.upper()}] (Relevance: {score:.3f}, Priority: {chunk.priority}/5)")
            context_parts.append(f"Source: {chunk.source}")
            context_parts.append(f"\n{chunk.content}\n")
            
            # For getting started guides, prominently highlight steps
            if chunk.category == "getting_started" and chunk.metadata and "guide_name" in chunk.metadata:
                # Extract steps from content - format is "Steps: step1 | step2 | step3"
                if "Steps:" in chunk.content:
                    steps_section = chunk.content.split("Steps:")[1].split("Tips:")[0] if "Tips:" in chunk.content else chunk.content.split("Steps:")[1]
                    steps_list = [step.strip() for step in steps_section.split("|") if step.strip()]
                    if steps_list:
                        context_parts.append("\n**STEPS TO FOLLOW (CRITICAL - USE ALL OF THESE IN YOUR RESPONSE):**")
                        for idx, step in enumerate(steps_list, 1):
                            context_parts.append(f"{idx}. {step}")
                        context_parts.append("")
            
            if chunk.metadata and chunk.category in ["faq", "tutorial", "getting_started", "scenario"]:
                # Include metadata for actionable chunks
                context_parts.append(f"**Additional Info:**")
                if "common_questions" in chunk.metadata:
                    context_parts.append(f"- Related Questions: {', '.join(chunk.metadata['common_questions'][:3])}")
                if "related_features" in chunk.metadata:
                    context_parts.append(f"- Related Features: {', '.join(chunk.metadata['related_features'][:5])}")
            
            context_parts.append("")
        
        return "\n".join(context_parts)
    
    def enhance_prompt_with_context(self, base_prompt: str, agent_type: str, 
                                   query: str, user_type: Optional[str] = None,
                                   conversation_context: Optional[Dict[str, Any]] = None) -> str:
        """Enhance a base prompt with relevant context from extra RAG system"""
        
        # Get relevant context
        context = self.get_context_for_agent(agent_type, query, user_type, conversation_context)
        
        # Detect intent to provide specific instructions
        intent = self._detect_query_intent(query)
        is_onboarding = intent.intent_type == "onboarding" or "get started" in query.lower() or "let's get started" in query.lower()
        
        # Build extra prompt with context-specific instructions
        onboarding_instructions = ""
        if is_onboarding:
            onboarding_instructions = """
# CRITICAL: Onboarding/Getting Started Query Detected
- The user is asking for getting started guidance. You MUST provide comprehensive, detailed step-by-step instructions
- Use ALL the steps from the "Getting Started" guides in the knowledge base - do not summarize or shorten them
- Include ALL steps from the knowledge chunks, especially from "getting_started" category
- Provide a complete walkthrough of the onboarding process, not just a brief overview
- Be welcoming and encouraging, but also provide ALL the detailed steps they need
- Format the steps clearly with numbered lists and explanations for each step
- If the knowledge base contains "Getting Started as a Lawyer" guide, use ALL steps from that guide
"""
        
        # Combine with base prompt
        enhanced_prompt = f"""
{context}

# AI Agent Instructions
{base_prompt}

{onboarding_instructions}

# Response Guidelines Based on Query Analysis
- The user's intent has been analyzed and relevant knowledge has been retrieved
- Prioritize information from higher-priority knowledge sources (tutorials, FAQs, guides)
- If the user is a beginner, provide step-by-step guidance with clear explanations
- If this is a "how to" query, provide actionable steps users can follow immediately
- If this is a navigation query, give specific UI locations and click paths
- If this is troubleshooting, provide solutions in priority order
- Always format responses in clear HTML with proper <p> tags, <ul><li> lists, and <br> for breaks
- Reference specific Notal features by name and explain where to find them
- For onboarding questions, be extra welcoming and provide comprehensive guidance
- IMPORTANT: When knowledge chunks contain "Steps:" lists, include ALL of those steps in your response
- DO NOT summarize or shorten getting started guides - provide the full detailed instructions
"""
        
        return enhanced_prompt
    
    def search_knowledge(self, query: str, user_type: Optional[str] = None,
                        category: Optional[str] = None) -> List[Dict[str, Any]]:
        """Search knowledge base and return structured results"""
        rag_result = self.retrieve_relevant_knowledge(query, top_k=10, user_type=user_type)
        
        results = []
        for chunk, score in zip(rag_result.chunks, rag_result.relevance_scores):
            if category and chunk.category != category:
                continue
            
            results.append({
                "id": chunk.id,
                "content": chunk.content,
                "source": chunk.source,
                "category": chunk.category,
                "priority": chunk.priority,
                "metadata": chunk.metadata,
                "relevance_score": float(score)
            })
        
        return results
    
    def get_knowledge_stats(self) -> Dict[str, Any]:
        """Get statistics about the extra knowledge base"""
        stats = {
            "total_chunks": len(self.knowledge_chunks),
            "vector_mode": self.vector_available,
            "categories": defaultdict(int),
            "sources": defaultdict(int),
            "priority_distribution": defaultdict(int)
        }
        
        for chunk in self.knowledge_chunks:
            stats["categories"][chunk.category] += 1
            stats["sources"][chunk.source] += 1
            stats["priority_distribution"][f"priority_{chunk.priority}"] += 1
        
        return dict(stats)

# Global extra RAG system instance
enhanced_notal_rag = EnhancedNotalRAGSystem()

# Utility functions for easy integration
def enhance_agent_prompt(agent_type: str, base_prompt: str, query: str, 
                        user_type: Optional[str] = None,
                        conversation_context: Optional[Dict[str, Any]] = None) -> str:
    """Enhance an agent prompt with extra RAG context"""
    return enhanced_notal_rag.enhance_prompt_with_context(
        base_prompt, agent_type, query, user_type, conversation_context
    )

def get_relevant_context(agent_type: str, query: str,
                        user_type: Optional[str] = None,
                        conversation_context: Optional[Dict[str, Any]] = None) -> str:
    """Get relevant context for an agent with extra retrieval"""
    return enhanced_notal_rag.get_context_for_agent(agent_type, query, user_type, conversation_context)

def search_project_knowledge(query: str, user_type: Optional[str] = None,
                            category: Optional[str] = None) -> List[Dict[str, Any]]:
    """Search the extra project knowledge base"""
    return enhanced_notal_rag.search_knowledge(query, user_type, category)

def get_knowledge_stats() -> Dict[str, Any]:
    """Get extra knowledge base statistics"""
    return enhanced_notal_rag.get_knowledge_stats()

