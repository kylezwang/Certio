using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectIdsToMatterIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProjectIds",
                table: "Users",
                newName: "MatterIds");

            migrationBuilder.RenameColumn(
                name: "ProjectType",
                table: "Matters",
                newName: "MatterType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MatterIds",
                table: "Users",
                newName: "ProjectIds");

            migrationBuilder.RenameColumn(
                name: "MatterType",
                table: "Matters",
                newName: "ProjectType");
        }
    }
}
