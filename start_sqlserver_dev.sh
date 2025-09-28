#!/bin/bash

# Start SQL Server and Certio Web Application

echo "🚀 Starting Certio with SQL Server LocalDB..."

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    print_warning "Docker is not running. Starting Docker Desktop..."
    open -a Docker
    print_status "Please wait for Docker to start, then run this script again."
    exit 1
fi

# Start SQL Server
print_status "Starting SQL Server container..."
docker-compose up -d sqlserver

# Wait for SQL Server to be ready
print_status "Waiting for SQL Server to be ready..."
sleep 10

# Check if SQL Server is running
if docker ps | grep -q certio-sqlserver; then
    print_success "SQL Server is running!"
else
    print_warning "SQL Server failed to start. Check Docker logs."
    exit 1
fi

# Set environment and run the application
print_status "Starting Certio Web Application..."
export ASPNETCORE_ENVIRONMENT=Development

# Run database migrations
print_status "Running database migrations..."
export PATH="$PATH:/Users/chloetang/.dotnet/tools"
dotnet ef database update --project Certio.Web

# Start the application
print_status "Starting web application..."
dotnet run --project Certio.Web

print_success "Certio is running with SQL Server LocalDB!"
print_status "Web application: http://localhost:5000"
print_status "SQL Server: localhost:1433"
print_status "Database: CertioLocal"
print_status "Username: sa"
print_status "Password: ${SQL_PASSWORD}"
