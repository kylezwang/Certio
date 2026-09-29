using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Web.Security;
using Certio.Web.Services;

namespace Certio.Web.Controllers.Api
{
    /// <summary>
    /// API controller for unified inbox (read-only in.
    /// Aggregates communications from Email, DirectMessage, Chat, and external sources.
    /// </summary>
    [ApiController]
    [Route("api/inbox")]
    [Authorize(Policy = "OrgMember")]
    public class UnifiedInboxController : ControllerBase
    {
        private readonly IUnifiedInboxService _inboxService;
        private readonly IClientContextAccessor _clientContextAccessor;
        private readonly ILogger<UnifiedInboxController> _logger;

        public UnifiedInboxController(
            IUnifiedInboxService inboxService,
            IClientContextAccessor clientContextAccessor,
            ILogger<UnifiedInboxController> logger)
        {
            _inboxService = inboxService;
            _clientContextAccessor = clientContextAccessor;
            _logger = logger;
        }

        private int OrgId => _clientContextAccessor.Current?.OrganizationId ?? 0;
        private int UserId => _clientContextAccessor.Current?.UserId ?? 0;

        #region Query Endpoints

        /// <summary>
        /// Get inbox items with filtering and pagination
        /// </summary>
        [HttpGet]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetInbox(
            [FromQuery] string? status = null,
            [FromQuery] string? source = null,
            [FromQuery] int? matterId = null,
            [FromQuery] bool? isFlagged = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            [FromQuery] string orderBy = "LastMessageAt",
            [FromQuery] bool descending = true,
            [FromQuery] bool includeArchived = false)
        {
            var options = new InboxQueryOptions
            {
                Status = status,
                Source = source,
                MatterId = matterId,
                IsFlagged = isFlagged,
                StartDate = startDate,
                EndDate = endDate,
                Skip = skip,
                Take = Math.Min(take, 100), // Cap at 100
                OrderBy = orderBy,
                Descending = descending,
                IncludeArchived = includeArchived
            };

            var result = await _inboxService.GetInboxAsync(OrgId, options);
            return Ok(result);
        }

        /// <summary>
        /// Get a specific inbox item by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetItem(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            return Ok(item);
        }

        /// <summary>
        /// Get messages for an inbox item
        /// </summary>
        [HttpGet("{id:int}/messages")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetMessages(int id, [FromQuery] int? limit = 50)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var messages = await _inboxService.GetMessagesAsync(id, limit);
            return Ok(new { messages, count = messages.Count });
        }

        /// <summary>
        /// Get inbox items for a specific matter
        /// </summary>
        [HttpGet("matter/{matterId:int}")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetMatterInbox(int matterId, [FromQuery] int? limit = 50)
        {
            var items = await _inboxService.GetMatterInboxAsync(matterId, limit);
            return Ok(new { items, count = items.Count });
        }

        /// <summary>
        /// Get unread count
        /// </summary>
        [HttpGet("unread-count")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _inboxService.GetUnreadCountAsync(OrgId, UserId);
            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Search inbox items
        /// </summary>
        [HttpGet("search")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] string? source = null,
            [FromQuery] int? matterId = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            [FromQuery] bool includeArchived = false)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { error = "Search query is required" });

            var options = new InboxQueryOptions
            {
                Source = source,
                MatterId = matterId,
                Skip = skip,
                Take = Math.Min(take, 100),
                IncludeArchived = includeArchived
            };

            var result = await _inboxService.SearchAsync(OrgId, q, options);
            return Ok(result);
        }

        /// <summary>
        /// Get inbox statistics
        /// </summary>
        [HttpGet("stats")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _inboxService.GetStatsAsync(OrgId);
            return Ok(stats);
        }

        #endregion

        #region Status Endpoints (Read-only actions)

        /// <summary>
        /// Mark an inbox item as read
        /// </summary>
        [HttpPost("{id:int}/read")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.MarkAsReadAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to mark as read" });
        }

        /// <summary>
        /// Mark multiple items as read
        /// </summary>
        [HttpPost("bulk-read")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> BulkMarkAsRead([FromBody] BulkInboxRequest request)
        {
            var count = await _inboxService.MarkMultipleAsReadAsync(request.ItemIds, UserId);
            return Ok(new { success = true, count });
        }

        /// <summary>
        /// Mark an inbox item as unread
        /// </summary>
        [HttpPost("{id:int}/unread")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> MarkAsUnread(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.MarkAsUnreadAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to mark as unread" });
        }

        /// <summary>
        /// Archive an inbox item
        /// </summary>
        [HttpPost("{id:int}/archive")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> Archive(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.ArchiveAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to archive" });
        }

        /// <summary>
        /// Unarchive an inbox item
        /// </summary>
        [HttpPost("{id:int}/unarchive")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> Unarchive(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.UnarchiveAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to unarchive" });
        }

        /// <summary>
        /// Toggle flag/star on an inbox item
        /// </summary>
        [HttpPost("{id:int}/toggle-flag")]
        [RequireAgentPermission(Permission.ViewInbox)]
        public async Task<IActionResult> ToggleFlag(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.ToggleFlagAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to toggle flag" });
        }

        /// <summary>
        /// Add labels to an inbox item
        /// </summary>
        [HttpPost("{id:int}/labels/add")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> AddLabels(int id, [FromBody] LabelsRequest request)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.AddLabelsAsync(id, request.Labels, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to add labels" });
        }

        /// <summary>
        /// Remove labels from an inbox item
        /// </summary>
        [HttpPost("{id:int}/labels/remove")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> RemoveLabels(int id, [FromBody] LabelsRequest request)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.RemoveLabelsAsync(id, request.Labels, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to remove labels" });
        }

        /// <summary>
        /// Snooze an inbox item
        /// </summary>
        [HttpPost("{id:int}/snooze")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> Snooze(int id, [FromBody] SnoozeRequest request)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.SnoozeAsync(id, request.SnoozeUntil, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to snooze" });
        }

        /// <summary>
        /// Link an inbox item to a matter
        /// </summary>
        [HttpPost("{id:int}/link-matter/{matterId:int}")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> LinkToMatter(int id, int matterId)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.LinkToMatterAsync(id, matterId, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to link to matter" });
        }

        /// <summary>
        /// Unlink an inbox item from a matter
        /// </summary>
        [HttpPost("{id:int}/unlink-matter")]
        [RequireAgentPermission(Permission.ManageInbox)]
        public async Task<IActionResult> UnlinkFromMatter(int id)
        {
            var item = await _inboxService.GetItemAsync(id);
            if (item == null || item.OrganizationId != OrgId)
                return NotFound();

            var success = await _inboxService.UnlinkFromMatterAsync(id, UserId);
            return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to unlink from matter" });
        }

        #endregion
    }

    #region Request DTOs

    public class BulkInboxRequest
    {
        public List<int> ItemIds { get; set; } = new();
    }

    public class LabelsRequest
    {
        public List<string> Labels { get; set; } = new();
    }

    public class SnoozeRequest
    {
        public DateTime SnoozeUntil { get; set; }
    }

    #endregion
}

