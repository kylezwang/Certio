using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Certio.Web.Hubs;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using Certio.Domain.Matters;
using System.Security.Claims;

namespace Certio.Tests.Hubs
{
    /// <summary>
    /// Security tests for NotificationHub to verify authorization fixes
    /// Tests that prevent unauthorized group subscriptions
    /// </summary>
    public class NotificationHubSecurityTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IPermissionService> _permissionServiceMock;
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<ISingleClientProxy> _callerMock;
        private readonly Mock<IGroupManager> _groupsMock;

        public NotificationHubSecurityTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            
            _permissionServiceMock = new Mock<IPermissionService>();
            
            // Setup SignalR mocks
            _callerMock = new Mock<ISingleClientProxy>();
            _clientsMock = new Mock<IHubCallerClients>();
            _clientsMock.Setup(x => x.Caller).Returns(_callerMock.Object);
            _groupsMock = new Mock<IGroupManager>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region JoinUserGroup Security Tests

        [Fact]
        public async Task JoinUserGroup_AnotherUsersGroup_ShouldReject()
        {
            // Arrange
            var currentUser = await CreateTestUser(1, "CurrentUser", "current@test.com");
            var targetUser = await CreateTestUser(2, "TargetUser", "target@test.com");
            
            var hub = CreateHubWithUser(currentUser.Id);
            
            // Act - Try to join another user's group
            await hub.JoinUserGroup(targetUser.Id);
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Unauthorized to join requested user group"),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinUserGroup_OwnGroup_ShouldSucceed()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var hub = CreateHubWithUser(user.Id);
            
            // Act - Join own group
            await hub.JoinUserGroup(user.Id);
            
            // Assert - Should not send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", It.IsAny<object[]>(), default(CancellationToken)),
                Times.Never);
            
            // Verify group was added
            _groupsMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(), 
                    $"user_{user.Id}", 
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region JoinMatterGroup Security Tests

        [Fact]
        public async Task JoinMatterGroup_UnauthorizedMatter_ShouldReject()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(user.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var matter = new Matter
            {
                Id = 100,
                OrganizationId = org.Id,
                Title = "Test Matter",
                AccessLevel = "Specific", // Requires explicit permission
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Matters.Add(matter);
            await _context.SaveChangesAsync();

            _permissionServiceMock
                .Setup(x => x.CanAccessMatterAsync(user.Id, 100))
                .ReturnsAsync(false);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinMatterGroup(100);
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Unauthorized to subscribe to this matter"),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinMatterGroup_AuthorizedMatter_ShouldSucceed()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(user.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var matter = new Matter
            {
                Id = 100,
                OrganizationId = org.Id,
                Title = "Test Matter",
                AccessLevel = "Everyone",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Matters.Add(matter);
            await _context.SaveChangesAsync();

            _permissionServiceMock
                .Setup(x => x.CanAccessMatterAsync(user.Id, 100))
                .ReturnsAsync(true);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinMatterGroup(100);
            
            // Assert - Should not send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", It.IsAny<object[]>(), default(CancellationToken)),
                Times.Never);
            
            // Verify group was added
            _groupsMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(), 
                    "matter_100", 
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region JoinOrganizationGroup Security Tests

        [Fact]
        public async Task JoinOrganizationGroup_UnauthorizedOrganization_ShouldReject()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org1 = await CreateTestOrganization(1, "Org1");
            var org2 = await CreateTestOrganization(2, "Org2");
            
            // User is only in Org1, trying to join Org2
            await AddUserToOrganizationAsync(user.Id, org1.Id, UserTypes.Client, OrganizationRoles.Member);

            _permissionServiceMock
                .Setup(x => x.IsOrganizationMemberAsync(user.Id, org2.Id))
                .ReturnsAsync(false);
            
            _permissionServiceMock
                .Setup(x => x.HasFirmBasedAccessAsync(user.Id, org2.Id))
                .ReturnsAsync(false);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinOrganizationGroup(org2.Id);
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Unauthorized to subscribe to this organization"),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinOrganizationGroup_DirectMember_ShouldSucceed()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(user.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);

            _permissionServiceMock
                .Setup(x => x.IsOrganizationMemberAsync(user.Id, org.Id))
                .ReturnsAsync(true);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinOrganizationGroup(org.Id);
            
            // Assert - Should not send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", It.IsAny<object[]>(), default(CancellationToken)),
                Times.Never);
            
            // Verify group was added
            _groupsMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(), 
                    $"org_{org.Id}", 
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinOrganizationGroup_FirmBasedAccess_ShouldSucceed()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org = await CreateTestOrganization(1, "Org1");

            _permissionServiceMock
                .Setup(x => x.IsOrganizationMemberAsync(user.Id, org.Id))
                .ReturnsAsync(false);
            
            _permissionServiceMock
                .Setup(x => x.HasFirmBasedAccessAsync(user.Id, org.Id))
                .ReturnsAsync(true);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinOrganizationGroup(org.Id);
            
            // Assert - Should not send error (firm-based access allowed)
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", It.IsAny<object[]>(), default(CancellationToken)),
                Times.Never);
        }

        #endregion

        #region OnConnectedAsync Security Tests

        [Fact]
        public async Task OnConnectedAsync_ShouldAutoJoinOwnUserGroup()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.OnConnectedAsync();
            
            // Assert - Should auto-join user's own group
            _groupsMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(), 
                    $"user_{user.Id}", 
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private NotificationHub CreateHubWithUser(int userId)
        {
            var httpContext = new DefaultHttpContext();
            
            var customUser = _context.Users
                .Include(u => u.UserOrganizations)
                .FirstOrDefault(u => u.Id == userId);
            
            if (customUser != null)
            {
                httpContext.Items["CustomUserId"] = userId;
                httpContext.Items["CustomUser"] = customUser;
            }
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("UserId", userId.ToString())
            };
            
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            
            httpContext.User = principal;
            
            var hubContext = new TestableHubCallerContext(
                httpContext,
                "test-connection-" + Guid.NewGuid(),
                principal,
                userId.ToString());
            
            var hub = new NotificationHub(_permissionServiceMock.Object)
            {
                Context = hubContext,
                Clients = _clientsMock.Object,
                Groups = _groupsMock.Object
            };
            
            return hub;
        }

        private async Task<User> CreateTestUser(int id, string firstName, string email)
        {
            var user = new User
            {
                Id = id,
                FirstName = firstName,
                LastName = "Test",
                Email = email,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private async Task<Organization> CreateTestOrganization(int id, string name)
        {
            var org = new Organization
            {
                Id = id,
                Name = name,
                Type = OrganizationType.Client,
                IsActive = true,
                OwnerId = 1,
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

        #endregion
    }
}

