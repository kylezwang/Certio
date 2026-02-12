# Certio - AI-Powered Legal Management Platform

**Version:** 5.0.0  
**Last Updated:** February 11, 2026  
**Status:** Production

---

## Overview

Certio is an enterprise-grade, AI-powered legal management platform built on ASP.NET Core 9.0 with Clean Architecture principles. It provides comprehensive matter management, task tracking, document handling, billing, real-time communications, and AI-powered workflow automation for law firms and their clients.

### Key Capabilities

- **Matter Management** - Full lifecycle management with assignments, permissions, and status tracking
- **Task Management** - Hierarchical tasks with subtasks, dependencies, comments, and reactions
- **Document Management** - Integration with Google Drive, OneDrive, and local uploads with vector search (RAG)
- **Real-time Communications** - Team channels, direct messaging, and AI-powered chat via SignalR
- **Billing** - Time entries, expenses, invoices, and trust/retainer accounting
- **Calendar** - Integrated calendar with Google Calendar and Outlook sync
- **AI Agents** - Python FastAPI microservice with specialized agents for legal workflows
- **Unified Inbox** - Aggregated inbox across email, direct messages, and chat
- **Change Control** - Change notice management with acknowledgement workflows
- **Multi-Tenancy** - Organization-scoped with law firm-client relationship support

---

## Architecture

```
Certio.Web (Presentation - .NET 9.0)
    Controllers, Views, SignalR Hubs, Middleware
        |
Certio.Application (Business Logic - .NET 8.0)
    Services, Interfaces, DTOs, Configuration
        |
Certio.Domain (Core Models - .NET 8.0)
    Entities, Enums, Permissions, Domain Logic
        |
Certio.Infrastructure (Data Access - .NET 8.0)
    ApplicationDbContext, Migrations, Interceptors
```

**External Services:**
- Python FastAPI AI microservice (`ai_agents/`)
- SQL Server (Azure SQL for production, Docker for local development)
- Redis (optional distributed caching)

---

## Quick Start

### Prerequisites

| Software | Version | Required |
|----------|---------|----------|
| .NET SDK | 9.0+ | Yes |
| Python | 3.11+ | Yes (for AI agents) |
| Docker Desktop | Latest | Yes (for local SQL Server) |
| Git | Latest | Yes |
| Visual Studio 2022 / VS Code | Latest | Recommended |

### Setup

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd Certio
   ```

2. **Run the Windows setup script:**
   ```cmd
   setup_windows.bat
   ```

3. **Configure environment variables** - Edit `.env` in the project root:
   ```env
   SQL_PASSWORD=YourStrong@Passw0rd
   USE_AZURE_SQL=false
   OPENAI_API_KEY=your_key_here
   ANTHROPIC_API_KEY=your_key_here
   ```

4. **Start all services:**
   ```cmd
   start_services.bat
   ```

5. **Access the application** at `http://localhost:5092`

### Manual Setup (Alternative)

```bash
# Start SQL Server via Docker
docker-compose up -d sqlserver

# Restore and build .NET solution
dotnet restore
dotnet build

# Apply database migrations
dotnet ef database update --project Certio.Web

# Set up Python AI agents
cd ai_agents
python -m venv venv
venv\Scripts\activate.bat    # Windows
pip install -r requirements.txt

# Run the web application
cd Certio.Web
dotnet run
```

---

## Project Structure

```
Certio/
├── Certio.sln                    # Visual Studio 2022 solution
├── Certio.Web/                   # ASP.NET Core 9.0 Web Application
│   ├── Controllers/              # 34 controllers (MVC + API)
│   ├── Views/                    # 57 Razor views (.cshtml)
│   ├── Hubs/                     # SignalR hubs (Chat, Direct, Notifications, Updates)
│   ├── Services/                 # Web-layer services
│   ├── Middleware/               # Custom middleware pipeline
│   ├── Security/                 # Authorization handlers and attributes
│   ├── Migrations/               # 42 EF Core migrations
│   ├── wwwroot/                  # Static assets (CSS, JS, images, libraries)
│   └── Program.cs               # Application entry point
├── Certio.Application/           # Application services layer (.NET 8.0)
│   ├── Services/                 # Business logic services
│   ├── Interfaces/               # Service contracts
│   ├── DTOs/                     # Data Transfer Objects
│   └── Configuration/            # Configuration classes
├── Certio.Domain/                # Domain layer (.NET 8.0)
│   ├── Users/                    # User, UserOrganization, TrustedDevice
│   ├── Organizations/            # Organization, OrganizationRelationship
│   ├── Matters/                  # Matter, MatterAssignment, MatterPermission
│   ├── Tasks/                    # TaskItem, SubTaskItem, TaskAssignment
│   ├── Documents/                # Document, DocumentVector, ExternalConnection
│   ├── Services/                 # ChatMessage, Conversation, DirectMessage
│   ├── Billing/                  # TimeEntry, Expense, Invoice, Retainer
│   ├── Calendar/                 # CalendarEvent, CalendarIntegration
│   ├── AIAgents/                 # AIAgent, AIUsage, ChatSummary
│   ├── AgentActions/             # AgentAction
│   ├── UnifiedInbox/             # InboxItem, InboxMessage
│   ├── ChangeControl/            # ChangeNotice, ChangeNoticeRecipient
│   ├── Audit/                    # AuditLog, AuditableEntity, IAuditable
│   ├── Notifications/            # Notification, NotificationTemplate
│   └── Workflows/                # Workflow, WorkflowInstance
├── Certio.Infrastructure/        # Infrastructure layer (.NET 8.0)
│   ├── Data/                     # ApplicationDbContext
│   ├── Migrations/               # (Shared with Web project)
│   └── Interceptors/             # AuditInterceptor
├── Certio.Tests/                 # Unit tests (.NET 9.0, xUnit + Moq)
│   ├── Services/                 # Service tests
│   ├── Controllers/              # Controller tests
│   ├── Hubs/                     # SignalR hub tests
│   └── Security/                 # Authorization tests
├── ai_agents/                    # Python FastAPI AI microservice
│   ├── main.py                   # FastAPI application entry
│   ├── Dockerfile                # Docker build for AI service
│   └── requirements.txt          # Python dependencies
├── .github/workflows/            # GitHub Actions CI/CD
├── scripts/                      # Setup and utility scripts
├── docker-compose.yml            # Docker Compose (SQL Server)
├── setup_windows.bat             # Windows setup script
├── start_services.bat            # Start all services
├── stop_services.bat             # Stop all services
└── .env                          # Environment variables (not committed)
```

