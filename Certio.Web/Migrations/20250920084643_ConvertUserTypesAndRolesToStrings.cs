using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class ConvertUserTypesAndRolesToStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add temporary columns for data conversion
            migrationBuilder.AddColumn<string>(
                name: "UserType_temp",
                table: "UserOrganizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role_temp",
                table: "UserOrganizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedUserType_temp",
                table: "OrganizationJoinCodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedRole_temp",
                table: "OrganizationJoinCodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Convert UserType values
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET UserType_temp = CASE UserType
                    WHEN 0 THEN 'Client'
                    WHEN 1 THEN 'External'
                    WHEN 2 THEN 'Certio'
                    ELSE 'Client'
                END");

            // Convert Role values
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET Role_temp = CASE Role
                    WHEN 0 THEN 'Owner'
                    WHEN 1 THEN 'Manager'
                    WHEN 2 THEN 'Member'
                    WHEN 3 THEN 'Lawyer'
                    WHEN 4 THEN 'OpposingCounsel'
                    WHEN 5 THEN 'ExpertWitness'
                    WHEN 6 THEN 'CourtPersonnel'
                    WHEN 7 THEN 'RegulatoryBody'
                    WHEN 8 THEN 'Other'
                    WHEN 9 THEN 'Admin'
                    WHEN 10 THEN 'ProjectManager'
                    WHEN 11 THEN 'Support'
                    WHEN 12 THEN 'Legal'
                    WHEN 13 THEN 'Guest'
                    ELSE 'Member'
                END");

            // Convert InvitedUserType values
            migrationBuilder.Sql(@"
                UPDATE OrganizationJoinCodes 
                SET InvitedUserType_temp = CASE InvitedUserType
                    WHEN 0 THEN 'Client'
                    WHEN 1 THEN 'External'
                    WHEN 2 THEN 'Certio'
                    ELSE 'Client'
                END");

            // Convert InvitedRole values
            migrationBuilder.Sql(@"
                UPDATE OrganizationJoinCodes 
                SET InvitedRole_temp = CASE InvitedRole
                    WHEN 0 THEN 'Owner'
                    WHEN 1 THEN 'Manager'
                    WHEN 2 THEN 'Member'
                    WHEN 3 THEN 'Lawyer'
                    WHEN 4 THEN 'OpposingCounsel'
                    WHEN 5 THEN 'ExpertWitness'
                    WHEN 6 THEN 'CourtPersonnel'
                    WHEN 7 THEN 'RegulatoryBody'
                    WHEN 8 THEN 'Other'
                    WHEN 9 THEN 'Admin'
                    WHEN 10 THEN 'ProjectManager'
                    WHEN 11 THEN 'Support'
                    WHEN 12 THEN 'Legal'
                    WHEN 13 THEN 'Guest'
                    ELSE 'Member'
                END");

            // Drop old columns
            migrationBuilder.DropColumn(
                name: "UserType",
                table: "UserOrganizations");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "UserOrganizations");

            migrationBuilder.DropColumn(
                name: "InvitedUserType",
                table: "OrganizationJoinCodes");

            migrationBuilder.DropColumn(
                name: "InvitedRole",
                table: "OrganizationJoinCodes");

            // Rename temporary columns to final names
            migrationBuilder.RenameColumn(
                name: "UserType_temp",
                table: "UserOrganizations",
                newName: "UserType");

            migrationBuilder.RenameColumn(
                name: "Role_temp",
                table: "UserOrganizations",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "InvitedUserType_temp",
                table: "OrganizationJoinCodes",
                newName: "InvitedUserType");

            migrationBuilder.RenameColumn(
                name: "InvitedRole_temp",
                table: "OrganizationJoinCodes",
                newName: "InvitedRole");

            // Set columns as NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "UserType",
                table: "UserOrganizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "UserOrganizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitedUserType",
                table: "OrganizationJoinCodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitedRole",
                table: "OrganizationJoinCodes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add temporary columns for reverse conversion
            migrationBuilder.AddColumn<int>(
                name: "UserType_temp",
                table: "UserOrganizations",
                type: "int",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role_temp",
                table: "UserOrganizations",
                type: "int",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitedUserType_temp",
                table: "OrganizationJoinCodes",
                type: "int",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvitedRole_temp",
                table: "OrganizationJoinCodes",
                type: "int",
                maxLength: 20,
                nullable: true);

            // Convert UserType values back to integers
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET UserType_temp = CASE UserType
                    WHEN 'Client' THEN 0
                    WHEN 'External' THEN 1
                    WHEN 'Certio' THEN 2
                    ELSE 0
                END");

            // Convert Role values back to integers
            migrationBuilder.Sql(@"
                UPDATE UserOrganizations 
                SET Role_temp = CASE Role
                    WHEN 'Owner' THEN 0
                    WHEN 'Manager' THEN 1
                    WHEN 'Member' THEN 2
                    WHEN 'Lawyer' THEN 3
                    WHEN 'OpposingCounsel' THEN 4
                    WHEN 'ExpertWitness' THEN 5
                    WHEN 'CourtPersonnel' THEN 6
                    WHEN 'RegulatoryBody' THEN 7
                    WHEN 'Other' THEN 8
                    WHEN 'Admin' THEN 9
                    WHEN 'ProjectManager' THEN 10
                    WHEN 'Support' THEN 11
                    WHEN 'Legal' THEN 12
                    WHEN 'Guest' THEN 13
                    ELSE 2
                END");

            // Convert InvitedUserType values back to integers
            migrationBuilder.Sql(@"
                UPDATE OrganizationJoinCodes 
                SET InvitedUserType_temp = CASE InvitedUserType
                    WHEN 'Client' THEN 0
                    WHEN 'External' THEN 1
                    WHEN 'Certio' THEN 2
                    ELSE 0
                END");

            // Convert InvitedRole values back to integers
            migrationBuilder.Sql(@"
                UPDATE OrganizationJoinCodes 
                SET InvitedRole_temp = CASE InvitedRole
                    WHEN 'Owner' THEN 0
                    WHEN 'Manager' THEN 1
                    WHEN 'Member' THEN 2
                    WHEN 'Lawyer' THEN 3
                    WHEN 'OpposingCounsel' THEN 4
                    WHEN 'ExpertWitness' THEN 5
                    WHEN 'CourtPersonnel' THEN 6
                    WHEN 'RegulatoryBody' THEN 7
                    WHEN 'Other' THEN 8
                    WHEN 'Admin' THEN 9
                    WHEN 'ProjectManager' THEN 10
                    WHEN 'Support' THEN 11
                    WHEN 'Legal' THEN 12
                    WHEN 'Guest' THEN 13
                    ELSE 2
                END");

            // Drop string columns
            migrationBuilder.DropColumn(
                name: "UserType",
                table: "UserOrganizations");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "UserOrganizations");

            migrationBuilder.DropColumn(
                name: "InvitedUserType",
                table: "OrganizationJoinCodes");

            migrationBuilder.DropColumn(
                name: "InvitedRole",
                table: "OrganizationJoinCodes");

            // Rename temporary columns to final names
            migrationBuilder.RenameColumn(
                name: "UserType_temp",
                table: "UserOrganizations",
                newName: "UserType");

            migrationBuilder.RenameColumn(
                name: "Role_temp",
                table: "UserOrganizations",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "InvitedUserType_temp",
                table: "OrganizationJoinCodes",
                newName: "InvitedUserType");

            migrationBuilder.RenameColumn(
                name: "InvitedRole_temp",
                table: "OrganizationJoinCodes",
                newName: "InvitedRole");

            // Set columns as NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "UserType",
                table: "UserOrganizations",
                type: "int",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "UserOrganizations",
                type: "int",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvitedUserType",
                table: "OrganizationJoinCodes",
                type: "int",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "InvitedRole",
                table: "OrganizationJoinCodes",
                type: "int",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
