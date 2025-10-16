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
using Certio.Domain.Tasks;

namespace Certio.Infrastructure.Data
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
        public DbSet<TaskCommentMention> TaskCommentMentions => Set<TaskCommentMention>();
        public DbSet<TaskCommentReaction> TaskCommentReactions => Set<TaskCommentReaction>();
        public DbSet<TaskItemDependency> TaskItemDependencies => Set<TaskItemDependency>();
        public DbSet<SubTaskItem> SubTaskItems => Set<SubTaskItem>();
        public DbSet<SubTaskAssignment> SubTaskAssignments => Set<SubTaskAssignment>();
        
        // Document Entities
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
        public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();
        public DbSet<DocumentComment> DocumentComments => Set<DocumentComment>();
        public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();
        
        // Chat Entities
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
        
        // Direct Message Entities
        public DbSet<DirectThread> DirectThreads => Set<DirectThread>();
        public DbSet<DirectParticipant> DirectParticipants => Set<DirectParticipant>();
        public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
        
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
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        
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
            ConfigureSubTaskRelationships(builder);
            ConfigureDocumentRelationships(builder);
            ConfigureChatRelationships(builder);
            ConfigureDirectMessageRelationships(builder);
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
                .WithMany(t => t.Matters)
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
                .WithMany(u => u.MatterAssignments)
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
                .WithMany(m => m.TaskItems)
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

            // TaskCommentMention relationships
            builder.Entity<TaskCommentMention>()
                .HasOne(tcm => tcm.Comment)
                .WithMany(c => c.Mentions)
                .HasForeignKey(tcm => tcm.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskCommentMention>()
                .HasOne(tcm => tcm.MentionedUser)
                .WithMany()
                .HasForeignKey(tcm => tcm.MentionedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskCommentMention>()
                .HasIndex(tcm => new { tcm.CommentId, tcm.MentionedUserId })
                .IsUnique();

            // TaskCommentReaction relationships
            builder.Entity<TaskCommentReaction>()
                .HasOne(tcr => tcr.Comment)
                .WithMany(c => c.Reactions)
                .HasForeignKey(tcr => tcr.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskCommentReaction>()
                .HasOne(tcr => tcr.User)
                .WithMany()
                .HasForeignKey(tcr => tcr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskCommentReaction>()
                .HasIndex(tcr => new { tcr.CommentId, tcr.UserId, tcr.ReactionType })
                .IsUnique();
        }

        private void ConfigureSubTaskRelationships(ModelBuilder builder)
        {
            // SubTaskItem -> TaskItem relationship
            builder.Entity<SubTaskItem>()
                .HasOne(sti => sti.Task)
                .WithMany(t => t.SubTasks)
                .HasForeignKey(sti => sti.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // SubTaskItem -> Matter relationship
            builder.Entity<SubTaskItem>()
                .HasOne(sti => sti.Matter)
                .WithMany()
                .HasForeignKey(sti => sti.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            // SubTaskAssignment -> SubTaskItem relationship
            builder.Entity<SubTaskAssignment>()
                .HasOne(sta => sta.SubTaskItem)
                .WithMany(sti => sti.Assignments)
                .HasForeignKey(sta => sta.SubTaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // SubTaskAssignment -> User relationship
            builder.Entity<SubTaskAssignment>()
                .HasOne(sta => sta.User)
                .WithMany()
                .HasForeignKey(sta => sta.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes for performance
            builder.Entity<SubTaskItem>()
                .HasIndex(sti => new { sti.TaskId, sti.IsCompleted });
            
            builder.Entity<SubTaskItem>()
                .HasIndex(sti => new { sti.MatterId, sti.IsCompleted });
            
            builder.Entity<SubTaskItem>()
                .HasIndex(sti => sti.OrgId);

            builder.Entity<SubTaskAssignment>()
                .HasIndex(sta => new { sta.SubTaskItemId, sta.AssignmentType });
            
            builder.Entity<SubTaskAssignment>()
                .HasIndex(sta => sta.UserId);
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
                .WithMany(m => m.Documents)
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

        private void ConfigureDirectMessageRelationships(ModelBuilder builder)
        {
            // DirectThread -> Organization relationship
            builder.Entity<DirectThread>()
                .HasOne(dt => dt.Organization)
                .WithMany()
                .HasForeignKey(dt => dt.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // DirectThread -> UserA relationship
            builder.Entity<DirectThread>()
                .HasOne(dt => dt.UserA)
                .WithMany()
                .HasForeignKey(dt => dt.UserAId)
                .OnDelete(DeleteBehavior.Restrict);

            // DirectThread -> UserB relationship
            builder.Entity<DirectThread>()
                .HasOne(dt => dt.UserB)
                .WithMany()
                .HasForeignKey(dt => dt.UserBId)
                .OnDelete(DeleteBehavior.Restrict);

            // DirectThread -> DeletedBy relationship
            builder.Entity<DirectThread>()
                .HasOne(dt => dt.DeletedBy)
                .WithMany()
                .HasForeignKey(dt => dt.DeletedById)
                .OnDelete(DeleteBehavior.SetNull);

            // DirectThread -> Messages relationship
            builder.Entity<DirectThread>()
                .HasMany(dt => dt.Messages)
                .WithOne(dm => dm.Thread)
                .HasForeignKey(dm => dm.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // DirectThread -> Participants relationship
            builder.Entity<DirectThread>()
                .HasMany(dt => dt.Participants)
                .WithOne(dp => dp.Thread)
                .HasForeignKey(dp => dp.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // DirectParticipant -> User relationship
            builder.Entity<DirectParticipant>()
                .HasOne(dp => dp.User)
                .WithMany()
                .HasForeignKey(dp => dp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // DirectMessage -> Sender relationship
            builder.Entity<DirectMessage>()
                .HasOne(dm => dm.Sender)
                .WithMany()
                .HasForeignKey(dm => dm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // DirectMessage -> DeletedBy relationship
            builder.Entity<DirectMessage>()
                .HasOne(dm => dm.DeletedBy)
                .WithMany()
                .HasForeignKey(dm => dm.DeletedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for performance
            builder.Entity<DirectThread>()
                .HasIndex(dt => new { dt.OrganizationId, dt.UserAId, dt.UserBId })
                .IsUnique();
            
            builder.Entity<DirectThread>()
                .HasIndex(dt => new { dt.UserAId, dt.LastMessageAt });
            
            builder.Entity<DirectThread>()
                .HasIndex(dt => new { dt.UserBId, dt.LastMessageAt });

            builder.Entity<DirectParticipant>()
                .HasIndex(dp => new { dp.ThreadId, dp.UserId })
                .IsUnique();
            
            builder.Entity<DirectParticipant>()
                .HasIndex(dp => new { dp.UserId, dp.Pinned, dp.Archived });

            builder.Entity<DirectMessage>()
                .HasIndex(dm => new { dm.ThreadId, dm.CreatedAt });
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

            // Configure ReplySuggestion relationships
            builder.Entity<ReplySuggestion>()
                .HasOne(rs => rs.Conversation)
                .WithMany()
                .HasForeignKey(rs => rs.ConversationId)
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
