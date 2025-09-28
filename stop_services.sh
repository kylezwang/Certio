#!/bin/bash
echo "Stopping Certio services..."

# Stop .NET web application started by start_services.sh
pkill -f "dotnet run --project Certio.Web" || true

# Stop AI agents (Python/uvicorn) started by start_services.sh
pkill -f "ai_agents/main.py" || true
pkill -f "uvicorn.*main:app" || true

# Stop Redis (started with --daemonize yes)
if command -v redis-cli >/dev/null 2>&1; then
  redis-cli shutdown || true
fi

# Optionally stop SQL Server container (uncomment if you want containers down too)
# docker compose down

# Extra safety: kill any listeners on common ports
for port in 5092 5000 8000; do
  pid=$(lsof -nP -iTCP:${port} -sTCP:LISTEN -t 2>/dev/null | tr '\n' ' ')
  if [ -n "$pid" ]; then
    echo "Killing processes on port ${port}: $pid"
    kill -9 $pid 2>/dev/null || true
  fi
done

echo "All services stopped."


