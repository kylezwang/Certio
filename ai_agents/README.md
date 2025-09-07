# Certio AI Agents

Python-based AI agent service for the Certio legal platform. This service provides intelligent chat processing, goal extraction, reply suggestions, and legal language clarity.

## Features

- **ChatSummarizer**: Summarizes conversations and extracts key insights
- **ClientGoalExtractor**: Identifies business and legal goals from client conversations
- **ReplySuggester**: Suggests professional replies for SeedJura team members
- **ClarityAgent**: Explains legal language in simple terms for clients

## Setup

1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Set up environment variables:
```bash
cp env.example .env
# Edit .env with your OpenAI API key
```

3. Run the service:
```bash
python main.py
```

The service will be available at `http://localhost:8000`

## API Endpoints

- `GET /` - Service status
- `GET /health` - Health check
- `POST /agents/summarize` - Summarize conversation
- `POST /agents/extract-goals` - Extract client goals
- `POST /agents/suggest-reply` - Suggest reply
- `POST /agents/explain-clarity` - Explain legal language
- `POST /agents/process-all` - Process all agents for a conversation

## Integration with ASP.NET Core

The ASP.NET Core application communicates with this Python service via HTTP API calls. The `AIAgentService` in the .NET application handles the integration.
