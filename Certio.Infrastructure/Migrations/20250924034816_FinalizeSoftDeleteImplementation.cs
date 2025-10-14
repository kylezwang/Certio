using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeSoftDeleteImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions");

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions",
                column: "RevokedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions");

            migrationBuilder.AddForeignKey(
                name: "FK_MatterPermissions_Users_RevokedById",
                table: "MatterPermissions",
                column: "RevokedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
