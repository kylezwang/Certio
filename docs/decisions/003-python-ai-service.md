# A separate Python service for models

Model calls, retrieval, and provider SDKs live in `ai_agents`, not in the .NET process.

The .NET side stays on the libraries it already uses for the product. The Python side can take OpenAI, Anthropic, and the numerical stack without pulling that into the web deploy. The two processes talk over HTTP, with `AI_API_KEY` as a shared secret.

The cost is a second deploy (`production_notal-ai.yml`), a health check, and the failure mode where the site is up but chat returns the fallback "AI services temporarily unavailable" HTML from `ChatService`.
