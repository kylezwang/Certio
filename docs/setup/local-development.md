# Local development

You need the .NET 9 SDK, Python 3.11, Docker Desktop, and Git.

## Windows

```
copy .env.example .env
scripts\setup_windows.bat
scripts\start_services.bat
```

The site is at `http://localhost:5092`. The AI service is at `http://localhost:8000`.

`start_services.bat` starts the SQL Server container, a Redis container, the Python service, and `dotnet run` in `Certio.Web`. It reads `.env` from the repo root even if you launch it from `scripts\`.

Stop it with `scripts\stop_services.bat`.

## macOS

```
cp .env.example .env
./scripts/setup_mac.sh
./scripts/start_services.sh
```

## Database only

```
docker compose up -d sqlserver
dotnet ef database update --project Certio.Infrastructure --startup-project Certio.Web
```

The compose file maps SQL Server to port 1433. The `sa` password is `SQL_PASSWORD` in `.env`. The default in `.env.example` matches the compose fallback.

Redis is optional. Leave `USE_REDIS=false` unless you are testing the backplane or the L2 cache.

## OAuth and mail

Gmail, Outlook, Drive, OneDrive, and calendar sync need client ids in `.env`. The app runs without them. Those buttons fail until the values are set. Use `http://localhost:5092` redirect URLs in the provider console, matching `.env.example`.
