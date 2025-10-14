using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Application.Services;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Infrastructure.Data;
using Certio.Domain.Exceptions;

namespace Certio.Tests.Services
{
    /// <summary>
    /// Comprehensive permission service tests
    /// Tests all permission combinations and scenarios
    /// </summary>
    public class PermissionServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly PermissionService _permissionService;
        private readonly Mock<ILogger<PermissionService>> _loggerMock;

        public PermissionServiceTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _loggerMock = new Mock<ILogger<PermissionService>>();
            _permissionService = new PermissionService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Organization-Level Permission Tests

        [Fact]
        public async Task HasPermissionAsync_ClientOwner_HasAllClientPermissions()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Owner);

            // Act & Assert
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.CreateMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.DeleteMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewDocuments));
        }

        [Fact]
        public async Task HasPermissionAsync_ClientManager_CannotDeleteMatters()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Manager);

            // Act & Assert
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.DeleteMatters));
        }

        [Fact]
        public async Task HasPermissionAsync_LawFirmPartner_HasFullAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.LawFirm, OrganizationRoles.Partner);

            // Act & Assert
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.CreateMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.DeleteMatters));
        }

        [Fact]
        public async Task HasPermissionAsync_LawFirmAssociate_LimitedAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.LawFirm, OrganizationRoles.Associate);

            // Act & Assert
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewMatters));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.CreateMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.DeleteMatters));
        }

        [Fact]
        public async Task HasPermissionAsync_ExternalUser_VeryLimitedAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.External, OrganizationRoles.OpposingCounsel);

            // Act & Assert
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewMatters));
            Assert.True(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.ViewDocuments));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.CreateMatters));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.EditMatters));
            Assert.False(await _permissionService.HasPermissionAsync(user.Id, org.Id, Permission.DeleteMatters));
        }

        #endregion

        #region Matter Access Tests

        [Fact]
        public async Task CanAccessMatterAsync_EveryoneAccessLevel_OrgMemberCanAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Everyone");

            // Act
            var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);

            // Assert
            Assert.True(canAccess);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessLevel_WithoutPermission_CannotAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");

            // Act
            var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);

            // Assert
            Assert.False(canAccess);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessLevel_WithPermission_CanAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            await GrantMatterPermissionAsync(matter.Id, user.Id);

            // Act
            var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);

            // Assert
            Assert.True(canAccess);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessLevel_WithAssignment_CanAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            await AssignUserToMatterAsync(matter.Id, user.Id);

            // Act
            var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);

            // Assert
            Assert.True(canAccess);
        }

        [Fact]
        public async Task CanAccessMatterAsync_RevokedPermission_CannotAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            var permission = await GrantMatterPermissionAsync(matter.Id, user.Id);
            
            // Revoke permission
            permission.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Act
            var canAccess = await _permissionService.CanAccessMatterAsync(user.Id, matter.Id);

            // Assert
            Assert.False(canAccess);
        }

        #endregion

        #region Task Access Tests

        [Fact]
        public async Task CanAccessTaskAsync_DirectTaskAssignment_CanAccess()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            var task = await CreateTaskAsync(matter.Id, org.Id);
            await AssignUserToTaskAsync(task.Id, user.Id);

            // Act
            var canAccess = await _permissionService.CanAccessTaskAsync(user.Id, task.Id);

            // Assert
            Assert.True(canAccess);
        }

        [Fact]
        public async Task CanAccessTaskAsync_MatterAssignment_CanAccessTasks()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            var task = await CreateTaskAsync(matter.Id, org.Id);
            await AssignUserToMatterAsync(matter.Id, user.Id);

            // Act
            var canAccess = await _permissionService.CanAccessTaskAsync(user.Id, task.Id);

            // Assert
            Assert.True(canAccess);
        }

        [Fact]
        public async Task CanAccessTaskAsync_NoAccessToMatter_CannotAccessTask()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");
            var task = await CreateTaskAsync(matter.Id, org.Id);

            // Act
            var canAccess = await _permissionService.CanAccessTaskAsync(user.Id, task.Id);

            // Assert
            Assert.False(canAccess);
        }

        #endregion

        #region Firm-Based Access Tests

        [Fact]
        public async Task HasFirmBasedAccessAsync_ValidRelationship_HasAccess()
        {
            // Arrange
            var lawFirm = await CreateOrganizationAsync("Law Firm", "LawFirm");
            var client = await CreateOrganizationAsync("Client Org", "Client");
            var lawyer = await CreateUserAsync("lawyer@firm.com");
            await AddUserToOrganizationAsync(lawyer.Id, lawFirm.Id, UserTypes.LawFirm, OrganizationRoles.Partner);
            await CreateRelationshipAsync(lawFirm.Id, client.Id);

            // Act
            var hasAccess = await _permissionService.HasFirmBasedAccessAsync(lawyer.Id, client.Id);

            // Assert
            Assert.True(hasAccess);
        }

        [Fact]
        public async Task HasFirmBasedAccessAsync_ExpiredRelationship_NoAccess()
        {
            // Arrange
            var lawFirm = await CreateOrganizationAsync("Law Firm", "LawFirm");
            var client = await CreateOrganizationAsync("Client Org", "Client");
            var lawyer = await CreateUserAsync("lawyer@firm.com");
            await AddUserToOrganizationAsync(lawyer.Id, lawFirm.Id, UserTypes.LawFirm, OrganizationRoles.Partner);
            await CreateRelationshipAsync(lawFirm.Id, client.Id, expiresAt: DateTime.UtcNow.AddDays(-1));

            // Act
            var hasAccess = await _permissionService.HasFirmBasedAccessAsync(lawyer.Id, client.Id);

            // Assert
            Assert.False(hasAccess);
        }

        [Fact]
        public async Task HasFirmBasedAccessAsync_NotLawFirmMember_NoAccess()
        {
            // Arrange
            var client1 = await CreateOrganizationAsync("Client Org 1", "Client");
            var client2 = await CreateOrganizationAsync("Client Org 2", "Client");
            var user = await CreateUserAsync("user@client1.com");
            await AddUserToOrganizationAsync(user.Id, client1.Id, UserTypes.Client, OrganizationRoles.Owner);

            // Act
            var hasAccess = await _permissionService.HasFirmBasedAccessAsync(user.Id, client2.Id);

            // Assert
            Assert.False(hasAccess);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task ValidatePermissionOrThrowAsync_WithPermission_DoesNotThrow()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Owner);

            // Act & Assert
            await _permissionService.ValidatePermissionOrThrowAsync(user.Id, org.Id, Permission.ViewMatters, "ViewMatters");
        }

        [Fact]
        public async Task ValidatePermissionOrThrowAsync_WithoutPermission_ThrowsException()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.External, OrganizationRoles.OpposingCounsel);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedOperationException>(async () =>
                await _permissionService.ValidatePermissionOrThrowAsync(user.Id, org.Id, Permission.DeleteMatters, "DeleteMatters"));
        }

        [Fact]
        public async Task ValidateMatterAccessOrThrowAsync_WithAccess_DoesNotThrow()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Everyone");

            // Act & Assert
            await _permissionService.ValidateMatterAccessOrThrowAsync(user.Id, matter.Id, "ViewMatter");
        }

        [Fact]
        public async Task ValidateMatterAccessOrThrowAsync_WithoutAccess_ThrowsException()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Member);
            var matter = await CreateMatterAsync(org.Id, "Specific");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedOperationException>(async () =>
                await _permissionService.ValidateMatterAccessOrThrowAsync(user.Id, matter.Id, "ViewMatter"));
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task GetEffectivePermissionsAsync_PerformanceTest_Under50ms()
        {
            // Arrange
            var (user, org) = await CreateUserWithRoleAsync(UserTypes.Client, OrganizationRoles.Owner);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var permissions = await _permissionService.GetEffectivePermissionsAsync(user.Id, org.Id);

            // Assert
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 50, 
                $"Permission check took {stopwatch.ElapsedMilliseconds}ms, expected < 50ms");
            Assert.NotEmpty(permissions);
        }

        #endregion

        #region Helper Methods

        private async Task<(User user, Organization org)> CreateUserWithRoleAsync(string userType, string role)
        {
            var user = await CreateUserAsync($"user_{Guid.NewGuid()}@test.com");
            var org = await CreateOrganizationAsync($"Org_{Guid.NewGuid()}", 
                userType == UserTypes.LawFirm ? "LawFirm" : "Client");
            await AddUserToOrganizationAsync(user.Id, org.Id, userType, role);
            return (user, org);
        }

        private async Task<User> CreateUserAsync(string email)
        {
            var user = new User
            {
                Email = email,
                FirstName = "Test",
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                UserOrganizations = new List<UserOrganization>()
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private async Task<Organization> CreateOrganizationAsync(string name, string type)
        {
            var org = new Organization
            {
                Name = name,
                Type = type == "LawFirm" ? OrganizationType.LawFirm : OrganizationType.Client,
                OwnerId = 1, // Set a default owner ID for tests
                CreatedAt = DateTime.UtcNow
            };
            _context.Organizations.Add(org);
            await _context.SaveChangesAsync();
            return org;
        }

        private async Task AddUserToOrganizationAsync(int userId, int orgId, string userType, string role)
        {
            var userOrg = new UserOrganization
            {
                UserId = userId,
                OrganizationId = orgId,
                UserType = userType,
                Role = role,
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };
            _context.UserOrganizations.Add(userOrg);
            await _context.SaveChangesAsync();
        }

        private async Task<Matter> CreateMatterAsync(int orgId, string accessLevel)
        {
            var matter = new Matter
            {
                Title = "Test Matter",
                OrganizationId = orgId,
                AccessLevel = accessLevel,
                CreatedAt = DateTime.UtcNow,
                Permissions = new List<MatterPermission>(),
                Assignments = new List<MatterAssignment>()
            };
            _context.Matters.Add(matter);
            await _context.SaveChangesAsync();
            return matter;
        }

        private async Task<MatterPermission> GrantMatterPermissionAsync(int matterId, int userId)
        {
            var permission = new MatterPermission
            {
                MatterId = matterId,
                UserId = userId,
                GrantedAt = DateTime.UtcNow
            };
            _context.MatterPermissions.Add(permission);
            await _context.SaveChangesAsync();
            return permission;
        }

        private async Task AssignUserToMatterAsync(int matterId, int userId)
        {
            var assignment = new MatterAssignment
            {
                MatterId = matterId,
                UserId = userId,
                AssignedAt = DateTime.UtcNow
            };
            _context.MatterAssignments.Add(assignment);
            await _context.SaveChangesAsync();
        }

        private async Task<TaskItem> CreateTaskAsync(int matterId, int orgId)
        {
            var task = new TaskItem
            {
                MatterId = matterId,
                OrgId = orgId,
                Title = "Test Task",
                CreatedAt = DateTime.UtcNow,
                TaskAssignments = new List<TaskAssignment>()
            };
            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();
            return task;
        }

        private async Task AssignUserToTaskAsync(int taskId, int userId)
        {
            var assignment = new TaskAssignment
            {
                TaskItemId = taskId,
                UserId = userId,
                AssignedAt = DateTime.UtcNow
            };
            _context.TaskAssignments.Add(assignment);
            await _context.SaveChangesAsync();
        }

        private async Task CreateRelationshipAsync(int lawFirmId, int clientId, DateTime? expiresAt = null)
        {
            var relationship = new OrganizationRelationship
            {
                SourceOrganizationId = lawFirmId,
                TargetOrganizationId = clientId,
                RelationshipType = RelationshipTypes.LawFirmClient,
                AccessLevel = "Full",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };
            _context.OrganizationRelationships.Add(relationship);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}
