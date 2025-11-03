using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailMessageSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add indexes for frequently searched fields to improve search performance
            // Subject is searched often and is indexed for better query performance
            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_Subject",
                table: "EmailMessages",
                column: "Subject");

            // FromEmail is used in filtering and searching
            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_FromEmail",
                table: "EmailMessages",
                column: "FromEmail");

            // FromName is used in search queries
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
