# Configuration

Secrets go in `.env`, which is gitignored. `.env.example` lists the keys with empty values. The web host also reads the usual ASP.NET environment variables and `appsettings` files. Those JSON files are gitignored on purpose. Do not add one that contains a real password.

## Required to boot

| Variable | Purpose |
|----------|---------|
| `SQL_PASSWORD` | Local SQL Server `sa` password |
| `USE_AZURE_SQL` | `true` in Azure, `false` on a laptop |
| `AI_API_KEY` | Shared secret between the web app and `ai_agents` |

## Model providers

Set at least one of `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, or the `AZURE_OPENAI_*` pair. The Python service reads them. The web app does not need the model key.

## Optional integrations

Mail, Drive, OneDrive, calendar, maps, document intelligence, and SMTP each have keys in `.env.example`. Empty values disable that integration.

`USE_REDIS=true` plus a Redis connection string turns on the distributed cache and the SignalR backplane. Without it, both stay in memory.

## Production

Azure App Service settings hold the same keys. Nothing in this repo is the production secret store. The deploy workflows in `.github/workflows` only build and push. They do not inline keys.
