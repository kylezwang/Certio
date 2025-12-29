using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq;
using System.Text.Json;
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
using Certio.Domain.Calendar;
using Certio.Domain.AgentActions;
using Certio.Domain.UnifiedInbox;
using Certio.Domain.ChangeControl;

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
        public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
        
        // Matter Entities
        public DbSet<Matter> Matters => Set<Matter>();
        public DbSet<MatterAssignment> MatterAssignments => Set<MatterAssignment>();
        public DbSet<MatterPermission> MatterPermissions => Set<MatterPermission>();
        public DbSet<StatusItem> StatusItems => Set<StatusItem>();
        public DbSet<StatusItemDependency> StatusItemDependencies => Set<StatusItemDependency>();
        public DbSet<StatusItemAssignment> StatusItemAssignments => Set<StatusItemAssignment>();
        public DbSet<StatusItemComment> StatusItemComments => Set<StatusItemComment>();

        // Change Control Entities
        public DbSet<ChangeNotice> ChangeNotices => Set<ChangeNotice>();
        public DbSet<ChangeNoticeRecipient> ChangeNoticeRecipients => Set<ChangeNoticeRecipient>();
        
        // Task Entities
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
        public DbSet<TaskItemComment> TaskItemComments => Set<TaskItemComment>();
        public DbSet<TaskCommentMention> TaskCommentMentions => Set<TaskCommentMention>();
        public DbSet<TaskCommentReaction> TaskCommentReactions => Set<TaskCommentReaction>();
        public DbSet<TaskItemDependency> TaskItemDependencies => Set<TaskItemDependency>();
        public DbSet<SubTaskItem> SubTaskItems => Set<SubTaskItem>();
        public DbSet<SubTaskAssignment> SubTaskAssignments => Set<SubTaskAssignment>();
        
        // Calendar Entities
        public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
        public DbSet<CalendarEventAttendee> CalendarEventAttendees => Set<CalendarEventAttendee>();
        public DbSet<CalendarIntegration> CalendarIntegrations => Set<CalendarIntegration>();
        
        // Document Entities
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
        public DbSet<DocumentVector> DocumentVectors => Set<DocumentVector>();
        public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();
        public DbSet<RagQuery> RagQueries => Set<RagQuery>();
        public DbSet<RagCacheEntry> RagCache => Set<RagCacheEntry>();
        public DbSet<ExternalConnection> ExternalConnections => Set<ExternalConnection>();
        
        // Chat Entities
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
        
        // Direct Message Entities
        public DbSet<DirectThread> DirectThreads => Set<DirectThread>();
        public DbSet<DirectParticipant> DirectParticipants => Set<DirectParticipant>();
        public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
        
        // Email Integration Entities
        public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();
        public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
        
        // AI Agent Entities
        public DbSet<AIAgent> AIAgents => Set<AIAgent>();
        public DbSet<AIAgentExecution> AIAgentExecutions => Set<AIAgentExecution>();
        public DbSet<AIAgentResult> AIAgentResults => Set<AIAgentResult>();
        public DbSet<ChatSummary> ChatSummaries => Set<ChatSummary>();
        public DbSet<ClientGoal> ClientGoals => Set<ClientGoal>();
        public DbSet<ReplySuggestion> ReplySuggestions => Set<ReplySuggestion>();
        public DbSet<ClarityExplanation> ClarityExplanations => Set<ClarityExplanation>();
        
        // AI Usage Tracking Entities
        public DbSet<AIUsage> AIUsages => Set<AIUsage>();
        public DbSet<AIUsageDaily> AIUsageDailies => Set<AIUsageDaily>();
        
        // Workflow Entities
        public DbSet<Workflow> Workflows => Set<Workflow>();
        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
        
        // Notification Entities
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        
        // Audit Entities
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        
        // Agent Action Entities
        public DbSet<AgentAction> AgentActions => Set<AgentAction>();
        
        // Unified Inbox Entities
        public DbSet<InboxItem> InboxItems => Set<InboxItem>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

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
            builder.Entity<CalendarEvent>().HasIndex(ce => ce.OrgId);
            builder.Entity<CalendarEvent>().HasIndex(ce => ce.MatterId);
            builder.Entity<CalendarEvent>().HasIndex(ce => ce.StartDateTime);
            builder.Entity<CalendarEvent>().HasIndex(ce => ce.EndDateTime);
            builder.Entity<CalendarEvent>().HasIndex(ce => new { ce.OrgId, ce.StartDateTime });
            builder.Entity<CalendarEvent>().HasIndex(ce => ce.IsDeleted);
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
            ConfigureCalendarRelationships(builder);
            ConfigureDocumentRelationships(builder);
            ConfigureChatRelationships(builder);
            ConfigureDirectMessageRelationships(builder);
            ConfigureEmailIntegrationRelationships(builder);
            ConfigureAIAgentRelationships(builder);
            ConfigureAIUsageRelationships(builder);
            ConfigureWorkflowRelationships(builder);
            ConfigureNotificationRelationships(builder);
            ConfigureUserDeletionRequestRelationships(builder);
            ConfigureAgentActionRelationships(builder);
            ConfigureUnifiedInboxRelationships(builder);
            ConfigureChangeControlRelationships(builder);
        }

        private void ConfigureChangeControlRelationships(ModelBuilder builder)
        {
            builder.Entity<ChangeNotice>()
                .HasIndex(cn => new { cn.OrganizationId, cn.MatterId, cn.IsDeleted });

            builder.Entity<ChangeNotice>()
                .HasOne(cn => cn.Matter)
                .WithMany()
                .HasForeignKey(cn => cn.MatterId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChangeNotice>()
                .HasOne(cn => cn.Organization)
                .WithMany()
                .HasForeignKey(cn => cn.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Avoid SQL Server "multiple cascade paths" by using NoAction for audit user FKs.
            builder.Entity<ChangeNotice>()
                .HasOne(cn => cn.CreatedBy)
                .WithMany()
                .HasForeignKey(cn => cn.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ChangeNotice>()
                .HasOne(cn => cn.ModifiedBy)
                .WithMany()
                .HasForeignKey(cn => cn.ModifiedById)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ChangeNotice>()
                .HasOne(cn => cn.DeletedBy)
                .WithMany()
                .HasForeignKey(cn => cn.DeletedById)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ChangeNoticeRecipient>()
                .HasIndex(r => new { r.ChangeNoticeId, r.Email });

            builder.Entity<ChangeNoticeRecipient>()
                .HasOne(r => r.ChangeNotice)
                .WithMany(cn => cn.Recipients)
                .HasForeignKey(r => r.ChangeNoticeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChangeNoticeRecipient>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull);
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

        private void ConfigureCalendarRelationships(ModelBuilder builder)
        {
            // CalendarEvent -> Organization relationship
            builder.Entity<CalendarEvent>()
                .HasOne(ce => ce.Organization)
                .WithMany()
                .HasForeignKey(ce => ce.OrgId)
                .OnDelete(DeleteBehavior.Restrict);

            // CalendarEvent -> Matter relationship (optional)
            builder.Entity<CalendarEvent>()
                .HasOne(ce => ce.Matter)
                .WithMany()
                .HasForeignKey(ce => ce.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // CalendarEvent -> CreatedBy relationship
            builder.Entity<CalendarEvent>()
                .HasOne(ce => ce.CreatedBy)
                .WithMany()
                .HasForeignKey(ce => ce.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            // CalendarEvent -> ModifiedBy relationship
            builder.Entity<CalendarEvent>()
                .HasOne(ce => ce.ModifiedBy)
                .WithMany()
                .HasForeignKey(ce => ce.ModifiedById)
                .OnDelete(DeleteBehavior.NoAction);

            // CalendarEvent -> Attendees relationship
            builder.Entity<CalendarEvent>()
                .HasMany(ce => ce.Attendees)
                .WithOne(cea => cea.CalendarEvent)
                .HasForeignKey(cea => cea.CalendarEventId)
                .OnDelete(DeleteBehavior.Cascade);

            // CalendarEventAttendee -> User relationship
            builder.Entity<CalendarEventAttendee>()
                .HasOne(cea => cea.User)
                .WithMany()
                .HasForeignKey(cea => cea.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes for performance
            builder.Entity<CalendarEventAttendee>()
                .HasIndex(cea => new { cea.CalendarEventId, cea.UserId })
                .IsUnique();

            builder.Entity<CalendarEventAttendee>()
                .HasIndex(cea => cea.UserId);

            // CalendarIntegration -> Organization relationship
            builder.Entity<CalendarIntegration>()
                .HasOne(ci => ci.Organization)
                .WithMany()
                .HasForeignKey(ci => ci.OrgId)
                .OnDelete(DeleteBehavior.Restrict);

            // CalendarIntegration -> User relationship
            builder.Entity<CalendarIntegration>()
                .HasOne(ci => ci.User)
                .WithMany()
                .HasForeignKey(ci => ci.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add index for calendar integrations
            builder.Entity<CalendarIntegration>()
                .HasIndex(ci => new { ci.OrgId, ci.UserId, ci.Provider })
                .IsUnique();
        }

        private void ConfigureDocumentRelationships(ModelBuilder builder)
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, jsonSerializerOptions) ?? new List<string>());

            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => ReferenceEquals(c1, c2) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                c => c == null ? new List<string>() : c.ToList());

            var stringDictionaryConverter = new ValueConverter<Dictionary<string, string?>, string>(
                v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? new Dictionary<string, string?>() : JsonSerializer.Deserialize<Dictionary<string, string?>>(v, jsonSerializerOptions) ?? new Dictionary<string, string?>());

            var stringDictionaryComparer = new ValueComparer<Dictionary<string, string?>>(
                (c1, c2) => ReferenceEquals(c1, c2) || (c1 != null && c2 != null && c1.Count == c2.Count && !c1.Except(c2).Any()),
                c => (c ?? new Dictionary<string, string?>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value == null ? 0 : v.Value.GetHashCode())),
                c => c == null ? new Dictionary<string, string?>() : c.ToDictionary(k => k.Key, v => v.Value));

            var guidListConverter = new ValueConverter<List<Guid>, string>(
                v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? new List<Guid>() : JsonSerializer.Deserialize<List<Guid>>(v, jsonSerializerOptions) ?? new List<Guid>());

            var guidListComparer = new ValueComparer<List<Guid>>(
                (c1, c2) => ReferenceEquals(c1, c2) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => (c ?? new List<Guid>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<Guid>() : c.ToList());

            var floatArrayConverter = new ValueConverter<float[]?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<float[]>(v, jsonSerializerOptions));

            var floatArrayComparer = new ValueComparer<float[]?>(
                (c1, c2) => (c1 ?? Array.Empty<float>()).SequenceEqual(c2 ?? Array.Empty<float>()),
                c => (c ?? Array.Empty<float>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? null : c.ToArray());

            builder.Entity<Document>(entity =>
            {
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.OrgId, e.MatterId, e.Status });
                entity.HasIndex(e => new { e.OrgId, e.SourceType, e.Status });
                entity.Property(e => e.Tags)
                    .HasConversion(stringListConverter)
                    .Metadata.SetValueComparer(stringListComparer);
                entity.Property(e => e.Metadata)
                    .HasConversion(stringDictionaryConverter)
                    .Metadata.SetValueComparer(stringDictionaryComparer);
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.ModifiedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.HasMany(d => d.Versions)
                    .WithOne(v => v.Document)
                    .HasForeignKey(v => v.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(d => d.Vectors)
                    .WithOne(v => v.Document)
                    .HasForeignKey(v => v.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(d => d.Permissions)
                    .WithOne(p => p.Document)
                    .HasForeignKey(p => p.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentVersion>(entity =>
            {
                entity.HasIndex(e => new { e.DocumentId, e.VersionNumber }).IsUnique();
            });

            builder.Entity<DocumentVector>(entity =>
            {
                entity.HasIndex(e => new { e.OrgId, e.DocumentId, e.ChunkIndex }).IsUnique();
                entity.Property(e => e.Tags)
                    .HasConversion(stringDictionaryConverter)
                    .Metadata.SetValueComparer(stringDictionaryComparer);
                entity.Property(e => e.Embedding)
                    .HasConversion(floatArrayConverter)
                    .Metadata.SetValueComparer(floatArrayComparer);

                entity.HasOne(v => v.Version)
                .WithMany()
                    .HasForeignKey(v => v.VersionId)
                .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<DocumentPermission>(entity =>
            {
                entity.HasIndex(e => new { e.DocumentId, e.UserId }).IsUnique();
            });

            builder.Entity<RagQuery>(entity =>
            {
                entity.HasIndex(e => new { e.OrgId, e.CreatedAt });
                entity.Property(e => e.RetrievedVectorIds)
                    .HasConversion(guidListConverter)
                    .Metadata.SetValueComparer(guidListComparer);
                entity.Property(e => e.ContextJson)
                    .HasConversion(stringDictionaryConverter)
                    .Metadata.SetValueComparer(stringDictionaryComparer);
            });

            builder.Entity<ExternalConnection>(entity =>
            {
                entity.HasIndex(e => new { e.OrgId, e.UserId, e.Provider }).IsUnique();
                entity.Property(e => e.Scopes)
                    .HasConversion(stringListConverter)
                    .Metadata.SetValueComparer(stringListComparer);
            });

            builder.Entity<RagCacheEntry>(entity =>
            {
                entity.HasIndex(e => new { e.OrgId, e.UserId, e.QueryHash });
            });
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

        private void ConfigureEmailIntegrationRelationships(ModelBuilder builder)
        {
            // EmailAccount -> User relationship
            builder.Entity<EmailAccount>()
                .HasOne(ea => ea.User)
                .WithMany()
                .HasForeignKey(ea => ea.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // EmailAccount -> EmailMessages relationship
            builder.Entity<EmailAccount>()
                .HasMany(ea => ea.EmailMessages)
                .WithOne(em => em.EmailAccount)
                .HasForeignKey(em => em.EmailAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // EmailMessage -> DirectMessage relationship (optional)
            builder.Entity<EmailMessage>()
                .HasOne(em => em.DirectMessage)
                .WithMany()
                .HasForeignKey(em => em.DirectMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for performance
            // Note: Unique constraint on (UserId, IsActive) when IsActive = 1 is enforced at application level
            // as EF Core doesn't support filtered unique indexes well across all SQL providers
            builder.Entity<EmailAccount>()
                .HasIndex(ea => ea.UserId);

            builder.Entity<EmailAccount>()
                .HasIndex(ea => ea.EmailAddress);

            builder.Entity<EmailMessage>()
                .HasIndex(em => new { em.EmailAccountId, em.ReceivedAt });

            builder.Entity<EmailMessage>()
                .HasIndex(em => em.ExternalEmailId)
                .IsUnique();

            builder.Entity<EmailMessage>()
                .HasIndex(em => em.ThreadId);

            builder.Entity<EmailMessage>()
                .HasIndex(em => em.DirectMessageId);
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

        private void ConfigureAIUsageRelationships(ModelBuilder builder)
        {
            // AIUsage -> User relationship
            builder.Entity<AIUsage>()
                .HasOne(au => au.User)
                .WithMany()
                .HasForeignKey(au => au.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AIUsage -> Organization relationship
            builder.Entity<AIUsage>()
                .HasOne(au => au.Organization)
                .WithMany()
                .HasForeignKey(au => au.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // AIUsageDaily -> User relationship
            builder.Entity<AIUsageDaily>()
                .HasOne(aud => aud.User)
                .WithMany()
                .HasForeignKey(aud => aud.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AIUsageDaily -> Organization relationship
            builder.Entity<AIUsageDaily>()
                .HasOne(aud => aud.Organization)
                .WithMany()
                .HasForeignKey(aud => aud.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes for performance
            builder.Entity<AIUsage>()
                .HasIndex(au => new { au.UserId, au.CreatedAt });
            
            builder.Entity<AIUsage>()
                .HasIndex(au => new { au.OrganizationId, au.CreatedAt });
            
            builder.Entity<AIUsage>()
                .HasIndex(au => au.ModelName);

            // Unique constraint for daily aggregates (one per user per day)
            builder.Entity<AIUsageDaily>()
                .HasIndex(aud => new { aud.UserId, aud.OrganizationId, aud.Date })
                .IsUnique();
            
            builder.Entity<AIUsageDaily>()
                .HasIndex(aud => new { aud.OrganizationId, aud.Date });
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

        private void ConfigureAgentActionRelationships(ModelBuilder builder)
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            
            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, jsonSerializerOptions) ?? new List<string>());

            // AgentAction -> Organization relationship
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.Organization)
                .WithMany()
                .HasForeignKey(aa => aa.OrganizationId)
                .OnDelete(DeleteBehavior.NoAction);

            // AgentAction -> Matter relationship (optional)
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.Matter)
                .WithMany()
                .HasForeignKey(aa => aa.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // AgentAction -> ProposedBy relationship
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.ProposedBy)
                .WithMany()
                .HasForeignKey(aa => aa.ProposedById)
                .OnDelete(DeleteBehavior.SetNull);

            // AgentAction -> ApprovedBy relationship
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.ApprovedBy)
                .WithMany()
                .HasForeignKey(aa => aa.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);

            // AgentAction -> RejectedBy relationship
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.RejectedBy)
                .WithMany()
                .HasForeignKey(aa => aa.RejectedById)
                .OnDelete(DeleteBehavior.SetNull);

            // AgentAction -> RolledBackBy relationship
            builder.Entity<AgentAction>()
                .HasOne(aa => aa.RolledBackBy)
                .WithMany()
                .HasForeignKey(aa => aa.RolledBackById)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for performance
            builder.Entity<AgentAction>()
                .HasIndex(aa => aa.RunId)
                .IsUnique();

            builder.Entity<AgentAction>()
                .HasIndex(aa => aa.CorrelationId);

            builder.Entity<AgentAction>()
                .HasIndex(aa => new { aa.OrganizationId, aa.Status, aa.CreatedAt });

            builder.Entity<AgentAction>()
                .HasIndex(aa => new { aa.Status, aa.CreatedAt });

            builder.Entity<AgentAction>()
                .HasIndex(aa => aa.MatterId);

            builder.Entity<AgentAction>()
                .HasIndex(aa => aa.ActionType);
        }

        private void ConfigureUnifiedInboxRelationships(ModelBuilder builder)
        {
            var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            
            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, jsonSerializerOptions) ?? new List<string>());

            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => ReferenceEquals(c1, c2) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                c => c == null ? new List<string>() : c.ToList());

            // InboxItem -> Organization relationship
            builder.Entity<InboxItem>()
                .HasOne(ii => ii.Organization)
                .WithMany()
                .HasForeignKey(ii => ii.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // InboxItem -> Matter relationship (optional)
            builder.Entity<InboxItem>()
                .HasOne(ii => ii.Matter)
                .WithMany()
                .HasForeignKey(ii => ii.MatterId)
                .OnDelete(DeleteBehavior.SetNull);

            // InboxItem -> User relationship (optional)
            builder.Entity<InboxItem>()
                .HasOne(ii => ii.User)
                .WithMany()
                .HasForeignKey(ii => ii.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // InboxItem -> Messages relationship
            builder.Entity<InboxItem>()
                .HasMany(ii => ii.Messages)
                .WithOne(im => im.InboxItem)
                .HasForeignKey(im => im.InboxItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // InboxItem Labels conversion
            builder.Entity<InboxItem>()
                .Property(ii => ii.Labels)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            // InboxMessage -> SenderUser relationship
            builder.Entity<InboxMessage>()
                .HasOne(im => im.SenderUser)
                .WithMany()
                .HasForeignKey(im => im.SenderUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for InboxItem
            builder.Entity<InboxItem>()
                .HasIndex(ii => new { ii.OrganizationId, ii.Status, ii.LastMessageAt });

            builder.Entity<InboxItem>()
                .HasIndex(ii => new { ii.OrganizationId, ii.Source, ii.LastMessageAt });

            builder.Entity<InboxItem>()
                .HasIndex(ii => ii.ThreadId);

            builder.Entity<InboxItem>()
                .HasIndex(ii => ii.ExternalId);

            builder.Entity<InboxItem>()
                .HasIndex(ii => ii.MatterId);

            builder.Entity<InboxItem>()
                .HasIndex(ii => new { ii.IsDeleted, ii.LastMessageAt });

            // Indexes for InboxMessage
            builder.Entity<InboxMessage>()
                .HasIndex(im => new { im.InboxItemId, im.CreatedAt });

            builder.Entity<InboxMessage>()
                .HasIndex(im => im.ExternalMessageId);
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
