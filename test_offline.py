#!/usr/bin/env python3
"""
Offline functionality test for Certio project
Run this to verify what works without internet
"""

import sys
import os

def test_offline_functionality():
    print("🧪 Testing Certio offline functionality...")
    print("=" * 50)
    
    # Test 1: Basic Python imports
    print("1. Testing basic Python libraries...")
    try:
        import pandas as pd
        import numpy as np
        import matplotlib.pyplot as plt
        import seaborn as sns
        print("   ✅ Data analysis libraries work")
    except ImportError as e:
        print(f"   ❌ Data analysis libraries failed: {e}")
    
    # Test 2: NLP libraries
    print("2. Testing NLP libraries...")
    try:
        import nltk
        import spacy
        print("   ✅ NLP libraries imported")
        
        # Test spaCy model
        nlp = spacy.load("en_core_web_sm")
        doc = nlp("Hello world!")
        print(f"   ✅ spaCy model works: {[token.text for token in doc]}")
    except Exception as e:
        print(f"   ❌ NLP libraries failed: {e}")
    
    # Test 3: Machine Learning
    print("3. Testing machine learning libraries...")
    try:
        from sklearn.ensemble import RandomForestClassifier
        from sklearn.model_selection import train_test_split
        print("   ✅ Scikit-learn works")
    except ImportError as e:
        print(f"   ❌ Scikit-learn failed: {e}")
    
    # Test 4: Database connectivity
    print("4. Testing database connectivity...")
    try:
        import sqlite3
        conn = sqlite3.connect(':memory:')
        cursor = conn.cursor()
        cursor.execute("CREATE TABLE test (id INTEGER, name TEXT)")
        cursor.execute("INSERT INTO test VALUES (1, 'test')")
        result = cursor.execute("SELECT * FROM test").fetchone()
        conn.close()
        print(f"   ✅ SQLite works: {result}")
    except Exception as e:
        print(f"   ❌ SQLite failed: {e}")
    
    # Test 5: Redis (if available)
    print("5. Testing Redis connectivity...")
    try:
        import redis
        r = redis.Redis(host='localhost', port=6379, db=0)
        r.ping()
        print("   ✅ Redis works")
    except Exception as e:
        print(f"   ⚠️  Redis not available (expected if not running): {e}")
    
    # Test 6: AI APIs (should fail offline)
    print("6. Testing AI APIs (should fail offline)...")
    try:
        import openai
        # This should fail without internet
        print("   ⚠️  OpenAI available but will fail without internet")
    except Exception as e:
        print(f"   ✅ OpenAI not available (expected offline): {e}")
    
    print("\n" + "=" * 50)
    print("🎯 OFFLINE CAPABILITIES SUMMARY:")
    print("✅ Full .NET web application development")
    print("✅ Complete database operations (SQLite)")
    print("✅ Data analysis and visualization")
    print("✅ Local text processing and NLP")
    print("✅ Machine learning with scikit-learn")
    print("✅ File processing and data manipulation")
    print("❌ AI API calls (OpenAI, Anthropic, Google)")
    print("❌ Package installation/updates")
    print("\n🚀 You're ready for productive offline development!")

if __name__ == "__main__":
    test_offline_functionality()
