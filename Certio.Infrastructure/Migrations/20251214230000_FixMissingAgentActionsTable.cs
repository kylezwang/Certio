using System;
using Certio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <summary>
    /// Hotfix migration: prior migration "20251209232137_AgentActionsAndUnifiedInbox" was accidentally generated
    /// with empty Up/Down methods, leaving the database without the AgentActions table.
    /// This migration creates the AgentActions table + indexes so agent workflows can function.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20251214230000_FixMissingAgentActionsTable")]
    public partial class FixMissingAgentActionsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    MatterId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActionPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BeforeState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsReversible = table.Column<bool>(type: "bit", nullable: false),
                    IsRolledBack = table.Column<bool>(type: "bit", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RolledBackAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProposedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    RejectedById = table.Column<int>(type: "int", nullable: true),
                    RolledBackById = table.Column<int>(type: "int", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceConversationId = table.Column<int>(type: "int", nullable: true),
                    SourceMessageId = table.Column<int>(type: "int", nullable: true),
                    AIAgentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedEntityId = table.Column<int>(type: "int", nullable: true),
                    CreatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentActions_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentActions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentActions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentActions_Users_ProposedById",
                        column: x => x.ProposedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentActions_Users_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentActions_Users_RolledBackById",
                        column: x => x.RolledBackById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_ActionType",
                table: "AgentActions",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_ApprovedById",
                table: "AgentActions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_CorrelationId",
                table: "AgentActions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_MatterId",
                table: "AgentActions",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_OrganizationId_Status_CreatedAt",
                table: "AgentActions",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_ProposedById",
                table: "AgentActions",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_RejectedById",
                table: "AgentActions",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_RolledBackById",
                table: "AgentActions",
                column: "RolledBackById");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_RunId",
                table: "AgentActions",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_Status_CreatedAt",
                table: "AgentActions",
                columns: new[] { "Status", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentActions");
        }
    }
}


