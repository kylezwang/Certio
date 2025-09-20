using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationLevelUserTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertioType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitedClientType",
                table: "OrganizationJoinCodes");

            migrationBuilder.DropColumn(
                name: "InvitedExternalType",
                table: "OrganizationJoinCodes");

            migrationBuilder.AddColumn<int>(
                name: "UserType",
                table: "UserOrganizations",
                type: "int",
                maxLength: 20,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvitedRole",
                table: "OrganizationJoinCodes",
                type: "int",
                maxLength: 20,
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserType",
                table: "UserOrganizations");

            migrationBuilder.DropColumn(
                name: "InvitedRole",
                table: "OrganizationJoinCodes");

            migrationBuilder.AddColumn<int>(
                name: "CertioType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserType",
                table: "Users",
                type: "int",
                maxLength: 20,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvitedClientType",
                table: "OrganizationJoinCodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitedExternalType",
                table: "OrganizationJoinCodes",
                type: "int",
                nullable: true);
        }
    }
}
