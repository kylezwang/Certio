using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMatterAssignmentForVendorsAndGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "MatterAssignments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "MatterAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AssignmentType",
                table: "MatterAssignments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "MatterAssignments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractAmount",
                table: "MatterAssignments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietaryRestrictions",
                table: "MatterAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "MatterAssignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "MatterAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "MatterAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MealChoice",
                table: "MatterAssignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartySize",
                table: "MatterAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "MatterAssignments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RsvpStatus",
                table: "MatterAssignments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TableNumber",
                table: "MatterAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorCategory",
                table: "MatterAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorStatus",
                table: "MatterAssignments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "ContractAmount",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "DietaryRestrictions",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "MealChoice",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "PartySize",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "RsvpStatus",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "TableNumber",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "VendorCategory",
                table: "MatterAssignments");

            migrationBuilder.DropColumn(
                name: "VendorStatus",
                table: "MatterAssignments");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "MatterAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "MatterAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignmentType",
                table: "MatterAssignments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
