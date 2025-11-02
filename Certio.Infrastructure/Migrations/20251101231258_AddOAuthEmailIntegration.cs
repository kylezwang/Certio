using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthEmailIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WebhookSubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailAccountId = table.Column<int>(type: "int", nullable: false),
                    DirectMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalEmailId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThreadId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ToEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CcEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BccEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessages_DirectMessages_DirectMessageId",
                        column: x => x.DirectMessageId,
                        principalTable: "DirectMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailMessages_EmailAccounts_EmailAccountId",
                        column: x => x.EmailAccountId,
                        principalTable: "EmailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_EmailAddress",
                table: "EmailAccounts",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_UserId",
                table: "EmailAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_DirectMessageId",
                table: "EmailMessages",
                column: "DirectMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_EmailAccountId_ReceivedAt",
                table: "EmailMessages",
                columns: new[] { "EmailAccountId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ExternalEmailId",
                table: "EmailMessages",
                column: "ExternalEmailId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ThreadId",
                table: "EmailMessages",
                column: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailAccounts");
        }
    }
}
