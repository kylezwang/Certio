using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Application.Services;
using Certio.Domain.AgentActions;
using Certio.Infrastructure.Data;

namespace Certio.Tests.Services
{
    /// <summary>
    /// Tenant isolation tests for AgentActionService.
    ///
    /// Before the organizationId parameter was added to the mutating methods, every one of these
    /// operations loaded the action by integer ID alone. A user holding ApproveAgentActions in their own
    /// organization could approve, reject, execute, or roll back any other organization's action by
    /// guessing its ID. These tests pin that behavior closed.
    ///
    /// Cross-tenant access is reported as NOT_FOUND rather than a forbidden result so the response does
    /// not confirm that the action exists.
    /// </summary>
    public class AgentActionServiceTenantIsolationTests : IDisposable
    {
        private const int OwnerOrgId = 1;
        private const int AttackerOrgId = 2;
        private const int AttackerUserId = 99;

        private readonly ApplicationDbContext _context;
        private readonly AgentActionService _service;

        public AgentActionServiceTenantIsolationTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _service = new AgentActionService(_context, new Mock<ILogger<AgentActionService>>().Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Mutations must not cross the tenant boundary

        [Fact]
        public async Task ApproveActionAsync_ActionInAnotherOrganization_ReturnsNotFoundAndLeavesActionPending()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.ApproveActionAsync(AttackerOrgId, action.Id, AttackerUserId);

            Assert.False(result.Success);
            Assert.Equal("NOT_FOUND", result.ErrorCode);
            await AssertUnchangedAsync(action.Id, AgentActionStatus.Pending);
        }

        [Fact]
        public async Task RejectActionAsync_ActionInAnotherOrganization_ReturnsNotFoundAndLeavesActionPending()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.RejectActionAsync(AttackerOrgId, action.Id, AttackerUserId);

            Assert.False(result.Success);
            Assert.Equal("NOT_FOUND", result.ErrorCode);
            await AssertUnchangedAsync(action.Id, AgentActionStatus.Pending);
        }

