using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationRelationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceOrganizationId = table.Column<int>(type: "int", nullable: false),
                    TargetOrganizationId = table.Column<int>(type: "int", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccessLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    ModifiedById = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<int>(type: "int", nullable: true),
                    DeletionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationRelationships_Organizations_SourceOrganizationId",
                        column: x => x.SourceOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRelationships_Organizations_TargetOrganizationId",
                        column: x => x.TargetOrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRelationships_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationRelationships_Users_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganizationRelationships_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_CreatedById",
                table: "OrganizationRelationships",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_DeletedById",
                table: "OrganizationRelationships",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_ExpiresAt",
                table: "OrganizationRelationships",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_ModifiedById",
                table: "OrganizationRelationships",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_RelationshipType_IsActive",
                table: "OrganizationRelationships",
                columns: new[] { "RelationshipType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_SourceOrganizationId_TargetOrganizationId_IsActive",
                table: "OrganizationRelationships",
                columns: new[] { "SourceOrganizationId", "TargetOrganizationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_SourceOrganizationId_TargetOrganizationId_RelationshipType",
                table: "OrganizationRelationships",
                columns: new[] { "SourceOrganizationId", "TargetOrganizationId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRelationships_TargetOrganizationId",
                table: "OrganizationRelationships",
                column: "TargetOrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationRelationships");
        }
    }
}
