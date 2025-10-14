using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectToMatter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIAgentResults_Projects_ProjectId",
                table: "AIAgentResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientGoals_Projects_ProjectId",
                table: "ClientGoals");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Projects_ProjectId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Projects_ProjectId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Projects_ProjectId1",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Projects_ProjectId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Projects_ProjectId",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Projects_ProjectId1",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItems_Projects_ProjectId",
                table: "StatusItems");

            migrationBuilder.DropTable(
                name: "ProjectAssignments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "StatusItems",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_StatusItems_ProjectId",
                table: "StatusItems",
                newName: "IX_StatusItems_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId1",
                table: "ServiceRequests",
                newName: "MatterId1");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "ServiceRequests",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceRequests_ProjectId1",
                table: "ServiceRequests",
                newName: "IX_ServiceRequests_MatterId1");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceRequests_ProjectId",
                table: "ServiceRequests",
                newName: "IX_ServiceRequests_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Notifications",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_ProjectId",
                table: "Notifications",
                newName: "IX_Notifications_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId1",
                table: "Documents",
                newName: "MatterId1");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Documents",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_ProjectId1",
                table: "Documents",
                newName: "IX_Documents_MatterId1");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                newName: "IX_Documents_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Conversations",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_ProjectId",
                table: "Conversations",
                newName: "IX_Conversations_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "ClientGoals",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientGoals_ProjectId",
                table: "ClientGoals",
                newName: "IX_ClientGoals_MatterId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "AIAgentResults",
                newName: "MatterId");

            migrationBuilder.RenameIndex(
                name: "IX_AIAgentResults_ProjectId",
                table: "AIAgentResults",
                newName: "IX_AIAgentResults_MatterId");

            migrationBuilder.CreateTable(
                name: "Matters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientGoals = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LegalRequirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TeamId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matters_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Matters_Teams_TeamId1",
                        column: x => x.TeamId1,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Matters_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MatterAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatterId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatterAssignments_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatterAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatterAssignments_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatterAssignments_MatterId",
                table: "MatterAssignments",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_MatterAssignments_UserId",
                table: "MatterAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatterAssignments_UserId1",
                table: "MatterAssignments",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ClientId",
                table: "Matters",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_CreatedAt",
                table: "Matters",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_TeamId",
                table: "Matters",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_TeamId1",
                table: "Matters",
                column: "TeamId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AIAgentResults_Matters_MatterId",
                table: "AIAgentResults",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientGoals_Matters_MatterId",
                table: "ClientGoals",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Matters_MatterId",
                table: "Conversations",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Matters_MatterId",
                table: "Documents",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Matters_MatterId1",
                table: "Documents",
                column: "MatterId1",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Matters_MatterId",
                table: "Notifications",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Matters_MatterId",
                table: "ServiceRequests",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Matters_MatterId1",
                table: "ServiceRequests",
                column: "MatterId1",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItems_Matters_MatterId",
                table: "StatusItems",
                column: "MatterId",
                principalTable: "Matters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIAgentResults_Matters_MatterId",
                table: "AIAgentResults");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientGoals_Matters_MatterId",
                table: "ClientGoals");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Matters_MatterId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Matters_MatterId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Matters_MatterId1",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Matters_MatterId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Matters_MatterId",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_Matters_MatterId1",
                table: "ServiceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItems_Matters_MatterId",
                table: "StatusItems");

            migrationBuilder.DropTable(
                name: "MatterAssignments");

            migrationBuilder.DropTable(
                name: "Matters");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "StatusItems",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_StatusItems_MatterId",
                table: "StatusItems",
                newName: "IX_StatusItems_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId1",
                table: "ServiceRequests",
                newName: "ProjectId1");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "ServiceRequests",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceRequests_MatterId1",
                table: "ServiceRequests",
                newName: "IX_ServiceRequests_ProjectId1");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceRequests_MatterId",
                table: "ServiceRequests",
                newName: "IX_ServiceRequests_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "Notifications",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Notifications_MatterId",
                table: "Notifications",
                newName: "IX_Notifications_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId1",
                table: "Documents",
                newName: "ProjectId1");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "Documents",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_MatterId1",
                table: "Documents",
                newName: "IX_Documents_ProjectId1");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_MatterId",
                table: "Documents",
                newName: "IX_Documents_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "Conversations",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Conversations_MatterId",
                table: "Conversations",
                newName: "IX_Conversations_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "ClientGoals",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientGoals_MatterId",
                table: "ClientGoals",
                newName: "IX_ClientGoals_ProjectId");

            migrationBuilder.RenameColumn(
                name: "MatterId",
                table: "AIAgentResults",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_AIAgentResults_MatterId",
                table: "AIAgentResults",
                newName: "IX_AIAgentResults_ProjectId");

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientGoals = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegalRequirements = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TeamId1 = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Projects_Teams_TeamId1",
                        column: x => x.TeamId1,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projects_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAssignments_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssignments_ProjectId",
                table: "ProjectAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssignments_UserId",
                table: "ProjectAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssignments_UserId1",
                table: "ProjectAssignments",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ClientId",
                table: "Projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedAt",
                table: "Projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TeamId",
                table: "Projects",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TeamId1",
                table: "Projects",
                column: "TeamId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AIAgentResults_Projects_ProjectId",
                table: "AIAgentResults",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientGoals_Projects_ProjectId",
                table: "ClientGoals",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Projects_ProjectId",
                table: "Conversations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Projects_ProjectId",
                table: "Documents",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Projects_ProjectId1",
                table: "Documents",
                column: "ProjectId1",
                principalTable: "Projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Projects_ProjectId",
                table: "Notifications",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Projects_ProjectId",
                table: "ServiceRequests",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_Projects_ProjectId1",
                table: "ServiceRequests",
                column: "ProjectId1",
                principalTable: "Projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItems_Projects_ProjectId",
                table: "StatusItems",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
