using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixServiceRequestAttachmentsCascadeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                table: "ServiceRequestAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                table: "ServiceRequestAttachments",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                table: "ServiceRequestAttachments");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                table: "ServiceRequestAttachments",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
