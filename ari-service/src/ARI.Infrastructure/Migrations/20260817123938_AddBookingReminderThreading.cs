using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReminderThreading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invite_email_message_id",
                table: "interview_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_auto_reminder_hours",
                table: "interview_bookings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invite_email_message_id",
                table: "interview_bookings");

            migrationBuilder.DropColumn(
                name: "last_auto_reminder_hours",
                table: "interview_bookings");
        }
    }
}
