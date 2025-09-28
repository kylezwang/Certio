#!/bin/bash
# Setup environment variables for Certio

echo "🔧 Setting up Certio environment variables..."

# Check if .env file exists
if [ -f .env ]; then
    echo "✅ .env file already exists"
    echo "📝 Current environment variables:"
    grep -v '^#' .env | grep -v '^$'
else
    echo "❌ .env file not found. Please create one with your database passwords."
    echo "📋 Required variables:"
    echo "   - SQL_PASSWORD (for local SQL Server)"
    echo "   - DB_PASSWORD (for Azure SQL Database)"
    echo "   - AZURE_SQL_CONNECTION_STRING (full Azure connection string)"
    echo "   - AI_API_KEY (for AI service)"
fi

echo ""
echo "🔒 Security reminder:"
echo "   - Change the default passwords immediately!"
echo "   - Never commit .env files to version control"
echo "   - Use strong, unique passwords for each environment"
echo ""
echo "🚀 To start the application with environment variables:"
echo "   ./start_services.sh"