# Local SQL by default, Azure SQL when asked

Developers should not need an Azure login to boot the site. `USE_AZURE_SQL` defaults to off, and `docker-compose.yml` starts SQL Server 2022 with the password in `SQL_PASSWORD`.

Production sets `USE_AZURE_SQL=true` and a connection string in the App Service settings, not in the repo. If the Azure check fails during startup, the host logs that and falls back to the local connection. That fallback is convenient on a laptop and dangerous if a production slot loses its setting, so production configuration should set the flag explicitly and the health check should fail when the database is wrong. `/healthz` is what the deploy workflow waits on.
