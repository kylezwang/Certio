#!/usr/bin/env python3
"""Debug complexity analysis for simple prompts"""

from simplified_cost_optimization import TaskComplexityAnalyzer

def test_complexity(prompt, context_length=0, user_type="Client"):
    analyzer = TaskComplexityAnalyzer()
    result = analyzer.analyze_task(prompt, context_length, user_type)
    
    print(f"Prompt: '{prompt}'")
    print(f"Context length: {context_length}")
    print(f"User type: {user_type}")
    print(f"Complexity score: {result.complexity_score}")
    print(f"Estimated tokens: {result.estimated_tokens}")
    print(f"Requires reasoning: {result.requires_reasoning}")
    print(f"Requires creativity: {result.requires_creativity}")
    print(f"Requires analysis: {result.requires_analysis}")
    print(f"Urgency: {result.urgency}")
    print("-" * 50)

if __name__ == "__main__":
    # Test simple prompts
    test_complexity("Hi, how are you?")
    test_complexity("Hello")
    test_complexity("Thanks")
    test_complexity("What is a contract?")
    test_complexity("Can you help me understand this legal document?")
    test_complexity("I need to analyze the terms and conditions of this agreement for potential liability issues and compliance requirements.")
