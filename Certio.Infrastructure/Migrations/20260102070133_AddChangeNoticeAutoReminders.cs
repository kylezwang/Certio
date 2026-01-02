using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeNoticeAutoReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoReminderHoursBeforeDue",
                table: "ChangeNotices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoReminderLastTriggeredAtUtc",
                table: "ChangeNotices",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoReminderHoursBeforeDue",
                table: "ChangeNotices");

            migrationBuilder.DropColumn(
                name: "AutoReminderLastTriggeredAtUtc",
                table: "ChangeNotices");
        }
    }
}
