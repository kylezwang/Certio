using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAndJoinCodeSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPersonalOrganization",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    IsPersonal = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Logo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationJoinCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvitedUserType = table.Column<int>(type: "int", maxLength: 20, nullable: false),
                    InvitedClientType = table.Column<int>(type: "int", nullable: true),
                    InvitedExternalType = table.Column<int>(type: "int", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaxUses = table.Column<int>(type: "int", nullable: false),
                    UsesRemaining = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationJoinCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationJoinCodes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationJoinCodes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId",
                table: "Users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OrganizationId",
                table: "Teams",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinCodes_Code",
                table: "OrganizationJoinCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinCodes_CreatedByUserId",
                table: "OrganizationJoinCodes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinCodes_ExpiresAt",
                table: "OrganizationJoinCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationJoinCodes_OrganizationId",
                table: "OrganizationJoinCodes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_OwnerId",
                table: "Organizations",
                column: "OwnerId");

            // Data migration: Create personal organizations for existing users
            migrationBuilder.Sql(@"
                INSERT INTO Organizations (Name, Description, OwnerId, IsPersonal, Color, Logo, IsActive, CreatedAt, LastModifiedDate)
                SELECT 
                    CASE 
                        WHEN u.FirstName IS NOT NULL AND u.LastName IS NOT NULL 
                        THEN u.FirstName + ' ' + u.LastName
                        ELSE u.Email
                    END as Name,
                    'Personal Organization' as Description,
                    u.Id as OwnerId,
                    1 as IsPersonal,
                    '#007bff' as Color,
                    NULL as Logo,
                    1 as IsActive,
                    GETUTCDATE() as CreatedAt,
                    NULL as LastModifiedDate
                FROM Users u
                WHERE u.OrganizationId IS NULL
            ");

            // Update users with their organization IDs
            migrationBuilder.Sql(@"
                UPDATE u 
                SET OrganizationId = o.Id
                FROM Users u
                INNER JOIN Organizations o ON o.OwnerId = u.Id
                WHERE u.OrganizationId IS NULL
            ");

            // Update teams with organization IDs (if any exist)
            // For now, we'll set teams to the first organization or leave them null
            // This will need to be handled manually or through a separate migration
            migrationBuilder.Sql(@"
                UPDATE t 
                SET OrganizationId = (SELECT TOP 1 Id FROM Organizations ORDER BY Id)
                FROM Teams t
                WHERE t.OrganizationId IS NULL
            ");

            // Make OrganizationId required for Users
            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Make OrganizationId required for Teams
            migrationBuilder.AlterColumn<int>(
                name: "OrganizationId",
                table: "Teams",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Organizations_OrganizationId",
                table: "Teams",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Organizations_OrganizationId",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "OrganizationJoinCodes");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OrganizationId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "IsPersonalOrganization",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Teams");
        }
    }
}
