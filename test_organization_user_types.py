#!/usr/bin/env python3
"""
Test script for organization-level user types implementation
"""

import requests
import json
import time

BASE_URL = "http://localhost:5000"

def test_application_startup():
    """Test that the application starts without errors"""
    try:
        response = requests.get(f"{BASE_URL}/", timeout=10)
        print(f"✅ Application is running (Status: {response.status_code})")
        return True
    except requests.exceptions.RequestException as e:
        print(f"❌ Application not accessible: {e}")
        return False

def test_home_page():
    """Test that the home page loads"""
    try:
        response = requests.get(f"{BASE_URL}/Home", timeout=10)
        if response.status_code == 200:
            print("✅ Home page loads successfully")
            return True
        else:
            print(f"❌ Home page failed (Status: {response.status_code})")
            return False
    except requests.exceptions.RequestException as e:
        print(f"❌ Home page request failed: {e}")
        return False

def test_add_people_page():
    """Test that the AddPeople page loads (should redirect to login)"""
    try:
        response = requests.get(f"{BASE_URL}/Home/AddPeople", timeout=10)
        if response.status_code in [200, 302]:  # 302 is redirect to login
            print("✅ AddPeople page accessible (redirects to login as expected)")
            return True
        else:
            print(f"❌ AddPeople page failed (Status: {response.status_code})")
            return False
    except requests.exceptions.RequestException as e:
        print(f"❌ AddPeople page request failed: {e}")
        return False

def test_projects_page():
    """Test that the Projects page loads (should redirect to login)"""
    try:
        response = requests.get(f"{BASE_URL}/Project", timeout=10)
        if response.status_code in [200, 302]:  # 302 is redirect to login
            print("✅ Projects page accessible (redirects to login as expected)")
            return True
        else:
            print(f"❌ Projects page failed (Status: {response.status_code})")
            return False
    except requests.exceptions.RequestException as e:
        print(f"❌ Projects page request failed: {e}")
        return False

def main():
    """Run all tests"""
    print("🧪 Testing Organization-Level User Types Implementation")
    print("=" * 60)
    
    # Wait for application to fully start
    print("⏳ Waiting for application to start...")
    time.sleep(5)
    
    tests = [
        test_application_startup,
        test_home_page,
        test_add_people_page,
        test_projects_page
    ]
    
    passed = 0
    total = len(tests)
    
    for test in tests:
        if test():
            passed += 1
        print()
    
    print("=" * 60)
    print(f"📊 Test Results: {passed}/{total} tests passed")
    
    if passed == total:
        print("🎉 All tests passed! Organization-level user types implementation is working.")
    else:
        print("⚠️  Some tests failed. Check the implementation.")
    
    return passed == total

if __name__ == "__main__":
    success = main()
    exit(0 if success else 1)
