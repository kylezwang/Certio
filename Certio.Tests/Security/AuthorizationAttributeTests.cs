using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Certio.Web.Security;
using Certio.Application.Interfaces;
using Certio.Domain.Users;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Certio.Tests.Security
{
    /// <summary>
    /// Tests for custom authorization attributes
    /// Validates that attributes correctly enforce permissions
    /// </summary>
    public class AuthorizationAttributeTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IPermissionService> _permissionServiceMock;
        private readonly Mock<ILogger<RequirePermissionFilter>> _permissionLoggerMock;
        private readonly Mock<ILogger<RequireMatterAccessFilter>> _matterLoggerMock;
        private readonly Mock<ILogger<RequireTaskAccessFilter>> _taskLoggerMock;

        public AuthorizationAttributeTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _permissionServiceMock = new Mock<IPermissionService>();
            _permissionLoggerMock = new Mock<ILogger<RequirePermissionFilter>>();
            _matterLoggerMock = new Mock<ILogger<RequireMatterAccessFilter>>();
            _taskLoggerMock = new Mock<ILogger<RequireTaskAccessFilter>>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region RequirePermission Attribute Tests

        [Fact]
        public async Task RequirePermissionFilter_UserHasPermission_AllowsAccess()
        {
            // Arrange
            var userId = 1;
            var orgId = 1;
            var permission = Permission.ViewMatters;

            _permissionServiceMock
                .Setup(s => s.HasPermissionAsync(userId, orgId, permission))
                .ReturnsAsync(true);

            var filter = new RequirePermissionFilter(
                permission,
                _permissionServiceMock.Object,
                _context,
                _permissionLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, orgId);

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.Null(authContext.Result); // No result = access granted
        }

        [Fact]
        public async Task RequirePermissionFilter_UserLacksPermission_DeniesAccess()
        {
            // Arrange
            var userId = 1;
            var orgId = 1;
            var permission = Permission.DeleteMatters;

            _permissionServiceMock
                .Setup(s => s.HasPermissionAsync(userId, orgId, permission))
                .ReturnsAsync(false);

            var filter = new RequirePermissionFilter(
                permission,
                _permissionServiceMock.Object,
                _context,
                _permissionLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, orgId);

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsType<ForbidResult>(authContext.Result);
        }

        [Fact]
        public async Task RequirePermissionFilter_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange
            var filter = new RequirePermissionFilter(
                Permission.ViewMatters,
                _permissionServiceMock.Object,
                _context,
                _permissionLoggerMock.Object);

            var authContext = CreateAuthorizationContext(null, null);

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsType<UnauthorizedResult>(authContext.Result);
        }

        #endregion

        #region RequireMatterAccess Attribute Tests

        [Fact]
        public async Task RequireMatterAccessFilter_UserHasAccess_AllowsAccess()
        {
            // Arrange
            var userId = 1;
            var matterId = 100;

            _permissionServiceMock
                .Setup(s => s.CanAccessMatterAsync(userId, matterId))
                .ReturnsAsync(true);

            var filter = new RequireMatterAccessFilter(
                "matterId",
                _permissionServiceMock.Object,
                _matterLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, null);
            authContext.RouteData.Values["matterId"] = matterId;

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.Null(authContext.Result);
        }

        [Fact]
        public async Task RequireMatterAccessFilter_UserLacksAccess_DeniesAccess()
        {
            // Arrange
            var userId = 1;
            var matterId = 100;

            _permissionServiceMock
                .Setup(s => s.CanAccessMatterAsync(userId, matterId))
                .ReturnsAsync(false);

            var filter = new RequireMatterAccessFilter(
                "matterId",
                _permissionServiceMock.Object,
                _matterLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, null);
            authContext.RouteData.Values["matterId"] = matterId;

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsType<ForbidResult>(authContext.Result);
        }

        [Fact]
        public async Task RequireMatterAccessFilter_MissingMatterId_ReturnsBadRequest()
        {
            // Arrange
            var userId = 1;

            var filter = new RequireMatterAccessFilter(
                "matterId",
                _permissionServiceMock.Object,
                _matterLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, null);
            // Don't add matterId to route

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsType<BadRequestObjectResult>(authContext.Result);
        }

        #endregion

        #region RequireTaskAccess Attribute Tests

        [Fact]
        public async Task RequireTaskAccessFilter_UserHasAccess_AllowsAccess()
        {
            // Arrange
            var userId = 1;
            var taskId = 200;

            _permissionServiceMock
                .Setup(s => s.CanAccessTaskAsync(userId, taskId))
                .ReturnsAsync(true);

            var filter = new RequireTaskAccessFilter(
                "taskId",
                _permissionServiceMock.Object,
                _taskLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, null);
            authContext.RouteData.Values["taskId"] = taskId;

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.Null(authContext.Result);
        }

        [Fact]
        public async Task RequireTaskAccessFilter_UserLacksAccess_DeniesAccess()
        {
            // Arrange
            var userId = 1;
            var taskId = 200;

            _permissionServiceMock
                .Setup(s => s.CanAccessTaskAsync(userId, taskId))
                .ReturnsAsync(false);

            var filter = new RequireTaskAccessFilter(
                "taskId",
                _permissionServiceMock.Object,
                _taskLoggerMock.Object);

            var authContext = CreateAuthorizationContext(userId, null);
            authContext.RouteData.Values["taskId"] = taskId;

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.IsType<ForbidResult>(authContext.Result);
        }

        [Fact]
        public async Task RequireTaskAccessFilter_TaskIdInQueryString_Works()
        {
            // Arrange
            var userId = 1;
            var taskId = 200;

            _permissionServiceMock
                .Setup(s => s.CanAccessTaskAsync(userId, taskId))
                .ReturnsAsync(true);

            var filter = new RequireTaskAccessFilter(
                "taskId",
                _permissionServiceMock.Object,
                _taskLoggerMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString($"?taskId={taskId}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            var authContext = new AuthorizationFilterContext(
                actionContext,
                new List<IFilterMetadata>());

            // Act
            await filter.OnAuthorizationAsync(authContext);

            // Assert
            Assert.Null(authContext.Result);
        }

        #endregion

        #region Helper Methods

        private AuthorizationFilterContext CreateAuthorizationContext(int? userId, int? orgId)
        {
            var httpContext = new DefaultHttpContext();
            
            if (userId.HasValue)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
                };
                var identity = new ClaimsIdentity(claims, "TestAuth");
                httpContext.User = new ClaimsPrincipal(identity);

                // Add CustomUser to HttpContext.Items for RequirePermissionFilter
                var customUser = new User
                {
                    Id = userId.Value,
                    Email = $"test{userId.Value}@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    UserOrganizations = new List<UserOrganization>()
                };
                httpContext.Items["CustomUser"] = customUser;
            }

            if (orgId.HasValue)
            {
                httpContext.Session = new Mock<ISession>().Object;
                Mock.Get(httpContext.Session)
                    .Setup(s => s.TryGetValue("OrganizationId", out It.Ref<byte[]>.IsAny))
                    .Returns(false);
                
                httpContext.Items["CurrentOrganizationId"] = orgId.Value;
            }

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            if (orgId.HasValue)
            {
                actionContext.RouteData.Values["orgId"] = orgId.Value;
            }

            return new AuthorizationFilterContext(
                actionContext,
                new List<IFilterMetadata>());
        }

        #endregion
    }
}

