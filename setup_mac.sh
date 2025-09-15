#!/bin/bash

# Certio Project Setup Script for macOS
# This script sets up the development environment for the Certio project

echo "🚀 Setting up Certio development environment on macOS..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Homebrew is installed
if ! command -v brew &> /dev/null; then
    print_error "Homebrew is not installed. Please install Homebrew first:"
    echo "  /bin/bash -c \"\$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\""
    exit 1
fi

print_success "Homebrew is installed"

# Set up .NET environment
print_status "Setting up .NET environment..."

# Add .NET to PATH
export PATH="/usr/local/share/dotnet:$HOME/.dotnet:$PATH"

# Check .NET versions
print_status "Checking .NET installations..."
if command -v dotnet &> /dev/null; then
    echo "Installed .NET SDKs:"
    dotnet --list-sdks
else
    print_error ".NET is not properly installed"
    exit 1
fi

# Check Python installation
print_status "Checking Python installation..."
if command -v python3.11 &> /dev/null; then
    PYTHON_VERSION=$(python3.11 --version)
    print_success "Python installed: $PYTHON_VERSION"
else
    print_error "Python 3.11 is not installed"
    exit 1
fi

# Check SQLite
print_status "Checking SQLite installation..."
if command -v sqlite3 &> /dev/null; then
    SQLITE_VERSION=$(sqlite3 --version)
    print_success "SQLite installed: $SQLITE_VERSION"
else
    print_error "SQLite is not installed"
    exit 1
fi

# Check Redis
print_status "Checking Redis installation..."
if command -v redis-server &> /dev/null; then
    REDIS_VERSION=$(redis-server --version)
    print_success "Redis installed: $REDIS_VERSION"
else
    print_error "Redis is not installed"
    exit 1
fi

# Set up Python virtual environment
print_status "Setting up Python virtual environment..."
cd ai_agents

if [ ! -d "venv" ]; then
    print_status "Creating Python virtual environment..."
    python3.11 -m venv venv
fi

print_status "Activating virtual environment and installing dependencies..."
source venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt

print_success "Python dependencies installed"

# Test .NET project build
print_status "Testing .NET project build..."
cd ..
export PATH="/usr/local/share/dotnet:$HOME/.dotnet:$PATH"

if dotnet build; then
    print_success ".NET project builds successfully"
else
    print_error ".NET project build failed"
    exit 1
fi

# Create environment setup script
print_status "Creating environment setup script..."
cat > setup_env.sh << 'EOF'
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
echo "  python ai_agents/main.py           # Run the AI agents"
echo "  redis-server                        # Start Redis server"
EOF

chmod +x setup_env.sh

# Create startup script
print_status "Creating startup script..."
cat > start_services.sh << 'EOF'
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
EOF

chmod +x start_services.sh

print_success "Setup complete! 🎉"
echo ""
echo "Next steps:"
echo "1. Run './setup_env.sh' to set up your environment"
echo "2. Run './start_services.sh' to start all services"
echo "3. Or run individual services:"
echo "   - dotnet run --project Certio.Web"
echo "   - cd ai_agents && source venv/bin/activate && python main.py"
echo "   - redis-server"
echo ""
echo "Environment variables needed:"
echo "- Add your API keys to ai_agents/.env file"
echo "- Configure database connection strings in appsettings.json"
