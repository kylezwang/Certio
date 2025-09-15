# Certio Database Schema Plan

## Overview
This document outlines the comprehensive database schema design for Certio, a legal services platform with AI integration. The schema supports all major features including project management, document handling, team collaboration, AI agents, and workflow automation.

## Core Architecture

### Multi-Tenant Design
- All entities include `TenantId` for multi-tenant isolation
- Automatic tenant assignment in `ApplicationDbContext.SaveChanges()`
- Indexed on `TenantId` for performance

### Key Design Principles
1. **Normalized Structure**: Proper foreign key relationships
2. **Audit Trail**: Comprehensive audit logging
3. **Flexibility**: JSON fields for extensible data
4. **Performance**: Strategic indexing
5. **AI Integration**: Dedicated AI agent tracking

## Entity Categories

### 1. Core User Management
- **User**: Central user entity with tenant isolation
- **Team**: Team/organization management
- **TeamMembership**: Many-to-many user-team relationships

### 2. Project Management
- **Project**: Main project entity with enhanced fields
- **ProjectAssignment**: User assignments to projects
- **StatusItem**: Individual project tasks/status items
- **StatusItemDependency**: Task dependency management
- **StatusItemAssignment**: Task assignments
- **StatusItemComment**: Task comments and notes

### 3. Document Management
- **Document**: Enhanced document entity with versioning
- **DocumentVersion**: Document version history
- **DocumentReview**: Review workflow tracking
- **DocumentComment**: Document comments and discussions
- **DocumentSignature**: E-signature tracking

### 4. Service Management
- **ServiceRequest**: Client service requests
- **ServiceRequestMessage**: Service request communications
- **ServiceRequestAttachment**: File attachments

### 5. Communication & Chat
- **Conversation**: Chat conversations
- **ChatMessage**: Individual chat messages
- **ConversationParticipant**: Conversation membership

### 6. AI Agent System
- **AIAgent**: AI agent definitions
- **AIAgentExecution**: Agent execution tracking
- **AIAgentTemplate**: Agent prompt templates
- **AIAgentResult**: Agent output results
- **ChatSummary**: AI-generated chat summaries
- **ClientGoal**: AI-extracted client goals
- **ReplySuggestion**: AI-generated reply suggestions
- **ClarityExplanation**: Legal language explanations

### 7. Workflow Automation
- **Workflow**: Workflow definitions
- **WorkflowStep**: Individual workflow steps
- **WorkflowStepDependency**: Step dependencies
- **WorkflowInstance**: Workflow execution instances
- **WorkflowStepInstance**: Individual step executions

### 8. Notifications & Audit
- **Notification**: User notifications
- **NotificationTemplate**: Notification templates
- **AuditLog**: Comprehensive audit trail

## Key Features Supported

### 1. Projects Page
- **Status Items**: Hierarchical task management with dependencies
- **Progress Tracking**: Automatic progress calculation
- **Team Assignment**: Multi-user project assignments
- **AI Integration**: AI-generated project goals and status updates

### 2. Documents Page
- **Version Control**: Complete document versioning
- **Review Workflow**: Multi-stage review process
- **E-Signatures**: Digital signature tracking
- **AI Document Analysis**: AI-powered document insights
- **Template System**: Document template management

### 3. Services Page
- **Centralized Chat**: Unified communication hub
- **AI Intake Analysis**: Automated request analysis
- **Goal Extraction**: AI-powered client goal identification
- **Reply Suggestions**: AI-assisted response generation
- **Legal Clarity**: AI language simplification

### 4. Teams Page
- **Multi-Team Support**: Client, Legal, External teams
- **Role Management**: Granular permission system
- **Team Collaboration**: Cross-team project assignments

### 5. AI Agent Integration
- **Chat Summarizer**: Automatic conversation summaries
- **Client Goal Extractor**: Business goal identification
- **Reply Suggester**: Response recommendations
- **Clarity Agent**: Legal language explanation
- **Doc Filler**: Template auto-completion
- **Review Coordinator**: Document review routing
- **Doc Validator**: Document consistency checking
- **Clause Highlighter**: Status information extraction
- **Doc Change Tracker**: Version history management
- **Doc Extractor**: Database information extraction

## Database Relationships

### Core Relationships
```
User 1:N TeamMembership N:1 Team
User 1:N ProjectAssignment N:1 Project
Project 1:N StatusItem
StatusItem 1:N StatusItemDependency
Document 1:N DocumentVersion
Conversation 1:N ChatMessage
AIAgent 1:N AIAgentExecution
```

### AI Agent Integration
- All AI agents can be linked to Projects, Documents, Conversations, or ServiceRequests
- Comprehensive execution tracking with input/output data
- Review and approval workflow for AI-generated content

## Migration Strategy

### Phase 1: Core Entities
1. Create User, Team, TeamMembership entities
2. Update Project entity with new relationships
3. Create StatusItem and related entities
4. Update Document entity with versioning

### Phase 2: Service & Communication
1. Create ServiceRequest and related entities
2. Update Conversation and ChatMessage entities
3. Create ConversationParticipant entity

### Phase 3: AI Agent System
1. Create AIAgent and execution tracking entities
2. Create AI result entities (ChatSummary, ClientGoal, etc.)
3. Update existing entities with AI agent relationships

### Phase 4: Workflow & Automation
1. Create Workflow and WorkflowStep entities
2. Create WorkflowInstance and execution tracking
3. Integrate with existing project and document workflows

### Phase 5: Notifications & Audit
1. Create Notification and NotificationTemplate entities
2. Create AuditLog entity
3. Implement audit logging in all entities

## Performance Considerations

### Indexing Strategy
- Composite indexes on `(TenantId, CreatedAt)` for time-based queries
- Indexes on foreign keys for relationship queries
- Indexes on frequently queried fields (Status, Priority, etc.)

### Query Optimization
- Lazy loading for navigation properties
- Eager loading for frequently accessed related data
- Pagination for large result sets

## Security Considerations

### Data Isolation
- Tenant-based data isolation
- Row-level security through TenantId filtering
- Audit trail for all data modifications

### Access Control
- Role-based access through TeamMembership
- Project-level permissions through ProjectAssignment
- Document-level permissions through Document visibility

## Implementation Timeline

### Week 1-2: Core Schema
- Implement User, Team, Project entities
- Create initial migration
- Update ApplicationDbContext

### Week 3-4: Document & Service Management
- Implement Document versioning system
- Create ServiceRequest workflow
- Update chat system

### Week 5-6: AI Agent Integration
- Implement AI agent tracking
- Create AI result entities
- Integrate with existing workflows

### Week 7-8: Workflow Automation
- Implement workflow system
- Create automation rules
- Integrate with AI agents

### Week 9-10: Notifications & Polish
- Implement notification system
- Add audit logging
- Performance optimization

## Next Steps

1. **Create Migration**: Generate Entity Framework migration for new schema
2. **Update Services**: Modify existing services to use new entities
3. **Update Controllers**: Update API endpoints for new data structure
4. **Update Views**: Modify UI to display new entity relationships
5. **Testing**: Comprehensive testing of all new functionality
6. **Documentation**: Update API documentation and user guides

This schema provides a solid foundation for Certio's current features while being extensible for future requirements. The AI agent integration is particularly robust, supporting all the planned AI features from your roadmap.
