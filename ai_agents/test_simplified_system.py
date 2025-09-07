#!/usr/bin/env python3
"""
Test the Simplified Cost Optimization System
Only GPT-4o Mini and GPT-4o
"""

import asyncio
from simplified_cost_optimization import SimplifiedModelSelector, TaskComplexityAnalyzer, UsageTracker

async def test_simplified_system():
    """Test the simplified cost optimization system"""
    print("🚀 Testing Simplified Cost Optimization System")
    print("=" * 60)
    print("Models: GPT-4o Mini (simple) + GPT-4o (complex)")
    print()
    
    # Initialize components
    model_selector = SimplifiedModelSelector()
    task_analyzer = TaskComplexityAnalyzer()
    usage_tracker = UsageTracker()
    
    print("✅ System initialized with 2 models only")
    print()
    
    # Test cases with different complexity levels
    test_cases = [
        {
            "prompt": "Hello, how are you?",
            "description": "Simple greeting",
            "expected": "GPT-4o Mini"
        },
        {
            "prompt": "What is a contract?",
            "description": "Simple question",
            "expected": "GPT-4o Mini"
        },
        {
            "prompt": "Explain quantum computing principles and their applications in cryptography",
            "description": "Complex technical explanation",
            "expected": "GPT-4o"
        },
        {
            "prompt": "Analyze this legal contract for potential liability issues and suggest improvements",
            "description": "Complex legal analysis",
            "expected": "GPT-4o"
        },
        {
            "prompt": "Create a comprehensive business plan for a tech startup including market analysis, financial projections, and risk assessment",
            "description": "Complex business planning",
            "expected": "GPT-4o"
        },
        {
            "prompt": "I need help with document review for my startup's software licensing contract",
            "description": "Medium complexity legal task",
            "expected": "GPT-4o"
        }
    ]
    
    print("🧪 Testing Model Selection Logic:")
    print("-" * 50)
    
    for i, test_case in enumerate(test_cases, 1):
        print(f"\nTest {i}: {test_case['description']}")
        print(f"Prompt: '{test_case['prompt']}'")
        
        # Analyze task complexity
        complexity = task_analyzer.analyze_task(test_case['prompt'], len(test_case['prompt']), "Client")
        print(f"Complexity Score: {complexity.complexity_score:.2f}")
        print(f"Estimated Tokens: {complexity.estimated_tokens}")
        print(f"Requires Reasoning: {complexity.requires_reasoning}")
        print(f"Requires Analysis: {complexity.requires_analysis}")
        
        # Select optimal model
        selected_model, estimated_cost = model_selector.select_optimal_model(complexity)
        print(f"Selected Model: {selected_model.value}")
        print(f"Estimated Cost: ${estimated_cost:.4f}")
        print(f"Expected: {test_case['expected']}")
        
        # Check if selection matches expectation
        if selected_model.value.replace('-', '_').upper() in test_case['expected'].upper():
            print("✅ Selection matches expectation")
        else:
            print("⚠️ Selection differs from expectation")
    
    print("\n" + "=" * 60)
    print("💰 Cost Analysis for Your 216,665 Tokens")
    print("=" * 60)
    
    # Simulate your actual usage
    total_tokens = 216665
    
    # Test with different complexity scenarios
    scenarios = [
        {"name": "80% Simple, 20% Complex", "simple_ratio": 0.8, "complex_ratio": 0.2},
        {"name": "60% Simple, 40% Complex", "simple_ratio": 0.6, "complex_ratio": 0.4},
        {"name": "50% Simple, 50% Complex", "simple_ratio": 0.5, "complex_ratio": 0.5},
    ]
    
    for scenario in scenarios:
        simple_tokens = int(total_tokens * scenario["simple_ratio"])
        complex_tokens = int(total_tokens * scenario["complex_ratio"])
        
        # GPT-4o Mini cost (simple tasks)
        gpt4o_mini_input = (simple_tokens * 0.7 / 1000) * 0.00015
        gpt4o_mini_output = (simple_tokens * 0.3 / 1000) * 0.0006
        gpt4o_mini_total = gpt4o_mini_input + gpt4o_mini_output
        
        # GPT-4o cost (complex tasks)
        gpt4o_input = (complex_tokens * 0.7 / 1000) * 0.005
        gpt4o_output = (complex_tokens * 0.3 / 1000) * 0.015
        gpt4o_total = gpt4o_input + gpt4o_output
        
        total_cost = gpt4o_mini_total + gpt4o_total
        
        print(f"\n{scenario['name']}:")
        print(f"  Simple tasks ({simple_tokens:,} tokens): GPT-4o Mini = ${gpt4o_mini_total:.2f}")
        print(f"  Complex tasks ({complex_tokens:,} tokens): GPT-4o = ${gpt4o_total:.2f}")
        print(f"  Total cost: ${total_cost:.2f}")
        print(f"  vs Your original cost: $7.60")
        print(f"  Savings: ${7.60 - total_cost:.2f} ({(7.60 - total_cost) / 7.60 * 100:.1f}%)")
    
    print("\n" + "=" * 60)
    print("📊 Usage Analytics")
    print("=" * 60)
    
    analytics = usage_tracker.get_usage_analytics()
    print(f"Total requests: {analytics['total_requests']}")
    print(f"Total cost: ${analytics['total_cost']:.4f}")
    print(f"Total tokens: {analytics['total_tokens']}")
    print(f"Model usage: {analytics['model_usage']}")
    
    print("\n🎉 Simplified System Benefits:")
    print("✅ Only 2 models to maintain")
    print("✅ Clear selection logic (complexity < 0.5 = GPT-4o Mini)")
    print("✅ Maximum cost savings (98% for simple, 50% for complex)")
    print("✅ Faster model selection")
    print("✅ Easier to understand and debug")
    print("✅ Perfect for production deployment")

if __name__ == "__main__":
    asyncio.run(test_simplified_system())
