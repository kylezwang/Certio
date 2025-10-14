using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomPermissions",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ExternalType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectIds",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CustomPermissions",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProjectIds",
                table: "Users");
        }
    }
}
