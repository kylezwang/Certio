using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIAuditFieldsToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAIGenerated",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceConversationId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceMessageId",
                table: "ChatMessages",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAIGenerated",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SourceConversationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SourceMessageId",
                table: "ChatMessages");
        }
    }
}
