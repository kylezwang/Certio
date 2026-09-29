# Notal

Notal is a multi-tenant workspace for event teams and the firms that work with them. It covers events, tasks, documents, billing, mail, calendar, and a chat assistant that can propose changes but cannot apply them until a person approves.

The code uses the codename `Certio`. Project names, namespaces, and the Python modules were not renamed. Azure resources are still `Notal-app` and `notal-ai`.

[![PR validation](https://github.com/kylezwang/Certio/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/kylezwang/Certio/actions/workflows/pr-validation.yml)

## What it does

- Organizations of several types (client, law firm, event planner, government, nonprofit) with relationships between them
- Events (the `Matter` type), tasks, comments, and a calendar with Google and Outlook sync
- Documents from upload, Google Drive, and OneDrive, including Office Online editing through WOPI
- Team channels, direct messages, and an inbox over synced mail
- Billing records: time, expenses, invoices, and trust
- An AI service that retrieves product docs and the current user's data, then files a proposed action for approval

## Architecture

```
Browser
  |
  v
Certio.Web (.NET 9)          controllers, Razor, SignalR
  |
  v
Certio.Application           services, DTOs, ServiceResult
  |
  v
Certio.Domain                entities and permissions
  |
  v
Certio.Infrastructure        EF Core, migrations, audit interceptor
  |
  +--> SQL Server
  +--> Redis, when USE_REDIS is set

Certio.Web -- HTTP --> ai_agents (FastAPI, Python 3.11)
```

A longer map is in [docs/architecture/overview.md](docs/architecture/overview.md). Short notes on the main choices are in [docs/decisions](docs/decisions/001-clean-architecture.md).

## Worth reading

| Topic | Where |
|-------|--------|
| Permission checks cached in memory, then Redis | `Certio.Web/Services/CachedPermissionService.cs`, `RedisCacheService.cs` |
| Audit rows written in the same save as the change | `Certio.Infrastructure/Interceptors/AuditInterceptor.cs` |
| SignalR backplane only when Redis is on | `Certio.Web/Program.cs` |
| Model output cannot write rows until someone approves | `Certio.Domain/AgentActions/AgentAction.cs` |
| Retrieval falls back if a heavier module will not import | `ai_agents/main.py` |
| Org membership is a policy, finer rights are a permission enum | `docs/architecture/multi-tenancy-and-permissions.md` |

## Stack

| Area | Choice |
|------|--------|
| Web | ASP.NET Core 9, Razor, Bootstrap 5, SignalR |
| Libraries | .NET 8 class libraries |
| Data | EF Core, SQL Server 2022 |
| Cache | In-memory, plus Redis when configured |
| Auth | ASP.NET Core Identity, Google and Microsoft sign-in |
| AI | FastAPI, OpenAI or Azure OpenAI or Anthropic |
| CI | GitHub Actions, deploy to Azure App Service |

## Run it

Install the .NET 9 SDK, Python 3.11, and Docker Desktop.

```
git clone https://github.com/kylezwang/Certio.git
cd Certio
copy .env.example .env
scripts\setup_windows.bat
scripts\start_services.bat
```

On macOS, use `scripts/setup_mac.sh` and `scripts/start_services.sh`.

| Service | URL |
|---------|-----|
| Web | http://localhost:5092 |
| AI | http://localhost:8000 |
| SQL Server | localhost:1433 |

Details, including what each environment variable does, are in [docs/setup/local-development.md](docs/setup/local-development.md).

## Tests

```
dotnet test
```

Python tests need a placeholder key so the app module can import:

```
set OPENAI_API_KEY=sk-test-placeholder
set AI_API_KEY=test-secret
python -m pytest ai_agents/tests -q
```

## Size

Counted from source, excluding `bin`, `obj`, `wwwroot/lib`, migrations, and virtualenvs:

| | Lines | Files |
|--|------:|------:|
| C# | 66,081 | 265 |
| Razor | 61,985 | 65 |
| JavaScript | 17,655 | 16 |
| CSS | 14,023 | 10 |
| Python | 11,247 | 15 |

34 controllers, 4 SignalR hubs, 11 test classes. The view and script files are larger than they should be. That is called out in [docs/known-limitations.md](docs/known-limitations.md).

## License

Source is public so it can be read. It is not licensed for reuse. See [LICENSE](LICENSE).
