# Certio Database Schema Diagram

## Entity Relationship Overview

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                CORE ENTITIES                                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│  User                    Team                  TeamMembership                   │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ FirstName           ├─ Name               ├─ UserId (FK)                  │
│  ├─ LastName            ├─ Description        ├─ TeamId (FK)                  │
│  ├─ Email               ├─ TeamType           ├─ Role                         │
│  ├─ UserType            ├─ Color              ├─ Status                       │
│  ├─ Company             └─ Icon               └─ JoinedAt                     │
│  ├─ JobTitle            │                     │                               │
│  ├─ Department          │                     │                               │
│  ├─ Location            │                     │                               │
│  ├─ Avatar              │                     │                               │
│  ├─ Color               │                     │                               │
│  └─ IsActive            │                     │                               │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              PROJECT MANAGEMENT                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Project                 StatusItem            StatusItemDependency            │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Title               ├─ ProjectId (FK)     ├─ StatusItemId (FK)            │
│  ├─ Description         ├─ Title              ├─ DependsOnStatusItemId (FK)   │
│  ├─ Status              ├─ Description        ├─ DependencyType               │
│  ├─ Priority            ├─ Status             └─ CreatedAt                    │
│  ├─ Category            ├─ Priority           │                               │
│  ├─ ProjectType         ├─ Category           │                               │
│  ├─ TeamId (FK)         ├─ ParentStatusItemId │                               │
│  ├─ ClientId (FK)       ├─ Order              │                               │
│  ├─ StartDate           ├─ DueDate            │                               │
│  ├─ DueDate             ├─ CompletedDate      │                               │
│  ├─ ClientGoals         └─ CreatedAt          │                               │
│  ├─ LegalRequirements   │                     │                               │
│  └─ Notes               │                     │                               │
│                        │                     │                               │
│  ProjectAssignment      StatusItemAssignment  StatusItemComment               │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ ProjectId (FK)      ├─ StatusItemId (FK)  ├─ StatusItemId (FK)            │
│  ├─ UserId (FK)         ├─ UserId (FK)        ├─ UserId (FK)                  │
│  ├─ Role                ├─ AssignmentType     ├─ Content                      │
│  ├─ AssignedAt          ├─ AssignedAt         ├─ CreatedAt                    │
│  └─ RemovedAt           └─ CompletedAt        └─ LastModifiedDate             │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              DOCUMENT MANAGEMENT                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Document               DocumentVersion        DocumentReview                   │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Name                ├─ DocumentId (FK)    ├─ DocumentId (FK)              │
│  ├─ Description         ├─ VersionNumber      ├─ ReviewerId (FK)              │
│  ├─ Type                ├─ ChangeDescription  ├─ Status                       │
│  ├─ FileSize            ├─ FilePath           ├─ Comments                     │
│  ├─ FilePath            ├─ CreatedById (FK)   ├─ ReviewedAt                   │
│  ├─ FileExtension       └─ CreatedAt          └─ CreatedAt                    │
│  ├─ MimeType            │                     │                               │
│  ├─ ProjectId (FK)      │                     │                               │
│  ├─ StatusItemId (FK)   │                     │                               │
│  ├─ CreatedById (FK)    │                     │                               │
│  ├─ Status              │                     │                               │
│  ├─ Visibility          │                     │                               │
│  ├─ DocumentType        │                     │                               │
│  ├─ Tags                │                     │                               │
│  ├─ Icon                │                     │                               │
│  ├─ IsTemplate          │                     │                               │
│  ├─ RequiresSignature   │                     │                               │
│  ├─ IsSigned            │                     │                               │
│  ├─ SignedDate          │                     │                               │
│  └─ ReviewDueDate       │                     │                               │
│                        │                     │                               │
│  DocumentComment        DocumentSignature                                      │
│  ├─ Id (PK)             ├─ Id (PK)                                            │
│  ├─ TenantId            ├─ TenantId                                           │
│  ├─ DocumentId (FK)     ├─ DocumentId (FK)                                    │
│  ├─ UserId (FK)         ├─ SignerId (FK)                                      │
│  ├─ Content             ├─ SignatureData                                      │
│  ├─ ParentCommentId     ├─ IPAddress                                          │
│  ├─ CreatedAt           ├─ UserAgent                                          │
│  └─ LastModifiedDate    └─ SignedAt                                           │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              SERVICE MANAGEMENT                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│  ServiceRequest         ServiceRequestMessage  ServiceRequestAttachment         │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Title               ├─ ServiceRequestId   ├─ ServiceRequestId (FK)        │
│  ├─ Description         │   (FK)              ├─ MessageId (FK)               │
│  ├─ ServiceType         ├─ UserId (FK)        ├─ FileName                     │
│  ├─ Status              ├─ Content            ├─ FilePath                     │
│  ├─ Priority            ├─ MessageType        ├─ FileSize                     │
│  ├─ ProjectId (FK)      ├─ ParentMessageId    ├─ MimeType                     │
│  ├─ ClientId (FK)       ├─ IsInternal         ├─ UploadedById (FK)            │
│  ├─ AssignedToId (FK)   ├─ CreatedAt          └─ CreatedAt                    │
│  ├─ ClientGoals         └─ LastModifiedDate   │                               │
│  ├─ Requirements        │                     │                               │
│  ├─ Notes               │                     │                               │
│  ├─ DueDate             │                     │                               │
│  └─ CompletedDate       │                     │                               │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              COMMUNICATION & CHAT                              │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Conversation           ChatMessage            ConversationParticipant          │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Title               ├─ ConversationId     ├─ ConversationId (FK)          │
│  ├─ Description         │   (FK)              ├─ UserId (FK)                  │
│  ├─ Status              ├─ UserId (FK)        ├─ Role                         │
│  ├─ ConversationType    ├─ Content            ├─ JoinedAt                     │
│  ├─ ProjectId (FK)      ├─ Sender             ├─ LeftAt                       │
│  ├─ ServiceRequestId    ├─ MessageType        └─ IsActive                     │
│  │   (FK)               ├─ SenderType         │                               │
│  ├─ CreatedById (FK)    ├─ IsFromUser         │                               │
│  ├─ IsPrivate           ├─ IsInternal         │                               │
│  ├─ IsArchived          ├─ ParentMessageId    │                               │
│  ├─ CreatedAt           ├─ ReplyToMessageId   │                               │
│  ├─ LastMessageAt       ├─ Metadata           │                               │
│  └─ ArchivedAt          ├─ Timestamp          │                               │
│                        └─ LastModifiedDate    │                               │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              AI AGENT SYSTEM                                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│  AIAgent                AIAgentExecution      AIAgentTemplate                  │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Name                ├─ AIAgentId (FK)     ├─ Name                         │
│  ├─ DisplayName         ├─ Status             ├─ DisplayName                  │
│  ├─ Description         ├─ ExecutionType      ├─ Description                  │
│  ├─ AgentType           ├─ InputData          ├─ Category                     │
│  ├─ Status              ├─ OutputData         ├─ PromptTemplate               │
│  ├─ Configuration       ├─ ErrorMessage       ├─ Configuration                │
│  ├─ PromptTemplate      ├─ ConversationId     ├─ IsActive                     │
│  ├─ IsEnabled           ├─ ProjectId (FK)     ├─ IsSystem                     │
│  ├─ RequiresReview      ├─ DocumentId (FK)    ├─ CreatedAt                    │
│  ├─ CreatedAt           ├─ ServiceRequestId   └─ LastModifiedDate             │
│  ├─ LastModifiedDate    │   (FK)              │                               │
│  └─ LastExecutedAt      ├─ StatusItemId (FK)  │                               │
│                        ├─ TriggeredById (FK)  │                               │
│                        ├─ ReviewedById (FK)   │                               │
│                        ├─ RequiresReview      │                               │
│                        ├─ IsApproved          │                               │
│                        ├─ StartedAt           │                               │
│                        ├─ CompletedAt         │                               │
│                        └─ ReviewedAt          │                               │
│                        │                     │                               │
│  AIAgentResult          ChatSummary           ClientGoal                       │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ AgentType           ├─ ConversationId     ├─ ConversationId (FK)          │
│  ├─ Status              │   (FK)              ├─ ProjectId (FK)               │
│  ├─ Content             ├─ Summary            ├─ ServiceRequestId (FK)        │
│  ├─ Confidence          ├─ KeyPoints          ├─ PrimaryGoal                  │
│  ├─ Metadata            ├─ Sentiment          ├─ SecondaryGoals               │
│  ├─ ConversationId (FK) ├─ Urgency            ├─ BusinessType                 │
│  ├─ ProjectId (FK)      ├─ SuggestedActions   ├─ LegalArea                    │
│  ├─ DocumentId (FK)     └─ CreatedAt          ├─ Timeline                     │
│  ├─ ServiceRequestId    │                     ├─ Budget                       │
│  │   (FK)               │                     ├─ RequiredDocuments            │
│  ├─ RequiresReview      │                     └─ CreatedAt                    │
│  ├─ IsApproved          │                     │                               │
│  ├─ ReviewedById (FK)   │                     │                               │
│  ├─ ReviewedAt          │                     │                               │
│  ├─ ProcessedAt         │                     │                               │
│  └─ CreatedAt           │                     │                               │
│                        │                     │                               │
│  ReplySuggestion        ClarityExplanation                                     │
│  ├─ Id (PK)             ├─ Id (PK)                                            │
│  ├─ TenantId            ├─ TenantId                                           │
│  ├─ ConversationId (FK) ├─ DocumentId (FK)                                    │
│  ├─ ServiceRequestId    ├─ ConversationId (FK)                                │
│  │   (FK)               ├─ OriginalText                                       │
│  ├─ SuggestedReply      ├─ SimplifiedExplanation                              │
│  ├─ Tone                ├─ KeyTerms                                           │
│  ├─ Purpose             ├─ Implications                                       │
│  ├─ KeyPoints           ├─ RiskLevel                                          │
│  ├─ RequiresLegalReview ├─ RecommendedActions                                 │
│  ├─ IsUsed              └─ CreatedAt                                          │
│  └─ CreatedAt           │                                                     │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              WORKFLOW AUTOMATION                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Workflow               WorkflowStep           WorkflowStepDependency           │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ Name                ├─ WorkflowId (FK)    ├─ StepId (FK)                  │
│  ├─ Description         ├─ Name               ├─ DependsOnStepId (FK)         │
│  ├─ WorkflowType        ├─ Description        ├─ DependencyType               │
│  ├─ Status              ├─ StepType           └─ CreatedAt                    │
│  ├─ Configuration       ├─ Order              │                               │
│  ├─ IsSystem            ├─ Configuration      │                               │
│  ├─ IsEnabled           ├─ RequiredRole       │                               │
│  ├─ CreatedAt           ├─ IsRequired         │                               │
│  └─ LastModifiedDate    ├─ IsParallel         │                               │
│                        └─ CreatedAt          │                               │
│                        │                     │                               │
│  WorkflowInstance       WorkflowStepInstance                                  │
│  ├─ Id (PK)             ├─ Id (PK)                                            │
│  ├─ TenantId            ├─ TenantId                                           │
│  ├─ WorkflowId (FK)     ├─ WorkflowInstanceId │                               │
│  ├─ Status              │   (FK)              │                               │
│  ├─ ProjectId (FK)      ├─ WorkflowStepId (FK)│                               │
│  ├─ ServiceRequestId    ├─ Status             │                               │
│  │   (FK)               ├─ AssignedToId (FK)  │                               │
│  ├─ DocumentId (FK)     ├─ CompletedById (FK) │                               │
│  ├─ StatusItemId (FK)   ├─ InputData          │                               │
│  ├─ StartedById (FK)    ├─ OutputData         │                               │
│  ├─ ContextData         ├─ ErrorMessage       │                               │
│  ├─ StartedAt           ├─ StartedAt          │                               │
│  ├─ CompletedAt         ├─ CompletedAt        │                               │
│  └─ LastActivityAt      └─ DueDate            │                               │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                              NOTIFICATIONS & AUDIT                            │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Notification           NotificationTemplate   AuditLog                        │
│  ├─ Id (PK)             ├─ Id (PK)            ├─ Id (PK)                      │
│  ├─ TenantId            ├─ TenantId           ├─ TenantId                     │
│  ├─ UserId (FK)         ├─ Name               ├─ EntityType                   │
│  ├─ Title               ├─ Title              ├─ EntityId                     │
│  ├─ Message             ├─ Message            ├─ Action                       │
│  ├─ Type                ├─ Type               ├─ UserId (FK)                  │
│  ├─ Category            ├─ Category           ├─ UserName                     │
│  ├─ ProjectId (FK)      ├─ ActionUrl          ├─ IPAddress                    │
│  ├─ DocumentId (FK)     ├─ ActionText         ├─ UserAgent                    │
│  ├─ ServiceRequestId    ├─ IsActive           ├─ OldValues                    │
│  │   (FK)               ├─ IsSystem           ├─ NewValues                    │
│  ├─ StatusItemId (FK)   ├─ CreatedAt          ├─ Description                  │
│  ├─ ConversationId (FK) └─ LastModifiedDate   └─ Timestamp                    │
│  ├─ ActionUrl           │                     │                               │
│  ├─ ActionText          │                     │                               │
│  ├─ IsRead              │                     │                               │
│  ├─ IsArchived          │                     │                               │
│  ├─ CreatedAt           │                     │                               │
│  ├─ ReadAt              │                     │                               │
│  └─ ArchivedAt          │                     │                               │
└─────────────────────────────────────────────────────────────────────────────────┘

## Key Relationships

### Primary Relationships
- **User** ↔ **TeamMembership** ↔ **Team** (Many-to-Many)
- **User** ↔ **ProjectAssignment** ↔ **Project** (Many-to-Many)
- **Project** → **StatusItem** (One-to-Many)
- **StatusItem** ↔ **StatusItemDependency** (Self-referencing Many-to-Many)
- **Document** → **DocumentVersion** (One-to-Many)
- **Conversation** → **ChatMessage** (One-to-Many)
- **AIAgent** → **AIAgentExecution** (One-to-Many)

### AI Agent Integration
- All AI agents can be linked to Projects, Documents, Conversations, or ServiceRequests
- Comprehensive execution tracking with input/output data
- Review and approval workflow for AI-generated content

### Workflow Integration
- Workflows can be triggered by any entity type
- Step-by-step execution tracking
- Dependency management between workflow steps

This schema provides a comprehensive foundation for Certio's legal services platform with full AI integration and workflow automation support.
