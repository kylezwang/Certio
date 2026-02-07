using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeEmailService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Body",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "BodyText",
                table: "EmailMessages");

            migrationBuilder.AddColumn<string>(
                name: "Preview",
                table: "EmailMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preview",
                table: "EmailMessages");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "EmailMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyText",
                table: "EmailMessages",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
