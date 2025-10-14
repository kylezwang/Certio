using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4_FinalAuditIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Matters_MatterId1",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_MatterAssignments_Users_UserId1",
                table: "MatterAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Teams_TeamId1",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_TeamId1",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_MatterAssignments_UserId1",
                table: "MatterAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Documents_MatterId1",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TeamId1",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "MatterId1",
                table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId1",
                table: "Matters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "MatterAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatterId1",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matters_TeamId1",
                table: "Matters",
                column: "TeamId1");

            migrationBuilder.CreateIndex(
                name: "IX_MatterAssignments_UserId1",
                table: "MatterAssignments",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_MatterId1",
                table: "Documents",
                column: "MatterId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Matters_MatterId1",
                table: "Documents",
                column: "MatterId1",
                principalTable: "Matters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MatterAssignments_Users_UserId1",
                table: "MatterAssignments",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Teams_TeamId1",
                table: "Matters",
                column: "TeamId1",
                principalTable: "Teams",
                principalColumn: "Id");
        }
    }
}
