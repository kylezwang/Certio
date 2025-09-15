#!/bin/bash
# Environment setup for Certio development

# Add .NET to PATH
export PATH="/usr/local/share/dotnet:$HOME/.dotnet:$PATH"

# Activate Python virtual environment
cd ai_agents
source venv/bin/activate
cd ..

echo "Environment ready for Certio development!"
echo "Available commands:"
echo "  dotnet run --project Certio.Web    # Run the web application"
echo "  cd ai_agents && source venv/bin/activate && python main.py  # Run the AI agents"
echo "  redis-server                        # Start Redis server"
echo ""
echo "Note: Always activate the virtual environment before running Python scripts:"
echo "  cd ai_agents"
echo "  source venv/bin/activate"
echo "  python main.py"
