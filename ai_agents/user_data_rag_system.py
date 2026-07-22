"""
User Data RAG System for Notal
Provides AI agents with comprehensive access to user-scoped data across all modules
(Events, Tasks, Calendar, Communications, Clients, Teams)
"""

import json
import logging
from typing import List, Dict, Any, Optional
from dataclasses import dataclass, field
from datetime import datetime
from collections import defaultdict
import asyncio
from pathlib import Path

# Try importing vector-based RAG, fall back to basic if unavailable
try:
    import numpy as np
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.metrics.pairwise import cosine_similarity
    VECTOR_AVAILABLE = True
    NumpyArrayType = np.ndarray
except ImportError:
    VECTOR_AVAILABLE = False
    NumpyArrayType = Any  # Fallback type when numpy isn't available
    logging.warning("numpy/sklearn not available for user data RAG, using basic text matching")

logger = logging.getLogger(__name__)

@dataclass
class UserDataChunk:
    """Represents a chunk of user data for RAG retrieval"""
    id: str
    module_name: str
    entity_type: str
    entity_id: int
    title: str
    content: str
    metadata: Dict[str, Any]
    created_at: str
    modified_at: Optional[str] = None
    relevance_score: float = 0.0
    embedding: Optional[NumpyArrayType] = None

@dataclass
class UserDataIndex:
    """Index of user data for a specific user and organization"""
    user_id: int
    organization_id: int
    user_name: str = "User"  # Actual user's name (not client name)
    chunks: List[UserDataChunk] = field(default_factory=list)
    module_summaries: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    last_synced: Optional[datetime] = None
    total_chunks: int = 0

@dataclass
class UserDataSearchResult:
    """Result from user data search"""
    chunks: List[UserDataChunk]
    relevance_scores: List[float]
    total_available: int
    query: str
    module_breakdown: Dict[str, int]

