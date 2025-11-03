using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMessageSearchIndexesFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_Subject",
                table: "EmailMessages",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_FromEmail",
                table: "EmailMessages",
                column: "FromEmail");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_FromName",
                table: "EmailMessages",
                column: "FromName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_Subject",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_FromEmail",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_FromName",
                table: "EmailMessages");
        }
    }
}
