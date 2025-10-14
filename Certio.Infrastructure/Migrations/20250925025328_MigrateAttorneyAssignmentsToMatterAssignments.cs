using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAttorneyAssignmentsToMatterAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate existing attorney assignments to MatterAssignments
            migrationBuilder.Sql(@"
                INSERT INTO MatterAssignments (MatterId, UserId, AssignmentType, Role, IsNotifyRecipient, AssignedAt)
                SELECT Id, OriginatingAttorneyId, 'OriginatingAttorney', '', 1, GETUTCDATE()
                FROM Matters 
                WHERE OriginatingAttorneyId IS NOT NULL;
                
                INSERT INTO MatterAssignments (MatterId, UserId, AssignmentType, Role, IsNotifyRecipient, AssignedAt)
                SELECT Id, ResponsibleAttorneyId, 'ResponsibleAttorney', '', 1, GETUTCDATE()
                FROM Matters 
                WHERE ResponsibleAttorneyId IS NOT NULL;
                
                INSERT INTO MatterAssignments (MatterId, UserId, AssignmentType, Role, IsNotifyRecipient, AssignedAt)
                SELECT Id, ResponsibleStaffId, 'ResponsibleStaff', '', 1, GETUTCDATE()
                FROM Matters 
                WHERE ResponsibleStaffId IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_OriginatingAttorneyId",
                table: "Matters");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_ResponsibleAttorneyId",
                table: "Matters");

            migrationBuilder.DropForeignKey(
                name: "FK_Matters_Users_ResponsibleStaffId",
                table: "Matters");

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
                name: "ResponsibleAttorneyId",
                table: "Matters");

            migrationBuilder.DropColumn(
                name: "ResponsibleStaffId",
                table: "Matters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginatingAttorneyId",
                table: "Matters",
                type: "int",
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
    }
}
