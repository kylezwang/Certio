# AI service

`ai_agents/main.py` is a FastAPI app. The web app calls it with a shared secret, `AI_API_KEY`. That value is not an OpenAI key. The model keys (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, or Azure OpenAI) stay in the Python process.

## RAG

On import, `main.py` tries three knowledge modules and keeps the first one that imports:

1. `certio_rag_system_enhanced` (onboarding text and intent checks)
2. `certio_rag_system` (numpy / scikit-learn)
3. `certio_rag_system_fallback` (no extra scientific packages)

`user_data_rag_system.py` is separate. The web app posts a snapshot of the current user's org data, and that module answers questions against it. The snapshot cache is created at runtime under `ai_agents/data/` and is not committed.

Short greetings skip retrieval. Other turns can pull product docs, user data, or both, depending on the intent check in `main.py`.

## Agent actions

The model can propose a change (create a task, update an event). The web app stores that proposal as an `AgentAction` and waits. Execution runs only after a user moves it to `Approved`. See `Certio.Domain/AgentActions/AgentAction.cs` and `AgentActionService`.

## Run it

From the repo root, with `.env` filled in:

```
scripts\run_ai_agents.bat
```

The service listens on port 8000. `GET /health` is the probe the deploy workflow uses.
