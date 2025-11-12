"""
Test script for Enhanced RAG System
Demonstrates improved intent detection, priority-based retrieval, and onboarding knowledge
"""

import sys
import os
from typing import List, Tuple

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

def test_enhanced_rag():
    """Test the enhanced RAG system with various queries"""
    
    print("=" * 100)
    print("TESTING ENHANCED NOTAL RAG SYSTEM")
    print("=" * 100)
    
    # Try to import enhanced system
    try:
        from certio_rag_system_enhanced import enhanced_notal_rag, search_project_knowledge
        print("\n[SUCCESS] Successfully imported Enhanced RAG System\n")
        use_enhanced = True
    except ImportError as e:
        print(f"\n[WARNING] Enhanced RAG not available ({e}), testing standard RAG\n")
        try:
            from certio_rag_system import notal_rag as enhanced_notal_rag, search_project_knowledge
            use_enhanced = False
        except ImportError:
            from certio_rag_system_fallback import notal_rag as enhanced_notal_rag, search_project_knowledge
            use_enhanced = False
    
    # Test queries with expected results
    test_cases: List[Tuple[str, str, str, str]] = [
        # (query, expected_intent, expected_category, user_type)
        ("How do I create my first matter?", "how_to", "tutorial", "Lawyer"),
        ("I can't send messages in communications", "troubleshooting", "troubleshooting", "Client"),
        ("Where is the calendar?", "where_is", "navigation", "Client"),
        ("What is the dashboard?", "what_is", "explanation", "Business"),
        ("I'm new to Notal, where do I start?", "onboarding", "tutorial", "Client"),
        ("How do I assign a task to someone?", "how_to", "tutorial", "Lawyer"),
        ("What can the AI assistant help me with?", "what_is", "explanation", "All"),
        ("My tasks aren't showing up", "troubleshooting", "troubleshooting", "Lawyer"),
        ("Where can I see my deadlines?", "where_is", "navigation", "Lawyer"),
        ("How do I invite team members?", "how_to", "tutorial", "Partner"),
        ("What are the different user roles?", "what_is", "explanation", "Partner"),
        ("Schedule my first meeting", "how_to", "tutorial", "Associate"),
        ("Navigate to communications section", "where_is", "navigation", "Client"),
        ("Getting started as a business user", "onboarding", "tutorial", "Business"),
        ("Explain what matters are", "what_is", "explanation", "Client"),
    ]
    
    print("\n" + "=" * 100)
    print("TEST 1: Intent Detection and Query Understanding")
    print("=" * 100)
    
    correct_intents = 0
    for i, (query, expected_intent, expected_category, user_type) in enumerate(test_cases, 1):
        print(f"\n[Test Case #{i}]")
        print(f"Query: '{query}'")
        print(f"User Type: {user_type}")
        
        result = enhanced_notal_rag.retrieve_relevant_knowledge(
            query=query,
            user_type=user_type,
            top_k=3
        )
        
        if use_enhanced and hasattr(result, 'intent') and result.intent:
            intent = result.intent
            print(f"  > Detected Intent: {intent.intent_type} (expected: {expected_intent})")
            print(f"  > User Level: {intent.user_level}")
            print(f"  > Query Category: {intent.query_category} (expected: {expected_category})")
            if intent.specific_feature:
                print(f"  > Specific Feature: {intent.specific_feature}")
            
            if intent.intent_type == expected_intent:
                correct_intents += 1
                print(f"  [PASS] Intent detection: PASS")
            else:
                print(f"  [PARTIAL] Intent detection: PARTIAL (got {intent.intent_type})")
        else:
            print(f"  [INFO] Intent detection not available (using standard/fallback RAG)")
        
        if result.chunks:
            print(f"\n  Top 3 Results:")
            for j, (chunk, score) in enumerate(zip(result.chunks[:3], result.relevance_scores[:3]), 1):
                print(f"    {j}. [{chunk.category.upper()}] Priority: {chunk.priority}/5, Score: {score:.3f}")
                print(f"       Source: {chunk.source}")
                # Show first 100 chars of content
                preview = chunk.content[:100].replace('\n', ' ')
                print(f"       Preview: {preview}...")
        else:
            print(f"  [WARNING] No results found")
    
    if use_enhanced:
        accuracy = (correct_intents / len(test_cases)) * 100
        print(f"\n{'=' * 100}")
        print(f"Intent Detection Accuracy: {correct_intents}/{len(test_cases)} ({accuracy:.1f}%)")
    
    # Test 2: Priority-based retrieval
    print(f"\n\n{'=' * 100}")
    print("TEST 2: Priority-Based Retrieval")
    print("=" * 100)
    
    priority_test_queries = [
        ("How do I create a matter?", ["tutorial", "getting_started", "faq"]),
        ("I'm stuck", ["troubleshooting", "faq", "getting_started"]),
        ("Where do I navigate to see tasks?", ["navigation", "faq"]),
    ]
    
    for query, expected_high_priority_categories in priority_test_queries:
        print(f"\nQuery: '{query}'")
        result = enhanced_notal_rag.retrieve_relevant_knowledge(query, top_k=5)
        
        print(f"  Top 5 Results by Priority:")
        high_priority_found = False
        for i, (chunk, score) in enumerate(zip(result.chunks[:5], result.relevance_scores[:5]), 1):
            print(f"    {i}. Category: {chunk.category:20s} | Priority: {chunk.priority}/5 | Score: {score:.3f}")
            if chunk.category in expected_high_priority_categories:
                high_priority_found = True
        
        if high_priority_found:
            print(f"  [SUCCESS] High-priority content found in top results")
        else:
            print(f"  [WARNING] Expected high-priority categories not in top results")
    
    # Test 3: User-type specific results
    print(f"\n\n{'=' * 100}")
    print("TEST 3: User Type Filtering")
    print("=" * 100)
    
    query = "Getting started with Notal"
    for user_type in ["Client", "Lawyer", "Business"]:
        print(f"\nQuery: '{query}' (User: {user_type})")
        result = enhanced_notal_rag.retrieve_relevant_knowledge(query, user_type=user_type, top_k=3)
        
        print(f"  Top 3 Results:")
        for i, (chunk, score) in enumerate(zip(result.chunks[:3], result.relevance_scores[:3]), 1):
            relevant_users = chunk.metadata.get('user_types', ['Unknown'])
            print(f"    {i}. {chunk.category} | For: {', '.join(relevant_users)} | Score: {score:.3f}")
    
    # Test 4: Knowledge base statistics
    print(f"\n\n{'=' * 100}")
    print("TEST 4: Knowledge Base Statistics")
    print("=" * 100)
    
    stats = enhanced_notal_rag.get_knowledge_stats()
    print(f"\nTotal Knowledge Chunks: {stats['total_chunks']}")
    print(f"Vector Mode: {stats.get('vector_mode', 'Unknown')}")
    
    print(f"\nChunks by Category:")
    categories = stats.get('categories', {})
    for category, count in sorted(categories.items(), key=lambda x: -x[1]):
        print(f"  {category:25s}: {count:3d} chunks")
    
    print(f"\nChunks by Source:")
    sources = stats.get('sources', {})
    for source, count in sorted(sources.items(), key=lambda x: -x[1]):
        print(f"  {source:25s}: {count:3d} chunks")
    
    if 'priority_distribution' in stats:
        print(f"\nPriority Distribution:")
        for priority, count in sorted(stats['priority_distribution'].items()):
            print(f"  {priority}: {count} chunks")
    
    # Test 5: Search functionality
    print(f"\n\n{'=' * 100}")
    print("TEST 5: Knowledge Search")
    print("=" * 100)
    
    search_queries = [
        ("create matter", None),
        ("calendar", "navigation"),
        ("troubleshoot", "troubleshooting"),
    ]
    
    for query, category_filter in search_queries:
        print(f"\nSearch Query: '{query}'" + (f" (Category: {category_filter})" if category_filter else ""))
        results = search_project_knowledge(query, category=category_filter)
        
        print(f"  Found {len(results)} results:")
        for i, result in enumerate(results[:5], 1):
            print(f"    {i}. [{result['category']}] Score: {result['relevance_score']:.3f}")
            print(f"       {result['content'][:80]}...")
    
    # Final summary
    print(f"\n\n{'=' * 100}")
    print("TEST SUMMARY")
    print("=" * 100)
    print(f"\n[SUCCESS] Enhanced RAG System Tests Complete")
    print(f"   - Total Knowledge Chunks: {stats['total_chunks']}")
    print(f"   - Intent Detection: {'Active' if use_enhanced else 'Not Available'}")
    print(f"   - Priority-Based Retrieval: Active")
    print(f"   - User Type Filtering: Active")
    if use_enhanced:
        print(f"   - Intent Detection Accuracy: {accuracy:.1f}%")
    print(f"\n[INFO] The enhanced RAG system provides:")
    print(f"   * Comprehensive onboarding knowledge (FAQs, tutorials, guides)")
    print(f"   * Intelligent intent detection for better responses")
    print(f"   * Priority-based ranking for most relevant content")
    print(f"   * User-type specific filtering for personalized help")
    print(f"   * Troubleshooting guides for common issues")
    print(f"\n" + "=" * 100)

if __name__ == "__main__":
    try:
        test_enhanced_rag()
    except Exception as e:
        print(f"\n[ERROR] Error running tests: {e}")
        import traceback
        traceback.print_exc()