class UserDataRAGSystem:
    """
    RAG system for user-scoped data across all Notal modules
    Provides AI with contextual knowledge about user's events, tasks, calendar, etc.
    """
    
    def __init__(self, cache_dir: str = "./data/user_data_cache"):
        self.cache_dir = Path(cache_dir)
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        
        # In-memory index by (user_id, org_id)
        self.user_indices: Dict[tuple, UserDataIndex] = {}
        
        self.vector_available = VECTOR_AVAILABLE
        if self.vector_available:
            self.vectorizer = TfidfVectorizer(
                max_features=2000,
                stop_words='english',
                ngram_range=(1, 3),
                min_df=1,
                max_df=0.95
            )
        
        logger.info(f"Initialized UserDataRAGSystem with vector support: {self.vector_available}")
    
    async def sync_user_data(self, user_id: int, organization_id: int, 
                            module_data: Dict[str, Any], metadata: Dict[str, Any],
                            user_name: str = "User") -> bool:
        """
        Sync user data from C# backend
        This is called when the backend sends updated user data context
        """
        try:
            logger.info(f"Syncing user data for {user_name} (User {user_id}) in Org {organization_id}")
            
            chunks = []
            module_summaries = {}
            
            # Process each module's data
            for module_name, module_info in module_data.items():
                if not isinstance(module_info, dict):
                    continue
                
                module_chunks = module_info.get('chunks', [])
                module_summary = module_info.get('summary', {})
                
                module_summaries[module_name] = module_summary
                
                # Convert chunks to UserDataChunk objects
                for chunk_data in module_chunks:
                    chunk = UserDataChunk(
                        id=chunk_data.get('id', ''),
                        module_name=chunk_data.get('moduleName', module_name),
                        entity_type=chunk_data.get('entityType', ''),
                        entity_id=chunk_data.get('entityId', 0),
                        title=chunk_data.get('title', ''),
                        content=chunk_data.get('content', ''),
                        metadata=chunk_data.get('metadata', {}),
                        created_at=chunk_data.get('createdAt', ''),
                        modified_at=chunk_data.get('modifiedAt'),
                        relevance_score=chunk_data.get('relevanceScore', 0.0)
                    )
                    chunks.append(chunk)
            
            # Build embeddings if vector support available
            if self.vector_available and chunks:
                await self._build_embeddings(chunks)
            
            # Create or update index
            key = (user_id, organization_id)
            self.user_indices[key] = UserDataIndex(
                user_id=user_id,
                organization_id=organization_id,
                user_name=user_name,
                chunks=chunks,
                module_summaries=module_summaries,
                last_synced=datetime.utcnow(),
                total_chunks=len(chunks)
            )
            
            # Persist to disk cache
            await self._save_to_cache(user_id, organization_id)
            
            logger.info(f"Successfully synced {len(chunks)} chunks for User {user_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to sync user data for User {user_id}: {e}", exc_info=True)
            return False
    
    async def _build_embeddings(self, chunks: List[UserDataChunk]):
        """Build TF-IDF embeddings for chunks"""
        try:
            if not chunks:
                return
            
            # Combine title and content for embedding
            texts = [f"{chunk.title} {chunk.content}" for chunk in chunks]
            
            # Build embeddings
            embeddings = self.vectorizer.fit_transform(texts)
            
            # Store embeddings in chunks
            for i, chunk in enumerate(chunks):
                chunk.embedding = embeddings[i]
            
            logger.debug(f"Built embeddings for {len(chunks)} chunks")
        except Exception as e:
            logger.warning(f"Failed to build embeddings: {e}")
    
    async def search_user_data(self, user_id: int, organization_id: int, 
                              query: str, top_k: int = 10,
                              module_filter: Optional[List[str]] = None) -> UserDataSearchResult:
        """
        Search user data with semantic/keyword matching
        Returns most relevant chunks for the query
        """
        key = (user_id, organization_id)
        
        # Check if we have index for this user
        if key not in self.user_indices:
            # Try loading from cache
            await self._load_from_cache(user_id, organization_id)
        
        if key not in self.user_indices:
            logger.warning(f"No data index found for User {user_id} in Org {organization_id}")
            return UserDataSearchResult(
                chunks=[],
                relevance_scores=[],
                total_available=0,
                query=query,
                module_breakdown={}
            )
        
        index = self.user_indices[key]
        chunks = index.chunks
        
        logger.info(f"🔍 Searching user data: {len(chunks)} total chunks available for User {user_id}")
        
        # Apply module filter if specified
        if module_filter:
            chunks = [c for c in chunks if c.module_name in module_filter]
            logger.info(f"📊 After module filter: {len(chunks)} chunks")
        
        if not chunks:
            logger.warning(f"No chunks available after filtering")
            return UserDataSearchResult(
                chunks=[],
                relevance_scores=[],
                total_available=0,
                query=query,
                module_breakdown={}
            )
        
        # Perform search
        if self.vector_available and chunks[0].embedding is not None:
            ranked_chunks, scores = await self._vector_search(chunks, query, top_k)
            logger.info(f"📈 Vector search returned {len(ranked_chunks)} chunks, top score: {scores[0] if scores else 0:.3f}")
        else:
            ranked_chunks, scores = await self._keyword_search(chunks, query, top_k)
            logger.info(f"📈 Keyword search returned {len(ranked_chunks)} chunks, top score: {scores[0] if scores else 0:.3f}")
        
        # Log what we found
        if ranked_chunks:
            logger.info(f"✅ Top result: {ranked_chunks[0].module_name} - {ranked_chunks[0].title}")
        
        # Build module breakdown
        module_breakdown = defaultdict(int)
        for chunk in ranked_chunks:
            module_breakdown[chunk.module_name] += 1
        
        return UserDataSearchResult(
            chunks=ranked_chunks,
            relevance_scores=scores,
            total_available=len(index.chunks),
            query=query,
            module_breakdown=dict(module_breakdown)
        )
    
    async def _vector_search(self, chunks: List[UserDataChunk], query: str, 
                            top_k: int) -> tuple[List[UserDataChunk], List[float]]:
        """Vector-based semantic search using TF-IDF"""
        try:
            # Transform query
            query_vector = self.vectorizer.transform([query])
            
            # Get embeddings from chunks
            chunk_embeddings = [chunk.embedding for chunk in chunks if chunk.embedding is not None]
            
            if not chunk_embeddings:
                return await self._keyword_search(chunks, query, top_k)
            
            # Calculate similarities
            from scipy.sparse import vstack
            embeddings_matrix = vstack(chunk_embeddings)
            similarities = cosine_similarity(query_vector, embeddings_matrix).flatten()
            
            # Get top-k indices
            top_indices = np.argsort(similarities)[::-1][:top_k]
            
            ranked_chunks = [chunks[i] for i in top_indices]
            scores = [float(similarities[i]) for i in top_indices]
            
            return ranked_chunks, scores
            
        except Exception as e:
            logger.warning(f"Vector search failed, falling back to keyword: {e}")
            return await self._keyword_search(chunks, query, top_k)
    
    async def _keyword_search(self, chunks: List[UserDataChunk], query: str, 
                             top_k: int) -> tuple[List[UserDataChunk], List[float]]:
        """Keyword-based search with enhanced exact matching for numbered documents"""
        import re
        
        query_lower = query.lower()
        query_words = set(query_lower.split())
        
        # Extract numbers from query for precise matching (e.g., "Post-Lab 7" -> ["7"])
        query_numbers = set(re.findall(r'\b\d+\b', query_lower))
        
        # Extract key phrases that should be matched exactly (e.g., "Post-Lab 7", "Lab 3")
        # Pattern matches things like "post-lab 7", "lab 3", "chapter 5", "section 2"
        key_phrases = re.findall(r'(?:post-?lab|lab|chapter|section|part|experiment|hw|homework|assignment)\s*\d+', query_lower)
        
        scored_chunks = []
        
        for chunk in chunks:
            content_lower = f"{chunk.title} {chunk.content}".lower()
            content_words = set(content_lower.split())
            title_lower = chunk.title.lower()
            
            # Calculate base overlap score
            overlap = len(query_words.intersection(content_words))
            total = len(query_words.union(content_words))
            score = overlap / total if total > 0 else 0.0
            
            # CRITICAL: Check for exact key phrase matches (e.g., "Post-Lab 7" in title)
            # This is the most important signal for numbered documents
            for phrase in key_phrases:
                # Normalize the phrase for matching (handle "post-lab" vs "postlab" vs "post lab")
                normalized_phrase = phrase.replace('-', '').replace(' ', '')
                normalized_title = title_lower.replace('-', '').replace(' ', '')
                
                if normalized_phrase in normalized_title:
                    score += 5.0  # VERY strong boost for exact numbered phrase match
                    logger.debug(f"Exact phrase match '{phrase}' in '{chunk.title}' (+5.0)")
            
            # Check for number mismatches - PENALIZE if query has a number but title has DIFFERENT number
            title_numbers = set(re.findall(r'\b\d+\b', title_lower))
            if query_numbers and title_numbers:
                # If query asks for "7" but title has "1", penalize heavily
                if query_numbers.isdisjoint(title_numbers):
                    # Numbers in query don't match numbers in title - reduce score
                    score *= 0.3  # Heavy penalty for number mismatch
                    logger.debug(f"Number mismatch: query has {query_numbers}, title '{chunk.title}' has {title_numbers}")
                elif query_numbers.intersection(title_numbers):
                    # At least one number matches - boost
                    score += 2.0
                    logger.debug(f"Number match: {query_numbers.intersection(title_numbers)} in '{chunk.title}'")
            
            # Boost for title word matches (but less than exact phrase)
            for word in query_words:
                if len(word) > 2 and word in title_lower:
                    score += 0.3
            
            # Boost for exact full query match in title
            if query_lower in title_lower:
                score += 3.0
            
            # Boost for content matches
            content_match_count = sum(1 for word in query_words if word in content_lower and len(word) > 2)
            score += content_match_count * 0.1
            
            # Boost recent items (smaller boost)
            try:
                created = datetime.fromisoformat(chunk.created_at.replace('Z', '+00:00'))
                days_old = (datetime.now() - created).days
                if days_old < 7:
                    score *= 1.1
                elif days_old < 30:
                    score *= 1.05
            except:
                pass
            
            scored_chunks.append((chunk, score))
        
        # Sort by score
        scored_chunks.sort(key=lambda x: x[1], reverse=True)
        
        # Log top results for debugging
        logger.info(f"🔍 Top 3 search results for query '{query}':")
        for i, (chunk, score) in enumerate(scored_chunks[:3], 1):
            logger.info(f"  {i}. [{score:.3f}] {chunk.module_name} - {chunk.title}")
        
        # Get top-k
        top_chunks = scored_chunks[:top_k]
        ranked_chunks = [chunk for chunk, _ in top_chunks]
        scores = [score for _, score in top_chunks]
        
        return ranked_chunks, scores
    
    async def get_context_for_agent(self, user_id: int, organization_id: int,
                                   query: str, agent_type: str = "general",
                                   top_k: int = 10,
                                   module_filter: Optional[List[str]] = None) -> str:
        """
        Get formatted context string for AI agent with user data
        This is what gets injected into the agent's prompt
        """
        # Check if query is about specific entities - if so, include ALL of that type
        # BUT: If asking about a SPECIFIC named entity, don't include all - just do semantic search
        query_lower = query.lower()
        
        module_keyword_map = {
            "events": ['event', 'events', 'engagement', 'matter', 'matters', 'case', 'cases'],
            "tasks": ['task', 'tasks', 'checklist', 'to-do', 'todo'],
            "communications": ['message', 'messages', 'conversation', 'conversations', 'channel', 'chat', 'dm', 'direct message'],
            "calendar": ['calendar', 'meeting', 'meetings', 'deadline', 'deadlines', 'schedule']
        }
        module_mentions = {
            module: any(keyword in query_lower for keyword in keywords)
            for module, keywords in module_keyword_map.items()
        }
        
        # Check if asking about a specific named event/task (contains proper nouns or specific names)
        is_specific_event_query = any(word in query_lower for word in ['about', 'tell me about', 'describe', 'what is']) and \
                                   module_mentions["events"]
        
        is_specific_task_query = any(word in query_lower for word in ['about', 'tell me about', 'describe', 'what is']) and \
                                 module_mentions["tasks"]
        
        # Only include ALL if it's a general query, not specific
        include_all_events = module_mentions["events"] and not is_specific_event_query
        include_all_tasks = module_mentions["tasks"] and not is_specific_task_query
        include_all_comms = module_mentions["communications"]
        
        primary_focus_module = module_filter[0] if module_filter else None
        if not primary_focus_module:
            mentioned_modules = [module for module, mentioned in module_mentions.items() if mentioned]
            if len(mentioned_modules) == 1:
                primary_focus_module = mentioned_modules[0]
        
        # Get the user index
        key = (user_id, organization_id)
        if key not in self.user_indices:
            await self._load_from_cache(user_id, organization_id)
        
        if key not in self.user_indices:
            return self._get_empty_context(user_id, organization_id)
        
        index = self.user_indices[key]
        
        # Build comprehensive context based on query intent
        chunks_to_include = []
        
        # If asking about specific module, include ALL chunks from that module
        from dataclasses import replace
        
        if include_all_events:
            event_chunks = [c for c in index.chunks if c.module_name == 'events']
            # Set base relevance score for all events
            event_chunks_with_score = [replace(c, relevance_score=1.0) for c in event_chunks[:15]]
            chunks_to_include.extend(event_chunks_with_score)
            logger.info(f"📋 Including ALL events: {len(event_chunks)} event chunks")
        
        if include_all_comms:
            comm_chunks = [c for c in index.chunks if c.module_name == 'communications']
            comm_chunks_with_score = [replace(c, relevance_score=1.0) for c in comm_chunks[:20]]
            chunks_to_include.extend(comm_chunks_with_score)
            logger.info(f"💬 Including ALL communications: {len(comm_chunks)} comm chunks")
            
            # When including communications, also include related events for context
            # Extract event names from channel names and find corresponding events
            event_chunks = [c for c in index.chunks if c.module_name == 'events']
            for event_chunk in event_chunks[:10]:  # Check up to 10 events
                event_title_lower = event_chunk.title.lower()
                # Check if any communication mentions this event
                for comm_chunk in comm_chunks[:10]:
                    if any(word in comm_chunk.title.lower() for word in event_title_lower.split()):
                        if event_chunk.id not in [c.id for c in chunks_to_include]:
                            chunks_to_include.append(replace(event_chunk, relevance_score=0.9))
                            logger.info(f"📋 Auto-including event '{event_chunk.title}' (related to communications)")
                            break
        
        if include_all_tasks:
            task_chunks = [c for c in index.chunks if c.module_name == 'tasks']
            task_chunks_with_score = [replace(c, relevance_score=1.0) for c in task_chunks[:15]]
            chunks_to_include.extend(task_chunks_with_score)
            logger.info(f"✅ Including ALL tasks: {len(task_chunks)} task chunks")
        
        # Also do semantic search for the most relevant items
        search_module_filter = module_filter
        if not search_module_filter and primary_focus_module:
            search_module_filter = [primary_focus_module]

        search_result = await self.search_user_data(
            user_id, organization_id, query, top_k, search_module_filter
        )
        
        # Merge search results with module-specific chunks (avoid duplicates)
        chunk_ids = {c.id for c in chunks_to_include}
        for chunk, score in zip(search_result.chunks, search_result.relevance_scores):
            if chunk.id not in chunk_ids:
                # Create a copy with updated relevance score
                updated_chunk = replace(chunk, relevance_score=score)
                chunks_to_include.append(updated_chunk)
                chunk_ids.add(chunk.id)
        
        if not chunks_to_include:
            return self._get_empty_context(user_id, organization_id)
        
        # Re-sort by relevance score
        chunks_to_include.sort(key=lambda c: c.relevance_score, reverse=True)
        
        # Dynamically increase limit when user explicitly asked for entire module
        max_chunks = top_k * 2
        if include_all_events:
            max_chunks = max(max_chunks, 60)  # events need broader context
        elif include_all_tasks or include_all_comms:
            max_chunks = max(max_chunks, 40)
        
        final_chunks = chunks_to_include[:max_chunks]

        # If the user clearly asked about one module (e.g., "what events"), keep only that module
        if primary_focus_module:
            focused_chunks = [c for c in final_chunks if c.module_name == primary_focus_module]
            # If this is a general event query, keep ALL events rather than trimming to top-k
            if primary_focus_module == "events" and include_all_events:
                focused_chunks = [c for c in chunks_to_include if c.module_name == "events"][:max_chunks]
            
            if focused_chunks:
                logger.info(f"🎯 Filtering context to module '{primary_focus_module}' ({len(focused_chunks)} chunks)")
                final_chunks = focused_chunks
            else:
                logger.info(f"⚠️ No chunks found for primary module '{primary_focus_module}', keeping combined context")
        
        # Build context string
        context_parts = []
        
        context_parts.append("=" * 80)
        context_parts.append(f"# USER'S ACTUAL DATA CONTEXT - DO NOT HALLUCINATE")
        context_parts.append("=" * 80)
        context_parts.append(f"Current User: {index.user_name} (User ID: {user_id})")
        context_parts.append(f"Organization ID: {organization_id}")
        context_parts.append(f"Query: {query}")
        context_parts.append(f"Total Data Items Available: {len(final_chunks)}")
        context_parts.append("")
        context_parts.append("⚠️ CRITICAL INSTRUCTIONS:")
        context_parts.append(f"1. The CURRENT USER is {index.user_name} - address them by this name, NOT by client names")
        context_parts.append("2. The data below is the user's ACTUAL data from the Notal database")
        context_parts.append("3. You MUST use ONLY this exact data when answering questions")
        context_parts.append("4. DO NOT make up or hallucinate information that is not explicitly shown below")
        context_parts.append("5. If data is missing or 'Not specified', say so - don't invent it")
        context_parts.append("6. When showing event/task details, ONLY show what was explicitly asked for")
        context_parts.append("")
        context_parts.append("📄 DOCUMENT CITATION RULES:")
        context_parts.append("1. When you FIND the requested document in the data below, CITE IT DIRECTLY")
        context_parts.append("2. DO NOT say 'I cannot access' or 'I'm unable to access' if you found the content")
        context_parts.append("3. Quote the exact text from the document when asked to cite or reference it")
        context_parts.append("4. For 'cite my discussion section' requests, extract and present the EXACT text")
        context_parts.append("5. Always specify which document you're citing from (e.g., 'From EECS 170LA Post-Lab 7:')")
        context_parts.append("")
        
        # Add module breakdown
        module_breakdown = defaultdict(int)
        for chunk in final_chunks:
            module_breakdown[chunk.module_name] += 1
        
        context_parts.append("## Data Modules Included:")
        for module, count in module_breakdown.items():
            context_parts.append(f"- {module.title()}: {count} items")
        context_parts.append("")
        
        # Get index for summaries
        if key in self.user_indices:
            context_parts.append("## User Data Summary:")
            for module, summary in index.module_summaries.items():
                context_parts.append(f"### {module.title()}")
                for sum_key, value in summary.items():
                    context_parts.append(f"- {sum_key}: {value}")
            context_parts.append("")
        
        # Add relevant chunks with clear labels
        context_parts.append("## USER'S ACTUAL DATA (Use this exact information - DO NOT hallucinate):")
        context_parts.append("")
        
        # If this is an event query, list ALL event titles first for easy reference
        if include_all_events:
            event_titles = [c.title for c in final_chunks if c.module_name == 'events']
            if event_titles:
                context_parts.append("📋 COMPLETE LIST OF ALL EVENTS (for reference):")
                for title in event_titles:
                    context_parts.append(f"  • {title}")
                context_parts.append("")
                context_parts.append("⚠️ If the user asks about an event not in this list, it does NOT exist.")
                context_parts.append("Tell them it's not in their current data and list what IS available.")
                context_parts.append("")
        
        for i, chunk in enumerate(final_chunks, 1):
            context_parts.append("─" * 80)
            context_parts.append(f"DATA ITEM #{i} - {chunk.module_name.upper()}: {chunk.entity_type}")
            context_parts.append(f"Title: {chunk.title}")
            if chunk.relevance_score > 0:
                context_parts.append(f"Relevance Score: {chunk.relevance_score:.3f}")
            context_parts.append("─" * 80)
            context_parts.append("")
            context_parts.append(chunk.content)
            
            # Add key metadata with clear labels
            if chunk.metadata:
                context_parts.append("")
                context_parts.append("**Metadata (exact values from database):**")
                for meta_key, value in list(chunk.metadata.items())[:8]:  # Show more metadata
                    if value is not None and meta_key not in ['id', 'entityId']:
                        context_parts.append(f"- {meta_key}: {value}")
            context_parts.append("")
        
        context_parts.append("=" * 80)
        context_parts.append("END OF USER'S ACTUAL DATA")
        context_parts.append("=" * 80)
        context_parts.append("")
        context_parts.append("⚠️ REMINDER:")
        context_parts.append("- Use ONLY the information above - do NOT make up or hallucinate data")
        context_parts.append("- If you FOUND the document/content above, CITE IT DIRECTLY and confidently")
        context_parts.append("- Do NOT say 'I cannot access' if the content is in the data above")
        context_parts.append("- Only say 'I don't have that' if the item is truly NOT in the data above")
        context_parts.append("- When asked to cite, quote the EXACT text from the document")
        context_parts.append("")
        
        return "\n".join(context_parts)
    
    def _get_empty_context(self, user_id: int, organization_id: int) -> str:
        """Return context when no user data is available"""
        return f"""{'=' * 80}
# USER DATA CONTEXT
{'=' * 80}
User ID: {user_id} | Organization ID: {organization_id}

⚠️ NO USER DATA FOUND

This could mean:
- The user is new and hasn't created any data yet
- Data hasn't been synced from the backend yet
- The data was synced but this query doesn't match anything

INSTRUCTION TO AI:
If the user is asking about a specific event/task/conversation that you can't find:
1. Tell them you don't have that specific item in their current data
2. List what you DO have available (e.g., "I have access to these events: X, Y, Z")
3. Ask if they meant one of those, or if the item might be in a different organization
4. Suggest they check if the item was deleted or is in a different workspace

DO NOT give generic navigation instructions. Be specific about what's missing.
{'=' * 80}
"""
    
    async def get_user_summary(self, user_id: int, organization_id: int) -> Dict[str, Any]:
        """Get summary of available data for a user"""
        key = (user_id, organization_id)
        
        if key not in self.user_indices:
            await self._load_from_cache(user_id, organization_id)
        
        if key not in self.user_indices:
            return {
                "user_id": user_id,
                "organization_id": organization_id,
                "has_data": False,
                "total_chunks": 0
            }
        
        index = self.user_indices[key]
        
        return {
            "user_id": user_id,
            "organization_id": organization_id,
            "has_data": True,
            "total_chunks": index.total_chunks,
            "module_summaries": index.module_summaries,
            "last_synced": index.last_synced.isoformat() if index.last_synced else None,
            "modules": list(index.module_summaries.keys())
        }
    
    async def _save_to_cache(self, user_id: int, organization_id: int):
        """Persist user data index to disk"""
        try:
            key = (user_id, organization_id)
            if key not in self.user_indices:
                return
            
            index = self.user_indices[key]
            cache_file = self.cache_dir / f"user_{user_id}_org_{organization_id}.json"
            
            # Convert to serializable format (exclude embeddings)
            cache_data = {
                "user_id": index.user_id,
                "organization_id": index.organization_id,
                "user_name": index.user_name,
                "total_chunks": index.total_chunks,
                "last_synced": index.last_synced.isoformat() if index.last_synced else None,
                "module_summaries": index.module_summaries,
                "chunks": [
                    {
                        "id": chunk.id,
                        "module_name": chunk.module_name,
                        "entity_type": chunk.entity_type,
                        "entity_id": chunk.entity_id,
                        "title": chunk.title,
                        "content": chunk.content,
                        "metadata": chunk.metadata,
                        "created_at": chunk.created_at,
                        "modified_at": chunk.modified_at,
                        "relevance_score": chunk.relevance_score
                    }
                    for chunk in index.chunks
                ]
            }
            
            with open(cache_file, 'w', encoding='utf-8') as f:
                json.dump(cache_data, f, indent=2)
            
            logger.debug(f"Saved user data cache to {cache_file}")
        except Exception as e:
            logger.warning(f"Failed to save cache for User {user_id}: {e}")
    
    async def _load_from_cache(self, user_id: int, organization_id: int):
        """Load user data index from disk cache"""
        try:
            cache_file = self.cache_dir / f"user_{user_id}_org_{organization_id}.json"
            
            if not cache_file.exists():
                return
            
            with open(cache_file, 'r', encoding='utf-8') as f:
                cache_data = json.load(f)
            
            # Rebuild chunks
            chunks = [
                UserDataChunk(
                    id=c['id'],
                    module_name=c['module_name'],
                    entity_type=c['entity_type'],
                    entity_id=c['entity_id'],
                    title=c['title'],
                    content=c['content'],
                    metadata=c['metadata'],
                    created_at=c['created_at'],
                    modified_at=c.get('modified_at'),
                    relevance_score=c.get('relevance_score', 0.0)
                )
                for c in cache_data.get('chunks', [])
            ]
            
            # Rebuild embeddings if vector support available
            if self.vector_available and chunks:
                await self._build_embeddings(chunks)
            
            key = (user_id, organization_id)
            self.user_indices[key] = UserDataIndex(
                user_id=cache_data['user_id'],
                organization_id=cache_data['organization_id'],
                user_name=cache_data.get('user_name', 'User'),
                chunks=chunks,
                module_summaries=cache_data.get('module_summaries', {}),
                last_synced=datetime.fromisoformat(cache_data['last_synced']) if cache_data.get('last_synced') else None,
                total_chunks=cache_data.get('total_chunks', len(chunks))
            )
            
            logger.info(f"Loaded {len(chunks)} chunks from cache for User {user_id}")
        except Exception as e:
            logger.warning(f"Failed to load cache for User {user_id}: {e}")

# Global instance
user_data_rag = UserDataRAGSystem()

# Utility functions for easy integration
async def sync_user_data_context(user_id: int, organization_id: int,
                                 module_data: Dict[str, Any], metadata: Dict[str, Any],
                                 user_name: str = "User") -> bool:
    """Sync user data from backend"""
    return await user_data_rag.sync_user_data(user_id, organization_id, module_data, metadata, user_name)

async def get_user_data_context(user_id: int, organization_id: int, query: str,
                                agent_type: str = "general", top_k: int = 10,
                                module_filter: Optional[List[str]] = None) -> str:
    """Get user data context for agent with optional module filtering for performance"""
    return await user_data_rag.get_context_for_agent(
        user_id, organization_id, query, agent_type, top_k, module_filter
    )

async def search_user_data(user_id: int, organization_id: int, query: str,
                          top_k: int = 10, module_filter: Optional[List[str]] = None) -> UserDataSearchResult:
    """Search user data"""
    return await user_data_rag.search_user_data(
        user_id, organization_id, query, top_k, module_filter
    )

async def get_user_data_summary(user_id: int, organization_id: int) -> Dict[str, Any]:
    """Get summary of user data"""
    return await user_data_rag.get_user_summary(user_id, organization_id)

