using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixServiceRequestAttachmentsUserConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestAttachments_Users_UploadedById",
                table: "ServiceRequestAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestAttachments_Users_UploadedById",
                table: "ServiceRequestAttachments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestAttachments_Users_UploadedById",
                table: "ServiceRequestAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestAttachments_Users_UploadedById",
                table: "ServiceRequestAttachments",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
