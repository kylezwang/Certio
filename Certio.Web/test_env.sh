#!/bin/bash
echo "Testing dotnet run behavior..."

echo "1. Testing WITHOUT environment variables:"
unset SQL_PASSWORD
unset AZURE_SQL_CONNECTION_STRING
timeout 5s dotnet run --no-build 2>&1 | head -10 || echo "Command failed or timed out"

echo ""
echo "2. Testing WITH environment variables:"
export SQL_PASSWORD="test123"
export AZURE_SQL_CONNECTION_STRING="test-connection"
timeout 5s dotnet run --no-build 2>&1 | head -10 || echo "Command failed or timed out"
