using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "confirmation_status",
                table: "interview_bookings",
                type: "text",
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "decline_reason",
                table: "interview_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "responded_at",
                table: "interview_bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmation_status",
                table: "interview_bookings");

            migrationBuilder.DropColumn(
                name: "decline_reason",
                table: "interview_bookings");

            migrationBuilder.DropColumn(
                name: "responded_at",
                table: "interview_bookings");
        }
    }
}
