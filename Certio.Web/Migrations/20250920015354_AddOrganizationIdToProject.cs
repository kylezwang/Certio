using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Projects");
        }
    }
}
