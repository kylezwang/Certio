using Xunit;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Web.Hubs;
using Certio.Web.Services;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Domain.Services;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using System.Security.Claims;

namespace Certio.Tests.Hubs
{
    /// <summary>
    /// Security tests for ChatHub to verify authorization fixes
    /// Tests that prevent unauthorized access and impersonation
    /// </summary>
    public class ChatHubSecurityTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IChatService> _chatServiceMock;
        private readonly Mock<IClientContextAccessor> _clientContextAccessorMock;
        private readonly Mock<IUserPresenceService> _userPresenceServiceMock;
        private readonly Mock<ILogger<ChatHub>> _loggerMock;
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<ISingleClientProxy> _callerMock;
        private readonly Mock<IGroupManager> _groupsMock;

        public ChatHubSecurityTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            
            _chatServiceMock = new Mock<IChatService>();
            _clientContextAccessorMock = new Mock<IClientContextAccessor>();
            _userPresenceServiceMock = new Mock<IUserPresenceService>();
            _loggerMock = new Mock<ILogger<ChatHub>>();
            
            // Setup SignalR mocks
            _callerMock = new Mock<ISingleClientProxy>();
            _clientsMock = new Mock<IHubCallerClients>();
            _clientsMock.Setup(x => x.Caller).Returns(_callerMock.Object);
            _clientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_callerMock.Object);
            _groupsMock = new Mock<IGroupManager>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region JoinConversation Security Tests

        [Fact]
        public async Task JoinConversation_UnauthorizedUser_ShouldReject()
        {
            // Arrange
            var user1 = await CreateTestUser(1, "User1", "user1@test.com");
            var user2 = await CreateTestUser(2, "User2", "user2@test.com");
            var org1 = await CreateTestOrganization(1, "Org1");
            var org2 = await CreateTestOrganization(2, "Org2");
            
            // User1 is in Org1, conversation is in Org2
            await AddUserToOrganizationAsync(user1.Id, org1.Id, UserTypes.Client, OrganizationRoles.Member);
            await AddUserToOrganizationAsync(user2.Id, org2.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org2.Id, // Different org
                CreatedById = user2.Id,
                Title = "Test Conversation",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, user1.Id, org2.Id))
                .ReturnsAsync(false);

            var hub = CreateHubWithUser(user1.Id);
            
            // Act
            await hub.JoinConversation("100");
            
            // Assert - Should send error to caller
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Conversation access denied"),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinConversation_AuthorizedUser_ShouldSucceed()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(user.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org.Id,
                CreatedById = user.Id,
                Title = "Test Conversation",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, user.Id, org.Id))
                .ReturnsAsync(true);

            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinConversation("100");
            
            // Assert - Should not send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", It.IsAny<object[]>(), default(CancellationToken)),
                Times.Never);
        }

        [Fact]
        public async Task JoinConversation_InvalidConversationId_ShouldReject()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinConversation("invalid");
            
            // Assert - Should send error for invalid ID
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Invalid conversation ID"),
                    default(CancellationToken)),
                Times.Once);
        }

        [Fact]
        public async Task JoinConversation_NonExistentConversation_ShouldReject()
        {
            // Arrange
            var user = await CreateTestUser(1, "User1", "user1@test.com");
            var hub = CreateHubWithUser(user.Id);
            
            // Act
            await hub.JoinConversation("99999");
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Conversation not found"),
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region SendMessage Security Tests

        [Fact]
        public async Task SendMessage_ImpersonationAttempt_ShouldUseServerUserId()
        {
            // Arrange
            var actualUser = await CreateTestUser(1, "ActualUser", "actual@test.com");
            var impersonatedUser = await CreateTestUser(2, "ImpersonatedUser", "impersonated@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(actualUser.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org.Id,
                CreatedById = actualUser.Id,
                Title = "Test Conversation",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, actualUser.Id, org.Id))
                .ReturnsAsync(true);

            // Client tries to send as user 2 (impersonation) - should use actual user ID
            var message = new ChatMessage 
            { 
                Id = 1, 
                ConversationId = 100,
                UserId = actualUser.Id, // Should use actual user ID
                Content = "Test message",
                CreatedAt = DateTime.UtcNow
            };

            _chatServiceMock
                .Setup(x => x.SendMessageAsync(
                    100, 
                    actualUser.Id, // Should use actual user ID, not client-provided
                    It.IsAny<string>(),
                    "Test message",
                    "Text",
                    It.IsAny<string?>()))
                .ReturnsAsync(message);

            var hub = CreateHubWithUser(actualUser.Id);
            
            // Act - Client provides wrong userId (impersonation attempt)
            await hub.SendMessage("100", "2", "Client", "Test message");
            
            // Assert - Verify service was called with actual user ID, not client-provided
            _chatServiceMock.Verify(
                x => x.SendMessageAsync(
                    100, 
                    actualUser.Id, // Actual user ID
                    It.IsAny<string>(), 
                    "Test message", 
                    "Text",
                    It.IsAny<string?>()),
                Times.Once);
            
            // Verify it was NOT called with the impersonated user ID
            _chatServiceMock.Verify(
                x => x.SendMessageAsync(
                    100, 
                    impersonatedUser.Id, // Should never use this
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task SendMessage_UnauthorizedConversation_ShouldReject()
        {
            // Arrange
            var user1 = await CreateTestUser(1, "User1", "user1@test.com");
            var user2 = await CreateTestUser(2, "User2", "user2@test.com");
            var org1 = await CreateTestOrganization(1, "Org1");
            var org2 = await CreateTestOrganization(2, "Org2");
            
            await AddUserToOrganizationAsync(user1.Id, org1.Id, UserTypes.Client, OrganizationRoles.Member);
            await AddUserToOrganizationAsync(user2.Id, org2.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org2.Id, // Different org
                CreatedById = user2.Id,
                Title = "Test Conversation",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, user1.Id, org2.Id))
                .ReturnsAsync(false);

            var hub = CreateHubWithUser(user1.Id);
            
            // Act
            await hub.SendMessage("100", "1", "Client", "Test message");
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Conversation access denied"),
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region SendChannelMessage Security Tests

        [Fact]
        public async Task SendChannelMessage_ImpersonationAttempt_ShouldUseServerUserId()
        {
            // Arrange
            var actualUser = await CreateTestUser(1, "ActualUser", "actual@test.com");
            var org = await CreateTestOrganization(1, "Org1");
            await AddUserToOrganizationAsync(actualUser.Id, org.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org.Id,
                CreatedById = actualUser.Id,
                IsChannel = true,
                Title = "Test Channel",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, actualUser.Id, org.Id))
                .ReturnsAsync(true);

            var message = new ChatMessage 
            { 
                Id = 1, 
                ConversationId = 100,
                UserId = actualUser.Id,
                Content = "Test message",
                CreatedAt = DateTime.UtcNow
            };

            _chatServiceMock
                .Setup(x => x.SendChannelMessageAsync(
                    100, 
                    actualUser.Id, // Should use actual user ID
                    It.IsAny<string>(),
                    "Test message",
                    "Text",
                    100,
                    null))
                .ReturnsAsync(message);

            var hub = CreateHubWithUser(actualUser.Id);
            
            // Act - Client provides wrong userId (impersonation attempt)
            await hub.SendChannelMessage("100", "999", "Client", "Test message");
            
            // Assert - Verify service was called with actual user ID
            _chatServiceMock.Verify(
                x => x.SendChannelMessageAsync(
                    100, 
                    actualUser.Id, // Actual user ID
                    It.IsAny<string>(), 
                    "Test message", 
                    "Text",
                    100,
                    null),
                Times.Once);
        }

        #endregion

        #region JoinChannel Security Tests

        [Fact]
        public async Task JoinChannel_UnauthorizedUser_ShouldReject()
        {
            // Arrange
            var user1 = await CreateTestUser(1, "User1", "user1@test.com");
            var user2 = await CreateTestUser(2, "User2", "user2@test.com");
            var org1 = await CreateTestOrganization(1, "Org1");
            var org2 = await CreateTestOrganization(2, "Org2");
            
            await AddUserToOrganizationAsync(user1.Id, org1.Id, UserTypes.Client, OrganizationRoles.Member);
            await AddUserToOrganizationAsync(user2.Id, org2.Id, UserTypes.Client, OrganizationRoles.Member);
            
            var conversation = new Conversation
            {
                Id = 100,
                OrganizationId = org2.Id, // Different org
                CreatedById = user2.Id,
                IsChannel = true,
                Title = "Test Channel",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            _chatServiceMock
                .Setup(x => x.CanUserAccessConversationAsync(100, user1.Id, org2.Id))
                .ReturnsAsync(false);

            var hub = CreateHubWithUser(user1.Id);
            
            // Act
            await hub.JoinChannel("100");
            
            // Assert - Should send error
            _callerMock.Verify(
                x => x.SendCoreAsync("Error", 
                    It.Is<object[]>(args => args[0].ToString() == "Channel not found or access denied"),
                    default(CancellationToken)),
                Times.Once);
        }

        #endregion

        #region Helper Methods

        private ChatHub CreateHubWithUser(int userId)
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
            
            var hub = new ChatHub(
                _chatServiceMock.Object,
                _context,
                _clientContextAccessorMock.Object,
                _userPresenceServiceMock.Object)
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

