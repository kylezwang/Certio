#!/usr/bin/env python3
"""
Certio AI Training System - Installation Script
Installs dependencies and sets up the training system
"""

import subprocess
import sys
import os

def install_package(package):
    """Install a package using pip"""
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", package])
        print(f"✅ Successfully installed {package}")
        return True
    except subprocess.CalledProcessError:
        print(f"❌ Failed to install {package}")
        return False

def main():
    print("🚀 Certio AI Training System - Installation")
    print("=" * 50)
    
    # Essential packages for basic functionality
    essential_packages = [
        "fastapi==0.104.1",
        "uvicorn==0.24.0",
        "pydantic==2.5.0",
        "python-dotenv==1.0.0",
        "openai==1.3.7",
        "python-multipart==0.0.6",
        "aiofiles==23.2.1"
    ]
    
    # Optional packages for enhanced functionality (Python 3.12 compatible)
    optional_packages = [
        "setuptools>=70.0.0",
        "numpy>=2.0.0",
        "scikit-learn>=1.7.0",
        "pandas>=2.1.0",
        "matplotlib>=3.8.0"
    ]
    
    print("\n📦 Installing essential packages...")
    essential_success = True
    for package in essential_packages:
        if not install_package(package):
            essential_success = False
    
    if not essential_success:
        print("\n❌ Some essential packages failed to install. Please check your Python environment.")
        return False
    
    print("\n📦 Installing optional packages (for enhanced RAG functionality)...")
    optional_success = True
    for package in optional_packages:
        if not install_package(package):
            optional_success = False
            print(f"⚠️  {package} failed to install - system will use fallback mode")
    
    print("\n🎯 Installation Summary:")
    print("=" * 30)
    
    if essential_success:
        print("✅ Essential packages: INSTALLED")
        print("✅ Basic AI training system: READY")
    else:
        print("❌ Essential packages: FAILED")
        print("❌ Basic AI training system: NOT READY")
        return False
    
    if optional_success:
        print("✅ Optional packages: INSTALLED")
        print("✅ Enhanced RAG system: READY")
    else:
        print("⚠️  Optional packages: PARTIAL")
        print("✅ Enhanced RAG system: FALLBACK MODE")
    
    print("\n🚀 Next Steps:")
    print("1. Set your OpenAI API key in .env file")
    print("2. Run: python main.py")
    print("3. Test the system with: curl http://localhost:8000/health")
    
    print("\n📚 Documentation:")
    print("- Training Guide: LLM_TRAINING_GUIDE.md")
    print("- Implementation Summary: TRAINING_IMPLEMENTATION_SUMMARY.md")
    
    return True

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
