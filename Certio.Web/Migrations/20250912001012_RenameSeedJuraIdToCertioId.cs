using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameSeedJuraIdToCertioId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if column exists before renaming (idempotent migration)
            var sql = @"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Conversations') 
                    AND name = 'SeedJuraId'
                )
                BEGIN
                    EXEC sp_rename 'Conversations.SeedJuraId', 'CertioId', 'COLUMN';
                END
            ";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Check if column exists before renaming (idempotent migration)
            var sql = @"
                IF EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Conversations') 
                    AND name = 'CertioId'
                )
                BEGIN
                    EXEC sp_rename 'Conversations.CertioId', 'SeedJuraId', 'COLUMN';
                END
            ";
            migrationBuilder.Sql(sql);
        }
    }
}
