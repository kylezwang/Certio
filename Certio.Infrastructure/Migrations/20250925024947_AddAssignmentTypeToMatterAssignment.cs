using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentTypeToMatterAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentType",
                table: "MatterAssignments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "RelevantContact");

            // Update existing records to have AssignmentType = "RelevantContact"
            migrationBuilder.Sql("UPDATE MatterAssignments SET AssignmentType = 'RelevantContact' WHERE AssignmentType = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "MatterAssignments");
        }
    }
}
