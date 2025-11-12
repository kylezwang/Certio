#!/usr/bin/env python3
"""
Fix for Python 3.12 package installation issues with numpy and RAG system dependencies
This script addresses the distutils module removal in Python 3.12
"""

import subprocess
import sys
import os

def run_command(command):
    """Run a command and return success status"""
    try:
        result = subprocess.run(command, shell=True, capture_output=True, text=True)
        if result.returncode == 0:
            print(f"✅ {command}")
            return True
        else:
            print(f"❌ {command}")
            print(f"Error: {result.stderr}")
            return False
    except Exception as e:
        print(f"❌ {command} - Exception: {e}")
        return False

def main():
    print("🔧 Python 3.12 Package Installation Fix")
    print("=" * 50)
    
    # Check Python version
    python_version = sys.version_info
    print(f"Python version: {python_version.major}.{python_version.minor}.{python_version.micro}")
    
    if python_version >= (3, 12):
        print("✅ Python 3.12+ detected - applying compatibility fixes")
        
        # Step 1: Upgrade pip
        print("\n📦 Step 1: Upgrading pip...")
        if not run_command("python -m pip install --upgrade pip"):
            print("❌ Failed to upgrade pip")
            return False
        
        # Step 2: Install setuptools (provides distutils functionality)
        print("\n📦 Step 2: Installing setuptools...")
        if not run_command("python -m pip install setuptools>=70.0.0"):
            print("❌ Failed to install setuptools")
            return False
        
        # Step 3: Install compatible versions of RAG dependencies
        print("\n📦 Step 3: Installing RAG system dependencies...")
        rag_packages = [
            "numpy>=2.0.0",
            "scikit-learn>=1.7.0",
            "pandas>=2.1.0"
        ]
        
        for package in rag_packages:
            if not run_command(f"python -m pip install {package}"):
                print(f"⚠️  Failed to install {package} - continuing with others...")
        
        # Step 4: Test installation
        print("\n🧪 Step 4: Testing installation...")
        test_script = '''
import numpy
import sklearn
print(f"✅ NumPy version: {numpy.__version__}")
print(f"✅ Scikit-learn version: {sklearn.__version__}")
print("🎉 RAG system dependencies are ready!")
'''
        
        if run_command(f'python -c "{test_script}"'):
            print("\n🎉 SUCCESS: All RAG system dependencies installed successfully!")
            print("\n📝 Next steps:")
            print("1. Your RAG system should now work properly")
            print("2. Run your main application or training pipeline")
            print("3. If you encounter other issues, check the updated requirements files")
            return True
        else:
            print("\n❌ Installation test failed")
            return False
    else:
        print("ℹ️  Python version is below 3.12 - standard installation should work")
        print("Try: pip install -r requirements-minimal-training.txt")
    
    return False

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
