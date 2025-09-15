#!/bin/bash

echo "🛑 Stopping SQL Server..."

# Stop SQL Server container
docker-compose down

echo "✅ SQL Server stopped!"
echo "💡 To start again, run: ./start_sqlserver_dev.sh"
