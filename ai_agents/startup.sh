#!/bin/bash
# Azure App Service startup script for Python AI agents

# Install dependencies if requirements file exists
if [ -f "requirements-minimal.txt" ]; then
    echo "Installing Python dependencies..."
    pip install --no-cache-dir -r requirements-minimal.txt
fi

# Use Azure's PORT environment variable if available, otherwise default to 8000
PORT=${PORT:-8000}

# Start the FastAPI application
echo "Starting Notal AI service on port $PORT..."
python -m uvicorn main:app --host 0.0.0.0 --port $PORT
