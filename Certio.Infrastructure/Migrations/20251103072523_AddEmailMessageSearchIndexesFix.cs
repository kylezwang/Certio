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
            // Create indexes only if they don't already exist (idempotent migration)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailMessages_Subject' AND object_id = OBJECT_ID('EmailMessages'))
                BEGIN
                    CREATE INDEX [IX_EmailMessages_Subject] ON [EmailMessages] ([Subject]);
                END
                
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailMessages_FromEmail' AND object_id = OBJECT_ID('EmailMessages'))
                BEGIN
                    CREATE INDEX [IX_EmailMessages_FromEmail] ON [EmailMessages] ([FromEmail]);
                END
                
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmailMessages_FromName' AND object_id = OBJECT_ID('EmailMessages'))
                BEGIN
                    CREATE INDEX [IX_EmailMessages_FromName] ON [EmailMessages] ([FromName]);
                END
            ");
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
