using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationScopedConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_UserId1",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_CreatedById",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_UserId1",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AI_Summary",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Client_Goals",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId_CreatedAt",
                table: "Conversations",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId_CreatedById",
                table: "Conversations",
                columns: new[] { "OrganizationId", "CreatedById" });

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Organizations_OrganizationId",
                table: "Conversations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_CreatedById",
                table: "Conversations",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Organizations_OrganizationId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_CreatedById",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_CreatedAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_CreatedById",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Conversations");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "Conversations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AI_Summary",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Client_Goals",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId1",
                table: "ChatMessages",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_UserId1",
                table: "ChatMessages",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_CreatedById",
                table: "Conversations",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
