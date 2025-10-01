using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskItemEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskItemId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrgId = table.Column<int>(type: "int", nullable: false),
                    MatterId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentTaskItemId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskItems_Matters_MatterId",
                        column: x => x.MatterId,
                        principalTable: "Matters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskItems_Organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskItems_TaskItems_ParentTaskItemId",
                        column: x => x.ParentTaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsNotifyRecipient = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskItemComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskItemComments_TaskItemComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "TaskItemComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskItemComments_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskItemComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskItemDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskItemId = table.Column<int>(type: "int", nullable: false),
                    DependsOnTaskItemId = table.Column<int>(type: "int", nullable: false),
                    DependencyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItemDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskItemDependencies_TaskItems_DependsOnTaskItemId",
                        column: x => x.DependsOnTaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskItemDependencies_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matters_OrganizationId",
                table: "Matters",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TaskItemId",
                table: "Documents",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_TaskItemId",
                table: "TaskAssignments",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_UserId",
                table: "TaskAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemComments_ParentCommentId",
                table: "TaskItemComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemComments_TaskItemId",
                table: "TaskItemComments",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemComments_UserId",
                table: "TaskItemComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemDependencies_DependsOnTaskItemId",
                table: "TaskItemDependencies",
                column: "DependsOnTaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemDependencies_TaskItemId",
                table: "TaskItemDependencies",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_MatterId",
                table: "TaskItems",
                column: "MatterId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_OrgId",
                table: "TaskItems",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ParentTaskItemId",
                table: "TaskItems",
                column: "ParentTaskItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_TaskItems_TaskItemId",
                table: "Documents",
                column: "TaskItemId",
                principalTable: "TaskItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Matters_Organizations_OrganizationId",
                table: "Matters",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_TaskItems_TaskItemId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Organizations_OrganizationId",
                table: "Matters");

            migrationBuilder.DropTable(
                name: "TaskAssignments");

            migrationBuilder.DropTable(
                name: "TaskItemComments");

            migrationBuilder.DropTable(
                name: "TaskItemDependencies");

            migrationBuilder.DropTable(
                name: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_Matters_OrganizationId",
                table: "Matters");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TaskItemId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TaskItemId",
                table: "Documents");
        }
    }
}
