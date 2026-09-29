#!/bin/bash
cd "$(dirname "$0")/.." || exit 1

echo "Stopping SQL Server..."

# Stop SQL Server container
docker-compose down

echo " SQL Server stopped!"
echo "To start again, run: ./scripts/start_sqlserver_dev.sh"
