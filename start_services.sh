#!/bin/bash
# Start all Certio services

echo "Starting Certio services..."

# Start Redis in background
echo "Starting Redis..."
redis-server --daemonize yes

# Start .NET web application
echo "Starting .NET web application..."
export PATH="/usr/local/share/dotnet:$HOME/.dotnet:$PATH"
dotnet run --project Certio.Web &

# Start Python AI agents
echo "Starting AI agents..."
cd ai_agents
source venv/bin/activate
python main.py &

echo "All services started!"
echo "Web application: http://localhost:5000"
echo "AI agents: Running in background"
echo ""
echo "To stop services, press Ctrl+C"
wait
