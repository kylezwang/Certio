using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Certio.Application.DTOs;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Certio.Application.Services;

/// <summary>
/// Comprehensive user data context service that builds RAG context from ALL user-scoped data
/// across Matters, Tasks, Calendar, Communications, Teams, and other modules
/// </summary>
public class UserDataContextService : IUserDataContextService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserDataContextService> _logger;
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UserDataContextService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<UserDataContextService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AIAgentService");
    }

    public async Task<UserDataContextResult> BuildUserDataContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken = default)
    {
        var queryId = Guid.NewGuid();
        _logger.LogInformation("Building user data context for User {UserId} in Org {OrgId}, Query: {Query}", 
            request.UserId, request.OrganizationId, request.Query);

        var moduleData = new Dictionary<string, UserModuleData>();
        var modules = request.IncludeModules ?? new List<string> 
        { 
            "matters", "tasks", "calendar", "communications", "clients", "teams", "documents"
        };

        // Build context from each module based on user permissions
        foreach (var module in modules)
        {
            try
            {
                var data = module switch
                {
                    "matters" => await BuildMattersContextAsync(request, cancellationToken),
                    "tasks" => await BuildTasksContextAsync(request, cancellationToken),
                    "calendar" => await BuildCalendarContextAsync(request, cancellationToken),
                    "communications" => await BuildCommunicationsContextAsync(request, cancellationToken),
                    "clients" => await BuildClientsContextAsync(request, cancellationToken),
                    "teams" => await BuildTeamsContextAsync(request, cancellationToken),
                    "documents" => await BuildDocumentsContextAsync(request, cancellationToken),
                    _ => null
                };

                if (data != null && data.TotalItems > 0)
                {
                    moduleData[module] = data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build context for module {Module}", module);
            }
        }

        // Calculate relevance scores based on query
        RankChunksByRelevance(moduleData, request.Query);

        var metadata = new Dictionary<string, object?>
        {
            ["totalModules"] = moduleData.Count,
            ["totalChunks"] = moduleData.Values.Sum(m => m.Chunks.Count),
            ["queryLength"] = request.Query.Length,
            ["topK"] = request.TopK,
            ["matterId"] = request.MatterId
        };

        return new UserDataContextResult(
            queryId,
            request.UserId,
            request.OrganizationId,
            request.Query,
            moduleData,
            metadata,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Determine whether the user can access the specified organization, either by direct membership
    /// or via law-firm-to-client relationships (one-way access).
    /// </summary>
    private async Task<bool> UserHasAccessToOrganizationAsync(
        int userId,
        int organizationId,
        CancellationToken cancellationToken)
    {
        var userOrgIds = await _dbContext.UserOrganizations
            .AsNoTracking()
            .Where(uo => uo.UserId == userId)
            .Select(uo => uo.OrganizationId)
            .ToListAsync(cancellationToken);

        if (userOrgIds.Contains(organizationId))
        {
            return true;
        }

        var hasLawFirmAccess = await _dbContext.OrganizationRelationships
            .AsNoTracking()
            .AnyAsync(r =>
                r.RelationshipType == "LawFirmClient" &&
                r.IsActive &&
                !r.IsDeleted &&
                r.TargetOrganizationId == organizationId &&
                userOrgIds.Contains(r.SourceOrganizationId),
                cancellationToken);

        if (hasLawFirmAccess)
        {
            _logger.LogInformation("Law-firm access: User {UserId} can view client org {OrgId}", userId, organizationId);
        }

        return hasLawFirmAccess;
    }

    /// <summary>
    /// Get all organization IDs accessible to the user (current org + client orgs if law firm)
    /// ONE-WAY ACCESS: Law firms can access client data, but clients CANNOT access law firm data
    /// </summary>
    private async Task<List<int>> GetAccessibleOrganizationIdsAsync(
        int organizationId, 
        CancellationToken cancellationToken)
    {
        var accessibleOrgIds = new List<int> { organizationId };
        
        // ONE-WAY RELATIONSHIP: Only if this org is the SOURCE (law firm) do we include targets (clients)
        // If this org is a TARGET (client), we do NOT include the source (law firm)
        // This ensures: Law Firm → Client ✅, Client → Law Firm ❌
        var clientOrgRelationships = await _dbContext.OrganizationRelationships
            .AsNoTracking()
            .Where(r => r.SourceOrganizationId == organizationId &&  // CRITICAL: Only when we are the SOURCE
                       r.RelationshipType == "LawFirmClient" &&
                       r.IsActive &&
                       !r.IsDeleted)
            .ToListAsync(cancellationToken);
        
        if (clientOrgRelationships.Any())
        {
            var clientOrgIds = clientOrgRelationships.Select(r => r.TargetOrganizationId).ToList();
            accessibleOrgIds.AddRange(clientOrgIds);
            _logger.LogInformation("Law firm (Org {OrgId}) - including {Count} client organizations: {ClientIds}", 
                organizationId, clientOrgIds.Count, string.Join(", ", clientOrgIds));
        }
        
        return accessibleOrgIds;
    }

    private async Task<UserModuleData> BuildMattersContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        // Check user has access to matters (via direct membership or law-firm access)
        var userHasAccess = await UserHasAccessToOrganizationAsync(request.UserId, request.OrganizationId, cancellationToken);

        if (!userHasAccess)
        {
            _logger.LogWarning("User {UserId} does NOT have access to org {OrgId} matters", request.UserId, request.OrganizationId);
            return new UserModuleData("matters", 0, new List<UserDataChunk>(), new Dictionary<string, object?>());
        }

        // Get accessible organization IDs (current org + client orgs if law firm)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(request.OrganizationId, cancellationToken);

        // Build query for matters from all accessible organizations
        var query = _dbContext.Matters
            .AsNoTracking()
            .Include(m => m.Assignments)
                .ThenInclude(a => a.User)
            .Include(m => m.TaskItems)
            .Include(m => m.Organization)  // Include org to show which client it belongs to
            .Where(m => accessibleOrgIds.Contains(m.OrganizationId) && !m.IsDeleted);

        if (request.MatterId.HasValue)
        {
            query = query.Where(m => m.Id == request.MatterId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(m => m.ModifiedAt >= request.FromDate.Value || m.CreatedAt >= request.FromDate.Value);
        }

        var matters = await query
            .OrderByDescending(m => m.ModifiedAt ?? m.CreatedAt)
            .Take(100)  // Increased limit to include client matters
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} matters for User {UserId} across {OrgCount} organizations", 
            matters.Count, request.UserId, accessibleOrgIds.Count);
        
        if (matters.Any())
        {
            _logger.LogDebug("Matter titles: {Titles}", 
                string.Join(", ", matters.Select(m => $"{m.Title} (Org:{m.OrganizationId})")));
        }

        var chunks = new List<UserDataChunk>();
        foreach (var matter in matters)
        {
            var content = BuildMatterContent(matter);
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "matters",
                EntityType: "Matter",
                EntityId: matter.Id,
                Title: matter.Title,
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["matterId"] = matter.Id,
                    ["organizationId"] = matter.OrganizationId,
                    ["organizationName"] = matter.Organization?.Name,
                    ["status"] = matter.Status,
                    ["practiceArea"] = matter.PracticeArea,
                    ["dueDate"] = matter.DueDate,
                    ["tasksCompleted"] = matter.TasksCompleted,
                    ["totalTasks"] = matter.TotalTasks,
                    ["assignees"] = matter.Assignments.Select(a => $"{a.User?.FirstName} {a.User?.LastName}").ToList(),
                    ["clientGoals"] = matter.ClientGoals,
                    ["legalRequirements"] = matter.LegalRequirements
                },
                CreatedAt: matter.CreatedAt,
                ModifiedAt: matter.ModifiedAt
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalMatters"] = matters.Count,
            ["activeMatters"] = matters.Count(m => m.Status == "InProgress" || m.Status == "Planning"),
            ["completedMatters"] = matters.Count(m => m.Status == "Completed"),
            ["practiceAreas"] = matters.Select(m => m.PracticeArea).Distinct().ToList()
        };

        return new UserModuleData("matters", matters.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildTasksContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        // Get accessible organization IDs (same as matters - include client orgs)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(request.OrganizationId, cancellationToken);
        
        var query = _dbContext.TaskItems
            .AsNoTracking()
            .Include(t => t.TaskAssignments)
                .ThenInclude(a => a.User)
            .Include(t => t.Matter)
            .Where(t => accessibleOrgIds.Contains(t.OrgId) && !t.IsDeleted);

        if (request.MatterId.HasValue)
        {
            query = query.Where(t => t.MatterId == request.MatterId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(t => t.ModifiedAt >= request.FromDate.Value || t.CreatedAt >= request.FromDate.Value);
        }

        // Filter to tasks user has access to (assigned or matter access)
        var tasks = await query
            .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt)
            .Take(100)  // More tasks since they're smaller
            .ToListAsync(cancellationToken);

        var chunks = new List<UserDataChunk>();
        foreach (var task in tasks)
        {
            var content = BuildTaskContent(task);
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "tasks",
                EntityType: "Task",
                EntityId: task.Id,
                Title: task.Title,
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["status"] = task.Status,
                    ["priority"] = task.Priority,
                    ["dueDate"] = task.DueDate,
                    ["matterId"] = task.MatterId,
                    ["matterTitle"] = task.Matter?.Title,
                    ["assignees"] = task.TaskAssignments.Select(a => $"{a.User?.FirstName} {a.User?.LastName}").ToList()
                },
                CreatedAt: task.CreatedAt,
                ModifiedAt: task.ModifiedAt
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalTasks"] = tasks.Count,
            ["pendingTasks"] = tasks.Count(t => t.Status == "Pending"),
            ["inProgressTasks"] = tasks.Count(t => t.Status == "InProgress"),
            ["completedTasks"] = tasks.Count(t => t.Status == "Completed"),
            ["highPriorityTasks"] = tasks.Count(t => t.Priority == "High" || t.Priority == "Critical")
        };

        return new UserModuleData("tasks", tasks.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildCalendarContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var futureLimit = now.AddMonths(3);

        // Get accessible organization IDs (include client orgs)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(request.OrganizationId, cancellationToken);

        var query = _dbContext.CalendarEvents
            .AsNoTracking()
            .Include(e => e.Attendees)
                .ThenInclude(a => a.User)
            .Where(e => accessibleOrgIds.Contains(e.OrgId) && !e.IsDeleted);

        if (request.MatterId.HasValue)
        {
            query = query.Where(e => e.MatterId == request.MatterId.Value);
        }

        // Focus on upcoming and recent events
        query = query.Where(e => e.StartDateTime >= now.AddMonths(-1) && e.StartDateTime <= futureLimit);

        var events = await query
            .OrderBy(e => e.StartDateTime)
            .Take(50)
            .ToListAsync(cancellationToken);

        var chunks = new List<UserDataChunk>();
        foreach (var evt in events)
        {
            var content = BuildCalendarEventContent(evt);
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "calendar",
                EntityType: "CalendarEvent",
                EntityId: evt.Id,
                Title: evt.Title,
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["startDateTime"] = evt.StartDateTime,
                    ["endDateTime"] = evt.EndDateTime,
                    ["eventType"] = evt.EventType,
                    ["location"] = evt.Location,
                    ["isAllDay"] = evt.IsAllDayEvent,
                    ["attendees"] = evt.Attendees.Select(a => $"{a.User?.FirstName} {a.User?.LastName}").ToList(),
                    ["matterId"] = evt.MatterId
                },
                CreatedAt: evt.CreatedAt,
                ModifiedAt: evt.ModifiedAt
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalEvents"] = events.Count,
            ["upcomingEvents"] = events.Count(e => e.StartDateTime >= now),
            ["todayEvents"] = events.Count(e => e.StartDateTime.Date == now.Date),
            ["thisWeekEvents"] = events.Count(e => e.StartDateTime >= now && e.StartDateTime <= now.AddDays(7))
        };

        return new UserModuleData("calendar", events.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildCommunicationsContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        var chunks = new List<UserDataChunk>();
        
        // Get accessible organization IDs (include client orgs)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(request.OrganizationId, cancellationToken);
        
        // PART 1: Get recent channel messages (actual Communications module messages between users)
        var channelMessagesQuery = _dbContext.ChatMessages
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Conversation)
            .Where(m => m.IsChannelMessage && 
                        m.Conversation != null && 
                        accessibleOrgIds.Contains(m.Conversation.OrganizationId) &&
                        !m.IsDeleted);

        if (request.MatterId.HasValue)
        {
            channelMessagesQuery = channelMessagesQuery.Where(m => m.Conversation!.MatterId == request.MatterId.Value);
        }

        if (request.FromDate.HasValue)
        {
            channelMessagesQuery = channelMessagesQuery.Where(m => m.CreatedAt >= request.FromDate.Value);
        }

        // Get recent messages, grouped by channel
        var channelMessages = await channelMessagesQuery
            .OrderByDescending(m => m.CreatedAt)
            .Take(200)  // Get more messages since they're smaller
            .ToListAsync(cancellationToken);

        // Group channel messages by channel
        var messagesByChannel = channelMessages
            .GroupBy(m => new { m.ChannelId, ChannelName = m.Conversation?.Title ?? "Unknown Channel" })
            .ToList();

        foreach (var channelGroup in messagesByChannel)
        {
            var messages = channelGroup.OrderByDescending(m => m.CreatedAt).Take(50).ToList();
            var content = BuildChannelMessagesContent(channelGroup.Key.ChannelName, messages);
            
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "communications",
                EntityType: "Channel",
                EntityId: channelGroup.Key.ChannelId ?? 0,
                Title: $"Channel: {channelGroup.Key.ChannelName}",
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["channelId"] = channelGroup.Key.ChannelId,
                    ["channelName"] = channelGroup.Key.ChannelName,
                    ["messageCount"] = messages.Count,
                    ["participants"] = messages.Select(m => $"{m.User?.FirstName} {m.User?.LastName}").Distinct().ToList(),
                    ["lastMessageAt"] = messages.FirstOrDefault()?.CreatedAt,
                    ["communicationType"] = "channel"
                },
                CreatedAt: messages.LastOrDefault()?.CreatedAt ?? DateTime.UtcNow,
                ModifiedAt: messages.FirstOrDefault()?.CreatedAt
            ));
        }

        // PART 2: Get direct message threads (include client org DMs)
        var dmThreadsQuery = _dbContext.DirectThreads
            .AsNoTracking()
            .Include(t => t.UserA)
            .Include(t => t.UserB)
            .Include(t => t.Messages.OrderByDescending(m => m.CreatedAt).Take(50))
                .ThenInclude(m => m.Sender)
            .Where(t => accessibleOrgIds.Contains(t.OrganizationId) && 
                       !t.IsDeleted &&
                       (t.UserAId == request.UserId || t.UserBId == request.UserId));

        if (request.FromDate.HasValue)
        {
            dmThreadsQuery = dmThreadsQuery.Where(t => t.LastMessageAt >= request.FromDate.Value || t.CreatedAt >= request.FromDate.Value);
        }

        var dmThreads = await dmThreadsQuery
            .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
            .Take(20)  // Limit DM threads
            .ToListAsync(cancellationToken);

        foreach (var thread in dmThreads)
        {
            var otherUser = thread.UserAId == request.UserId ? thread.UserB : thread.UserA;
            var content = BuildDirectThreadContent(otherUser, thread.Messages.ToList());
            
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "communications",
                EntityType: "DirectThread",
                EntityId: 0,  // DirectThread uses Guid, not int
                Title: $"DM with {otherUser?.FirstName} {otherUser?.LastName}",
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["threadId"] = thread.Id.ToString(),
                    ["otherUserId"] = otherUser?.Id,
                    ["otherUserName"] = $"{otherUser?.FirstName} {otherUser?.LastName}",
                    ["messageCount"] = thread.Messages.Count,
                    ["lastMessageAt"] = thread.LastMessageAt,
                    ["communicationType"] = "direct_message"
                },
                CreatedAt: thread.CreatedAt,
                ModifiedAt: thread.LastMessageAt ?? thread.CreatedAt
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalChannels"] = messagesByChannel.Count,
            ["totalChannelMessages"] = channelMessages.Count,
            ["totalDMThreads"] = dmThreads.Count,
            ["totalDMMessages"] = dmThreads.Sum(t => t.Messages.Count),
            ["uniqueParticipants"] = channelMessages.Select(m => m.UserId).Distinct().Count()
        };

        return new UserModuleData("communications", chunks.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildClientsContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        // Get client information (users with Client role or related organizations)
        var clientUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserOrganizations.Any(uo => uo.OrganizationId == request.OrganizationId))
            .Take(50)
            .ToListAsync(cancellationToken);

        var chunks = new List<UserDataChunk>();
        foreach (var client in clientUsers)
        {
            var content = BuildClientContent(client);
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "clients",
                EntityType: "User",
                EntityId: client.Id,
                Title: $"{client.FirstName} {client.LastName}",
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["email"] = client.Email,
                    ["phoneNumber"] = client.PhoneNumber,
                    ["company"] = client.Company,
                    ["jobTitle"] = client.JobTitle
                },
                CreatedAt: client.CreatedAt,
                ModifiedAt: null
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalClients"] = clientUsers.Count,
            ["activeClients"] = clientUsers.Count(u => u.IsActive)
        };

        return new UserModuleData("clients", clientUsers.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildTeamsContextAsync(
        UserDataContextRequest request, 
        CancellationToken cancellationToken)
    {
        var teams = await _dbContext.Teams
            .AsNoTracking()
            .Include(t => t.Memberships)
                .ThenInclude(m => m.User)
            .Where(t => t.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);

        var chunks = new List<UserDataChunk>();
        foreach (var team in teams)
        {
            var content = BuildTeamContent(team);
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "teams",
                EntityType: "Team",
                EntityId: team.Id,
                Title: team.Name,
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["description"] = team.Description,
                    ["memberCount"] = team.Memberships.Count,
                    ["members"] = team.Memberships.Select(m => $"{m.User?.FirstName} {m.User?.LastName}").ToList()
                },
                CreatedAt: team.CreatedAt,
                ModifiedAt: null
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalTeams"] = teams.Count,
            ["totalMembers"] = teams.Sum(t => t.Memberships.Count)
        };

        return new UserModuleData("teams", teams.Count, chunks, summary);
    }

    private async Task<UserModuleData> BuildDocumentsContextAsync(
        UserDataContextRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("BuildDocumentsContextAsync: Starting for User {UserId} Org {OrgId}", request.UserId, request.OrganizationId);
        
        // Get accessible organization IDs (include client orgs for law firms)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(request.OrganizationId, cancellationToken);
        
        // Convert int org IDs to Guid format used by Documents
        var accessibleOrgGuids = accessibleOrgIds
            .Select(id => CreateDeterministicGuid("certio:organization", id))
            .ToList();
        
        _logger.LogInformation("BuildDocumentsContextAsync: Searching for documents with OrgIds: {OrgGuids}", string.Join(", ", accessibleOrgGuids.Take(5)));
        
        // Debug: Check total documents in DB
        var totalDocs = await _dbContext.Documents.CountAsync(cancellationToken);
        var deletedDocs = await _dbContext.Documents.CountAsync(d => d.DeletedAt != null, cancellationToken);
        var sampleOrgIds = await _dbContext.Documents
            .AsNoTracking()
            .GroupBy(d => d.OrgId)
            .OrderByDescending(g => g.Count())
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .Take(5)
            .ToListAsync(cancellationToken);
        _logger.LogInformation("BuildDocumentsContextAsync: Total docs in DB: {Total}, Deleted: {Deleted}, TopOrgIds: {OrgCounts}",
            totalDocs,
            deletedDocs,
            string.Join("; ", sampleOrgIds.Select(x => $"{x.OrgId}:{x.Count}")));

        var documents = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .Where(d => accessibleOrgGuids.Contains(d.OrgId) && d.DeletedAt == null)
            .OrderByDescending(d => d.ModifiedAt)
            .Take(50)  // Recent documents
            .ToListAsync(cancellationToken);
        
        _logger.LogInformation("BuildDocumentsContextAsync: Found {Count} documents for {OrgCount} orgs", documents.Count, accessibleOrgGuids.Count);

        // If matter-specific request, also get documents linked to that matter
        if (request.MatterId.HasValue)
        {
            var matterGuid = CreateDeterministicGuid("certio:matter", request.MatterId.Value);
            var matterDocs = await _dbContext.Documents
                .AsNoTracking()
                .Include(d => d.Versions)
                .Where(d => d.MatterId == matterGuid && d.DeletedAt == null)
                .OrderByDescending(d => d.ModifiedAt)
                .Take(20)
                .ToListAsync(cancellationToken);
            
            documents = documents.Union(matterDocs, new DocumentComparer()).ToList();
        }

        var chunks = new List<UserDataChunk>();
        
        // Also try to get document vector content if available (contains extracted text)
        foreach (var doc in documents)
        {
            // Get extracted content from vector store if available
            var extractedContent = await GetDocumentExtractedContentAsync(doc.Id, cancellationToken);
            _logger.LogDebug("Document {DocId} ({Title}): ExtractedContent={HasContent} ({Length} chars)", 
                doc.Id, doc.Title, !string.IsNullOrEmpty(extractedContent), extractedContent?.Length ?? 0);
            var content = BuildDocumentContent(doc, extractedContent);
            
            chunks.Add(new UserDataChunk(
                Id: Guid.NewGuid(),
                ModuleName: "documents",
                EntityType: "Document",
                EntityId: 0, // Guid-based ID, use 0 as placeholder
                Title: doc.Title,
                Content: content,
                Metadata: new Dictionary<string, object?>
                {
                    ["documentId"] = doc.Id.ToString(),
                    ["fileType"] = doc.FileType,
                    ["category"] = doc.Category,
                    ["status"] = doc.Status.ToString(),
                    ["sourceType"] = doc.SourceType.ToString(),
                    ["hasExtractedContent"] = !string.IsNullOrEmpty(extractedContent),
                    ["matterId"] = doc.MatterId?.ToString(),
                    ["modifiedAt"] = doc.ModifiedAt,
                    ["tags"] = doc.Tags
                },
                CreatedAt: doc.CreatedAt,
                ModifiedAt: doc.ModifiedAt
            ));
        }

        var summary = new Dictionary<string, object?>
        {
            ["totalDocuments"] = documents.Count,
            ["documentsByType"] = documents.GroupBy(d => d.FileType).ToDictionary(g => g.Key ?? "unknown", g => g.Count()),
            ["documentsWithContent"] = chunks.Count(c => c.Metadata.ContainsKey("hasExtractedContent") && (bool)c.Metadata["hasExtractedContent"]!)
        };

        _logger.LogInformation("BuildDocumentsContextAsync: Returning {ChunkCount} document chunks, {WithContent} with extracted content", chunks.Count, summary["documentsWithContent"]);
        
        return new UserModuleData("documents", documents.Count, chunks, summary);
    }

    private async Task<string?> GetDocumentExtractedContentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            // Get document vectors which contain extracted/chunked content
            var vectors = await _dbContext.DocumentVectors
                .AsNoTracking()
                .Where(v => v.DocumentId == documentId)
                .OrderBy(v => v.ChunkIndex)
                .Take(10) // First 10 chunks to limit size
                .ToListAsync(cancellationToken);

            if (vectors.Any())
            {
                // Combine chunk content
                return string.Join("\n\n", vectors.Select(v => v.ContentChunk));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve extracted content for document {DocumentId}", documentId);
        }
        
        return null;
    }

    private static Guid CreateDeterministicGuid(string prefix, int id)
    {
        var input = $"{prefix}:{id}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // version 4
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(guidBytes);
    }

    private class DocumentComparer : IEqualityComparer<Certio.Domain.Documents.Document>
    {
        public bool Equals(Certio.Domain.Documents.Document? x, Certio.Domain.Documents.Document? y) => x?.Id == y?.Id;
        public int GetHashCode(Certio.Domain.Documents.Document obj) => obj.Id.GetHashCode();
    }

    #region Content Builders

    private string BuildMatterContent(Certio.Domain.Matters.Matter matter)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== MATTER: {matter.Title} ===");
        sb.AppendLine($"Matter ID: {matter.Id}");
        
        // Show organization name if available (important for client matters)
        if (matter.Organization != null)
            sb.AppendLine($"Client/Organization: {matter.Organization.Name}");
        
        sb.AppendLine($"Status: {matter.Status}");
        
        // CRITICAL: Practice Area - must be explicit
        if (!string.IsNullOrEmpty(matter.PracticeArea))
            sb.AppendLine($"Practice Area: {matter.PracticeArea}");
        else
            sb.AppendLine("Practice Area: Not specified");
        
        if (!string.IsNullOrEmpty(matter.Description))
            sb.AppendLine($"Description: {matter.Description}");
        
        if (!string.IsNullOrEmpty(matter.ClientGoals))
            sb.AppendLine($"Client Goals: {matter.ClientGoals}");
        
        if (!string.IsNullOrEmpty(matter.LegalRequirements))
            sb.AppendLine($"Legal Requirements: {matter.LegalRequirements}");
        
        if (matter.StartDate.HasValue)
            sb.AppendLine($"Start Date: {matter.StartDate.Value:yyyy-MM-dd}");
        
        if (matter.DueDate.HasValue)
            sb.AppendLine($"Due Date: {matter.DueDate.Value:yyyy-MM-dd}");
        
        if (matter.PendingDate.HasValue)
            sb.AppendLine($"Pending Date: {matter.PendingDate.Value:yyyy-MM-dd}");
        
        if (matter.StatuteOfLimitationsDate.HasValue)
            sb.AppendLine($"Statute of Limitations: {matter.StatuteOfLimitationsDate.Value:yyyy-MM-dd}");
        
        sb.AppendLine($"Tasks Progress: {matter.TasksCompleted}/{matter.TotalTasks} tasks completed");
        
        // Detailed team information with assignment types
        if (matter.Assignments.Any())
        {
            sb.AppendLine("Team Members:");
            foreach (var assignment in matter.Assignments)
            {
                var userName = $"{assignment.User?.FirstName} {assignment.User?.LastName}".Trim();
                sb.AppendLine($"  - {userName} ({assignment.AssignmentType})");
            }
        }
        
        if (!string.IsNullOrEmpty(matter.Notes))
            sb.AppendLine($"Additional Notes: {matter.Notes}");
        
        if (matter.IsAIGenerated)
            sb.AppendLine($"AI Generated: Yes (Agent: {matter.AIAgentType}, Approval: {matter.ApprovalStatus})");
        
        sb.AppendLine($"Created: {matter.CreatedAt:yyyy-MM-dd}");
        if (matter.ModifiedAt.HasValue)
            sb.AppendLine($"Last Modified: {matter.ModifiedAt.Value:yyyy-MM-dd}");
        
        return sb.ToString();
    }

    private string BuildTaskContent(Certio.Domain.Tasks.TaskItem task)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Task: {task.Title}");
        sb.AppendLine($"Status: {task.Status}");
        sb.AppendLine($"Priority: {task.Priority}");
        
        if (!string.IsNullOrEmpty(task.Description))
            sb.AppendLine($"Description: {task.Description}");
        
        if (task.Matter != null)
            sb.AppendLine($"Related Matter: {task.Matter.Title}");
        
        if (task.DueDate.HasValue)
            sb.AppendLine($"Due Date: {task.DueDate.Value:yyyy-MM-dd}");
        
        if (task.TaskAssignments.Any())
        {
            sb.AppendLine($"Assigned to: {string.Join(", ", task.TaskAssignments.Select(a => $"{a.User?.FirstName} {a.User?.LastName}"))}");
        }
        
        return sb.ToString();
    }

    private string BuildDocumentContent(Certio.Domain.Documents.Document doc, string? extractedContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== DOCUMENT: {doc.Title} ===");
        sb.AppendLine($"Document ID: {doc.Id}");
        sb.AppendLine($"Type: {doc.FileType}");
        sb.AppendLine($"Source: {doc.SourceType}");
        sb.AppendLine($"Status: {doc.Status}");
        
        if (!string.IsNullOrEmpty(doc.Category))
            sb.AppendLine($"Category: {doc.Category}");
        
        if (doc.Metadata.TryGetValue("description", out var descriptionValue) && !string.IsNullOrWhiteSpace(descriptionValue))
            sb.AppendLine($"Description: {descriptionValue}");
        
        if (doc.Tags != null && doc.Tags.Any())
            sb.AppendLine($"Tags: {string.Join(", ", doc.Tags)}");
        
        sb.AppendLine($"Created: {doc.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Last Modified: {doc.ModifiedAt:yyyy-MM-dd HH:mm}");
        
        // Include the actual extracted content if available
        if (!string.IsNullOrEmpty(extractedContent))
        {
            sb.AppendLine("");
            sb.AppendLine("=== DOCUMENT CONTENT ===");
            // Limit content to prevent token explosion but include meaningful amount
            var contentToInclude = extractedContent.Length > 8000 
                ? extractedContent.Substring(0, 8000) + "\n[... content truncated ...]" 
                : extractedContent;
            sb.AppendLine(contentToInclude);
            sb.AppendLine("=== END CONTENT ===");
        }
        else
        {
            sb.AppendLine("");
            sb.AppendLine("[Document content not yet extracted - request specific document analysis to trigger extraction]");
        }
        
        return sb.ToString();
    }

    private string BuildCalendarEventContent(Certio.Domain.Calendar.CalendarEvent evt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Event: {evt.Title}");
        sb.AppendLine($"Type: {evt.EventType}");
        sb.AppendLine($"Start: {evt.StartDateTime:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"End: {evt.EndDateTime:yyyy-MM-dd HH:mm}");
        
        if (!string.IsNullOrEmpty(evt.Location))
            sb.AppendLine($"Location: {evt.Location}");
        
        if (!string.IsNullOrEmpty(evt.Description))
            sb.AppendLine($"Description: {evt.Description}");
        
        if (evt.Attendees.Any())
        {
            sb.AppendLine($"Attendees: {string.Join(", ", evt.Attendees.Select(a => $"{a.User?.FirstName} {a.User?.LastName}"))}");
        }
        
        return sb.ToString();
    }

    private string BuildChannelMessagesContent(string channelName, List<Certio.Domain.Services.ChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Channel: {channelName}");
        sb.AppendLine($"Total Messages: {messages.Count}");
        sb.AppendLine("");
        sb.AppendLine("Recent Messages (newest first):");
        sb.AppendLine("");
        
        foreach (var msg in messages.Take(30))  // Include more messages for better context
        {
            var userName = $"{msg.User?.FirstName} {msg.User?.LastName}".Trim();
            if (string.IsNullOrEmpty(userName))
            {
                userName = "Unknown User";
            }
            
            var timestamp = msg.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            var content = msg.Content ?? "";
            
            sb.AppendLine($"[{timestamp}] {userName}: {content}");
        }
        
        return sb.ToString();
    }

    private string BuildDirectThreadContent(Certio.Domain.Users.User? otherUser, List<Certio.Domain.Services.DirectMessage> messages)
    {
        var sb = new StringBuilder();
        var otherUserName = otherUser != null ? $"{otherUser.FirstName} {otherUser.LastName}" : "Unknown User";
        
        sb.AppendLine($"Direct Message Thread with: {otherUserName}");
        sb.AppendLine($"Total Messages: {messages.Count}");
        sb.AppendLine("");
        sb.AppendLine("Recent Messages (newest first):");
        sb.AppendLine("");
        
        foreach (var msg in messages.Take(30))
        {
            var senderName = $"{msg.Sender?.FirstName} {msg.Sender?.LastName}".Trim();
            if (string.IsNullOrEmpty(senderName))
            {
                senderName = "Unknown User";
            }
            
            var timestamp = msg.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            var content = msg.Body ?? "";
            
            sb.AppendLine($"[{timestamp}] {senderName}: {content}");
        }
        
        return sb.ToString();
    }

    private string BuildClientContent(Certio.Domain.Users.User client)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Client: {client.FirstName} {client.LastName}");
        sb.AppendLine($"Email: {client.Email}");
        
        if (!string.IsNullOrEmpty(client.PhoneNumber))
            sb.AppendLine($"Phone: {client.PhoneNumber}");
        
        if (!string.IsNullOrEmpty(client.Company))
            sb.AppendLine($"Company: {client.Company}");
        
        if (!string.IsNullOrEmpty(client.JobTitle))
            sb.AppendLine($"Job Title: {client.JobTitle}");
        
        return sb.ToString();
    }

    private string BuildTeamContent(Certio.Domain.Teams.Team team)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Team: {team.Name}");
        
        if (!string.IsNullOrEmpty(team.Description))
            sb.AppendLine($"Description: {team.Description}");
        
        if (team.Memberships.Any())
        {
            sb.AppendLine($"Members: {string.Join(", ", team.Memberships.Select(m => $"{m.User?.FirstName} {m.User?.LastName}"))}");
        }
        
        return sb.ToString();
    }

    #endregion

    private void RankChunksByRelevance(Dictionary<string, UserModuleData> moduleData, string query)
    {
        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var module in moduleData.Values)
        {
            for (int i = 0; i < module.Chunks.Count; i++)
            {
                var chunk = module.Chunks[i];
                var contentLower = (chunk.Title + " " + chunk.Content).ToLowerInvariant();
                
                // Simple relevance scoring
                var score = 0.0f;
                foreach (var word in queryWords)
                {
                    if (contentLower.Contains(word))
                        score += 1.0f;
                }
                
                // Boost recent items
                var daysSinceModified = (DateTime.UtcNow - (chunk.ModifiedAt ?? chunk.CreatedAt)).TotalDays;
                if (daysSinceModified < 7)
                    score *= 1.5f;
                else if (daysSinceModified < 30)
                    score *= 1.2f;
                
                // Update score
                module.Chunks[i] = chunk with { RelevanceScore = score };
            }
            
            // Sort chunks by relevance (create new sorted list)
            var sortedChunks = module.Chunks
                .OrderByDescending(c => c.RelevanceScore)
                .ToList();
            
            module.Chunks.Clear();
            module.Chunks.AddRange(sortedChunks);
        }
    }

    public async Task<Dictionary<string, object>> GetUserDataSummaryAsync(
        int userId, 
        int organizationId, 
        CancellationToken cancellationToken = default)
    {
        var summary = new Dictionary<string, object>();

        // Get accessible organization IDs (include client orgs)
        var accessibleOrgIds = await GetAccessibleOrganizationIdsAsync(organizationId, cancellationToken);

        // Get counts for all modules across accessible organizations
        summary["mattersCount"] = await _dbContext.Matters
            .CountAsync(m => accessibleOrgIds.Contains(m.OrganizationId) && !m.IsDeleted, cancellationToken);
        
        summary["tasksCount"] = await _dbContext.TaskItems
            .CountAsync(t => accessibleOrgIds.Contains(t.OrgId) && !t.IsDeleted, cancellationToken);
        
        summary["calendarEventsCount"] = await _dbContext.CalendarEvents
            .CountAsync(e => accessibleOrgIds.Contains(e.OrgId) && !e.IsDeleted, cancellationToken);
        
        summary["conversationsCount"] = await _dbContext.Conversations
            .CountAsync(c => accessibleOrgIds.Contains(c.OrganizationId), cancellationToken);
        
        summary["teamsCount"] = await _dbContext.Teams
            .CountAsync(t => t.OrganizationId == organizationId, cancellationToken);  // Teams stay org-specific
        
        summary["organizationId"] = organizationId;
        summary["userId"] = userId;
        summary["accessibleOrganizations"] = accessibleOrgIds.Count;

        return summary;
    }

    public async Task SyncUserDataToPythonAsync(
        int userId, 
        int organizationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UserDataContextRequest(
                userId,
                organizationId,
                "comprehensive sync",
                TopK: 100
            );

            var context = await BuildUserDataContextAsync(request, cancellationToken);

            // Get the actual current user's name (not client name)
            var currentUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            
            var userName = currentUser != null 
                ? $"{currentUser.FirstName} {currentUser.LastName}"
                : "User";

            // Send to Python AI service for indexing
            var payload = new
            {
                userId,
                organizationId,
                userName,  // Add the actual user's name
                moduleData = context.ModuleData,
                metadata = context.Metadata,
                timestamp = context.GeneratedAt
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var syncUrl = "/data-context/sync";
            _logger.LogInformation("Syncing user data to Python AI at endpoint: {BaseAddress}{Endpoint}", _httpClient.BaseAddress, syncUrl);

            var response = await _httpClient.PostAsync(syncUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully synced user data context to Python AI for User {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user data context to Python AI for User {UserId}", userId);
            throw;
        }
    }
}

