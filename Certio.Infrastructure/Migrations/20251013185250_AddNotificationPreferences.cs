using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EmailNotifications = table.Column<bool>(type: "bit", nullable: false),
                    PushNotifications = table.Column<bool>(type: "bit", nullable: false),
                    InAppNotifications = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnMatterChanges = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnTaskAssignment = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnTaskCompletion = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnDocumentShared = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnMentions = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnAIGeneration = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnComments = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnAIPendingApproval = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnAIApproved = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnAIRejected = table.Column<bool>(type: "bit", nullable: false),
                    EnableQuietHours = table.Column<bool>(type: "bit", nullable: false),
                    QuietHoursStart = table.Column<TimeSpan>(type: "time", nullable: true),
                    QuietHoursEnd = table.Column<TimeSpan>(type: "time", nullable: true),
                    EnableDailyDigest = table.Column<bool>(type: "bit", nullable: false),
                    EnableWeeklyDigest = table.Column<bool>(type: "bit", nullable: false),
                    DailyDigestTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    WeeklyDigestDay = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId");

            // Add audit query indexes for better performance
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_MatterId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "MatterId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IsAIAction_Timestamp",
                table: "AuditLogs",
                columns: new[] { "IsAIAction", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SourceConversationId",
                table: "AuditLogs",
                column: "SourceConversationId");

            // Add indexes on AI-generated content for approval tracking
            migrationBuilder.CreateIndex(
                name: "IX_Matters_ApprovalStatus",
                table: "Matters",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_IsAIGenerated_ApprovalStatus",
                table: "Matters",
                columns: new[] { "IsAIGenerated", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ApprovalStatus",
                table: "TaskItems",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_IsAIGenerated_ApprovalStatus",
                table: "TaskItems",
                columns: new[] { "IsAIGenerated", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApprovalStatus",
                table: "Documents",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsAIGenerated_ApprovalStatus",
                table: "Documents",
                columns: new[] { "IsAIGenerated", "ApprovalStatus" });

            // Add indexes on audit tracking fields
            migrationBuilder.CreateIndex(
                name: "IX_Matters_CreatedById",
                table: "Matters",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ModifiedById",
                table: "Matters",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_CreatedById",
                table: "TaskItems",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ModifiedById",
                table: "TaskItems",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ModifiedById",
                table: "Documents",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_CreatedById",
                table: "Organizations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ModifiedById",
                table: "Organizations",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CreatedById",
                table: "Teams",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ModifiedById",
                table: "Teams",
                column: "ModifiedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop audit indexes
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_UserId_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_OrganizationId_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_MatterId_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_IsAIAction_Timestamp", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Action", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_SourceConversationId", table: "AuditLogs");

            // Drop AI approval indexes
            migrationBuilder.DropIndex(name: "IX_Matters_ApprovalStatus", table: "Matters");
            migrationBuilder.DropIndex(name: "IX_Matters_IsAIGenerated_ApprovalStatus", table: "Matters");
            migrationBuilder.DropIndex(name: "IX_TaskItems_ApprovalStatus", table: "TaskItems");
            migrationBuilder.DropIndex(name: "IX_TaskItems_IsAIGenerated_ApprovalStatus", table: "TaskItems");
            migrationBuilder.DropIndex(name: "IX_Documents_ApprovalStatus", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Documents_IsAIGenerated_ApprovalStatus", table: "Documents");

            // Drop audit tracking field indexes
            migrationBuilder.DropIndex(name: "IX_Matters_CreatedById", table: "Matters");
            migrationBuilder.DropIndex(name: "IX_Matters_ModifiedById", table: "Matters");
            migrationBuilder.DropIndex(name: "IX_TaskItems_CreatedById", table: "TaskItems");
            migrationBuilder.DropIndex(name: "IX_TaskItems_ModifiedById", table: "TaskItems");
            migrationBuilder.DropIndex(name: "IX_Documents_ModifiedById", table: "Documents");
            migrationBuilder.DropIndex(name: "IX_Organizations_CreatedById", table: "Organizations");
            migrationBuilder.DropIndex(name: "IX_Organizations_ModifiedById", table: "Organizations");
            migrationBuilder.DropIndex(name: "IX_Teams_CreatedById", table: "Teams");
            migrationBuilder.DropIndex(name: "IX_Teams_ModifiedById", table: "Teams");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}