        [Fact]
        public async Task ExecuteActionAsync_ActionInAnotherOrganization_ReturnsNotFoundAndDoesNotRun()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Approved);

            var result = await _service.ExecuteActionAsync(AttackerOrgId, action.Id);

            Assert.False(result.Success);
            Assert.Equal("NOT_FOUND", result.ErrorCode);

            var reloaded = await ReloadAsync(action.Id);
            Assert.Equal(AgentActionStatus.Approved, reloaded.Status);
            Assert.Equal(0, reloaded.AttemptCount);
            Assert.Null(reloaded.StartedAt);
        }

        [Fact]
        public async Task RollbackActionAsync_ActionInAnotherOrganization_ReturnsNotFoundAndDoesNotRollBack()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Done);

            var result = await _service.RollbackActionAsync(AttackerOrgId, action.Id, AttackerUserId);

            Assert.False(result.Success);
            Assert.Equal("NOT_FOUND", result.ErrorCode);

            var reloaded = await ReloadAsync(action.Id);
            Assert.Equal(AgentActionStatus.Done, reloaded.Status);
            Assert.False(reloaded.IsRolledBack);
        }

        #endregion

        #region Bulk operations isolate per action

        [Fact]
        public async Task BulkApproveActionsAsync_MixedOrganizations_ApprovesOnlyCallersOwnActions()
        {
            var own = await CreateActionAsync(AttackerOrgId, AgentActionStatus.Pending);
            var other = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.BulkApproveActionsAsync(
                AttackerOrgId, new[] { own.Id, other.Id }, AttackerUserId);

            Assert.False(result.Success);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);

            Assert.Equal(AgentActionStatus.Approved, (await ReloadAsync(own.Id)).Status);
            await AssertUnchangedAsync(other.Id, AgentActionStatus.Pending);
        }

        [Fact]
        public async Task BulkRejectActionsAsync_MixedOrganizations_RejectsOnlyCallersOwnActions()
        {
            var own = await CreateActionAsync(AttackerOrgId, AgentActionStatus.Pending);
            var other = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.BulkRejectActionsAsync(
                AttackerOrgId, new[] { own.Id, other.Id }, AttackerUserId);

            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);

            Assert.Equal(AgentActionStatus.Rejected, (await ReloadAsync(own.Id)).Status);
            await AssertUnchangedAsync(other.Id, AgentActionStatus.Pending);
        }

        #endregion

        #region Reads must not cross the tenant boundary

        [Fact]
        public async Task GetActionAsync_ActionInAnotherOrganization_ReturnsNull()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            Assert.Null(await _service.GetActionAsync(AttackerOrgId, action.Id));
            Assert.NotNull(await _service.GetActionAsync(OwnerOrgId, action.Id));
        }

        [Fact]
        public async Task GetActionByRunIdAsync_ActionInAnotherOrganization_ReturnsNull()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending, runId: "shared-run-id");

            Assert.Null(await _service.GetActionByRunIdAsync(AttackerOrgId, "shared-run-id"));
            Assert.NotNull(await _service.GetActionByRunIdAsync(OwnerOrgId, "shared-run-id"));
        }

        [Fact]
        public async Task GetMatterActionsAsync_SameMatterIdInAnotherOrganization_ReturnsNothing()
        {
            const int matterId = 500;
            await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending, matterId: matterId);

            var visibleToAttacker = await _service.GetMatterActionsAsync(AttackerOrgId, matterId);
            var visibleToOwner = await _service.GetMatterActionsAsync(OwnerOrgId, matterId);

            Assert.Empty(visibleToAttacker);
            Assert.Single(visibleToOwner);
        }

        #endregion

        #region RunId is globally unique, so collisions must not disclose the owner's action

        [Fact]
        public async Task ProposeActionAsync_RunIdOwnedByAnotherOrganization_FailsWithoutReturningThatAction()
        {
            var existing = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending, runId: "replayed-run-id");

            var result = await _service.ProposeActionAsync(
                AttackerOrgId,
                AgentActionTypes.AddNote,
                new AddNotePayload { MatterId = 1, TargetEntityId = 1, Content = "probe" },
                AttackerUserId,
                "replayed-run-id");

            Assert.False(result.Success);
            Assert.Equal("RUNID_CONFLICT", result.ErrorCode);
            Assert.False(result.IsDuplicate);
            Assert.Null(result.Action);

            // The owner's action must be untouched and still reachable only by its owner.
            await AssertUnchangedAsync(existing.Id, AgentActionStatus.Pending);
        }

        #endregion

        #region Positive controls: the fix must not break same-organization behavior

        [Fact]
        public async Task ApproveActionAsync_ActionInCallersOrganization_Succeeds()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.ApproveActionAsync(OwnerOrgId, action.Id, approvedByUserId: 7);

            Assert.True(result.Success);

            var reloaded = await ReloadAsync(action.Id);
            Assert.Equal(AgentActionStatus.Approved, reloaded.Status);
            Assert.Equal(7, reloaded.ApprovedById);
            Assert.NotNull(reloaded.ApprovedAt);
        }

        [Fact]
        public async Task RejectActionAsync_ActionInCallersOrganization_Succeeds()
        {
            var action = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending);

            var result = await _service.RejectActionAsync(OwnerOrgId, action.Id, rejectedByUserId: 7, reason: "not needed");

            Assert.True(result.Success);

            var reloaded = await ReloadAsync(action.Id);
            Assert.Equal(AgentActionStatus.Rejected, reloaded.Status);
            Assert.Equal("not needed", reloaded.ReviewNotes);
        }

        [Fact]
        public async Task ProposeActionAsync_RunIdOwnedByCallersOrganization_ReturnsDuplicate()
        {
            var existing = await CreateActionAsync(OwnerOrgId, AgentActionStatus.Pending, runId: "same-org-run-id");

            var result = await _service.ProposeActionAsync(
                OwnerOrgId,
                AgentActionTypes.AddNote,
                new AddNotePayload { MatterId = 1, TargetEntityId = 1, Content = "probe" },
                proposedByUserId: 7,
                runId: "same-org-run-id");

            Assert.True(result.Success);
            Assert.True(result.IsDuplicate);
            Assert.Equal(existing.Id, result.Action!.Id);
        }

        #endregion

        #region Helpers

        private async Task<AgentAction> CreateActionAsync(
            int organizationId,
            string status,
            string? runId = null,
            int? matterId = null)
        {
            var action = new AgentAction
            {
                RunId = runId ?? Guid.NewGuid().ToString(),
                OrganizationId = organizationId,
                MatterId = matterId,
                ActionType = AgentActionTypes.AddNote,
                Status = status,
                Description = "test action",
                IsReversible = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.AgentActions.Add(action);
            await _context.SaveChangesAsync();
            return action;
        }

        private async Task<AgentAction> ReloadAsync(int actionId)
        {
            // The service mutates tracked entities, so drop local state before asserting on persisted values.
            _context.ChangeTracker.Clear();
            var action = await _context.AgentActions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == actionId);
            Assert.NotNull(action);
            return action!;
        }

        private async Task AssertUnchangedAsync(int actionId, string expectedStatus)
        {
            var reloaded = await ReloadAsync(actionId);
            Assert.Equal(expectedStatus, reloaded.Status);
            Assert.Null(reloaded.ApprovedById);
            Assert.Null(reloaded.ApprovedAt);
            Assert.Null(reloaded.RejectedById);
            Assert.Null(reloaded.RolledBackById);
        }

        #endregion
    }
}
