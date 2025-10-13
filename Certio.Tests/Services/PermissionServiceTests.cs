using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Certio.Application.Services;
using Certio.Application.Interfaces;
using Certio.Infrastructure.Data;
using Certio.Domain.Matters;
using Certio.Domain.Tasks;
using Certio.Domain.Users;
using Certio.Domain.Organizations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Certio.Tests.Services
{
    public class PermissionServiceTests
    {
        private ApplicationDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private IPermissionService CreateService(ApplicationDbContext context)
        {
            var logger = new Mock<ILogger<PermissionService>>().Object;
            return new PermissionService(context, logger);
        }

        [Fact]
        public async Task IsOrganizationMemberAsync_UserIsMember_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            await context.SaveChangesAsync();

            // Act
            var result = await service.IsOrganizationMemberAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsOrganizationMemberAsync_UserNotMember_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };

            context.Users.Add(user);
            context.Organizations.Add(org);
            await context.SaveChangesAsync();

            // Act
            var result = await service.IsOrganizationMemberAsync(1, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsOrganizationMemberAsync_InactiveMembership_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = false,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            await context.SaveChangesAsync();

            // Act
            var result = await service.IsOrganizationMemberAsync(1, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanAccessMatterAsync_EveryoneAccessLevel_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Everyone",
                Status = "Active"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessMatterAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessWithPermission_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Specific",
                Status = "Active"
            };
            var permission = new MatterPermission
            {
                Id = 1,
                MatterId = 1,
                UserId = 1,
                RevokedAt = null
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            context.MatterPermissions.Add(permission);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessMatterAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessWithoutPermission_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Specific",
                Status = "Active"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessMatterAsync(1, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanAccessMatterAsync_SpecificAccessWithAssignment_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Specific",
                Status = "Active"
            };
            var assignment = new MatterAssignment
            {
                Id = 1,
                MatterId = 1,
                UserId = 1,
                AssignmentType = "Assignee",
                RemovedAt = null
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            context.MatterAssignments.Add(assignment);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessMatterAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanAccessMatterAsync_NotOrganizationMember_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Everyone",
                Status = "Active"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.Matters.Add(matter);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessMatterAsync(1, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanAccessTaskAsync_CanAccessParentMatter_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Everyone",
                Status = "Active"
            };
            var task = new TaskItem
            {
                Id = 1,
                MatterId = 1,
                OrgId = 1,
                Title = "Test Task",
                Status = "Pending",
                Priority = "Medium"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            context.TaskItems.Add(task);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessTaskAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanAccessTaskAsync_CannotAccessParentMatter_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Specific",
                Status = "Active"
            };
            var task = new TaskItem
            {
                Id = 1,
                MatterId = 1,
                OrgId = 1,
                Title = "Test Task",
                Status = "Pending",
                Priority = "Medium"
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.Matters.Add(matter);
            context.TaskItems.Add(task);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessTaskAsync(1, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanAccessSubTaskAsync_CanAccessParentTask_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var user = new User { Id = 1, FirstName = "Test", LastName = "User", Email = "test@test.com" };
            var org = new Organization { Id = 1, Name = "Test Org", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var membership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var matter = new Matter 
            { 
                Id = 1, 
                Title = "Test Matter", 
                OrganizationId = 1, 
                AccessLevel = "Everyone",
                Status = "Active"
            };
            var task = new TaskItem
            {
                Id = 1,
                MatterId = 1,
                OrgId = 1,
                Title = "Test Task",
                Status = "Pending",
                Priority = "Medium"
            };
            var subTask = new SubTaskItem
            {
                Id = 1,
                TaskId = 1,
                MatterId = 1,
                OrgId = 1,
                Title = "Test SubTask",
                IsCompleted = false
            };

            context.Users.Add(user);
            context.Organizations.Add(org);
            context.UserOrganizations.Add(membership);
            context.Matters.Add(matter);
            context.TaskItems.Add(task);
            context.SubTaskItems.Add(subTask);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CanAccessSubTaskAsync(1, 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasFirmBasedAccessAsync_ValidRelationship_ReturnsTrue()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var lawFirmUser = new User { Id = 1, FirstName = "Law", LastName = "Firm", Email = "lawfirm@test.com" };
            var lawFirmOrg = new Organization { Id = 1, Name = "Law Firm", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var clientOrg = new Organization { Id = 2, Name = "Client", Type = OrganizationType.Client, OwnerId = 1 };
            var lawFirmMembership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };
            var relationship = new OrganizationRelationship
            {
                Id = 1,
                SourceOrganizationId = 1,
                TargetOrganizationId = 2,
                RelationshipType = RelationshipTypes.LawFirmClient,
                IsActive = true,
                IsDeleted = false,
                ExpiresAt = null,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(lawFirmUser);
            context.Organizations.Add(lawFirmOrg);
            context.Organizations.Add(clientOrg);
            context.UserOrganizations.Add(lawFirmMembership);
            context.OrganizationRelationships.Add(relationship);
            await context.SaveChangesAsync();

            // Act
            var result = await service.HasFirmBasedAccessAsync(1, 2);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasFirmBasedAccessAsync_NoRelationship_ReturnsFalse()
        {
            // Arrange
            var context = CreateInMemoryContext();
            var service = CreateService(context);

            var lawFirmUser = new User { Id = 1, FirstName = "Law", LastName = "Firm", Email = "lawfirm@test.com" };
            var lawFirmOrg = new Organization { Id = 1, Name = "Law Firm", Type = OrganizationType.LawFirm, OwnerId = 1 };
            var clientOrg = new Organization { Id = 2, Name = "Client", Type = OrganizationType.Client, OwnerId = 1 };
            var lawFirmMembership = new UserOrganization 
            { 
                UserId = 1, 
                OrganizationId = 1, 
                IsActive = true,
                UserType = UserTypes.LawFirm,
                Role = "Member"
            };

            context.Users.Add(lawFirmUser);
            context.Organizations.Add(lawFirmOrg);
            context.Organizations.Add(clientOrg);
            context.UserOrganizations.Add(lawFirmMembership);
            await context.SaveChangesAsync();

            // Act
            var result = await service.HasFirmBasedAccessAsync(1, 2);

            // Assert
            Assert.False(result);
        }
    }
}

