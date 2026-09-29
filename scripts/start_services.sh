#!/bin/bash
cd "$(dirname "$0")/.." || exit 1
# Start the Notal web app, AI service, and Redis.

echo "Starting Certio services..."

# Load environment variables
if [ -f .env ]; then
    echo "Loading environment variables from .env file..."
    export $(cat .env | grep -v '^#' | xargs)
else
    echo "Warning: .env file not found. Make sure environment variables are set."
fi

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
