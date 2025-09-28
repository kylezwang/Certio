using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class ImplementSoftDeleteAndResolveCascadeConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Users_UserId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentComments_Users_UserId",
                table: "DocumentComments");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentReviews_Users_ReviewerId",
                table: "DocumentReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterAssignments_Users_UserId",
                table: "MatterAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_UserId",
                table: "MatterPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestMessages_Users_UserId",
                table: "ServiceRequestMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItemAssignments_Users_UserId",
                table: "StatusItemAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItemComments_Users_UserId",
                table: "StatusItemComments");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedById",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserDeletionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DeletionType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConfirmDataLoss = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedById = table.Column<int>(type: "int", nullable: true),
                    ProcessingNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDeletionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDeletionRequests_Users_ProcessedById",
                        column: x => x.ProcessedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserDeletionRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionRequests_ProcessedById",
                table: "UserDeletionRequests",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionRequests_RequestedAt",
                table: "UserDeletionRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserDeletionRequests_UserId_IsProcessed",
                table: "UserDeletionRequests",
                columns: new[] { "UserId", "IsProcessed" });

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Users_UserId",
                table: "ConversationParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentComments_Users_UserId",
                table: "DocumentComments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentReviews_Users_ReviewerId",
                table: "DocumentReviews",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures",
                column: "SignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterAssignments_Users_UserId",
                table: "MatterAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions",
                column: "RevokedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_UserId",
                table: "MatterPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestMessages_Users_UserId",
                table: "ServiceRequestMessages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItemAssignments_Users_UserId",
                table: "StatusItemAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItemComments_Users_UserId",
                table: "StatusItemComments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Users_UserId",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentComments_Users_UserId",
                table: "DocumentComments");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentReviews_Users_ReviewerId",
                table: "DocumentReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterAssignments_Users_UserId",
                table: "MatterAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_UserId",
                table: "MatterPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestMessages_Users_UserId",
                table: "ServiceRequestMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItemAssignments_Users_UserId",
                table: "StatusItemAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_StatusItemComments_Users_UserId",
                table: "StatusItemComments");

            migrationBuilder.DropTable(
                name: "UserDeletionRequests");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Users_UserId",
                table: "ConversationParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentComments_Users_UserId",
                table: "DocumentComments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentReviews_Users_ReviewerId",
                table: "DocumentReviews",
                column: "ReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSignatures_Users_SignerId",
                table: "DocumentSignatures",
                column: "SignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterAssignments_Users_UserId",
                table: "MatterAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions",
                column: "RevokedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_UserId",
                table: "MatterPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestMessages_Users_UserId",
                table: "ServiceRequestMessages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItemAssignments_Users_UserId",
                table: "StatusItemAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StatusItemComments_Users_UserId",
                table: "StatusItemComments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
