import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

print("Environment variables loaded:")
print(f"AZURE_OPENAI_ENDPOINT: {os.getenv('AZURE_OPENAI_ENDPOINT')}")
print(f"AZURE_OPENAI_API_KEY: {os.getenv('AZURE_OPENAI_API_KEY')[:10]}..." if os.getenv('AZURE_OPENAI_API_KEY') else "Not set")

# Test Azure connection
try:
    from openai import AzureOpenAI

    client = AzureOpenAI(
        azure_endpoint=os.getenv('AZURE_OPENAI_ENDPOINT'),
        api_key=os.getenv('AZURE_OPENAI_API_KEY'),
        api_version='2024-02-15-preview'
    )

    response = client.chat.completions.create(
        model='gpt-4o-mini-deployment',
        messages=[{'role': 'user', 'content': 'Test Certio AI system'}],
        max_tokens=100
    )
    print('✅ Azure OpenAI working!')
    print(f'Response: {response.choices[0].message.content}')
except Exception as e:
    print(f'❌ Error: {e}')