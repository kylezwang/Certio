"""
Notal Platform Knowledge Base
Comprehensive knowledge base for training LLMs on Notal's core functionalities
"""

from typing import Dict, List, Any
from dataclasses import dataclass
from datetime import datetime

@dataclass
class NotalFeature:
    """Represents a core feature of the Notal platform"""
    name: str
    description: str
    technical_details: str
    user_types: List[str]
    legal_areas: List[str]
    api_endpoints: List[str]
    business_value: str

@dataclass
class NotalWorkflow:
    """Represents a business workflow in Notal"""
    name: str
    description: str
    steps: List[str]
    participants: List[str]
    legal_requirements: List[str]
    ai_agents_involved: List[str]

class NotalKnowledgeBase:
    """Full knowledge base for Notal project understanding"""
    
    def __init__(self):
        self.features = self._initialize_features()
        self.workflows = self._initialize_workflows()
        self.legal_domains = self._initialize_legal_domains()
        self.user_types = self._initialize_user_types()
        self.technical_architecture = self._initialize_technical_architecture()
    
    def _initialize_features(self) -> Dict[str, NotalFeature]:
        """Initialize core features of the Notal platform"""
        return {
            "dashboard": NotalFeature(
                name="Notal Dashboard [FULLY IMPLEMENTED]",
                description="Central hub with AI chat, upcoming deadlines, calendar, quick access to all features, firm summary, daily briefing, and next suggestions",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Responsive layout with left column (deadlines, calendar, recent activity) and right column (AI chat, quick access grid, firm metrics). Features real-time data updates, personalized views for different user roles. Pulls real data from database for deadlines, firm metrics, suggestions.",
                user_types=["Director", "Client", "Business", "Planner", "Coordinator"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Dashboard"],
                business_value="Provides at-a-glance overview of all critical information, reduces time to find information, improves daily workflow efficiency"
            ),
            
            "notal_ai_assistant": NotalFeature(
                name="Notal AI Assistant [FULLY IMPLEMENTED]",
                description="Intelligent AI assistant integrated throughout platform with conversational interface, accessible via chat panel",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Uses 4 specialized AI agents (ChatSummarizer, ClientGoalExtractor, ReplySuggester, ClarityAgent) with intelligent model selection, cost optimization, GPT-4o and GPT-4o-mini. Features context-aware responses, conversation history, real-time insights panel, intelligent thinking indicators, sticky message navigation. All agents operational with RAG system for Notal-specific knowledge.",
                user_types=["Client", "Director", "Business", "Planner", "Coordinator"],
                legal_areas=["All event categories"],
                api_endpoints=["/agents/summarize", "/agents/extract-goals", "/agents/suggest-reply", "/agents/explain-clarity", "/Chat/GenerateAIResponse", "/Chat/SendMessage"],
                business_value="Automates conversation analysis, provides instant event planning guidance, reduces response time, improves client satisfaction, available 24/7"
            ),
            
            "events": NotalFeature(
                name="Events Management System [FULLY IMPLEMENTED]",
                description="Comprehensive event/engagement management with status tracking, tasks, team assignments, and detailed event information pages with tabs for Overview, Details, Tasks, Conversations, and History",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Entity Framework Core with Azure SQL/SQLite, hierarchical status items, team assignments, progress tracking, event carousel navigation, status cards (Active, In Review, Completed), search and filtering, critical status indicators, due dates, team member assignments with profile pictures, full CRUD operations, event details with tabbed interface.",
                user_types=["Director", "Client", "Planner", "Business"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Matter", "/Matter/Create", "/Matter/Edit", "/Matter/Details/{matterId}"],
                business_value="Centralizes event information, improves collaboration, tracks progress efficiently, provides complete event lifecycle management"
            ),
            
            "tasks": NotalFeature(
                name="Tasks Management System [FULLY IMPLEMENTED]",
                description="Comprehensive task management with filtering, assignment, status tracking, subtasks, and integration with events",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Task creation, assignment, due dates, priority levels, status tracking (To Do, In Progress, Completed), filtering by status/assignee/event, subtask management, task dependencies, bulk operations. Full database integration with audit logging.",
                user_types=["Director", "Client", "Planner", "Coordinator", "Business"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Tasks", "/Tasks/Create", "/Tasks/Update"],
                business_value="Improves task tracking, ensures accountability, prevents missed deadlines, enables workload management"
            ),
            
            "calendar": NotalFeature(
                name="Calendar and Events System [FULLY IMPLEMENTED]",
                description="Full-featured calendar with event creation, attendee management, meeting scheduling, and deadline tracking",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Monthly/weekly/daily views, event creation with title/description/location, attendee management with profile icons, time zone support, recurring events, calendar integration, deadline reminders, upcoming deadlines sidebar. Full CRUD operations with database persistence and audit logging.",
                user_types=["Director", "Client", "Planner", "Coordinator", "Business"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Calendar", "/Calendar/CreateEvent", "/Calendar/UpdateEvent"],
                business_value="Prevents scheduling conflicts, improves time management, ensures important dates are tracked, facilitates team coordination"
            ),
            
            "communications": NotalFeature(
                name="Communications and Team Chat [FULLY IMPLEMENTED]",
                description="Real-time team communications with channels, direct messages, event-specific channels, and cross-organization messaging",
                technical_details="**STATUS: FULLY IMPLEMENTED** - SignalR real-time messaging, channel-based communication, direct messaging between users, event channels, active member status, message history, file sharing in channels, @mentions, unread indicators. Full WebSocket implementation with Redis caching for performance.",
                user_types=["Director", "Client", "Planner", "Coordinator", "Business"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Communications", "/Communications/SendMessage", "/api/DirectMessages"],
                business_value="Reduces email overhead, improves team collaboration, enables quick decision-making, keeps communication organized by event"
            ),
            
            "documents": NotalFeature(
                name="Document Management System [IN DEVELOPMENT - UI PLACEHOLDER]",
                description="Document management interface with sample data - full implementation in development",
                technical_details="**STATUS: UI PLACEHOLDER WITH SAMPLE DATA** - Current: Documents page with folders and file list UI, search bar, filter buttons, event folders section. **Planned**: Real document upload/download, version control, folder organization, access permissions, document sharing, file preview, AI document analysis, metadata tracking. Currently shows sample documents and folders for UI demonstration only.",
                user_types=["Director", "Client", "Planner", "Coordinator", "Business"],
                legal_areas=["Vendor Contracts", "Venue Agreements", "Permits and Compliance", "All event categories"],
                api_endpoints=["/Client/{orgId}/Documents (view only)"],
                business_value="[IN DEVELOPMENT] Will ensure document security, enable collaboration, maintain audit trail, support compliance"
            ),
            
            "teams": NotalFeature(
                name="Teams and People Management [FULLY IMPLEMENTED]",
                description="Team member management with role-based permissions, adding people to organizations, and team collaboration",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Multi-tenant architecture, policy-based authorization (OrgMember policy), role management (Director, Planner, Coordinator, Client), team member invitations, permission levels, organization member management. Full integration with user management and permissions system.",
                user_types=["Director", "Business Owner"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Teams", "/Settings/AddPerson"],
                business_value="Enables scalable team management, ensures proper access control, facilitates collaboration, maintains security"
            ),
            
            "firm_settings": NotalFeature(
                name="Firm Settings and Configuration [FULLY IMPLEMENTED]",
                description="Organization-wide settings including firm details, user management, and configuration",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Organization configuration, user management, notification preferences, security settings, firm details. Settings controller with full CRUD operations, policy-based access control for Directors and Administrators.",
                user_types=["Director", "Business Owner", "Administrator"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/Settings", "/Settings/Update"],
                business_value="Centralizes firm management, streamlines administration, ensures consistency across organization"
            ),
            
            "history": NotalFeature(
                name="Activity History and Audit Log [FULLY IMPLEMENTED]",
                description="Comprehensive audit log of all platform activities including event updates, document changes, user actions",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Comprehensive audit logging for all critical actions, timestamped entries, user attribution, filterable history, activity feed, event-specific history. Full database integration with AuditController, searchable and filterable audit logs.",
                user_types=["Director", "Administrator"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/History", "/Audit/GetLogs"],
                business_value="Provides accountability, supports compliance, enables investigation, tracks event progress"
            ),
            
            "account_settings": NotalFeature(
                name="Account Settings and Preferences [FULLY IMPLEMENTED]",
                description="Complete user profile management with account settings, security, notifications, and preferences",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Profile management (name, email, photo), security settings (password, 2FA), notification preferences, personal preferences. Tabbed interface with sections: Account, Security, Notifications, Preferences, and Billing & Plan link. Real user data editing and updates.",
                user_types=["All users"],
                legal_areas=["All event categories"],
                api_endpoints=["/Client/{orgId}/AccountSettings"],
                business_value="Empowers users to customize their experience, enhances security, improves notification management"
            ),
            
            "billing_system": NotalFeature(
                name="Billing and Subscription Management [PLACEHOLDER]",
                description="Billing metrics displayed in dashboard - full billing system in development",
                technical_details="**STATUS: PLACEHOLDER** - Current: Quick access button on dashboard, billing backlog percentage in firm summary card, link in account settings dropdown to 'Billing & Plan'. **Not Implemented**: Full billing system, invoice management, payment processing, subscription management, time tracking integration. Dashboard shows sample billing metrics only.",
                user_types=["Director", "Business Owner", "Administrator"],
                legal_areas=["All event categories"],
                api_endpoints=["No API endpoints yet"],
                business_value="[IN DEVELOPMENT] Will enable invoicing, payment tracking, subscription management, financial reporting"
            ),
            
            "clients_management": NotalFeature(
                name="Clients Management (Event Planning Companies) [FULLY IMPLEMENTED]",
                description="Client organization management for event planning companies to manage multiple client organizations and events",
                technical_details="**STATUS: FULLY IMPLEMENTED** - Client organization listing via global dashboard button/popup, event associations, client contacts, cross-organization communication via Communications, client-specific permissions via OrgMember policy. Full multi-tenant architecture supporting event planning companies managing multiple client organizations.",
                user_types=["Director", "Planner"],
                legal_areas=["All event categories"],
                api_endpoints=["/Global/Clients", "/Client/{orgId}/Matter"],
                business_value="Enables event planning companies to manage multiple clients efficiently, improves client service, centralizes client information"
            )
        }
    
    def _initialize_workflows(self) -> Dict[str, NotalWorkflow]:
        """Initialize key business workflows"""
        return {
            "event_lifecycle": NotalWorkflow(
                name="Complete Event Lifecycle",
                description="End-to-end workflow for managing events from creation to completion",
                steps=[
                    "Event created with client information and details",
                    "Team members assigned with appropriate roles",
                    "Tasks created and distributed among team",
                    "Communications channel established for event",
                    "Documents uploaded and organized",
                    "Calendar events scheduled for deadlines",
                    "Regular status updates and task completion",
                    "Event review and completion",
                    "History logged and archived"
                ],
                participants=["Director", "Planner", "Coordinator", "Client"],
                legal_requirements=["Client confidentiality", "Signed contract", "Vendor agreements", "Audit trail"],
                ai_agents_involved=["Notal AI Assistant", "ChatSummarizer", "ClientGoalExtractor"]
            ),
            
            "client_intake": NotalWorkflow(
                name="Client Intake and Onboarding",
                description="Streamlined client onboarding with AI-assisted goal extraction and event creation",
                steps=[
                    "Client registers or is invited to organization",
                    "Initial consultation via Communications or AI chat",
                    "Notal AI analyzes conversation for client goals and event needs",
                    "System suggests event type and category",
                    "Team assignment based on expertise and availability",
                    "Event created with AI-extracted requirements",
                    "Welcome package and next steps communicated"
                ],
                participants=["Client", "Director", "Planner", "Notal AI"],
                legal_requirements=["Client confidentiality", "Signed contract", "Deposit and payment terms"],
                ai_agents_involved=["ClientGoalExtractor", "ChatSummarizer", "ReplySuggester"]
            ),
            
            "document_collaboration": NotalWorkflow(
                name="Document Collaboration Workflow",
                description="Team collaboration on document creation, review, and approval",
                steps=[
                    "Document uploaded to event folder in Documents",
                    "Document shared with relevant team members via permissions",
                    "Notal AI provides initial analysis and key term identification",
                    "Team members review and annotate using Communications channel",
                    "Revisions made and new versions uploaded",
                    "Client feedback collected via AI chat or direct messaging",
                    "Final approval documented in History",
                    "Document marked as approved in system"
                ],
                participants=["Director", "Planner", "Coordinator", "Client"],
                legal_requirements=["Client confidentiality", "Document retention", "Version control", "Audit trail"],
                ai_agents_involved=["ClarityAgent", "ChatSummarizer", "Notal AI Assistant"]
            ),
            
            "task_management_workflow": NotalWorkflow(
                name="Task Creation and Completion Workflow",
                description="Complete workflow for task lifecycle from creation to completion",
                steps=[
                    "Task created from event or dashboard",
                    "Task assigned to team member with due date and priority",
                    "Assignee receives notification",
                    "Task appears in assignee's task list and calendar",
                    "Subtasks created if needed for complex tasks",
                    "Task status updated (To Do → In Progress → Completed)",
                    "Task completion logged in event history",
                    "Team notified via Communications channel"
                ],
                participants=["Director", "Planner", "Coordinator"],
                legal_requirements=["Task accountability", "Deadline tracking", "Workload documentation"],
                ai_agents_involved=["ChatSummarizer", "Notal AI Assistant"]
            ),
            
            "team_communication_workflow": NotalWorkflow(
                name="Team Communication and Collaboration",
                description="Real-time team communication workflow across events and organizations",
                steps=[
                    "Team members join organization and events",
                    "Communications channels automatically created for events",
                    "Team members send messages in relevant channels",
                    "Direct messages for private conversations",
                    "Files shared in channel threads",
                    "Important decisions documented",
                    "Communications history searchable and archived"
                ],
                participants=["All team members", "Clients"],
                legal_requirements=["Communication confidentiality", "Message retention"],
                ai_agents_involved=["ReplySuggester", "ChatSummarizer"]
            ),
            
            "ai_assisted_guidance": NotalWorkflow(
                name="AI-Assisted Event Planning Guidance",
                description="Client receives instant event planning guidance through Notal AI assistant",
                steps=[
                    "Client opens Notal AI chat from dashboard or panel",
                    "Client asks an event planning question or describes their situation",
                    "Notal AI analyzes query using context from events and conversations",
                    "AI provides clear explanation using ClarityAgent for contract and vendor terms",
                    "AI suggests next steps or relevant event actions",
                    "If needed, AI connects client with the appropriate planner",
                    "Conversation saved in history for future reference"
                ],
                participants=["Client", "Notal AI", "Planner if escalated"],
                legal_requirements=["Accurate information", "Clear AI disclaimers", "Professional responsibility", "Client confidentiality"],
                ai_agents_involved=["ClarityAgent", "ReplySuggester", "ClientGoalExtractor", "ChatSummarizer"]
            )
        }
    
    def _initialize_legal_domains(self) -> Dict[str, Dict[str, Any]]:
        """Initialize event category knowledge"""
        return {
            "weddings": {
                "description": "Wedding planning, ceremony and reception coordination, and bridal party logistics",
                "common_documents": ["Venue Contracts", "Catering Agreements", "Vendor Contracts", "Timeline and Run-of-Show"],
                "key_terms": ["ceremony", "reception", "bridal party", "vendor coordination", "seating chart"],
                "ai_applications": ["Vendor contract analysis", "Timeline generation", "Budget tracking"]
            },
            
            "corporate_events": {
                "description": "Corporate meetings, product launches, conferences, and company celebrations",
                "common_documents": ["Venue Agreements", "Sponsorship Agreements", "AV/Production Contracts", "Attendee Lists"],
                "key_terms": ["agenda", "keynote", "sponsorship", "branding", "attendee registration"],
                "ai_applications": ["Contract review", "Budget analysis", "Vendor comparison"]
            },
            
            "galas_and_fundraisers": {
                "description": "Galas, fundraisers, and non-profit benefit events",
                "common_documents": ["Venue Contracts", "Catering Agreements", "Sponsorship Packages", "Auction Documentation"],
                "key_terms": ["honoree", "silent auction", "sponsorship tiers", "seating arrangement", "program"],
                "ai_applications": ["Budget tracking", "Sponsorship analysis", "Guest list management"]
            },
            
            "conferences_and_conventions": {
                "description": "Multi-day conferences, trade shows, and conventions with sessions and exhibitors",
                "common_documents": ["Exhibitor Agreements", "Venue Contracts", "Speaker Agreements", "Registration Terms"],
                "key_terms": ["exhibitor booth", "session tracks", "speaker lineup", "registration tiers", "badge scanning"],
                "ai_applications": ["Schedule optimization", "Exhibitor contract review", "Attendee analytics"]
            },
            
            "vendor_and_venue_management": {
                "description": "Coordinating vendors, venues, and suppliers across events",
                "common_documents": ["Vendor Contracts", "Venue Rental Agreements", "Certificates of Insurance", "Deposit Receipts"],
                "key_terms": ["deposit", "cancellation policy", "liability", "indemnification", "force majeure"],
                "ai_applications": ["Contract analysis", "Vendor comparison", "Risk identification"]
            },
            
            "compliance_and_permits": {
                "description": "Permits, insurance, and regulatory requirements for hosting events",
                "common_documents": ["Event Permits", "Certificates of Insurance", "Fire/Safety Approvals", "Alcohol Licenses"],
                "key_terms": ["permit", "occupancy limit", "liability insurance", "safety compliance", "licensing"],
                "ai_applications": ["Compliance checklist generation", "Risk assessment", "Permit tracking"]
            }
        }
    
    def _initialize_user_types(self) -> Dict[str, Dict[str, Any]]:
        """Initialize user type definitions"""
        return {
            "client": {
                "description": "Individual or business hosting an event and seeking event planning services",
                "permissions": ["View own events", "Upload documents", "Participate in conversations"],
                "ai_interactions": ["Clarity explanations", "Goal extraction", "Conversation analysis"],
                "typical_needs": ["Event planning guidance", "Vendor and contract understanding", "Progress updates"]
            },
            
            "business": {
                "description": "Business entity hosting corporate events or requiring ongoing event planning services",
                "permissions": ["View business events", "Manage team members", "Access analytics"],
                "ai_interactions": ["Business goal analysis", "Compliance assistance", "Contract review"],
                "typical_needs": ["Compliance and permit management", "Vendor contract negotiation", "Risk assessment"]
            },
            
            "event_planner": {
                "description": "Event planning professionals coordinating events, vendors, and clients",
                "permissions": ["Event document review", "Client representation", "Event strategy"],
                "ai_interactions": ["Document analysis", "Vendor research assistance", "Client communication"],
                "typical_needs": ["Vendor research", "Contract review", "Client consultation"]
            }
        }
    
    def _initialize_technical_architecture(self) -> Dict[str, Any]:
        """Initialize technical architecture information"""
        return {
            "backend": {
                "framework": "ASP.NET Core 9.0 with Entity Framework Core",
                "database": "Azure SQL Database (production) / SQLite (local development)",
                "authentication": "ASP.NET Identity with JWT tokens",
                "authorization": "Policy-based authorization with OrgMember requirements and role-based access control",
                "api_style": "RESTful APIs with SignalR for real-time features (Communications, AI chat)",
                "caching": "Redis for session management and performance optimization"
            },
            
            "ai_service": {
                "framework": "Python FastAPI with async support",
                "models": ["GPT-4o (complex tasks)", "GPT-4o Mini (simple tasks)"],
                "agents": ["ChatSummarizer", "ClientGoalExtractor", "ReplySuggester", "ClarityAgent", "Notal AI Assistant"],
                "optimization": "Intelligent model selection based on task complexity, cost tracking, usage analytics, rate limiting",
                "provider": "Azure OpenAI Service (preferred) with OpenAI API fallback",
                "rag_system": "TF-IDF based RAG system with comprehensive Notal knowledge base"
            },
            
            "frontend": {
                "framework": "ASP.NET Razor Pages with modern JavaScript",
                "real_time": "SignalR for live chat, notifications, and real-time updates",
                "ui_components": "Bootstrap 5 with custom CSS, responsive mobile-first design",
                "interactive_features": "AI chat panel with sticky messages, Communications sidebar, Calendar views, Event carousel, Profile icons, Real-time notifications",
                "styling": "Modern card-based layouts, shadow effects, smooth transitions, global scrollbar styling"
            },
            
            "deployment": {
                "local_development": "Docker Compose with SQL Server container, Python FastAPI service",
                "production": "Azure App Service with Azure SQL Database and Azure OpenAI Service",
                "ai_service": "Azure Container Instances or Azure App Service with Python runtime",
                "monitoring": "Application Insights, comprehensive audit logging, cost analytics, usage tracking",
                "security": "Azure Key Vault for secrets, SSL/TLS encryption, data residency controls"
            },
            
            "data_architecture": {
                "multi_tenancy": "Organization-based isolation with OrgMember policy",
                "entity_relationships": "Organizations → Users, Events → Tasks, Events → Communications, Documents → Events",
                "audit_trail": "Comprehensive audit logging for all critical operations",
                "soft_delete": "Soft delete pattern for data recovery and audit compliance"
            }
        }
    
    def get_project_context(self) -> str:
        """Get full project context for LLM training"""
        context = f"""
# Notal Event Planning Platform - Full Overview

## Project Purpose
Notal is an AI-powered event planning platform that streamlines event management by integrating advanced AI capabilities throughout the entire workflow. The platform combines event management, task tracking, team communications, document management, calendar scheduling, and intelligent AI assistance to provide a full solution for event planning companies and vendors.

## Brand Identity
- **Name**: Notal (formerly Certio)
- **AI Assistant**: Notal AI
- **Positioning**: Modern, AI-first event planning management platform
- **Target Users**: Event planning companies, vendors, businesses, and clients

## Core Architecture
- **Backend**: ASP.NET Core 9.0 with Entity Framework Core
- **AI Service**: Python FastAPI with Azure OpenAI / OpenAI integration
- **Database**: Azure SQL Database (production) / SQLite (local development)
- **Caching**: Redis for performance and session management
- **Frontend**: ASP.NET Razor Pages with modern JavaScript and Bootstrap 5
- **Real-time**: SignalR for live chat, notifications, and updates
- **Authentication**: ASP.NET Identity with JWT tokens
- **Authorization**: Policy-based with OrgMember requirements and role-based access control

## Key Features
"""
        
        for feature_name, feature in self.features.items():
            context += f"""
### {feature.name}
- **Description**: {feature.description}
- **Technical Details**: {feature.technical_details}
- **User Types**: {', '.join(feature.user_types)}
- **Event Categories**: {', '.join(feature.legal_areas)}
- **Business Value**: {feature.business_value}
"""
        
        context += """
## Business Workflows
"""
        
        for workflow_name, workflow in self.workflows.items():
            context += f"""
### {workflow.name}
- **Description**: {workflow.description}
- **Steps**: {' → '.join(workflow.steps)}
- **Participants**: {', '.join(workflow.participants)}
- **AI Agents**: {', '.join(workflow.ai_agents_involved)}
"""
        
        context += """
## Event Category Expertise
"""
        
        for domain, info in self.legal_domains.items():
            context += f"""
### {domain.replace('_', ' ').title}
- **Description**: {info['description']}
- **Common Documents**: {', '.join(info['common_documents'])}
- **Key Terms**: {', '.join(info['key_terms'])}
- **AI Applications**: {', '.join(info['ai_applications'])}
"""
        
        context += """
## User Types and Permissions
"""
        
        for user_type, info in self.user_types.items():
            context += f"""
### {user_type.title}
- **Description**: {info['description']}
- **Permissions**: {', '.join(info['permissions'])}
- **AI Interactions**: {', '.join(info['ai_interactions'])}
- **Typical Needs**: {', '.join(info['typical_needs'])}
"""
        
        return context
    
    def get_feature_specific_context(self, feature_name: str) -> str:
        """Get detailed context for a specific feature"""
        if feature_name not in self.features:
            return f"Feature '{feature_name}' not found in knowledge base"
        
        feature = self.features[feature_name]
        return f"""
# {feature.name} - Detailed Context

## Description
{feature.description}

## Technical Implementation
{feature.technical_details}

## User Types
{', '.join(feature.user_types)}

## Event Categories Covered
{', '.join(feature.legal_areas)}

## API Endpoints
{', '.join(feature.api_endpoints)}

## Business Value
{feature.business_value}
"""
    
    def get_workflow_context(self, workflow_name: str) -> str:
        """Get detailed context for a specific workflow"""
        if workflow_name not in self.workflows:
            return f"Workflow '{workflow_name}' not found in knowledge base"
        
        workflow = self.workflows[workflow_name]
        return f"""
# {workflow.name} - Workflow Details

## Description
{workflow.description}

## Process Steps
{chr(10).join([f"{i+1}. {step}" for i, step in enumerate(workflow.steps)])}

## Participants
{', '.join(workflow.participants)}

## Requirements
{', '.join(workflow.legal_requirements)}

## AI Agents Involved
{', '.join(workflow.ai_agents_involved)}
"""
    
    def get_legal_domain_context(self, domain: str) -> str:
        """Get detailed context for a specific event category"""
        if domain not in self.legal_domains:
            return f"Event category '{domain}' not found in knowledge base"
        
        info = self.legal_domains[domain]
        return f"""
# {domain.replace('_', ' ').title} - Event Category Context

## Description
{info['description']}

## Common Documents
{', '.join(info['common_documents'])}

## Key Terms
{', '.join(info['key_terms'])}

## AI Applications
{', '.join(info['ai_applications'])}
"""
    
    def search_knowledge(self, query: str) -> List[str]:
        """Search the knowledge base for relevant information"""
        query_lower = query.lower()
        results = []
        
        # Prioritize security and privacy searches
        if any(term in query_lower for term in ['security', 'privacy', 'data protection', 'compliance', 'azure', 'encryption', 'gdpr', 'soc', 'infrastructure']):
            # Add security-specific features first
            security_features = ['security_privacy', 'azure_infrastructure']
            for feature_name in security_features:
                if feature_name in self.features:
                    feature = self.features[feature_name]
                    results.append(f"Security Feature: {feature.name} - {feature.technical_details}")
        
        # Prioritize UI layout searches
        if any(term in query_lower for term in ['layout', 'interface', 'button', 'navigation', 'sidebar', 'panel', 'page', 'walk me through']):
            # Add UI-specific features first
            ui_features = ['ui_navigation', 'events_ui', 'ai_assistant_panel']
            for feature_name in ui_features:
                if feature_name in self.features:
                    feature = self.features[feature_name]
                    results.append(f"UI Feature: {feature.name} - {feature.technical_details}")
        
        # Search features
        for feature_name, feature in self.features.items():
            if (query_lower in feature.name.lower() or 
                query_lower in feature.description.lower() or
                query_lower in feature.technical_details.lower()):
                results.append(f"Feature: {feature.name} - {feature.description}")
        
        # Search workflows
        for workflow_name, workflow in self.workflows.items():
            if (query_lower in workflow.name.lower() or 
                query_lower in workflow.description.lower()):
                results.append(f"Workflow: {workflow.name} - {workflow.description}")
        
        # Search event categories
        for domain, info in self.legal_domains.items():
            if (query_lower in domain.lower() or 
                query_lower in info['description'].lower()):
                results.append(f"Event Category: {domain.replace('_', ' ').title()} - {info['description']}")
        
        return results

# Add UI layout and navigation knowledge to existing features
def add_ui_layout_knowledge():
    """Add UI layout knowledge to the global knowledge base"""
    notal_kb.features["navigation_system"] = NotalFeature(
        name="Notal Platform Navigation",
        description="Intuitive left sidebar navigation with Dashboard, Events, Tasks, Calendar, Communications, Documents, Teams, and Firm Settings. Global dashboard sidebar for Calendar and History pages.",
        technical_details="Main navigation in client sidebar: Dashboard (overview with AI chat and quick access), Events (briefcase icon), Tasks (progress bars icon), Calendar (calendar icon, redirects to global dashboard), Communications (comments icon), Documents (file icon), Teams (user group icon), Firm Settings (cog icon), and Logout button at bottom. Global dashboard sidebar appears for Calendar and History pages with upcoming deadlines card, calendar widget, and recent activity. Mobile responsive with hamburger menu and offcanvas navigation.",
        user_types=["Client", "Director", "Business", "Planner", "Coordinator"],
        legal_areas=["All event categories"],
        api_endpoints=["/Client/{orgId}/Dashboard", "/Client/{orgId}/Matter", "/Client/{orgId}/Tasks", "/Client/{orgId}/Calendar", "/Client/{orgId}/Communications", "/Client/{orgId}/Documents", "/Client/{orgId}/Teams", "/Client/{orgId}/Settings"],
        business_value="Streamlined navigation improves user efficiency and reduces time to find features"
    )
    
    notal_kb.features["events_ui"] = NotalFeature(
        name="Events User Interface",
        description="Modern events page with status cards, event carousel, search, filtering, and detailed event view with tabbed interface",
        technical_details="Status overview cards: Active Events, In Review, Completed, Team Members with counts. Blue '+ New Event' button. Search bar, Filter and Date Range buttons. Event cards with: client/event name, status badges (Critical, Review, In Progress), progress indicators (X/Y tasks), due dates, team member profile pictures, card shadow hover effects. Event details page with carousel navigation between events, tabs for Overview, Details, Tasks, Conversations, History. Modern card-based design with 12px border-radius and shadow effects.",
        user_types=["Director", "Client", "Planner", "Business"],
        legal_areas=["All event categories"],
        api_endpoints=["/Client/{orgId}/Matter", "/Matter/Create", "/Matter/Details/{matterId}"],
        business_value="Visual, intuitive interface improves event tracking and team collaboration"
    )
    
    notal_kb.features["dashboard_ui"] = NotalFeature(
        name="Dashboard User Interface",
        description="Central dashboard with left column (deadlines, calendar, recent files) and right column (AI chat, quick access grid 4x2, firm summary, daily briefing, suggestions)",
        technical_details="Left column (28% width, gray background): Upcoming Deadlines card with priority indicators, Calendar widget with month view and today highlight, Recent Activity with file icons. Right column (72% width): 'What can I help with?' AI chat with large search input, Quick Access 4x2 grid (Clients/Teams, Calendar, Billing, History, Events, Tasks, Communications, Documents) with icons and hover effects, Firm Summary with metrics and trends, Daily Briefing with today's items, Next Suggestions with AI-powered recommendations. All cards have border-radius: 12px and shadow: 0 2px 8px rgba(0,0,0,0.25).",
        user_types=["Director", "Client", "Planner", "Coordinator", "Business"],
        legal_areas=["All event categories"],
        api_endpoints=["/Client/{orgId}/Dashboard"],
        business_value="At-a-glance overview of all critical information saves time and improves daily workflow"
    )
    
    notal_kb.features["ai_chat_ui"] = NotalFeature(
        name="AI Chat Panel User Interface",
        description="Floating AI chat panel with conversation tabs, message history, sticky message navigation, and real-time thinking indicators",
        technical_details="Chat panel accessible from top-right button. Features: conversation tabs with pills design, message history with user/AI messages, sticky message overlay for reviewing past messages with navigation arrows, intelligent thinking indicators ('Analyzing event requirements...', 'Processing your request...'), profile icons for users, 'Notal AI' branding for AI messages, timestamp display, send button, attachment support. Panel slides in from right with smooth animation. Messages support HTML formatting with <p>, <strong>, <ul><li> tags.",
        user_types=["All users"],
        legal_areas=["All event categories"],
        api_endpoints=["/Chat/SendMessage", "/Chat/GenerateAIResponse", "/Chat/LoadConversations"],
        business_value="Always-accessible AI assistant improves user productivity and provides instant help"
    )
    
    notal_kb.features["security_privacy"] = NotalFeature(
        name="Security and Data Privacy Infrastructure",
        description="Enterprise-grade security measures using Azure OpenAI and Azure SQL with comprehensive data protection",
        technical_details="Azure OpenAI Service provides enterprise security with data residency controls, encryption at rest and in transit, Azure Active Directory integration, and private endpoints. Azure SQL Database offers advanced threat protection, transparent data encryption, row-level security, dynamic data masking, and Always Encrypted. Notal implements zero-trust architecture with multi-factor authentication, role-based access control (Director, Planner, Coordinator, Client), OrgMember policy authorization, and comprehensive audit logging. All data processing occurs within Azure's secure cloud infrastructure with geographic data residency controls. Azure provides built-in compliance frameworks including SOC 2 Type II, ISO 27001, and GDPR compliance capabilities that Notal leverages. Notal is currently working toward its own SOC 2 Type II certification.",
        user_types=["Client", "Director", "Business", "All users"],
        legal_areas=["All event categories"],
        api_endpoints=["/Audit/GetLogs", "/Security/Settings"],
        business_value="Enterprise-grade security ensures client confidentiality and data protection for sensitive event and vendor information"
    )
    
    notal_kb.features["notal_compliance_status"] = NotalFeature(
        name="Notal Compliance and Certification Status",
        description="Current compliance status and certification efforts for Notal platform",
        technical_details="Notal is currently working toward SOC 2 Type II certification. While Notal leverages Azure's built-in compliance frameworks and certifications (SOC 2, ISO 27001, GDPR), Notal itself is in the process of obtaining independent compliance certifications. The platform implements comprehensive security measures and data protection controls that align with industry standards and regulatory requirements. Organizations using Notal should consult with their compliance teams to ensure specific regulatory requirements are met for their use case. Notal provides comprehensive audit logging, data encryption, access controls, and privacy features to support compliance efforts.",
        user_types=["Director", "Business Owner"],
        legal_areas=["All event categories"],
        api_endpoints=["/Audit/GetLogs", "/Security/Compliance"],
        business_value="Transparent communication about compliance status builds trust with clients and supports procurement decisions"
    )

# Global instance for easy access
notal_kb = NotalKnowledgeBase()
# Keep certio_kb as alias for backwards compatibility
certio_kb = notal_kb

# Add UI layout knowledge
add_ui_layout_knowledge()
