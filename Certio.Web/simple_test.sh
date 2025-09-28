#!/bin/bash
echo "Testing dotnet run behavior..."

echo "1. Testing WITHOUT environment variables:"
unset SQL_PASSWORD
unset AZURE_SQL_CONNECTION_STRING
echo "Environment variables cleared"

echo ""
echo "2. Testing WITH environment variables:"
export SQL_PASSWORD="test123"
export AZURE_SQL_CONNECTION_STRING="test-connection"
echo "Environment variables set"