---

## Technology Stack

### Backend

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | ASP.NET Core | 9.0 |
| Language | C# | 12 |
| ORM | Entity Framework Core | 9.0.8 / 8.0.8 |
| Database | SQL Server | 2022 |
| Cache | Redis + In-Memory | 2.8.16 |
| Real-time | SignalR | 9.0.9 |
| Authentication | ASP.NET Core Identity | 9.0.8 |
| Email | MailKit | 4.14.1 |
| API Docs | Swagger/OpenAPI | 9.0.4 |
| AI/ML | Semantic Kernel | 1.4.0 |
| Graph API | Microsoft Graph | 5.49.0 |
| Gmail API | Google.Apis.Gmail.v1 | 1.68.0 |

### AI/ML Stack (Python)

| Component | Technology |
|-----------|-----------|
| API Framework | FastAPI |
| LLM Providers | OpenAI, Anthropic |
| Vector Search | LangChain |
| Web Server | Uvicorn |

### Frontend

| Component | Technology |
|-----------|-----------|
| View Engine | Razor (server-side) |
| CSS Framework | Bootstrap 5 |
| JavaScript | jQuery 3.x + Vanilla JS |
| Icons | Font Awesome 6.7.2, Bootstrap Icons 1.11.0 |
| Fonts | Katibeh (headings), Figtree (body) |
| Real-time | SignalR JavaScript client |

### Infrastructure

| Component | Technology |
|-----------|-----------|
| Cloud | Azure (App Service, SQL, AI) |
| CI/CD | GitHub Actions |
| Containers | Docker + Docker Compose |
| Version Control | Git |

---

## Key Features

### Multi-Tenancy & Organizations
- 5 organization types: Client, LawFirm, EventPlanner, Government, NonProfit
- Organization relationships (law firm-client, referral, co-counsel, consultant)
- Join code invitation system
- Per-organization settings and terminology customization

### Security & Authorization
- ASP.NET Core Identity with OAuth (Google, Microsoft)
- Two-factor authentication (email-based)
- 23 fine-grained permissions across 6 categories
- 17 permission sets across 4 user types (Client, LawFirm, External, Certio)
- Organization membership policy enforcement
- IDOR protection on all endpoints
- Comprehensive audit logging via EF Core interceptor

### Real-time Communication
- 4 SignalR hubs: Chat, Direct Messages, Notifications, Updates
- Team channels with matter-scoped conversations
- AI-powered chat with streaming responses
- User presence tracking

### AI Integration
- Python FastAPI microservice with specialized agents
- RAG (Retrieval-Augmented Generation) with document vector search
- AI-generated matters and tasks with approval workflows
- Agent actions system (propose, approve, execute, rollback)
- AI usage tracking and cost analytics

---

## Development

### Running Tests
```bash
dotnet test
```

### Database Migrations
```bash
# Add a new migration
dotnet ef migrations add MigrationName --project Certio.Web

# Apply migrations
dotnet ef database update --project Certio.Web
```

### Ports

| Service | Port |
|---------|------|
| .NET Web App | 5092 |
| AI Agents (Python) | 8000 |
| SQL Server (Docker) | 1433 |
| Redis | 6379 |

---

## Deployment

The project uses GitHub Actions for CI/CD:

- **Main App** (`production_notal-app.yml`): Builds on Windows, deploys to Azure Web App `Notal-app`
- **AI Service** (`production_notal-ai.yml`): Builds on Ubuntu (Python 3.11), deploys to Azure Web App `notal-ai`

Both workflows trigger on push to the `production` branch or manual dispatch.

---

## Documentation

Comprehensive documentation is available in the following files:

| Document | Description |
|----------|-------------|
| `CERTIO_TECHNICAL_ARCHITECTURE_DOCUMENT.md` | Full technical architecture reference |
| `WINDOWS_SETUP_GUIDE.md` | Windows development environment setup |
| `SMART_DATABASE_GUIDE.md` | Database configuration (local vs Azure) |
| `REDIS_SETUP.md` | Redis caching setup guide |
| `TESTING_QUICK_START.md` | Testing quick start guide |
| `PHASE_3_PERMISSION_SYSTEM_GUIDE.md` | Permission system reference |
| `WOPI_IMPLEMENTATION_GUIDE.md` | WOPI/Office Online integration |
| `ai_agents/LLM_TRAINING_GUIDE.md` | AI agent training guide |
| `ai_agents/QUICK_START.md` | AI agents quick start |

---

## Codebase Statistics

| Metric | Count |
|--------|-------|
| Total Lines of Code (excl. migrations, libs) | ~159,000 |
| C# Code Lines (excl. migrations) | ~58,500 |
| Domain Entities | 62+ |
| Controllers | 34 |
| Razor Views | 57 |
| Application Services | 50+ |
| EF Core Migrations | 42 |
| Unit Test Files | 17 |
| SignalR Hubs | 4 |
| API Endpoint Groups | 15+ |
| Documentation Files | 190+ (.md) |
