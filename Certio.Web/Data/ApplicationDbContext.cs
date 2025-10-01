using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Certio.Domain.Matters;
using Certio.Domain.Services;
using Certio.Domain.Users;
using Certio.Domain.Teams;
using Certio.Domain.Documents;
using Certio.Domain.AIAgents;
using Certio.Domain.Workflows;
using Certio.Domain.Notifications;
using Certio.Domain.Audit;
using Certio.Domain.Organizations;

namespace Certio.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Core Entities
        public new DbSet<User> Users => Set<User>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
        public DbSet<OrganizationJoinCode> OrganizationJoinCodes => Set<OrganizationJoinCode>();
        public DbSet<OrganizationRelationship> OrganizationRelationships => Set<OrganizationRelationship>();
        public DbSet<OrganizationRelationshipAssignedUser> OrganizationRelationshipAssignedUsers => Set<OrganizationRelationshipAssignedUser>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
        public DbSet<UserDeletionRequest> UserDeletionRequests => Set<UserDeletionRequest>();
        
        // Matter Entities
        public DbSet<Matter> Matters => Set<Matter>();
        public DbSet<MatterAssignment> MatterAssignments => Set<MatterAssignment>();
        public DbSet<MatterPermission> MatterPermissions => Set<MatterPermission>();
        public DbSet<StatusItem> StatusItems => Set<StatusItem>();
        public DbSet<StatusItemDependency> StatusItemDependencies => Set<StatusItemDependency>();
        public DbSet<StatusItemAssignment> StatusItemAssignments => Set<StatusItemAssignment>();
        public DbSet<StatusItemComment> StatusItemComments => Set<StatusItemComment>();
        
        // Task Entities
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
        public DbSet<TaskItemComment> TaskItemComments => Set<TaskItemComment>();
        public DbSet<TaskItemDependency> TaskItemDependencies => Set<TaskItemDependency>();
        
        // Document Entities
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
        public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();
        public DbSet<DocumentComment> DocumentComments => Set<DocumentComment>();
        public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();
        
        // Service Entities
        public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
        public DbSet<ServiceRequestMessage> ServiceRequestMessages => Set<ServiceRequestMessage>();
        public DbSet<ServiceRequestAttachment> ServiceRequestAttachments => Set<ServiceRequestAttachment>();
        
        // Chat Entities
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
        
        // AI Agent Entities
        public DbSet<AIAgent> AIAgents => Set<AIAgent>();
        public DbSet<AIAgentExecution> AIAgentExecutions => Set<AIAgentExecution>();
        public DbSet<AIAgentResult> AIAgentResults => Set<AIAgentResult>();
        public DbSet<ChatSummary> ChatSummaries => Set<ChatSummary>();
        public DbSet<ClientGoal> ClientGoals => Set<ClientGoal>();
        public DbSet<ReplySuggestion> ReplySuggestions => Set<ReplySuggestion>();
        public DbSet<ClarityExplanation> ClarityExplanations => Set<ClarityExplanation>();
        
        // Workflow Entities
        public DbSet<Workflow> Workflows => Set<Workflow>();
        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
        
        // Notification Entities
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
        
        // Audit Entities
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Configure indexes for performance
            builder.Entity<Matter>().HasIndex(p => p.CreatedAt);
            builder.Entity<Conversation>().HasIndex(c => c.CreatedAt);
            builder.Entity<ChatMessage>().HasIndex(m => new { m.ConversationId, m.CreatedAt });
            builder.Entity<User>().HasIndex(u => u.Email);
            builder.Entity<Document>().HasIndex(d => d.CreatedAt);
            builder.Entity<ServiceRequest>().HasIndex(sr => sr.CreatedAt);
            builder.Entity<StatusItem>().HasIndex(si => si.MatterId);
            builder.Entity<TaskItem>().HasIndex(ti => ti.MatterId);
            builder.Entity<TaskItem>().HasIndex(ti => ti.OrgId);
            builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
            builder.Entity<AuditLog>().HasIndex(a => new { a.EntityType, a.EntityId });
            
            // Configure relationships
            ConfigureOrganizationRelationships(builder);
            ConfigureOrganizationRelationshipRelationships(builder);
            ConfigureOrganizationRelationshipAssignedUserRelationships(builder);
            ConfigureUserOrganizationRelationships(builder);
            ConfigureUserRelationships(builder);
            ConfigureMatterRelationships(builder);
            ConfigureTaskRelationships(builder);
            ConfigureDocumentRelationships(builder);
            ConfigureServiceRelationships(builder);
            ConfigureChatRelationships(builder);
            ConfigureAIAgentRelationships(builder);
            ConfigureWorkflowRelationships(builder);
            ConfigureNotificationRelationships(builder);
            ConfigureUserDeletionRequestRelationships(builder);
        }

        private void ConfigureOrganizationRelationships(ModelBuilder builder)
        {
            // Organization -> Owner relationship
            builder.Entity<Organization>()
                .HasOne(o => o.Owner)
                .WithMany()
                .HasForeignKey(o => o.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Organization -> Teams relationship
            builder.Entity<Organization>()
                .HasMany(o => o.Teams)
                .WithOne(t => t.Organization)
                .HasForeignKey(t => t.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization -> UserOrganizations relationship
            builder.Entity<Organization>()
                .HasMany(o => o.UserOrganizations)
                .WithOne(uo => uo.Organization)
                .HasForeignKey(uo => uo.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization -> JoinCodes relationship
            builder.Entity<Organization>()
                .HasMany(o => o.JoinCodes)
                .WithOne(jc => jc.Organization)
                .HasForeignKey(jc => jc.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationJoinCode -> CreatedBy relationship
            builder.Entity<OrganizationJoinCode>()
                .HasOne(jc => jc.CreatedBy)
                .WithMany(u => u.CreatedJoinCodes)
                .HasForeignKey(jc => jc.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure indexes for performance
            builder.Entity<Organization>().HasIndex(o => o.OwnerId);
            builder.Entity<OrganizationJoinCode>().HasIndex(jc => jc.Code).IsUnique();
            builder.Entity<OrganizationJoinCode>().HasIndex(jc => jc.ExpiresAt);
        }

        private void ConfigureOrganizationRelationshipRelationships(ModelBuilder builder)
        {
            // OrganizationRelationship -> SourceOrganization relationship
            builder.Entity<OrganizationRelationship>()
                .HasOne(or => or.SourceOrganization)
                .WithMany(o => o.OrganizationRelationships)
                .HasForeignKey(or => or.SourceOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrganizationRelationship -> TargetOrganization relationship
            builder.Entity<OrganizationRelationship>()
                .HasOne(or => or.TargetOrganization)
                .WithMany(o => o.RelatedOrganizations)
                .HasForeignKey(or => or.TargetOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrganizationRelationship -> CreatedBy relationship
            builder.Entity<OrganizationRelationship>()
                .HasOne(or => or.CreatedBy)
                .WithMany()
                .HasForeignKey(or => or.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // OrganizationRelationship -> ModifiedBy relationship
            builder.Entity<OrganizationRelationship>()
                .HasOne(or => or.ModifiedBy)
                .WithMany()
                .HasForeignKey(or => or.ModifiedById)
                .OnDelete(DeleteBehavior.NoAction);

            // OrganizationRelationship -> DeletedBy relationship
            builder.Entity<OrganizationRelationship>()
                .HasOne(or => or.DeletedBy)
                .WithMany()
                .HasForeignKey(or => or.DeletedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Add indexes for performance
            builder.Entity<OrganizationRelationship>()
                .HasIndex(or => new { or.SourceOrganizationId, or.TargetOrganizationId, or.IsActive });

            builder.Entity<OrganizationRelationship>()
                .HasIndex(or => new { or.RelationshipType, or.IsActive });

            builder.Entity<OrganizationRelationship>()
                .HasIndex(or => or.ExpiresAt);

            // Composite unique index to prevent duplicate relationships
            builder.Entity<OrganizationRelationship>()
                .HasIndex(or => new { or.SourceOrganizationId, or.TargetOrganizationId, or.RelationshipType })
                .IsUnique();
        }

        private void ConfigureOrganizationRelationshipAssignedUserRelationships(ModelBuilder builder)
        {
            builder.Entity<OrganizationRelationshipAssignedUser>()
                .HasOne(orau => orau.Relationship)
                .WithMany()
                .HasForeignKey(orau => orau.RelationshipId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrganizationRelationshipAssignedUser>()
                .HasIndex(orau => new { orau.RelationshipId, orau.UserId })
                .IsUnique();

            builder.Entity<OrganizationRelationshipAssignedUser>()
                .HasIndex(orau => orau.UserId);
        }

        private void ConfigureUserOrganizationRelationships(ModelBuilder builder)
        {
            // UserOrganization -> User relationship
            builder.Entity<UserOrganization>()
                .HasOne(uo => uo.User)
                .WithMany(u => u.UserOrganizations)
                .HasForeignKey(uo => uo.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserOrganization -> Organization relationship
            builder.Entity<UserOrganization>()
                .HasOne(uo => uo.Organization)
                .WithMany(o => o.UserOrganizations)
                .HasForeignKey(uo => uo.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite unique index to prevent duplicate memberships
            builder.Entity<UserOrganization>()
                .HasIndex(uo => new { uo.UserId, uo.OrganizationId })
                .IsUnique();

            // Index for performance
            builder.Entity<UserOrganization>()
                .HasIndex(uo => new { uo.OrganizationId, uo.IsActive });
                
            // Index for primary organization lookup
            builder.Entity<UserOrganization>()
                .HasIndex(uo => new { uo.UserId, uo.IsPrimary, uo.IsActive });
        }

        private void ConfigureUserRelationships(ModelBuilder builder)
        {
            builder.Entity<User>()
                .HasMany(u => u.TeamMemberships)
                .WithOne(tm => tm.User)
                .HasForeignKey(tm => tm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigureMatterRelationships(ModelBuilder builder)
        {
            // Configure Organization relationship for Matter
            builder.Entity<Matter>()
                .HasOne(p => p.Organization)
                .WithMany()
                .HasForeignKey(p => p.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Matter>()
                .HasMany(p => p.StatusItems)
                .WithOne(si => si.Matter)
                .HasForeignKey(si => si.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Matter>()
                .HasMany(p => p.Assignments)
                .WithOne(pa => pa.Matter)
                .HasForeignKey(pa => pa.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User relationships for Matter
            builder.Entity<Matter>()
                .HasOne(p => p.Client)
                .WithMany()
                .HasForeignKey(p => p.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Team relationship for Matter
            builder.Entity<Matter>()
                .HasOne(p => p.Team)
                .WithMany()
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure self-referencing relationships for StatusItem
            builder.Entity<StatusItem>()
                .HasOne(si => si.ParentStatusItem)
                .WithMany(si => si.SubStatusItems)
                .HasForeignKey(si => si.ParentStatusItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StatusItem>()
                .HasMany(si => si.Dependencies)
                .WithOne(sid => sid.StatusItem)
                .HasForeignKey(sid => sid.StatusItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StatusItem>()
                .HasMany(si => si.DependentItems)
                .WithOne(sid => sid.DependsOnStatusItem)
                .HasForeignKey(sid => sid.DependsOnStatusItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure StatusItemAssignment relationships
            builder.Entity<StatusItemAssignment>()
                .HasOne(sia => sia.StatusItem)
                .WithMany(si => si.Assignments)
                .HasForeignKey(sia => sia.StatusItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StatusItemAssignment>()
                .HasOne(sia => sia.User)
                .WithMany()
                .HasForeignKey(sia => sia.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure StatusItemComment relationships
            builder.Entity<StatusItemComment>()
                .HasOne(sic => sic.StatusItem)
                .WithMany(si => si.Comments)
                .HasForeignKey(sic => sic.StatusItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StatusItemComment>()
                .HasOne(sic => sic.User)
                .WithMany()
                .HasForeignKey(sic => sic.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure MatterAssignment relationships
            builder.Entity<MatterAssignment>()
                .HasOne(pa => pa.User)
                .WithMany()
                .HasForeignKey(pa => pa.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            // Configure MatterPermission relationships
            builder.Entity<MatterPermission>()
                .HasOne(mp => mp.Matter)
                .WithMany(m => m.Permissions)
                .HasForeignKey(mp => mp.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MatterPermission>()
                .HasOne(mp => mp.User)
                .WithMany()
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MatterPermission>()
                .HasOne(mp => mp.GrantedBy)
                .WithMany()
                .HasForeignKey(mp => mp.GrantedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<MatterPermission>()
                .HasOne(mp => mp.RevokedBy)
                .WithMany()
                .HasForeignKey(mp => mp.RevokedById)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureTaskRelationships(ModelBuilder builder)
        {
            // TaskItem -> Matter relationship
            builder.Entity<TaskItem>()
                .HasOne(ti => ti.Matter)
                .WithMany()
                .HasForeignKey(ti => ti.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem -> Organization relationship
            builder.Entity<TaskItem>()
                .HasOne(ti => ti.Organization)
                .WithMany()
                .HasForeignKey(ti => ti.OrgId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskItem self-referencing relationship (ParentTask -> SubTasks)
            builder.Entity<TaskItem>()
                .HasOne(ti => ti.ParentTaskItem)
                .WithMany(ti => ti.SubTaskItems)
                .HasForeignKey(ti => ti.ParentTaskItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskItem -> TaskAssignments relationship
            builder.Entity<TaskItem>()
                .HasMany(ti => ti.TaskAssignments)
                .WithOne(ta => ta.TaskItem)
                .HasForeignKey(ta => ta.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem -> Comments relationship
            builder.Entity<TaskItem>()
                .HasMany(ti => ti.Comments)
                .WithOne(tic => tic.TaskItem)
                .HasForeignKey(tic => tic.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem -> Dependencies relationship
            builder.Entity<TaskItem>()
                .HasMany(ti => ti.Dependencies)
                .WithOne(tid => tid.TaskItem)
                .HasForeignKey(tid => tid.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem -> DependentItems relationship
            builder.Entity<TaskItem>()
                .HasMany(ti => ti.DependentItems)
                .WithOne(tid => tid.DependsOnTaskItem)
                .HasForeignKey(tid => tid.DependsOnTaskItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskAssignment -> User relationship
            builder.Entity<TaskAssignment>()
                .HasOne(ta => ta.User)
                .WithMany()
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskItemComment -> User relationship
            builder.Entity<TaskItemComment>()
                .HasOne(tic => tic.User)
                .WithMany()
                .HasForeignKey(tic => tic.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskItemComment self-referencing relationship (ParentComment -> Replies)
            builder.Entity<TaskItemComment>()
                .HasOne(tic => tic.ParentComment)
                .WithMany(tic => tic.Replies)
                .HasForeignKey(tic => tic.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureDocumentRelationships(ModelBuilder builder)
        {
            builder.Entity<Document>()
                .HasMany(d => d.Versions)
                .WithOne(dv => dv.Document)
                .HasForeignKey(dv => dv.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Document>()
                .HasMany(d => d.Reviews)
                .WithOne(dr => dr.Document)
                .HasForeignKey(dr => dr.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Document>()
                .HasMany(d => d.Comments)
                .WithOne(dc => dc.Document)
                .HasForeignKey(dc => dc.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Document>()
                .HasMany(d => d.Signatures)
                .WithOne(ds => ds.Document)
                .HasForeignKey(ds => ds.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User relationships for Document
            builder.Entity<Document>()
                .HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Matter relationship for Document
            builder.Entity<Document>()
                .HasOne(d => d.Matter)
                .WithMany()
                .HasForeignKey(d => d.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure StatusItem relationship for Document
            builder.Entity<Document>()
                .HasOne(d => d.StatusItem)
                .WithMany(si => si.RelatedDocuments)
                .HasForeignKey(d => d.StatusItemId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure DocumentVersion relationships
            builder.Entity<DocumentVersion>()
                .HasOne(dv => dv.CreatedBy)
                .WithMany()
                .HasForeignKey(dv => dv.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure DocumentReview relationships
            builder.Entity<DocumentReview>()
                .HasOne(dr => dr.Reviewer)
                .WithMany()
                .HasForeignKey(dr => dr.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DocumentComment relationships
            builder.Entity<DocumentComment>()
                .HasOne(dc => dc.User)
                .WithMany()
                .HasForeignKey(dc => dc.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure self-referencing relationships for DocumentComment
            builder.Entity<DocumentComment>()
                .HasOne(dc => dc.ParentComment)
                .WithMany(dc => dc.Replies)
                .HasForeignKey(dc => dc.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DocumentSignature relationships
            builder.Entity<DocumentSignature>()
                .HasOne(ds => ds.Signer)
                .WithMany()
                .HasForeignKey(ds => ds.SignerId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureServiceRelationships(ModelBuilder builder)
        {
            builder.Entity<ServiceRequest>()
                .HasMany(sr => sr.Messages)
                .WithOne(srm => srm.ServiceRequest)
                .HasForeignKey(srm => srm.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServiceRequest>()
                .HasMany(sr => sr.Attachments)
                .WithOne(sra => sra.ServiceRequest)
                .HasForeignKey(sra => sra.ServiceRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure User relationships for ServiceRequest
            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Client)
                .WithMany()
                .HasForeignKey(sr => sr.ClientId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.AssignedTo)
                .WithMany()
                .HasForeignKey(sr => sr.AssignedToId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Matter relationship for ServiceRequest
            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Matter)
                .WithMany()
                .HasForeignKey(sr => sr.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure self-referencing relationships for ServiceRequestMessage
            builder.Entity<ServiceRequestMessage>()
                .HasOne(srm => srm.ParentMessage)
                .WithMany(srm => srm.Replies)
                .HasForeignKey(srm => srm.ParentMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User relationship for ServiceRequestMessage
            builder.Entity<ServiceRequestMessage>()
                .HasOne(srm => srm.User)
                .WithMany()
                .HasForeignKey(srm => srm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User relationship for ServiceRequestAttachment
            builder.Entity<ServiceRequestAttachment>()
                .HasOne(sra => sra.UploadedBy)
                .WithMany()
                .HasForeignKey(sra => sra.UploadedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure ServiceRequestMessage relationship for ServiceRequestAttachment
            builder.Entity<ServiceRequestAttachment>()
                .HasOne(sra => sra.Message)
                .WithMany()
                .HasForeignKey(sra => sra.MessageId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureChatRelationships(ModelBuilder builder)
        {
            // Configure Organization relationship for Conversation
            builder.Entity<Conversation>()
                .HasOne(c => c.Organization)
                .WithMany()
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User relationship for Conversation (CreatedBy)
            builder.Entity<Conversation>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Conversation -> Messages relationship
            builder.Entity<Conversation>()
                .HasMany(c => c.Messages)
                .WithOne(cm => cm.Conversation)
                .HasForeignKey(cm => cm.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Conversation -> Participants relationship
            builder.Entity<Conversation>()
                .HasMany(c => c.Participants)
                .WithOne(cp => cp.Conversation)
                .HasForeignKey(cp => cp.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Matter relationship for Conversation
            builder.Entity<Conversation>()
                .HasOne(c => c.Matter)
                .WithMany()
                .HasForeignKey(c => c.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ServiceRequest relationship for Conversation
            builder.Entity<Conversation>()
                .HasOne(c => c.ServiceRequest)
                .WithMany()
                .HasForeignKey(c => c.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure User relationship for ConversationParticipant
            builder.Entity<ConversationParticipant>()
                .HasOne(cp => cp.User)
                .WithMany()
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User relationship for ChatMessage
            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.User)
                .WithMany(u => u.ChatMessages)
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure self-referencing relationships for ChatMessage
            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.ParentMessage)
                .WithMany(cm => cm.ChildMessages)
                .HasForeignKey(cm => cm.ParentMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.ReplyToMessage)
                .WithMany(cm => cm.Replies)
                .HasForeignKey(cm => cm.ReplyToMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes for performance
            builder.Entity<Conversation>()
                .HasIndex(c => new { c.OrganizationId, c.CreatedById });
            
            builder.Entity<Conversation>()
                .HasIndex(c => new { c.OrganizationId, c.CreatedAt });
            
            builder.Entity<ChatMessage>()
                .HasIndex(cm => new { cm.ConversationId, cm.CreatedAt });
        }

        private void ConfigureAIAgentRelationships(ModelBuilder builder)
        {
            builder.Entity<AIAgent>()
                .HasMany(aa => aa.Executions)
                .WithOne(aae => aae.AIAgent)
                .HasForeignKey(aae => aae.AIAgentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User relationship for AIAgentExecution
            builder.Entity<AIAgentExecution>()
                .HasOne(aae => aae.TriggeredBy)
                .WithMany()
                .HasForeignKey(aae => aae.TriggeredById)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure AIAgentResult relationships
            builder.Entity<AIAgentResult>()
                .HasOne(aar => aar.Conversation)
                .WithMany()
                .HasForeignKey(aar => aar.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AIAgentResult>()
                .HasOne(aar => aar.Matter)
                .WithMany()
                .HasForeignKey(aar => aar.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AIAgentResult>()
                .HasOne(aar => aar.Document)
                .WithMany()
                .HasForeignKey(aar => aar.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AIAgentResult>()
                .HasOne(aar => aar.ServiceRequest)
                .WithMany()
                .HasForeignKey(aar => aar.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AIAgentResult>()
                .HasOne(aar => aar.ReviewedBy)
                .WithMany()
                .HasForeignKey(aar => aar.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ChatSummary relationships
            builder.Entity<ChatSummary>()
                .HasOne(cs => cs.Conversation)
                .WithMany()
                .HasForeignKey(cs => cs.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ClientGoal relationships
            builder.Entity<ClientGoal>()
                .HasOne(cg => cg.Conversation)
                .WithMany()
                .HasForeignKey(cg => cg.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ClientGoal>()
                .HasOne(cg => cg.Matter)
                .WithMany()
                .HasForeignKey(cg => cg.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ClientGoal>()
                .HasOne(cg => cg.ServiceRequest)
                .WithMany()
                .HasForeignKey(cg => cg.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ReplySuggestion relationships
            builder.Entity<ReplySuggestion>()
                .HasOne(rs => rs.Conversation)
                .WithMany()
                .HasForeignKey(rs => rs.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ReplySuggestion>()
                .HasOne(rs => rs.ServiceRequest)
                .WithMany()
                .HasForeignKey(rs => rs.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ClarityExplanation relationships
            builder.Entity<ClarityExplanation>()
                .HasOne(ce => ce.Document)
                .WithMany()
                .HasForeignKey(ce => ce.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ClarityExplanation>()
                .HasOne(ce => ce.Conversation)
                .WithMany()
                .HasForeignKey(ce => ce.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureWorkflowRelationships(ModelBuilder builder)
        {
            builder.Entity<Workflow>()
                .HasMany(w => w.Instances)
                .WithOne(wi => wi.Workflow)
                .HasForeignKey(wi => wi.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User relationship for WorkflowInstance
            builder.Entity<WorkflowInstance>()
                .HasOne(wi => wi.StartedBy)
                .WithMany()
                .HasForeignKey(wi => wi.StartedById)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureNotificationRelationships(ModelBuilder builder)
        {
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Matter relationship for Notification
            builder.Entity<Notification>()
                .HasOne(n => n.Matter)
                .WithMany()
                .HasForeignKey(n => n.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Document relationship for Notification
            builder.Entity<Notification>()
                .HasOne(n => n.Document)
                .WithMany()
                .HasForeignKey(n => n.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ServiceRequest relationship for Notification
            builder.Entity<Notification>()
                .HasOne(n => n.ServiceRequest)
                .WithMany()
                .HasForeignKey(n => n.ServiceRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure StatusItem relationship for Notification
            builder.Entity<Notification>()
                .HasOne(n => n.StatusItem)
                .WithMany()
                .HasForeignKey(n => n.StatusItemId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Conversation relationship for Notification
            builder.Entity<Notification>()
                .HasOne(n => n.Conversation)
                .WithMany()
                .HasForeignKey(n => n.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureUserDeletionRequestRelationships(ModelBuilder builder)
        {
            builder.Entity<UserDeletionRequest>()
                .HasOne(udr => udr.User)
                .WithMany()
                .HasForeignKey(udr => udr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserDeletionRequest>()
                .HasOne(udr => udr.ProcessedBy)
                .WithMany()
                .HasForeignKey(udr => udr.ProcessedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Add indexes for performance
            builder.Entity<UserDeletionRequest>()
                .HasIndex(udr => new { udr.UserId, udr.IsProcessed });

            builder.Entity<UserDeletionRequest>()
                .HasIndex(udr => udr.RequestedAt);
        }

        // Query optimization for active users
        public IQueryable<User> ActiveUsers => Users.Where(u => !u.IsDeleted);

        public override int SaveChanges()
        {
            // Handle soft delete for Users
            foreach (var entry in ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    // Note: DeletedById should be set by the service layer
                }
            }

            // Handle soft delete for OrganizationRelationships
            foreach (var entry in ChangeTracker.Entries<OrganizationRelationship>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    // Note: DeletedById should be set by the service layer
                }
            }

            return base.SaveChanges();
        }
    }
}
