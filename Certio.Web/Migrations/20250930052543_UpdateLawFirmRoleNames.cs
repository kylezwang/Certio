using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLawFirmRoleNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update Law Firm role names to remove redundant "LawFirm" prefix
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET Role = CASE Role
                    WHEN 'LawFirmPartner' THEN 'Partner'
                    WHEN 'LawFirmAssociate' THEN 'Associate'
                    WHEN 'LawFirmParalegal' THEN 'Paralegal'
                    WHEN 'LawFirmStaff' THEN 'Staff'
                    ELSE Role
                END
                WHERE Role IN ('LawFirmPartner', 'LawFirmAssociate', 'LawFirmParalegal', 'LawFirmStaff')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback Law Firm role names to add back "LawFirm" prefix
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET Role = CASE Role
                    WHEN 'Partner' THEN 'LawFirmPartner'
                    WHEN 'Associate' THEN 'LawFirmAssociate'
                    WHEN 'Paralegal' THEN 'LawFirmParalegal'
                    WHEN 'Staff' THEN 'LawFirmStaff'
                    ELSE Role
                END
                WHERE Role IN ('Partner', 'Associate', 'Paralegal', 'Staff')
            ");
        }
    }
}
