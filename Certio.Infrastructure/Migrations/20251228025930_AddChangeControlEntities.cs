using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <summary>
    /// Change Control: add ChangeNotices + ChangeNoticeRecipients.
    ///
    /// NOTE: This migration is intentionally scoped to ONLY the new Change Control tables.
    /// Existing schema is managed by prior migrations.
    /// </summary>
    public partial class AddChangeControlEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatterId = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcknowledgementDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastResentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeNotices_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeNotices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    // Avoid SQL Server multiple cascade paths for audit user links
                    table.ForeignKey(
                        name: "FK_ChangeNotices_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ChangeNotices_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ChangeNotices_Users_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "ChangeNoticeRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeNoticeId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TokenVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeNoticeRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeNoticeRecipients_ChangeNotices_ChangeNoticeId",
                        column: x => x.ChangeNoticeId,
                        principalTable: "ChangeNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeNoticeRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_OrganizationId_MatterId_IsDeleted",
                table: "ChangeNotices",
                columns: new[] { "OrganizationId", "MatterId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_MatterId",
                table: "ChangeNotices",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_OrganizationId",
                table: "ChangeNotices",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_CreatedById",
                table: "ChangeNotices",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_ModifiedById",
                table: "ChangeNotices",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNotices_DeletedById",
                table: "ChangeNotices",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNoticeRecipients_ChangeNoticeId_Email",
                table: "ChangeNoticeRecipients",
                columns: new[] { "ChangeNoticeId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNoticeRecipients_UserId",
                table: "ChangeNoticeRecipients",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChangeNoticeRecipients");
            migrationBuilder.DropTable(name: "ChangeNotices");
        }
    }
}


