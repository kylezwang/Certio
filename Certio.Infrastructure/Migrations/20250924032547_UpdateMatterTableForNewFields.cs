using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMatterTableForNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Matters",
                newName: "PracticeArea");

            migrationBuilder.AddColumn<int>(
                name: "OriginatingAttorneyId",
                table: "Matters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingDate",
                table: "Matters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponsibleAttorneyId",
                table: "Matters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponsibleStaffId",
                table: "Matters",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatuteOfLimitationsDate",
                table: "Matters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StatuteOfLimitationsSatisfied",
                table: "Matters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MatterPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatterId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedById = table.Column<int>(type: "int", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatterPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatterPermissions_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatterPermissions_Users_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MatterPermissions_Users_RevokedById",
                        column: x => x.RevokedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MatterPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matters_OriginatingAttorneyId",
                table: "Matters",
                column: "OriginatingAttorneyId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ResponsibleAttorneyId",
                table: "Matters",
                column: "ResponsibleAttorneyId");

            migrationBuilder.CreateIndex(
                name: "IX_Matters_ResponsibleStaffId",
                table: "Matters",
                column: "ResponsibleStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MatterPermissions_GrantedById",
                table: "MatterPermissions",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatterPermissions_MatterId",
                table: "MatterPermissions",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_MatterPermissions_RevokedById",
                table: "MatterPermissions",
                column: "RevokedById");

            migrationBuilder.CreateIndex(
                name: "IX_MatterPermissions_UserId",
                table: "MatterPermissions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Users_OriginatingAttorneyId",
                table: "Matters",
                column: "OriginatingAttorneyId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Users_ResponsibleAttorneyId",
                table: "Matters",
                column: "ResponsibleAttorneyId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Users_ResponsibleStaffId",
                table: "Matters",
                column: "ResponsibleStaffId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_OriginatingAttorneyId",
                table: "Matters");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_ResponsibleAttorneyId",
                table: "Matters");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_ResponsibleStaffId",
                table: "Matters");

            migrationBuilder.DropTable(
                name: "MatterPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Matters_OriginatingAttorneyId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_ResponsibleAttorneyId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Matters_ResponsibleStaffId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "OriginatingAttorneyId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "PendingDate",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ResponsibleAttorneyId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ResponsibleStaffId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "StatuteOfLimitationsDate",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "StatuteOfLimitationsSatisfied",
                table: "Matters");

            migrationBuilder.RenameColumn(
                name: "PracticeArea",
                table: "Matters",
                newName: "Category");
        }
    }
}
