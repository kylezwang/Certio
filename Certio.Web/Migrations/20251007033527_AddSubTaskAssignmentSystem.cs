using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSubTaskAssignmentSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubTaskAssignment",
                table: "SubTaskItems");

            migrationBuilder.CreateTable(
                name: "SubTaskAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubTaskItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsNotifyRecipient = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubTaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubTaskAssignments_SubTaskItems_SubTaskItemId",
                        column: x => x.SubTaskItemId,
                        principalTable: "SubTaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubTaskAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubTaskAssignments_SubTaskItemId_AssignmentType",
                table: "SubTaskAssignments",
                columns: new[] { "SubTaskItemId", "AssignmentType" });

            migrationBuilder.CreateIndex(
                name: "IX_SubTaskAssignments_UserId",
                table: "SubTaskAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubTaskAssignments");

            migrationBuilder.AddColumn<string>(
                name: "SubTaskAssignment",
                table: "SubTaskItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
