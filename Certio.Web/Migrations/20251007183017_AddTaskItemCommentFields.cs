using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskItemCommentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskCommentMentions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    MentionedUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCommentMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskCommentMentions_TaskItemComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "TaskItemComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskCommentMentions_Users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskCommentReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReactionType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCommentReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskCommentReactions_TaskItemComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "TaskItemComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskCommentReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentMentions_CommentId_MentionedUserId",
                table: "TaskCommentMentions",
                columns: new[] { "CommentId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentMentions_MentionedUserId",
                table: "TaskCommentMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentReactions_CommentId_UserId_ReactionType",
                table: "TaskCommentReactions",
                columns: new[] { "CommentId", "UserId", "ReactionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentReactions_UserId",
                table: "TaskCommentReactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskCommentMentions");

            migrationBuilder.DropTable(
                name: "TaskCommentReactions");
        }
    }
}
