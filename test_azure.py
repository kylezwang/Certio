import os
from dotenv import load_dotenv
from openai import AzureOpenAI

# Load environment variables
load_dotenv()

print("=== Certio Azure OpenAI Test ===")
print(f"AZURE_OPENAI_ENDPOINT: {os.getenv('AZURE_OPENAI_ENDPOINT')}")
print(f"AZURE_OPENAI_API_KEY: {os.getenv('AZURE_OPENAI_API_KEY')[:10]}..." if os.getenv('AZURE_OPENAI_API_KEY') else "Not set")
print(f"AZURE_GPT4O_MINI_DEPLOYMENT: {os.getenv('AZURE_GPT4O_MINI_DEPLOYMENT')}")

# Test Azure OpenAI connection
try:
    client = AzureOpenAI(
        azure_endpoint=os.getenv('AZURE_OPENAI_ENDPOINT'),
        api_key=os.getenv('AZURE_OPENAI_API_KEY'),
        api_version='2024-02-15-preview'
    )

    # Test with the correct deployment name
    response = client.chat.completions.create(
        model='gpt-4o-mini',  # Using the working deployment name
        messages=[{'role': 'user', 'content': 'Hello! This is a test of the Certio AI system. Please respond with a brief greeting.'}],
        max_tokens=100
    )
    
    print("\n✅ SUCCESS! Azure OpenAI is working!")
    print(f"Response: {response.choices[0].message.content}")
    print("\n🎉 Your Certio AI system is ready to use Azure OpenAI!")
    
except Exception as e:
    print(f"\n❌ Error: {e}")
    print("Please check your Azure OpenAI configuration.")
